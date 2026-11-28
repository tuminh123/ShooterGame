using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Hành vi bắn: hỏi luật chọn chỗ bắn rồi bắn một lượt đạn, chia hướng bằng <see cref="IShooter"/>.</summary>
/// <remarks>
/// Không có mục tiêu thì không tiêu cooldown (trừ khi data cho bắn không cần mục tiêu). Lượt bắn nối đuôi (dao ném) được
/// xếp hàng và bắn dần trong <see cref="Tick"/> từ vị trí hiện tại của chủ. Data: <see cref="ShootingData"/>.
/// </remarks>
public sealed class ShootingSkill : Skill
{
    #region Runtime State

    private ShootingData data; // Cấu hình đang dùng, chỉ đọc.
    private IShooter liveShooter; // Trạng thái bắn riêng, ví dụ góc Spiral.
    private IProjectileMove movement; // Quỹ đạo của cấp hiện tại; không trạng thái nên dùng thẳng instance trong asset.
    private IGameplayEffect[] effectSnapshot; // Đạn giữ snapshot của cấp lúc bắn.
    private readonly List<SkillAim> aims = new List<SkillAim>(1); // Kết quả luật chọn; dùng lại, không cấp phát mỗi lần bắn.

    // Lượt bắn nối đuôi đang chờ: chốt cấu hình, gói đòn và hướng của lượt đó.
    private ShootingData burstData;
    private HitPayload burstHit;
    private Vector2 burstAim;
    private CharacterHandle burstTarget;
    private float burstMaxDistance, burstTimer;
    private int burstNext;

    public override SkillBehaviourType Type => SkillBehaviourType.Shooting;

    #endregion

    #region Configuration

    // Template không giữ state; bản clone bắt đầu với shooter chưa được tạo.
    protected override void InitializeRuntime()
    {
        data = null;
        liveShooter = null;
        movement = null;
        aims.Clear();
        if (effectSnapshot == null) effectSnapshot = Array.Empty<IGameplayEffect>(); // Giữ mảng cũ để dùng lại.
        ClearBurst();
    }

    // Giữ trạng thái shooter khi chỉ đổi thông số; đổi kiểu bắn thì tạo shooter đúng cấu hình mới.
    public override void ApplyData(SkillBehaviourData configuration)
    {
        var next = configuration as ShootingData ?? throw new ArgumentException("ShootingSkill requires ShootingData.");
        next.Validate();
        if (data == null || !SameShooter(data.Shooter, next.Shooter))
            liveShooter = next.Shooter.CreateRuntime(); // Bản sao riêng, để góc Spiral không dùng chung với template.
        movement = next.ProjectileMove;
        effectSnapshot = SnapshotEffects(effectSnapshot, next.Effects);
        data = next;
    }

    // Các shooter hiện có chỉ chứa config góc; JSON so sánh config không chứa state NonSerialized.
    private static bool SameShooter(IShooter first, IShooter second)
        => first.GetType() == second.GetType() && JsonUtility.ToJson(first) == JsonUtility.ToJson(second);

    #endregion

    #region Execution

    // Hỏi luật chọn (data.Target) chỗ bắn rồi thực hiện một volley. Không có chỗ bắn mà RequiresTarget thì không tiêu cooldown;
    // tắt RequiresTarget thì bắn theo DefaultDirection.
    public override bool TryExecute()
    {
        if (!Owner.CanRun || data == null) return false;
        CharacterView self = Owner.Owner;
        // Tầm chiến đấu là của nhân vật (gồm bonus CombatRange), không phải của skill.
        float range = Owner.Identity.CombatRange;
        var origin = new SkillOrigin(self.HitPosition, self.AimDirection, Owner.Identity.Team, self, range);
        Vector2 position = origin.Position + data.MuzzleOffset;
        Vector2 direction = data.DefaultDirection.normalized;
        Vector2 aimPoint = position + direction * range;
        CharacterHandle selected = default;
        if (data.Target.Select(in origin, self.Search, 1, aims) > 0)
        {
            SkillAim aim = aims[0];
            selected = aim.Target;
            aimPoint = aim.Point;
            // Có mục tiêu thì ngắm từ nòng tới mục tiêu; luật chỉ trả hướng thì dùng thẳng hướng đó.
            Vector2 toward = selected.TryGetCharacter(out CharacterView target) ? (Vector2)target.HitPosition - position : aim.Direction;
            if (selected.TryGetCharacter(out target)) aimPoint = target.HitPosition;
            if (toward.sqrMagnitude > 0.000001f) direction = toward.normalized;
        }
        else if (data.RequiresTarget) return false;
        return ExecuteVolley(position, direction, selected, MaxDistance(data, range, position, aimPoint)) > 0;
    }

