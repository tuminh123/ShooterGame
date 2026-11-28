using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Cấu hình một vùng trên đất (vũng lửa, mây độc, nấm độc, bẫy gai, vùng thánh…). Chỉ đọc, nằm trong data của skill/tác động.
/// </summary>
/// <remarks>
/// Hai kiểu chạy, chọn bằng <see cref="TrapCharges"/>: 0 = gây đòn theo nhịp lên mọi đối thủ trong vùng;
/// &gt; 0 = bẫy, chỉ nổ khi có đối thủ bước vào (mỗi lần nổ tốn một lượt).
/// Không đổi tên class hoặc field: Unity lưu chúng trong asset skill.
/// </remarks>
[Serializable]
public sealed class ZoneSettings
{
    [Tooltip("Bán kính vùng (world unit).")][SerializeField, Min(0.01f)] private float radius = 0.35f;
    [Tooltip("Thời gian tồn tại (giây).")][SerializeField, Min(0.05f)] private float duration = 2f;
    [Tooltip("Số giây giữa hai lần gây đòn (kiểu theo nhịp).")][SerializeField, Min(0.05f)] private float tickInterval = 0.5f;
    [Tooltip("% Damage của nhân vật mỗi nhịp (hoặc mỗi lần bẫy nổ). 0 = không gây sát thương.")]
    [SerializeField, Min(0f)] private float damagePercent = 20f;
    [Tooltip("Effect áp lên đối thủ mỗi lần trúng (cháy, độc, làm chậm ngắn…).")]
    [SerializeReference] private List<IGameplayEffect> effects = new List<IGameplayEffect>();
    [Tooltip("% HP tối đa mỗi giây hồi cho CHỦ khi đứng trong vùng (vùng thánh). 0 = không hồi.")]
    [SerializeField, Min(0f)] private float healPercentPerSecond;
    [Tooltip("> 0: vùng là BẪY, chỉ nổ khi có đối thủ bước vào; số lần nổ trước khi mất.")]
    [SerializeField, Min(0)] private int trapCharges;
    [Tooltip("Tác động khi bẫy nổ (nổ lan…).")][SerializeReference] private List<ISkillImpact> onTrigger = new List<ISkillImpact>();
    [Tooltip("Tác động khi vùng hết giờ (nấm sinh nấm con…).")][SerializeReference] private List<ISkillImpact> onExpire = new List<ISkillImpact>();
    [Tooltip("VFX mỗi lần gây đòn / bẫy nổ, tại tâm vùng.")][SerializeField] private EffectType tickVfx;
    [Tooltip("Màu vùng vẽ trên đất.")][SerializeField] private Color color = new Color(1f, 0.45f, 0.1f, 0.35f);

    public float Radius => radius;
    public float Duration => duration;
    public float TickInterval => tickInterval;
    public float DamageMultiplier => damagePercent / 100f;
    public IReadOnlyList<IGameplayEffect> Effects => effects;
    public float HealPercentPerSecond => healPercentPerSecond;
    public int TrapCharges => trapCharges;
    public IReadOnlyList<ISkillImpact> OnTrigger => onTrigger;
    public IReadOnlyList<ISkillImpact> OnExpire => onExpire;
    public EffectType TickVfx => tickVfx;
    public Color Color => color;

    /// <summary>Ném lỗi nếu tham số sai.</summary>
    public void Validate()
    {
        if (!(radius > 0f) || !(duration > 0f) || !(tickInterval > 0f) || !(damagePercent >= 0f) ||
            !(healPercentPerSecond >= 0f) || trapCharges < 0 || float.IsInfinity(duration))
            throw new InvalidOperationException("Invalid zone settings.");
        foreach (IGameplayEffect effect in effects) { if (effect == null) throw new InvalidOperationException("Missing effect."); effect.Validate(); }
        SkillImpacts.Validate(onTrigger);
        SkillImpacts.Validate(onExpire);
    }
}

/// <summary>
/// System vùng trên đất của miền Combat: tạo vùng, gây đòn theo nhịp / nổ bẫy, hồi máu cho chủ, hết giờ thì thu hồi.
/// </summary>
/// <remarks>
/// Giống <see cref="ProjectileSystem"/>: một vòng cập nhật chung cho mọi vùng, visual thuê từ pool.
/// Do <see cref="CombatManager"/> tạo và tick; không bind vào Zenject.
/// </remarks>
public sealed class ZoneSystem : IDisposable
{
    #region Types

    private sealed class Zone
    {
        public ZoneSettings Settings;
        public HitPayload Hit; // Gói đòn đã đổi % sát thương/effect theo vùng.
        public ICharacterSearch Search;
        public Vector2 Position;
        public float Remaining, TickTimer;
        public int Charges;
        public SkillAreaVisual Visual;
    }

    #endregion

    #region State

    private readonly IGameObjectPool pools;
    private readonly SkillAreaVisuals areaVisuals;
    private readonly SkillAreaVisual circlePrefab;
    private readonly ImpactServices impacts;
    private readonly List<Zone> active = new List<Zone>(32);
    private readonly Stack<Zone> spare = new Stack<Zone>();
    private CharacterHandle[] targets = new CharacterHandle[32];
    private bool ticking, disposed;

    /// <summary>Số vùng giới hạn cùng lúc; vượt thì vùng cũ nhất kết thúc sớm (giữ hiệu năng khi skill tạo vùng dày).</summary>
    public const int MaxZones = 96;

    /// <summary>Số vùng đang tồn tại (kiểm tra).</summary>
    public int ActiveCount => active.Count;

    #endregion

    #region Construction

