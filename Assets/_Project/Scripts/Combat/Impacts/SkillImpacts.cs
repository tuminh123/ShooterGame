using System;
using System.Collections.Generic;
using UnityEngine;

// Các tác động phụ của skill (strategy của ISkillImpact): nổ, tách đạn, tạo vùng, ngẫu nhiên một trong nhiều, buff cho chủ.
// Dùng chung cho đạn (khi trúng / khi hết đường bay / theo nhịp khi bay), đòn từ trời (khi rơi), chém (khi xong) và vùng (khi hết giờ / khi bẫy nổ).
// KHÔNG đổi tên class hoặc field [SerializeField] trong file này: Unity lưu chúng trong asset skill.

/// <summary>Các system mà một tác động cần. CombatManager tạo một instance, đạn và mọi hành vi skill dùng chung.</summary>
public sealed class ImpactServices
{
    /// <summary>Gây đòn (nơi duy nhất tính sát thương).</summary>
    public CombatHits Hits { get; }
    /// <summary>Bắn đạn con (tách đạn).</summary>
    public ProjectileSystem Projectiles { get; internal set; }
    /// <summary>Tạo vùng trên đất.</summary>
    public ZoneSystem Zones { get; internal set; }
    /// <summary>VFX một lần.</summary>
    public CombatVfx Vfx { get; }
    /// <summary>Buff có thời hạn cho chủ.</summary>
    public TimedBuffSystem Buffs { get; }

    public ImpactServices(CombatHits hits, CombatVfx vfx, TimedBuffSystem buffs)
    {
        Hits = hits ?? throw new ArgumentNullException(nameof(hits));
        Vfx = vfx ?? throw new ArgumentNullException(nameof(vfx));
        Buffs = buffs ?? throw new ArgumentNullException(nameof(buffs));
    }
}

/// <summary>Ngữ cảnh của một lần tác động: ở đâu, hướng nào, gói đòn gốc và bộ tìm kiếm.</summary>
public readonly struct ImpactArgs
{
    public readonly ImpactServices Services;
    public readonly ICharacterSearch Search;
    public readonly Vector2 Position;
    public readonly Vector2 Direction; // Hướng bay/đánh lúc tác động; zero nếu không có.
    public readonly HitPayload Hit; // Gói đòn gốc; tác động thay % sát thương/effect bằng HitPayload.With.
    public readonly CharacterHandle Excluded; // Mục tiêu vừa trúng (đạn tách không bắn lại nó ngay).

    public ImpactArgs(ImpactServices services, ICharacterSearch search, Vector2 position, Vector2 direction, in HitPayload hit,
        CharacterHandle excluded = default)
    {
        Services = services;
        Search = search;
        Position = position;
        Direction = direction;
        Hit = hit;
        Excluded = excluded;
    }
}

/// <summary>Strategy của một tác động phụ, chọn trong data bằng <c>[SerializeReference]</c>. Không giữ state (dùng chung trong asset).</summary>
public interface ISkillImpact
{
    /// <summary>Ném lỗi nếu tham số sai.</summary>
    void Validate();

    /// <summary>Thực hiện tác động. Có thể gây đòn (callback chết/despawn) nên bên gọi phải chốt dữ liệu trước.</summary>
    void Execute(in ImpactArgs args);
}

/// <summary>Kiểm tra và chạy danh sách tác động.</summary>
public static class SkillImpacts
{
    // Buffer tìm mục tiêu dùng chung: chỉ sống trong một lần Explode (main thread). Nổ lồng nổ dùng buffer riêng theo độ sâu.
    private static readonly List<CharacterHandle[]> buffers = new List<CharacterHandle[]>();
    private static int depth;

    /// <summary>Kiểm tra mọi phần tử (null là lỗi cấu hình).</summary>
    public static void Validate(IReadOnlyList<ISkillImpact> impacts)
    {
        if (impacts == null) return;
        for (int index = 0; index < impacts.Count; index++)
        {
            if (impacts[index] == null) throw new InvalidOperationException("Missing skill impact.");
            impacts[index].Validate();
        }
    }

    /// <summary>Chạy lần lượt; một tác động lỗi được log, không chặn các tác động sau.</summary>
    public static void Run(IReadOnlyList<ISkillImpact> impacts, in ImpactArgs args)
    {
        if (impacts == null) return;
        for (int index = 0; index < impacts.Count; index++)
        {
            try { impacts[index].Execute(in args); }
            catch (Exception exception) { Debug.LogException(exception); }
        }
    }