    private static float MaxDistance(ShootingData shot, float combatRange, Vector2 from, Vector2 aimPoint)
    {
        switch (shot.RangeMode)
        {
            case ProjectileRange.CombatRange: return combatRange * shot.RangeScale;
            case ProjectileRange.TargetPoint: return Mathf.Max(0.05f, Vector2.Distance(from, aimPoint));
            default: return float.PositiveInfinity;
        }
    }

    // Chốt cả cấu hình và stats trước callback spawn; nâng cấp giữa volley không trộn hai cấp.
    private int ExecuteVolley(Vector2 position, Vector2 aim, CharacterHandle target, float maxDistance)
    {
        ShootingData shot = data;
        // Một gói đòn cho cả loạt: chỉ số chụp một lần, mỗi viên vẫn tung chí mạng riêng lúc trúng (CombatHits).
        var hit = new HitPayload(Owner.Handle, Owner.Identity.FinalStats, shot.HitBonus.AddTo(Owner.Identity.FinalBonuses),
            Owner.Identity.Team, shot.DamageMultiplier, effectSnapshot, shot.Knockback);
        if (shot.BurstInterval > 0f && shot.ProjectileCount > 1)
        {
            if (!SpawnOne(shot, in hit, position, aim.normalized, target, maxDistance, 0)) return 0;
            burstData = shot;
            burstHit = hit;
            burstAim = aim.normalized;
            burstTarget = target;
            burstMaxDistance = maxDistance;
            burstNext = 1;
            burstTimer = shot.BurstInterval;
            return 1;
        }
        int spawned = 0;
        for (int i = 0; i < shot.ProjectileCount; i++)
        {
            if (!Owner.CanRun) break;
            if (SpawnOne(shot, in hit, position, aim.normalized, target, maxDistance, i)) spawned++;
        }
        if (spawned > 0) liveShooter.OnShotCompleted();
        return spawned;
    }

    private bool SpawnOne(ShootingData shot, in HitPayload hit, Vector2 position, Vector2 aim, CharacterHandle target, float maxDistance, int index)
    {
        Vector2 direction = liveShooter.GetDirection(aim, index, shot.ProjectileCount);
        var projectile = new ProjectileSpawnData(shot.BulletType, in hit, shot.Speed, shot.Lifetime, movement, shot.Traits,
            maxDistance, Owner.Owner.Search);
        return Services.Projectiles.SpawnBullet(in projectile, position, direction, target);
    }

    #endregion

    #region Burst

    /// <summary>Bắn các viên còn lại của lượt nối đuôi, từ vị trí hiện tại của chủ.</summary>
    public override void Tick(float deltaTime)
    {
        if (burstData == null) return;
        if (!Owner.CanRun) { ClearBurst(); return; }
        burstTimer -= deltaTime;
        while (burstData != null && burstTimer <= 0f)
        {
            Vector2 position = (Vector2)Owner.Owner.HitPosition + burstData.MuzzleOffset;
            SpawnOne(burstData, in burstHit, position, burstAim, burstTarget, burstMaxDistance, burstNext);
            burstNext++;
            burstTimer += burstData.BurstInterval;
            if (burstNext >= burstData.ProjectileCount)
            {
                liveShooter.OnShotCompleted();
                ClearBurst();
            }
        }
    }

    public override void SetPaused(bool paused) { if (paused) ClearBurst(); }

    public override void Dispose() => ClearBurst();

    private void ClearBurst()
    {
        burstData = null;
        burstHit = default;
        burstTarget = default;
        burstNext = 0;
        burstTimer = 0f;
    }

    #endregion
}
