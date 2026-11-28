using System;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Logic sét nảy dùng chung cho skill Chain và pet bắn sét (trụ Tesla): đánh mục tiêu đầu, rồi nảy sang đối thủ gần nhất
/// chưa trúng trong tầm nảy, vẽ đường sét giữa các điểm.
/// </summary>
public static class ChainLightning
{
    private static CharacterHandle[] candidates = new CharacterHandle[32]; // Chỉ sống trong một lời gọi (main thread).
    private static readonly CharacterHandle[] chain = new CharacterHandle[33];

    /// <summary>Chạy một chuỗi sét từ <paramref name="from"/>. Trả số mục tiêu đã trúng.</summary>
    public static int Run(CombatHits hits, CombatVfx vfx, ICharacterSearch search, Vector2 from, CharacterHandle first,
        in HitPayload source, int jumps, float jumpRange, float falloff, EffectType hitVfx, Color color, float width)
    {
        if (search == null || !first.TryGetCharacter(out CharacterView current)) return 0;
        Vector2 previous = from;
        float multiplier = source.DamageMultiplier;
        int count = 0;
        CharacterTeam opponents = CharacterSearch.OpponentOf(source.Team);
        CharacterHandle target = first;
        for (int hop = 0; hop <= jumps && hop < chain.Length; hop++)
        {
            Vector2 point = current.HitPosition;
            vfx.SpawnLine(previous, point, color, width, 0.18f);
            HitPayload strike = source.With(multiplier, source.Effects, source.Knockback);
            chain[count++] = target;
            hits.Apply(target, in strike, point, hitVfx);
            previous = point;
            multiplier *= 1f - falloff;
            if (hop == jumps) break;
            // Tìm mục tiêu kế tiếp gần nhất chưa trúng.
            int found = search.FindAll(point, jumpRange, opponents, ref candidates);
            CharacterHandle next = default;
            float best = float.MaxValue;
            for (int index = 0; index < found; index++)
            {
                CharacterHandle candidate = candidates[index];
                if (Contains(candidate, count) || !candidate.TryGetCharacter(out CharacterView view)) continue;
                float distance = ((Vector2)view.HitPosition - point).sqrMagnitude;
                if (distance < best) { best = distance; next = candidate; }
            }
            Array.Clear(candidates, 0, found);
            if (!next.TryGetCharacter(out current)) break;
            target = next;
        }
        Array.Clear(chain, 0, count);
        return count;
    }

    private static bool Contains(CharacterHandle candidate, int count)
    {
        for (int index = 0; index < count; index++)
            if (chain[index].Equals(candidate)) return true;
        return false;
    }
}

/// <summary>Hành vi sét nảy: đánh đối thủ gần nhất trong tầm chiến đấu rồi nảy. Data: <see cref="ChainData"/>.</summary>
public sealed class ChainSkill : Skill
{
    private ChainData data;
    private IGameplayEffect[] effectSnapshot;
    public override SkillBehaviourType Type => SkillBehaviourType.Chain;

    protected override void InitializeRuntime()
    {
        data = null;
        if (effectSnapshot == null) effectSnapshot = Array.Empty<IGameplayEffect>();
    }

    public override void ApplyData(SkillBehaviourData configuration)
    {
        var next = configuration as ChainData ?? throw new ArgumentException("ChainSkill requires ChainData.");
        next.Validate();
        effectSnapshot = SnapshotEffects(effectSnapshot, next.Effects);
        data = next;
    }

    public override bool TryExecute()
    {
        if (!Owner.CanRun || data == null) return false;
        CharacterView self = Owner.Owner;
        CharacterHandle first = self.Search.FindNearest(self.HitPosition, Owner.Identity.CombatRange,
            CharacterSearch.OpponentOf(Owner.Identity.Team), self);
        if (!first.IsAlive) return false;
        var hit = new HitPayload(Owner.Handle, Owner.Identity.FinalStats, Owner.Identity.FinalBonuses, Owner.Identity.Team,
            data.DamageMultiplier, effectSnapshot);
        ChainLightning.Run(Services.Hits, Services.Vfx, self.Search, self.HitPosition, first, in hit, data.Jumps, data.JumpRange,
            data.Falloff, data.HitVfx, data.LineColor, data.LineWidth);
        return true;
    }
}

