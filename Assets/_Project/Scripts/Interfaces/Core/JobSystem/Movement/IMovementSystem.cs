using UnityEngine;

#region MovementRegistration

/// <summary>Đăng ký đối tượng và kiểm tra danh tính lượt di chuyển.</summary>
public interface IMovementRegistration
{
    MovementHandle Register(Transform target);
    bool IsRegistered(MovementHandle handle);
    bool Unregister(MovementHandle handle);
}

#endregion

#region MovementCommand

/// <summary>Gửi hướng, tốc độ và giới hạn quãng đường cho đối tượng.</summary>
public interface IMovementCommand
{
    bool SetMovement(MovementHandle handle, Vector2 direction, float speed, float maxDistance = float.PositiveInfinity);
}

#endregion

#region MovementArena

/// <summary>Thiết lập hoặc gỡ giới hạn chuyển động trong đấu trường.</summary>
public interface IMovementArena
{
    void SetArenaBounds(Bounds? bounds);
}

#endregion

#region MovementCapacity

/// <summary>Chuẩn bị dung lượng trước gameplay.</summary>
public interface IMovementCapacity
{
    int Count { get; }
    void PrewarmCapacity(int count);
}

#endregion

#region System Contract

/// <summary>Gom hợp đồng của MovementManager; consumer nhận vai trò nhỏ cần dùng.</summary>
public interface IMovementSystem
{
    IMovementRegistration GetRegistration();
    IMovementCommand GetCommands();
    IMovementArena GetArena();
    IMovementCapacity GetCapacity();
}

#endregion
