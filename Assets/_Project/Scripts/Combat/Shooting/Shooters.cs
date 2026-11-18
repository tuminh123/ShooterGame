using System;
using UnityEngine;

// Các kiểu bắn (strategy của IShooter). Asset chọn kiểu bắn qua [SerializeReference].
// KHÔNG đổi tên class hoặc tên field [SerializeField] trong file này: Unity lưu tên class và tên field
// trong asset skill, nên đổi tên sẽ làm mất cấu hình đã nhập.

/// <summary>Bắn mọi viên theo đúng hướng ngắm.</summary>
[Serializable]
public sealed class StraightShooter : IShooter
{
    #region IShooter

    /// <inheritdoc/>
    public void Validate() { } // Không có thông số để kiểm tra.

    /// <inheritdoc/>
    public IShooter CreateRuntime() => (StraightShooter)MemberwiseClone();

    /// <inheritdoc/>
    public Vector2 GetDirection(Vector2 aimDirection, int projectileIndex, int projectileCount) => aimDirection;

    /// <inheritdoc/>
    public void OnShotCompleted() { } // Không có trạng thái giữa các lượt bắn.

    #endregion
}

/// <summary>Chia các viên thành hình quạt đối xứng quanh hướng ngắm.</summary>
[Serializable]
public sealed class FanShooter : IShooter
{
    #region Configuration

    [SerializeField, Range(0f, 360f)] private float spreadAngle = 60f; // Tổng góc của quạt (độ).

    #endregion

    #region IShooter

    /// <inheritdoc/>
    public void Validate()
    {
        if (!ShotGeometry.IsFiniteNumber(spreadAngle) || spreadAngle < 0f || spreadAngle > 360f)
            throw new InvalidOperationException("Invalid fan shooting angle.");
    }

    /// <inheritdoc/>
    public IShooter CreateRuntime() => (FanShooter)MemberwiseClone();

    /// <inheritdoc/>
    public Vector2 GetDirection(Vector2 aimDirection, int projectileIndex, int projectileCount)
    {
        // Một viên thì giữ đúng hướng ngắm; nhiều viên thì trải đều từ -spread/2 tới +spread/2.
        float offsetDegrees = projectileCount == 1
            ? 0f
            : -spreadAngle * 0.5f + spreadAngle * projectileIndex / (projectileCount - 1);
        return ShotGeometry.Rotate(aimDirection, offsetDegrees);
    }

    /// <inheritdoc/>
    public void OnShotCompleted() { } // Không có trạng thái giữa các lượt bắn.

    #endregion
}

/// <summary>Chia đều các viên trên một vòng tròn 360°, bắt đầu từ một góc lệch so với hướng ngắm.</summary>
/// <remarks>Không sealed vì <see cref="SpiralShooter"/> kế thừa phần chia vòng tròn.</remarks>
[Serializable]
public class CircularShooter : IShooter
{
    #region Configuration

    [SerializeField] private float startAngle; // Góc của viên đầu tiên so với hướng ngắm (độ).

    #endregion

    #region IShooter

    /// <inheritdoc/>
    public virtual void Validate()
    {
        if (!ShotGeometry.IsFiniteNumber(startAngle))
            throw new InvalidOperationException("Invalid circular shooting angle.");
    }

    /// <inheritdoc/>
    public virtual IShooter CreateRuntime() => (CircularShooter)MemberwiseClone();

    /// <inheritdoc/>
    public virtual Vector2 GetDirection(Vector2 aimDirection, int projectileIndex, int projectileCount)
    {
        float angleDegrees = startAngle + 360f * projectileIndex / projectileCount;
        return ShotGeometry.Rotate(aimDirection, angleDegrees);
    }

    /// <inheritdoc/>
    public virtual void OnShotCompleted() { } // Vòng tròn thường giữ nguyên góc giữa các lượt.

    #endregion
}

/// <summary>Như <see cref="CircularShooter"/>, nhưng cả vòng xoay thêm một góc sau mỗi lượt bắn.</summary>
[Serializable]
public sealed class SpiralShooter : CircularShooter
{
    #region Configuration And Runtime State

    [SerializeField] private float rotationPerCast = 15f; // Góc xoay thêm sau mỗi lượt bắn thành công (độ).

    // Góc đã tích lũy của riêng bản sao đang chạy. NonSerialized để không bao giờ ghi vào asset.
    [NonSerialized] private float castRotationDegrees;

    #endregion

    #region IShooter

    /// <inheritdoc/>
    public override void Validate()
    {
        base.Validate();
        if (!ShotGeometry.IsFiniteNumber(rotationPerCast))
            throw new InvalidOperationException("Invalid spiral rotation per cast.");
    }

    /// <inheritdoc/>
    public override IShooter CreateRuntime()
    {
        var runtime = (SpiralShooter)MemberwiseClone();
        runtime.castRotationDegrees = 0f; // Mỗi skill bắt đầu xoắn từ góc 0, không kế thừa góc của template.
        return runtime;
    }

    /// <inheritdoc/>
    public override Vector2 GetDirection(Vector2 aimDirection, int projectileIndex, int projectileCount)
    {
        Vector2 circularDirection = base.GetDirection(aimDirection, projectileIndex, projectileCount);
        return ShotGeometry.Rotate(circularDirection, castRotationDegrees);
    }

    /// <inheritdoc/>
    public override void OnShotCompleted()
        => castRotationDegrees = Mathf.Repeat(castRotationDegrees + rotationPerCast, 360f);

    #endregion
}

/// <summary>Phép tính hình học dùng chung cho các kiểu bắn.</summary>
internal static class ShotGeometry
{
    /// <summary>Loại NaN và vô cực, để phép xoay không sinh ra hướng không hợp lệ.</summary>
    public static bool IsFiniteNumber(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

    /// <summary>Xoay một vector 2D quanh gốc tọa độ một góc tính bằng độ (dương là ngược chiều kim đồng hồ).</summary>
    public static Vector2 Rotate(Vector2 direction, float angleDegrees)
    {
        float radians = angleDegrees * Mathf.Deg2Rad;
        float sine = Mathf.Sin(radians), cosine = Mathf.Cos(radians);
        return new Vector2(direction.x * cosine - direction.y * sine,
                           direction.x * sine + direction.y * cosine);
    }
}
