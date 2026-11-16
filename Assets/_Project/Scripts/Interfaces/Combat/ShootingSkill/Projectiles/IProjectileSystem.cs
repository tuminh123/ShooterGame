#region ProjectileRegistration

/// <summary>Theo dõi đạn theo đăng ký movement của từng lượt spawn.</summary>
public interface IProjectileRegistration
{
    void Register(ProjectileView projectile, MovementHandle handle);
    void Unregister(MovementHandle handle);
}

#endregion

#region ProjectileCleanup

/// <summary>Thu hồi toàn bộ đạn của lượt chơi.</summary>
public interface IProjectileCleanup
{
    void ClearProjectiles();
}

#endregion

#region ProjectileCapacity

/// <summary>Chuẩn bị dung lượng trước gameplay.</summary>
public interface IProjectileCapacity
{
    void PrewarmCapacity(int count);
}

#endregion

#region System Contract

/// <summary>Gom hợp đồng của ProjectileManager; consumer nhận vai trò nhỏ cần dùng.</summary>
public interface IProjectileSystem
{
    IProjectileRegistration GetProjectileRegistration();
    IProjectileCleanup GetProjectileCleanup();
    IProjectileCapacity GetProjectileCapacity();
}

#endregion
