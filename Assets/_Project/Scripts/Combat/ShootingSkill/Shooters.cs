using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class ShootBehaviorFactory
{
    // Danh mục kiểu bắn được hỗ trợ; không phụ thuộc vào class skill.
    private static readonly HashSet<Type> supportedShooters = new HashSet<Type>
    {
        typeof(StraightShooter), typeof(CircularShooter), typeof(SpiralShooter)
    };

    public static void ValidateSupported(IShooter shooter)
    {
        if (shooter == null || !supportedShooters.Contains(shooter.GetType()))
            throw new ArgumentException("Unsupported shooter configuration.", nameof(shooter));
    }

    // Factory chung chọn loại, nhưng không chia sẻ trạng thái Spiral giữa các nhân vật/skill.
    public IShooter CreateRuntime(IShooter template)
    {
        ValidateSupported(template);
        template.Validate();
        return template.CreateRuntime();
    }

    public Vector2 ShootingHandle(IShooter selectedShooter, Vector2 aimDirection,
        int projectileIndex, int projectileCount)
    {
        if (selectedShooter == null) throw new ArgumentNullException(nameof(selectedShooter));
        ValidateSupported(selectedShooter);

        // Dùng đúng instance đã cấu hình góc trong asset, không thay bằng instance mặc định.
        return selectedShooter.GetDirection(aimDirection, projectileIndex, projectileCount);
    }
}

public interface IShooter
{
    void Validate();

    // Sao chép cấu hình để mỗi skill có trạng thái chạy độc lập.
    IShooter CreateRuntime();

    // Gọi một lần khi lượt bắn đã tạo được đạn, không gọi sau từng viên.
    void OnShotCompleted();

    // Chỉ tính hướng từng viên; không tạo đạn hay quản lý cooldown.
    Vector2 GetDirection(Vector2 aimDirection,
        int projectileIndex, int projectileCount);
}

[Serializable]
public sealed class StraightShooter : IShooter
{
    [SerializeField, Range(0f, 360f)] private float spreadAngle; // Độ rộng cả chùm đạn, tính bằng độ.
    [SerializeField] private float startAngle; // Góc lệch của cả chùm so với hướng ngắm.

    public IShooter CreateRuntime() => (StraightShooter)MemberwiseClone();

    public void OnShotCompleted() { } // Kiểu bắn này không có trạng thái giữa các lượt.

    public void Validate()
    {
        if (!ShotGeometry.IsFiniteNumber(spreadAngle) || spreadAngle < 0f || spreadAngle > 360f ||
            !ShotGeometry.IsFiniteNumber(startAngle))
            throw new InvalidOperationException("Invalid straight shooting angles.");
    }

    public Vector2 GetDirection(Vector2 aimDirection,
        int projectileIndex, int projectileCount)
    {
        // Một viên giữ hướng ngắm; nhiều viên trải đều hai bên hướng ngắm.
        float spreadOffsetDegrees = projectileCount == 1 ? 0f :
            -spreadAngle * 0.5f + spreadAngle * projectileIndex / (projectileCount - 1);
        return ShotGeometry.Rotate(aimDirection, startAngle + spreadOffsetDegrees);
    }
}

[Serializable]
public class CircularShooter : IShooter
{
    [SerializeField] private float startAngle; // Góc bắt đầu vòng đạn so với hướng ngắm.

    public virtual IShooter CreateRuntime() => (CircularShooter)MemberwiseClone();

    public virtual void OnShotCompleted() { } // Vòng đạn thông thường giữ nguyên góc giữa các lượt.

    public virtual void Validate()
    {
        if (!ShotGeometry.IsFiniteNumber(startAngle))
            throw new InvalidOperationException("Invalid circular shooting angle.");
    }

    public virtual Vector2 GetDirection(Vector2 aimDirection,
        int projectileIndex, int projectileCount)
    {
        float shotAngleDegrees = startAngle + 360f * projectileIndex / projectileCount;
        return ShotGeometry.Rotate(aimDirection, shotAngleDegrees);
    }
}

[Serializable]
public sealed class SpiralShooter : CircularShooter
{
    [SerializeField] private float rotationPerCast = 15f; // Góc xoay thêm sau mỗi lượt bắn thành công.
    [NonSerialized] private float castRotationDegrees; // Góc tích lũy của riêng shooter đang chạy.

    public override IShooter CreateRuntime()
    {
        var runtimeShooter = (SpiralShooter)MemberwiseClone();
        runtimeShooter.castRotationDegrees = 0f;
        return runtimeShooter;
    }

    public override void OnShotCompleted()
        => castRotationDegrees = Mathf.Repeat(castRotationDegrees + rotationPerCast, 360f);

    public override void Validate()
    {
        base.Validate();
        if (!ShotGeometry.IsFiniteNumber(rotationPerCast))
            throw new InvalidOperationException("Invalid spiral rotation per cast.");
    }

    public override Vector2 GetDirection(Vector2 aimDirection,
        int projectileIndex, int projectileCount)
    {
        Vector2 circularDirection = base.GetDirection(aimDirection, projectileIndex, projectileCount);
        return ShotGeometry.Rotate(circularDirection, castRotationDegrees);
    }
}

internal static class ShotGeometry
{
    // Loại NaN/vô cực để phép xoay không sinh ra hướng không hợp lệ.
    public static bool IsFiniteNumber(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

    public static Vector2 Rotate(Vector2 inputDirection, float angleDegrees)
    {
        float angleRadians = angleDegrees * Mathf.Deg2Rad;
        float sine = Mathf.Sin(angleRadians), cosine = Mathf.Cos(angleRadians);
        return new Vector2(inputDirection.x * cosine - inputDirection.y * sine,
            inputDirection.x * sine + inputDirection.y * cosine);
    }
}
