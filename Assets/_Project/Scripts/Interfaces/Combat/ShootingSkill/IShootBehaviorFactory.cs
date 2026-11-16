using UnityEngine;

#region ShooterFactory

/// <summary>Tạo trạng thái kiểu bắn riêng từ mẫu cấu hình.</summary>
public interface IShooterFactory
{
    IShooter CreateRuntime(IShooter template);
}

#endregion

#region ShotDirection

/// <summary>Tính hướng cho từng viên trong một lượt bắn.</summary>
public interface IShotDirection
{
    Vector2 ShootingHandle(IShooter selectedShooter, Vector2 aimDirection, int projectileIndex, int projectileCount);
}

#endregion

#region System Contract

/// <summary>Gom hợp đồng của ShootBehaviorFactory; consumer nhận vai trò nhỏ cần dùng.</summary>
public interface IShootBehaviorFactory
{
    IShooterFactory GetFactory();
    IShotDirection GetDirectionProvider();
}

#endregion
