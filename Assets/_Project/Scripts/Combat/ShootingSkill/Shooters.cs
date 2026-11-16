using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class ShootBehaviorFactory : IShootBehaviorFactory, IShooterFactory, IShotDirection
{

    #region Dependencies And State
    // Danh mục kiểu bắn được hỗ trợ; không phụ thuộc vào class skill.
    private static readonly HashSet<Type> supportedShooters = new HashSet<Type>
    {
        typeof(StraightShooter), typeof(FanShooter), typeof(CircularShooter), typeof(SpiralShooter)
    };

    #endregion

    #region Validation

    public static void ValidateSupported(IShooter shooter)
    {
        if (shooter == null || !supportedShooters.Contains(shooter.GetType()))
            throw new ArgumentException("Unsupported shooter configuration.", nameof(shooter));
    }

    #endregion

    #region Creation

    // Factory chung chọn loại, nhưng không chia sẻ trạng thái Spiral giữa các nhân vật/skill.
    public IShooter CreateRuntime(IShooter template)
    {
        ValidateSupported(template);
        template.GetDefinition().Validate();
        return template.GetDefinition().CreateRuntime();
    }

    #endregion

    #region Runtime Operations

    public Vector2 ShootingHandle(
        IShooter selectedShooter,
        Vector2 aimDirection,
        int projectileIndex,
        int projectileCount)
    {
        if (selectedShooter == null) throw new ArgumentNullException(nameof(selectedShooter));
        ValidateSupported(selectedShooter);

        // Dùng đúng instance đã cấu hình góc trong asset, không thay bằng instance mặc định.
        return selectedShooter.GetExecution().GetDirection(aimDirection, projectileIndex, projectileCount);
    }

    #endregion

    #region Facade Access

    // Các getter trả về vai trò của cùng instance; không tạo thêm service.
    public IShooterFactory GetFactory() => this;

    public IShotDirection GetDirectionProvider() => this;

    #endregion
}



[Serializable]
public sealed class StraightShooter : IShooter, IShooterDefinition, IShooterExecution
{

    #region Creation
    public IShooter CreateRuntime() => (StraightShooter)MemberwiseClone();

    #endregion

    #region Runtime Operations

    public void OnShotCompleted() { } // Kiểu bắn này không có trạng thái giữa các lượt.

    #endregion

    #region Validation

    public void Validate() { } // Bắn thẳng không có thông số góc.

    #endregion

    #region Runtime Operations

    public Vector2 GetDirection(Vector2 aimDirection,
        int projectileIndex, int projectileCount) => aimDirection;

    #endregion

    #region Facade Access

    // Các getter trả về vai trò của cùng instance; không tạo thêm service.
    public IShooterDefinition GetDefinition() => this;

    public IShooterExecution GetExecution() => this;

    #endregion
}

[Serializable]
public sealed class FanShooter : IShooter, IShooterDefinition, IShooterExecution
{

    #region Dependencies And State
    [SerializeField, Range(0f, 360f)] private float spreadAngle = 60f; // Tổng góc quạt; các viên tự chia đều quanh hướng ngắm.

    #endregion

    #region Creation

    public IShooter CreateRuntime() => (FanShooter)MemberwiseClone();

    #endregion

    #region Runtime Operations

    public void OnShotCompleted() { }

    #endregion

    #region Validation

    public void Validate()
    {
        if (!ShotGeometry.IsFiniteNumber(spreadAngle) || spreadAngle < 0f || spreadAngle > 360f)
            throw new InvalidOperationException("Invalid fan shooting angle.");
    }

    #endregion

    #region Runtime Operations

    public Vector2 GetDirection(Vector2 aimDirection,
        int projectileIndex, int projectileCount)
    {
        // Một viên giữ hướng ngắm; nhiều viên trải đều hai bên hướng ngắm.
        float spreadOffsetDegrees = projectileCount == 1 ? 0f :
            -spreadAngle * 0.5f + spreadAngle * projectileIndex / (projectileCount - 1);
        return ShotGeometry.Rotate(aimDirection, spreadOffsetDegrees);
    }

    #endregion

    #region Facade Access

    // Các getter trả về vai trò của cùng instance; không tạo thêm service.
    public IShooterDefinition GetDefinition() => this;

    public IShooterExecution GetExecution() => this;

    #endregion
}

[Serializable]
public class CircularShooter : IShooter, IShooterDefinition, IShooterExecution
{

    #region Dependencies And State
    [SerializeField] private float startAngle; // Góc bắt đầu vòng đạn so với hướng ngắm.

    #endregion

    #region Creation

    public virtual IShooter CreateRuntime() => (CircularShooter)MemberwiseClone();

    #endregion

    #region Runtime Operations

    public virtual void OnShotCompleted() { } // Vòng đạn thông thường giữ nguyên góc giữa các lượt.

    #endregion

    #region Validation

    public virtual void Validate()
    {
        if (!ShotGeometry.IsFiniteNumber(startAngle))
            throw new InvalidOperationException("Invalid circular shooting angle.");
    }

    #endregion

    #region Runtime Operations

    public virtual Vector2 GetDirection(Vector2 aimDirection,
        int projectileIndex, int projectileCount)
    {
        float shotAngleDegrees = startAngle + 360f * projectileIndex / projectileCount;
        return ShotGeometry.Rotate(aimDirection, shotAngleDegrees);
    }

    #endregion

    #region Facade Access

    // Các getter trả về vai trò của cùng instance; không tạo thêm service.
    public IShooterDefinition GetDefinition() => this;

    public IShooterExecution GetExecution() => this;

    #endregion
}

[Serializable]
public sealed class SpiralShooter : CircularShooter
{

    #region Dependencies And State
    [SerializeField] private float rotationPerCast = 15f; // Góc xoay thêm sau mỗi lượt bắn thành công.
    [NonSerialized] private float castRotationDegrees; // Góc tích lũy của riêng shooter đang chạy.

    #endregion

    #region Creation

    public override IShooter CreateRuntime()
    {
        var runtimeShooter = (SpiralShooter)MemberwiseClone();
        runtimeShooter.castRotationDegrees = 0f;
        return runtimeShooter;
    }

    #endregion

    #region Runtime Operations

    public override void OnShotCompleted()
        => castRotationDegrees = Mathf.Repeat(castRotationDegrees + rotationPerCast, 360f);

    #endregion

    #region Validation

    public override void Validate()
    {
        base.Validate();
        if (!ShotGeometry.IsFiniteNumber(rotationPerCast))
            throw new InvalidOperationException("Invalid spiral rotation per cast.");
    }

    #endregion

    #region Runtime Operations

    public override Vector2 GetDirection(Vector2 aimDirection,
        int projectileIndex, int projectileCount)
    {
        Vector2 circularDirection = base.GetDirection(aimDirection, projectileIndex, projectileCount);
        return ShotGeometry.Rotate(circularDirection, castRotationDegrees);
    }

    #endregion
}

internal static class ShotGeometry
{

    #region Runtime Operations
    // Loại NaN/vô cực để phép xoay không sinh ra hướng không hợp lệ.
    public static bool IsFiniteNumber(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

    public static Vector2 Rotate(Vector2 inputDirection, float angleDegrees)
    {
        float angleRadians = angleDegrees * Mathf.Deg2Rad;
        float sine = Mathf.Sin(angleRadians), cosine = Mathf.Cos(angleRadians);
        return new Vector2(inputDirection.x * cosine - inputDirection.y * sine,
            inputDirection.x * sine + inputDirection.y * cosine);
    }

    #endregion
}