/// <summary>
/// Hành vi tia: bắn một tia dài bằng tầm chiến đấu theo hướng nhìn (hoặc xoay quanh người) trong một khoảng thời gian,
/// gây sát thương theo nhịp lên mọi đối thủ nằm trên tia. Data: <see cref="BeamData"/>.
/// </summary>
public sealed class BeamSkill : Skill
{
    private BeamData data, cast;
    private IGameplayEffect[] effectSnapshot;
    private CharacterHandle[] targets = new CharacterHandle[32];
    private HitPayload hit;
    private Vector2 direction;
    private float remaining, tickTimer;
    private SkillLineVisual line;
    private int lineVersion;
    private bool paused;
    public override SkillBehaviourType Type => SkillBehaviourType.Beam;

    protected override void InitializeRuntime()
    {
        data = cast = null;
        if (effectSnapshot == null) effectSnapshot = Array.Empty<IGameplayEffect>();
        hit = default;
        remaining = tickTimer = 0f;
        line = null;
        paused = false;
    }

    public override void ApplyData(SkillBehaviourData configuration)
    {
        var next = configuration as BeamData ?? throw new ArgumentException("BeamSkill requires BeamData.");
        next.Validate();
        effectSnapshot = SnapshotEffects(effectSnapshot, next.Effects);
        data = next;
    }

    public override bool TryExecute()
    {
        if (!Owner.CanRun || data == null || cast != null) return false;
        CharacterView self = Owner.Owner;
        // Tia theo hướng nhìn chỉ bắn khi có đối thủ trong tầm, để không phí lượt.
        CharacterHandle any = self.Search.FindNearest(self.HitPosition, Owner.Identity.CombatRange,
            CharacterSearch.OpponentOf(Owner.Identity.Team), self);
        if (!any.IsAlive) return false;
        cast = data;
        direction = self.AimDirection;
        remaining = cast.Duration;
        tickTimer = 0f;
        hit = new HitPayload(Owner.Handle, Owner.Identity.FinalStats, Owner.Identity.FinalBonuses, Owner.Identity.Team,
            cast.DamageMultiplier, effectSnapshot);
        return true;
    }

    public override void Tick(float deltaTime)
    {
        if (cast == null || paused) return;
        if (!Owner.CanRun) { EndBeam(); return; }
        CharacterView self = Owner.Owner;
        direction = cast.RotateDegreesPerSecond != 0f
            ? ShotGeometry.Rotate(direction, cast.RotateDegreesPerSecond * deltaTime).normalized
            : self.AimDirection;
        Vector2 start = self.HitPosition;
        Vector2 end = start + direction * Owner.Identity.CombatRange * cast.LengthScale;
        DrawLine(start, end);
        tickTimer -= deltaTime;
        if (tickTimer <= 0f)
        {
            tickTimer += cast.TickInterval;
            Sweep(self, start, end);
            if (cast == null) return;
        }
        remaining -= deltaTime;
        if (remaining <= 0f) EndBeam();
    }

    private void Sweep(CharacterView self, Vector2 start, Vector2 end)
    {
        float half = Vector2.Distance(start, end) * 0.5f;
        int count = self.Search.FindAll((start + end) * 0.5f, half + cast.Width + 0.3f,
            CharacterSearch.OpponentOf(Owner.Identity.Team), ref targets, self);
        try
        {
            for (int index = 0; index < count && cast != null; index++)
            {
                CharacterHandle target = targets[index];
                if (!target.TryGetCharacter(out CharacterView victim)) continue;
                if (!SpatialGeometry.SweepCircle(new float2(start.x, start.y), new float2(end.x, end.y), cast.Width,
                    victim.GetSpatialEntity(), out _)) continue;
                Services.Hits.Apply(target, in hit, victim.HitPosition);
            }
        }
        finally { Array.Clear(targets, 0, count); }
    }

    private void DrawLine(Vector2 start, Vector2 end)
    {
        if (line == null || line.Version != lineVersion || !line.isActiveAndEnabled)
        {
            line = Services.Vfx.SpawnLine(start, end, cast.Color, cast.Width * 2f, 0.1f);
            lineVersion = line != null ? line.Version : 0;
        }
        if (line == null) return;
        line.SetPoints(start, end);
        line.Hold(0.1f);
    }

    private void EndBeam()
    {
        cast = null;
        hit = default;
        line = null; // CombatVfx tự mờ và thu hồi đường cuối.
    }

    public override void SetPaused(bool value) => paused = value;

    public override void Dispose() => EndBeam();
}
