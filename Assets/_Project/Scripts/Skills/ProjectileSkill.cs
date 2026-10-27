using System;
using UnityEngine;

// * Skill tự động bắn vào mục tiêu gần nhất; dùng được cho mọi Character có gán mẫu này.
// * Chỉ field cấu hình được serialize; context/cooldown là dữ liệu riêng của bản runtime.
[Serializable]
public sealed class ProjectileSkill : ISkill
{
    #region Template Configuration

    [SerializeField] private ProjectileView projectilePrefab;
    [SerializeField, Min(0.01f)] private float cooldown = 0.6f;
    [SerializeField, Min(0f)] private float damageMultiplier = 1f;
    [SerializeField, Min(0.01f)] private float targetSearchInterval = 0.1f;
    [SerializeField] private Vector2 muzzleOffset = new Vector2(0f, 0.02f);

    #endregion

    [NonSerialized] private SkillContext context;
    [NonSerialized] private float nextShotTime;
    [NonSerialized] private float nextSearchTime;
    [NonSerialized] private bool isActive;
    public bool IsActive => isActive;

    public ISkill CreateRuntime(SkillContext runtimeContext)
    {
        if (runtimeContext == null || runtimeContext.Projectiles == null || projectilePrefab == null)
            throw new InvalidOperationException("Projectile skill needs context, ProjectileService and a projectile prefab.");
        return new ProjectileSkill
        {
            projectilePrefab = projectilePrefab,
            cooldown = Mathf.Max(0.01f, cooldown),
            damageMultiplier = Mathf.Max(0f, damageMultiplier),
            targetSearchInterval = Mathf.Max(0.01f, targetSearchInterval),
            muzzleOffset = muzzleOffset,
            context = runtimeContext
        };
    }

    public void Activate()
    {
        if (context == null)
            throw new InvalidOperationException("Activate a runtime skill, not the shared template.");
        isActive = true;
    }

    public void Deactivate() => isActive = false;

    public void Tick(float deltaTime)
    {
        CharacterView owner = context.Owner;
        if (!isActive || deltaTime <= 0f || owner == null || owner.RuntimeData == null ||
            !owner.RuntimeData.IsAlive || Time.time < nextShotTime || Time.time < nextSearchTime)
            return;

        // * Không có mục tiêu thì thử lại theo nhịp tìm kiếm, không quét liên tục từng frame.
        nextSearchTime = Time.time + targetSearchInterval;
        CharacterRuntimeData runtime = owner.RuntimeData;
        CharacterTarget target = CharacterSearch.FindNearest(owner.HitPosition,
            runtime.FinalStats.range, CharacterSearch.OpponentOf(runtime.Team), owner);
        if (!target.TryGetCharacter(out _))
            return;

        DamageInfo damage = runtime.CreateDamage(damageMultiplier);
        context.Projectiles.Fire(projectilePrefab, (Vector2)owner.HitPosition + muzzleOffset, target, damage);
        // * Không bắn bù nhiều viên trong một frame khi game bị chậm.
        nextShotTime = Time.time + cooldown;
    }
}
