#region SkillServices

/// <summary>Dịch vụ tạo đạn và chọn hướng bắn cho hành vi skill.</summary>
public interface ISkillShootingServices
{
    IProjectileSpawner Spawner { get; }
    IShootBehaviorFactory Shooters { get; }
    IProjectileMoveSelection Moves { get; }
}

/// <summary>Dịch vụ thuê visual và áp dụng effect cho hành vi skill.</summary>
public interface ISkillEffectServices
{
    IGameObjectPool Pools { get; }
    IEffectApplication Effects { get; }
    IEffectHandleControl EffectHandles { get; }
}

/// <summary>Context dịch vụ của hành vi; không cung cấp API quản lý skill toàn cục.</summary>
public interface ISkillServices
{
    ISkillShootingServices GetShooting();
    ISkillEffectServices GetEffects();
}

#endregion
