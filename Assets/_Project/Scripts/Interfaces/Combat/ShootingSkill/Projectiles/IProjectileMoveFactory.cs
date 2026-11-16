#region ProjectileMoveSelection

/// <summary>Chọn chiến lược tính hướng bay theo cấu hình.</summary>
public interface IProjectileMoveSelection
{
    IProjectileMove Create(IProjectileMove selectedMovement);
}

#endregion

#region System Contract

/// <summary>Gom hợp đồng của ProjectileMoveFactory; consumer nhận vai trò nhỏ cần dùng.</summary>
public interface IProjectileMoveFactory
{
    IProjectileMoveSelection GetProjectileMoveSelection();
}

#endregion
