using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Hành vi đánh từ trời: chọn N chỗ trong tầm chiến đấu, vẽ vùng báo trên đất, hết thời gian báo thì đánh mọi đối thủ
/// trong từng vòng. Data: <see cref="StrikeData"/>.
/// </summary>
/// <remarks>Gói đòn chụp lúc dùng skill. Vòng đang chờ vẫn rơi khi skill lên cấp; dọn sạch khi skill kết thúc.</remarks>
public sealed class StrikeSkill : Skill
{
    #region Runtime State

    private sealed class Pending
    {
        public Vector2 Center;
        public float Radius, TimeLeft, Telegraph;
        public HitPayload Hit;
        public StrikeData Data; // Cấp lúc dùng, để vòng đang chờ đánh đúng thông số cũ.
        public SkillAreaVisual Visual;
    }

    private StrikeData data;
    private IGameplayEffect[] effectSnapshot;
    private readonly List<SkillAim> aims = new List<SkillAim>(8);
    private readonly List<StrikeCircle> circles = new List<StrikeCircle>(16);
    private readonly List<Pending> pending = new List<Pending>(16);
    private readonly Stack<Pending> spare = new Stack<Pending>();
    private bool paused;

    public override SkillBehaviourType Type => SkillBehaviourType.Strike;

    #endregion

    #region Configuration

    protected override void InitializeRuntime()
    {
        data = null;
        if (effectSnapshot == null) effectSnapshot = Array.Empty<IGameplayEffect>();
        aims.Clear();
        circles.Clear();
        paused = false;
    }

    public override void ApplyData(SkillBehaviourData configuration)
    {
        var next = configuration as StrikeData ?? throw new ArgumentException("StrikeSkill requires StrikeData.");
        next.Validate();
        effectSnapshot = SnapshotEffects(effectSnapshot, next.Effects);
        data = next;
    }

    #endregion

    #region Execution

    public override bool TryExecute()
    {
        if (!Owner.CanRun || data == null) return false;
        CharacterView self = Owner.Owner;
        var origin = new SkillOrigin(self.HitPosition, self.AimDirection, Owner.Identity.Team, self, Owner.Identity.CombatRange);
        int found = data.Target.Select(in origin, self.Search, data.TargetCount, aims);
        if (found == 0) return false;
        var hit = new HitPayload(Owner.Handle, Owner.Identity.FinalStats, Owner.Identity.FinalBonuses, Owner.Identity.Team,
            data.DamageMultiplier, effectSnapshot, data.Knockback);
        for (int index = 0; index < found; index++)
        {
            SkillAim aim = aims[index];
            circles.Clear();
            data.Shape.Build(origin.Position, aim.Point, aim.Direction, circles);
            foreach (StrikeCircle circle in circles)
                Schedule(circle, data.Telegraph + data.Interval * index + circle.Delay, in hit);
        }
        circles.Clear();
        aims.Clear();
        return true;
    }

    private void Schedule(StrikeCircle circle, float delay, in HitPayload hit)
    {
        Pending strike = spare.Count > 0 ? spare.Pop() : new Pending();
        strike.Center = circle.Center;
        strike.Radius = circle.Radius;
        strike.TimeLeft = strike.Telegraph = delay;
        strike.Hit = hit;
        strike.Data = data;
        strike.Visual = null;
        if (delay > 0f)
        {
            GameObject clone = Services.Pools.Spawn(Services.Assets.CirclePrefab.gameObject, circle.Center, Quaternion.identity);
            strike.Visual = clone != null ? clone.GetComponent<SkillAreaVisual>() : null;
            strike.Visual?.InitializeAt(circle.Center, circle.Radius, data.TelegraphColor, Services.AreaVisuals);
        }
        pending.Add(strike);
    }

    /// <summary>Đếm lùi và đánh các vòng đến hạn. Vòng báo đậm dần khi sắp đánh.</summary>
    public override void Tick(float deltaTime)
    {
        if (paused || pending.Count == 0) return;
        for (int index = pending.Count - 1; index >= 0; index--)
        {
            if (index >= pending.Count) continue; // Callback đánh có thể đã dọn skill.
            Pending strike = pending[index];
            strike.TimeLeft -= deltaTime;
            if (strike.TimeLeft > 0f)
            {
                if (strike.Visual != null && strike.Telegraph > 0f)
                {
                    Color color = strike.Data.TelegraphColor;
                    float progress = 1f - strike.TimeLeft / strike.Telegraph;
                    strike.Visual.SetColor(new Color(color.r, color.g, color.b, color.a * (0.4f + 0.6f * progress)));
                }
                continue;
            }
            pending.RemoveAt(index);
            Land(strike);
        }
    }

    private void Land(Pending strike)
    {
        if (strike.Visual != null) Services.Pools.Despawn(strike.Visual.gameObject);
        strike.Visual = null;
        StrikeData landed = strike.Data;
        HitPayload hit = strike.Hit;
        Vector2 center = strike.Center;
        float radius = strike.Radius;
        strike.Data = null;
        strike.Hit = default;
        spare.Push(strike);
        Services.Vfx.Spawn(landed.LandVfx, center);
        ICharacterSearch search = Owner != null && Owner.Owner != null ? Owner.Owner.Search : null;
        if (search == null) return;
        SkillImpacts.HitCircle(Services.Hits, search, center, radius, in hit);
        if (landed.OnLand.Count > 0)
        {
            var args = new ImpactArgs(Services.Impacts, search, center, Vector2.zero, in hit);
            SkillImpacts.Run(landed.OnLand, in args);
        }
    }

    #endregion

    #region Pause And Cleanup

    public override void SetPaused(bool value)
    {
        paused = value;
        foreach (Pending strike in pending) strike.Visual?.SetVisible(!value);
    }

    public override void Dispose()
    {
        for (int index = pending.Count - 1; index >= 0; index--)
        {
            Pending strike = pending[index];
            if (strike.Visual != null) Services.Pools.Despawn(strike.Visual.gameObject);
            strike.Visual = null;
            strike.Data = null;
            strike.Hit = default;
            spare.Push(strike);
        }
        pending.Clear();
    }

    #endregion
}