    /// <summary>
    /// Gây đòn lên mọi đối thủ trong vòng tròn. Dùng chung cho nổ, vùng, chém 360°, đòn từ trời.
    /// Trả số mục tiêu đã trúng.
    /// </summary>
    public static int HitCircle(CombatHits hits, ICharacterSearch search, Vector2 center, float radius, in HitPayload hit,
        EffectType vfx = EffectType.None, CharacterHandle excluded = default)
    {
        if (search == null || radius <= 0f) return 0;
        if (depth >= buffers.Count) buffers.Add(new CharacterHandle[32]);
        CharacterHandle[] targets = buffers[depth];
        depth++;
        int count = 0;
        try
        {
            int found = search.FindAll(center, radius, CharacterSearch.OpponentOf(hit.Team), ref targets, null);
            buffers[depth - 1] = targets; // FindAll có thể đã nới mảng.
            for (int index = 0; index < found; index++)
            {
                CharacterHandle target = targets[index];
                if (target.Equals(excluded) || !target.TryGetCharacter(out CharacterView victim)) continue;
                hits.Apply(target, in hit, victim.HitPosition, vfx);
                count++;
            }
            Array.Clear(targets, 0, found);
        }
        finally { depth--; }
        return count;
    }
}

/// <summary>Nổ vùng tròn: gây % sát thương riêng (và effect riêng) lên mọi đối thủ trong bán kính.</summary>
[Serializable]
public sealed class ExplodeImpact : ISkillImpact
{
    [Tooltip("Bán kính nổ (world unit).")][SerializeField, Min(0.01f)] private float radius = 0.25f;
    [Tooltip("% Damage của nhân vật lên mỗi mục tiêu trong vùng nổ.")][SerializeField, Min(0f)] private float damagePercent = 50f;
    [Tooltip("Đẩy lùi (dương) / kéo (âm) mỗi mục tiêu, world unit.")][SerializeField] private float knockback;
    [Tooltip("VFX tại tâm nổ.")][SerializeField] private EffectType vfx = EffectType.FireImpact;
    [Tooltip("Effect áp lên mục tiêu trúng nổ. Để trống = dùng effect của đòn gốc.")]
    [SerializeReference] private List<IGameplayEffect> effects = new List<IGameplayEffect>();
    [Tooltip("Bật: không đánh lại mục tiêu vừa trúng đạn (đã ăn đòn chính).")][SerializeField] private bool skipDirectTarget;

    public void Validate()
    {
        if (!(radius > 0f) || !(damagePercent >= 0f) || float.IsInfinity(damagePercent) || float.IsNaN(knockback))
            throw new InvalidOperationException("Invalid explode impact.");
        foreach (IGameplayEffect effect in effects) { if (effect == null) throw new InvalidOperationException("Missing effect."); effect.Validate(); }
    }

    public void Execute(in ImpactArgs args)
    {
        args.Services.Vfx.Spawn(vfx, args.Position);
        IReadOnlyList<IGameplayEffect> chosen = effects.Count > 0 ? (IReadOnlyList<IGameplayEffect>)effects : args.Hit.Effects;
        HitPayload blast = args.Hit.With(damagePercent / 100f, chosen, knockback);
        SkillImpacts.HitCircle(args.Services.Hits, args.Search, args.Position, radius, in blast, EffectType.None,
            skipDirectTarget ? args.Excluded : default);
    }
}

/// <summary>Tách ra N đạn nhỏ: tỏa đều 360° hoặc xòe quạt theo hướng bay.</summary>
[Serializable]
public sealed class SplitImpact : ISkillImpact
{
    [Tooltip("Số đạn con.")][SerializeField, Min(1)] private int count = 2;
    [Tooltip("% Damage mỗi đạn con.")][SerializeField, Min(0f)] private float damagePercent = 35f;
    [Tooltip("Góc xòe quanh hướng bay (độ). 360 = tỏa tròn đều.")][SerializeField, Range(0f, 360f)] private float spreadAngle = 360f;
    [SerializeField] private BulletType bulletType = BulletType.Shard;
    [SerializeField, Min(0.01f)] private float speed = 2.4f;
    [SerializeField, Min(0.01f)] private float lifetime = 0.6f;
    [Tooltip("Effect của đạn con. Để trống = dùng effect của đòn gốc.")]
    [SerializeReference] private List<IGameplayEffect> effects = new List<IGameplayEffect>();

