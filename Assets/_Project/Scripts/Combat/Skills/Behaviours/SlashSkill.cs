using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Hành vi chém quanh chủ: hình quạt theo hướng chọn hoặc 360°, có thể là vòng sóng lan dần, nhiều đợt liên tiếp,
/// kéo quái vào trước đợt đầu. Data: <see cref="SlashData"/>.
/// </summary>
/// <remarks>Một lần dùng chạy hết các đợt rồi mới dùng lại được (cooldown tính từ lúc bắt đầu).</remarks>
public sealed class SlashSkill : Skill
{
    #region Runtime State

    private SlashData data; // Cấp hiện tại (cho lần dùng sau).
    private SlashData cast; // Cấp của lần dùng đang chạy.
    private IGameplayEffect[] effectSnapshot;
    private readonly List<SkillAim> aims = new List<SkillAim>(1);
    private readonly HashSet<CharacterHandle> waveHits = new HashSet<CharacterHandle>();
    private CharacterHandle[] targets = new CharacterHandle[32];
    private HitPayload hit;
    private Vector2 aimDirection;
    private float radius, waveElapsed, waveDelay;
    private int wavesLeft;
    private bool waveActive, paused;
    private SkillAreaVisual ring;

    public override SkillBehaviourType Type => SkillBehaviourType.Slash;

    private const float MinVisualSeconds = 0.15f; // Thời gian tối thiểu vòng chém hiện trên màn.

    #endregion

    #region Configuration

    protected override void InitializeRuntime()
    {
        data = cast = null;
        if (effectSnapshot == null) effectSnapshot = Array.Empty<IGameplayEffect>();
        aims.Clear();
        waveHits.Clear();
        hit = default;
        wavesLeft = 0;
        waveActive = paused = false;
        ring = null;
    }

    public override void ApplyData(SkillBehaviourData configuration)
    {
        var next = configuration as SlashData ?? throw new ArgumentException("SlashSkill requires SlashData.");
        next.Validate();
        effectSnapshot = SnapshotEffects(effectSnapshot, next.Effects);
        data = next;
    }

    #endregion

    #region Execution

    public override bool TryExecute()
    {
        if (!Owner.CanRun || data == null || cast != null) return false;
        CharacterView self = Owner.Owner;
        float range = Owner.Identity.CombatRange;
        var origin = new SkillOrigin(self.HitPosition, self.AimDirection, Owner.Identity.Team, self, range);
        bool found = data.Target.Select(in origin, self.Search, 1, aims) > 0;
        if (!found && data.RequiresTarget) return false;
        aimDirection = found ? aims[0].Direction : self.AimDirection;
        aims.Clear();
        cast = data;
        radius = cast.RadiusFor(range);
        hit = new HitPayload(Owner.Handle, Owner.Identity.FinalStats, cast.HitBonus.AddTo(Owner.Identity.FinalBonuses),
            Owner.Identity.Team, cast.DamageMultiplier, effectSnapshot, cast.Knockback);
        wavesLeft = cast.Waves;
        waveDelay = 0f;
        if (cast.PullRadius > 0f) Pull(self);
        return true;
    }

    // Kéo đối thủ quanh chủ về phía chủ (không qua CombatHits vì không gây sát thương).
    private void Pull(CharacterView self)
    {
        Vector2 center = self.HitPosition;
        int count = self.Search.FindAll(center, cast.PullRadius, CharacterSearch.OpponentOf(Owner.Identity.Team), ref targets, self);
        for (int index = 0; index < count; index++)
        {
            if (!targets[index].TryGetCharacter(out CharacterView victim)) continue;
            Vector2 toward = center - (Vector2)victim.HitPosition;
            float distance = Mathf.Min(cast.PullDistance, Mathf.Max(0f, toward.magnitude - 0.12f));
            victim.Push(toward, distance, CombatHits.PushSeconds);
        }
        Array.Clear(targets, 0, count);
    }