    public ZoneSystem(IGameObjectPool pools, SkillAreaVisuals areaVisuals, SkillAreaVisual circlePrefab, ImpactServices impacts)
    {
        this.pools = pools ?? throw new ArgumentNullException(nameof(pools));
        this.areaVisuals = areaVisuals ?? throw new ArgumentNullException(nameof(areaVisuals));
        this.circlePrefab = circlePrefab != null ? circlePrefab : throw new ArgumentNullException(nameof(circlePrefab));
        this.impacts = impacts ?? throw new ArgumentNullException(nameof(impacts));
    }

    #endregion

    #region Spawn

    /// <summary>Tạo vùng tại <paramref name="position"/>. Gói đòn lấy bên đánh/chỉ số từ <paramref name="source"/>, % sát thương từ cấu hình vùng.</summary>
    public void Spawn(ZoneSettings settings, Vector2 position, in HitPayload source, ICharacterSearch search)
    {
        if (disposed || settings == null || search == null) return;
        if (active.Count >= MaxZones) End(0, runExpire: false);
        Zone zone = spare.Count > 0 ? spare.Pop() : new Zone();
        zone.Settings = settings;
        zone.Hit = source.With(settings.DamageMultiplier, settings.Effects, 0f);
        zone.Search = search;
        zone.Position = position;
        zone.Remaining = settings.Duration;
        zone.TickTimer = 0f;
        zone.Charges = settings.TrapCharges;
        GameObject clone = pools.Spawn(circlePrefab.gameObject, position, Quaternion.identity);
        zone.Visual = clone != null ? clone.GetComponent<SkillAreaVisual>() : null;
        zone.Visual?.InitializeAt(position, settings.Radius, settings.Color, areaVisuals);
        active.Add(zone);
    }

    #endregion

    #region Frame

    /// <summary>Cập nhật mọi vùng. Vùng tạo ra trong callback chỉ chạy từ frame sau.</summary>
    public void Tick(float deltaTime)
    {
        if (disposed || deltaTime <= 0f || ticking) return;
        ticking = true;
        try
        {
            for (int index = active.Count - 1; index >= 0; index--)
            {
                if (index >= active.Count) continue; // Callback có thể đã gỡ vùng.
                Zone zone = active[index];
                try { Advance(zone, deltaTime); }
                catch (Exception exception) { Debug.LogException(exception); }
                int current = active.IndexOf(zone);
                if (current >= 0 && (zone.Remaining <= 0f || (zone.Settings.TrapCharges > 0 && zone.Charges <= 0)))
                    End(current, runExpire: zone.Remaining <= 0f);
            }
        }
        finally { ticking = false; }
    }

    private void Advance(Zone zone, float deltaTime)
    {
        zone.Remaining -= deltaTime;
        ZoneSettings settings = zone.Settings;
        if (settings.HealPercentPerSecond > 0f) HealOwner(zone, deltaTime);
        if (settings.TrapCharges > 0)
        {
            CharacterHandle intruder = zone.Search.FindNearest(zone.Position, settings.Radius, CharacterSearch.OpponentOf(zone.Hit.Team));
            if (!intruder.IsAlive) return;
            zone.Charges--;
            Burst(zone);
            var args = new ImpactArgs(impacts, zone.Search, zone.Position, Vector2.zero, in zone.Hit);
            SkillImpacts.Run(settings.OnTrigger, in args);
            return;
        }
        zone.TickTimer += deltaTime;
        while (zone.TickTimer >= settings.TickInterval && zone.Remaining > -settings.TickInterval)
        {
            zone.TickTimer -= settings.TickInterval;
            Burst(zone);
        }
    }

    // Một nhịp: gây đòn lên mọi đối thủ đang trong vùng.
    private void Burst(Zone zone)
    {
        if (zone.Hit.DamageMultiplier <= 0f && zone.Hit.Effects.Count == 0) return;
        impacts.Vfx.Spawn(zone.Settings.TickVfx, zone.Position);
        int found = zone.Search.FindAll(zone.Position, zone.Settings.Radius, CharacterSearch.OpponentOf(zone.Hit.Team), ref targets);
        try
        {
            for (int index = 0; index < found; index++)
            {
                CharacterHandle target = targets[index];
                if (target.TryGetCharacter(out CharacterView victim))
                    impacts.Hits.Apply(target, in zone.Hit, victim.HitPosition);
            }
        }
        finally { Array.Clear(targets, 0, found); }
    }

    private static void HealOwner(Zone zone, float deltaTime)
    {
        if (!zone.Hit.Attacker.TryGetCharacter(out CharacterView owner) || !owner.RuntimeData.IsAlive) return;
        float radius = zone.Settings.Radius;
        if (((Vector2)owner.HitPosition - zone.Position).sqrMagnitude > radius * radius) return;
        owner.Restore(owner.RuntimeData.MaxHealth * zone.Settings.HealPercentPerSecond / 100f * deltaTime);
    }

    #endregion

    #region End And Cleanup

    private void End(int index, bool runExpire)
    {
        Zone zone = active[index];
        active.RemoveAt(index);
        SkillAreaVisual visual = zone.Visual;
        zone.Visual = null;
        if (visual != null) pools.Despawn(visual.gameObject);
        if (runExpire && zone.Settings.OnExpire.Count > 0)
        {
            var args = new ImpactArgs(impacts, zone.Search, zone.Position, Vector2.zero, in zone.Hit);
            SkillImpacts.Run(zone.Settings.OnExpire, in args);
        }
        zone.Settings = null;
        zone.Search = null;
        zone.Hit = default;
        spare.Push(zone);
    }

    /// <summary>Thu hồi mọi vùng (kết thúc trận), không chạy tác động hết giờ.</summary>
    public void Clear()
    {
        while (active.Count > 0) End(active.Count - 1, runExpire: false);
    }

    public void Dispose()
    {
        if (disposed) return;
        Clear();
        disposed = true;
    }

    #endregion
}