    private static readonly StraightProjectileMove Straight = new StraightProjectileMove();

    public void Validate()
    {
        if (count < 1 || count > 64 || !(damagePercent >= 0f) || !(speed > 0f) || !(lifetime > 0f) || float.IsNaN(spreadAngle))
            throw new InvalidOperationException("Invalid split impact.");
        foreach (IGameplayEffect effect in effects) { if (effect == null) throw new InvalidOperationException("Missing effect."); effect.Validate(); }
    }

    public void Execute(in ImpactArgs args)
    {
        if (args.Services.Projectiles == null) return;
        IReadOnlyList<IGameplayEffect> chosen = effects.Count > 0 ? (IReadOnlyList<IGameplayEffect>)effects : args.Hit.Effects;
        HitPayload child = args.Hit.With(damagePercent / 100f, chosen, 0f);
        Vector2 forward = args.Direction.sqrMagnitude > 0.000001f ? args.Direction.normalized : Vector2.right;
        var data = new ProjectileSpawnData(bulletType, in child, speed, lifetime, Straight, search: args.Search);
        bool full = spreadAngle >= 359.9f;
        for (int index = 0; index < count; index++)
        {
            float angle = full
                ? 360f * index / count
                : (count == 1 ? 0f : -spreadAngle * 0.5f + spreadAngle * index / (count - 1));
            Vector2 direction = ShotGeometry.Rotate(forward, angle);
            // Đặt đạn con lệch khỏi tâm một chút để không trúng lại ngay mục tiêu vừa nổ.
            args.Services.Projectiles.SpawnBullet(in data, args.Position + direction * 0.06f, direction);
        }
    }
}

/// <summary>Tạo một vùng trên đất tại điểm tác động (vũng lửa, mây độc, vùng thánh…).</summary>
[Serializable]
public sealed class SpawnZoneImpact : ISkillImpact
{
    [SerializeField] private ZoneSettings zone = new ZoneSettings();

    public void Validate()
    {
        if (zone == null) throw new InvalidOperationException("Missing zone settings.");
        zone.Validate();
    }

    public void Execute(in ImpactArgs args) => args.Services.Zones?.Spawn(zone, args.Position, in args.Hit, args.Search);
}

/// <summary>Chọn ngẫu nhiên MỘT trong các tác động (bình thuốc bất ổn: lửa / băng / độc).</summary>
[Serializable]
public sealed class RandomImpact : ISkillImpact
{
    [Tooltip("Các lựa chọn; mỗi lần tác động bốc một cái với xác suất bằng nhau.")]
    [SerializeReference] private List<ISkillImpact> options = new List<ISkillImpact>();

    public void Validate()
    {
        if (options == null || options.Count == 0) throw new InvalidOperationException("Random impact needs options.");
        SkillImpacts.Validate(options);
    }

    public void Execute(in ImpactArgs args) => options[UnityEngine.Random.Range(0, options.Count)].Execute(in args);
}

/// <summary>Buff chỉ số có thời hạn cho NGƯỜI ĐÁNH (khiên về tay: +20% DEF trong 3 giây).</summary>
[Serializable]
public sealed class BuffOwnerImpact : ISkillImpact
{
    [SerializeField] private StatModifier modifier = new StatModifier(StatType.Defense, StatOperation.Percent, 20f);
    [SerializeField, Min(0.01f)] private float seconds = 3f;

    public void Validate()
    {
        modifier.Validate();
        if (!(seconds > 0f) || float.IsInfinity(seconds)) throw new InvalidOperationException("Invalid buff duration.");
    }

    public void Execute(in ImpactArgs args) => args.Services.Buffs.Apply(args.Hit.Attacker, this, modifier, seconds);
}

/// <summary>Chạy nhiều tác động cùng lúc (nổ + để lại vũng cùng nguyên tố), dùng làm một lựa chọn của <see cref="RandomImpact"/>.</summary>
[Serializable]
public sealed class MultiImpact : ISkillImpact
{
    [SerializeReference] private List<ISkillImpact> impacts = new List<ISkillImpact>();

    public void Validate()
    {
        if (impacts == null || impacts.Count == 0) throw new InvalidOperationException("Multi impact needs impacts.");
        SkillImpacts.Validate(impacts);
    }

    public void Execute(in ImpactArgs args) => SkillImpacts.Run(impacts, in args);
}