    /// <summary>Chạy các đợt chém của lần dùng hiện tại.</summary>
    public override void Tick(float deltaTime)
    {
        if (cast == null || paused) return;
        if (!Owner.CanRun) { EndCast(runFinish: false); return; }
        if (!waveActive)
        {
            waveDelay -= deltaTime;
            if (waveDelay > 0f) return;
            StartWave();
        }
        waveElapsed += deltaTime;
        float progress = cast.ExpandDuration > 0f ? Mathf.Clamp01(waveElapsed / cast.ExpandDuration) : 1f;
        float current = Mathf.Max(0.02f, radius * progress);
        Vector2 center = Owner.Owner.HitPosition;
        HitTargets(center, current);
        if (cast == null) return; // Callback đánh có thể đã dọn skill.
        // Đợt chém tức thì vẫn giữ vòng hiện thêm một chút để người chơi thấy (không ảnh hưởng gây đòn).
        float visualTime = Mathf.Max(cast.ExpandDuration, MinVisualSeconds);
        float visualProgress = Mathf.Clamp01(waveElapsed / visualTime);
        UpdateRing(center, current, visualProgress);
        if (visualProgress < 1f) return;
        waveActive = false;
        wavesLeft--;
        waveDelay = cast.WaveInterval;
        if (wavesLeft <= 0) EndCast(runFinish: true);
    }

    private void StartWave()
    {
        waveActive = true;
        waveElapsed = 0f;
        waveHits.Clear();
        Vector2 center = Owner.Owner.HitPosition;
        if (cast.ArcDegrees < 360f)
            for (int index = 0; index < cast.Directions; index++)
            {
                Vector2 direction = ShotGeometry.Rotate(aimDirection, 360f * index / cast.Directions);
                Services.Vfx.Spawn(cast.SlashVfx, center + direction * radius * 0.6f);
            }
        if (ring == null && (cast.ArcDegrees >= 360f || cast.ExpandDuration > 0f))
        {
            GameObject clone = Services.Pools.Spawn(Services.Assets.CirclePrefab.gameObject, center, Quaternion.identity);
            ring = clone != null ? clone.GetComponent<SkillAreaVisual>() : null;
            ring?.InitializeAt(center, 0.02f, cast.RingColor, Services.AreaVisuals);
        }
    }

    private void HitTargets(Vector2 center, float currentRadius)
    {
        CharacterView self = Owner.Owner;
        int count = self.Search.FindAll(center, currentRadius, CharacterSearch.OpponentOf(Owner.Identity.Team), ref targets, self);
        try
        {
            for (int index = 0; index < count && cast != null; index++)
            {
                CharacterHandle target = targets[index];
                if (waveHits.Contains(target) || !target.TryGetCharacter(out CharacterView victim)) continue;
                if (!InArc((Vector2)victim.HitPosition - center)) continue;
                waveHits.Add(target);
                Services.Hits.Apply(target, in hit, victim.HitPosition);
            }
        }
        finally { Array.Clear(targets, 0, count); }
    }

    // Mục tiêu nằm trong một trong các hình quạt (360° thì luôn đúng).
    private bool InArc(Vector2 offset)
    {
        if (cast.ArcDegrees >= 360f || offset.sqrMagnitude < 0.0001f) return true;
        float half = cast.ArcDegrees * 0.5f;
        for (int index = 0; index < cast.Directions; index++)
        {
            Vector2 direction = ShotGeometry.Rotate(aimDirection, 360f * index / cast.Directions);
            if (Vector2.Angle(direction, offset) <= half) return true;
        }
        return false;
    }

    private void UpdateRing(Vector2 center, float currentRadius, float progress)
    {
        if (ring == null) return;
        ring.SetAnchor(center);
        ring.SetRadius(currentRadius);
        Color color = cast.RingColor;
        ring.SetColor(new Color(color.r, color.g, color.b, color.a * (1f - 0.7f * progress)));
    }

    private void EndCast(bool runFinish)
    {
        SlashData finished = cast;
        HitPayload payload = hit;
        cast = null;
        waveActive = false;
        wavesLeft = 0;
        waveHits.Clear();
        hit = default;
        ReleaseRing();
        if (!runFinish || finished == null || finished.OnFinish.Count == 0 || Owner == null || !Owner.CanRun) return;
        var args = new ImpactArgs(Services.Impacts, Owner.Owner.Search, Owner.Owner.HitPosition, aimDirection, in payload);
        SkillImpacts.Run(finished.OnFinish, in args);
    }

    private void ReleaseRing()
    {
        if (ring != null) Services.Pools.Despawn(ring.gameObject);
        ring = null;
    }

    #endregion

    #region Pause And Cleanup

    public override void SetPaused(bool value)
    {
        paused = value;
        ring?.SetVisible(!value);
    }

    public override void Dispose() => EndCast(runFinish: false);

    #endregion
}
