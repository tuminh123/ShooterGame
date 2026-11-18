using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Hành vi bắn: tìm mục tiêu gần nhất rồi bắn một lượt đạn, chia hướng bằng <see cref="IShooter"/>.</summary>
/// <remarks>Không có mục tiêu thì không tiêu cooldown (trừ khi data cho bắn không cần mục tiêu). Data: <see cref="ShootingData"/>.</remarks>
[Serializable]
public sealed class ShootingSkill : Skill
{
    #region Runtime State

    [NonSerialized] private ShootingData data; // Cấu hình đang dùng, chỉ đọc.
    [NonSerialized] private IShooter liveShooter; // Trạng thái bắn riêng, ví dụ góc Spiral.
    [NonSerialized] private IProjectileMove movement; // Quỹ đạo của cấp hiện tại; không trạng thái nên dùng thẳng instance trong asset.
    [NonSerialized] private IGameplayEffect[] effectSnapshot; // Đạn giữ snapshot của cấp lúc bắn.
    public override SkillBehaviourType Type => SkillBehaviourType.Shooting;

    #endregion

    #region Configuration

    // Template không giữ state; bản clone bắt đầu với shooter chưa được tạo.
    protected override void InitializeRuntime()
    {
        data = null;
        liveShooter = null;
        movement = null;
        effectSnapshot = Array.Empty<IGameplayEffect>();
    }

    // Giữ trạng thái shooter khi chỉ đổi thông số; đổi kiểu bắn thì tạo shooter đúng cấu hình mới.
    public override void ApplyData(SkillBehaviourData configuration)
    {
        var next = configuration as ShootingData ?? throw new ArgumentException("ShootingSkill requires ShootingData.");
        next.Validate();
        if (data == null || !SameShooter(data.shooter, next.shooter))
            liveShooter = next.shooter.CreateRuntime(); // Bản sao riêng, để góc Spiral không dùng chung với template.
        movement = next.projectileMove;
        effectSnapshot = next.effects.ToArray();
        data = next;
    }

    // Các shooter hiện có chỉ chứa config góc; JSON so sánh config không chứa state NonSerialized.
    private static bool SameShooter(IShooter first, IShooter second)
        => first.GetType() == second.GetType() && JsonUtility.ToJson(first) == JsonUtility.ToJson(second);

    #endregion

    #region Execution

    // Tìm mục tiêu rồi thực hiện một volley; thiếu mục tiêu không tiêu cooldown.
    public override bool TryExecute()
    {
        if (!Owner.CanRun || data == null) return false;
        CharacterHandle selected = default;
        Vector2 position = (Vector2)Owner.Owner.HitPosition + data.muzzleOffset;
        Vector2 direction = data.defaultDirection.normalized;
        // Đạn đuổi cần mục tiêu để bay, kể cả khi data cho phép bắn khi không có mục tiêu.
        if (data.requiresTarget || movement.TracksTarget)
        {
            selected = Owner.Owner.Search.FindNearest(Owner.Owner.HitPosition, data.range,
                CharacterSearch.OpponentOf(Owner.Identity.Team), Owner.Owner);
            if (selected.TryGetCharacter(out CharacterView target))
            {
                Vector2 offset = (Vector2)target.HitPosition - position;
                if (offset.sqrMagnitude > 0.000001f) direction = offset.normalized;
            }
            else if (data.requiresTarget) return false;
        }
        return ExecuteVolley(position, direction, selected) > 0;
    }

    // Chốt cả cấu hình và stats trước callback spawn; nâng cấp giữa volley không trộn hai cấp.
    private int ExecuteVolley(Vector2 position, Vector2 aim, CharacterHandle target)
    {
        ShootingData shot = data;
        IShooter shooter = liveShooter;
        IProjectileMove move = movement;
        IGameplayEffect[] effects = effectSnapshot;
        BaseStats stats = Owner.Identity.FinalStats;
        CharacterTeam team = Owner.Identity.Team;
        int spawned = 0;
        for (int i = 0; i < shot.projectileCount; i++)
        {
            if (!Owner.CanRun) break;
            Vector2 direction = shooter.GetDirection(aim.normalized, i, shot.projectileCount);
            var projectile = new ProjectileSpawnData(shot.bulletType, CharacterRuntimeData.CreateDamage(stats, team),
                shot.speed, shot.lifetime, move, effects, stats.effectHit);
            if (Services.Projectiles.SpawnBullet(projectile, position, direction, target)) spawned++;
        }
        if (spawned > 0) shooter.OnShotCompleted();
        return spawned;
    }

    #endregion
}
