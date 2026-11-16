using UnityEngine;

#region ShooterDefinition

/// <summary>Kiểm tra cấu hình và tạo trạng thái kiểu bắn riêng.</summary>
public interface IShooterDefinition
{
    void Validate();
    IShooter CreateRuntime();
}

#endregion

#region ShooterExecution

/// <summary>Tính hướng từng viên và cập nhật trạng thái sau một lượt bắn.</summary>
public interface IShooterExecution
{
    void OnShotCompleted();
    Vector2 GetDirection(Vector2 aimDirection, int projectileIndex, int projectileCount);
}

#endregion

#region Aggregate Contract

/// <summary>Gom các vai trò của Shooter; không thêm hành vi.</summary>
public interface IShooter
{
    IShooterDefinition GetDefinition();
    IShooterExecution GetExecution();
}

#endregion
