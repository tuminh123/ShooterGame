using UnityEngine;

/// <summary>
/// Phản lực của MỘT con quái sau khi đánh trúng Player: lùi một quãng cố định trong một khoảng thời gian,
/// theo hướng chốt lúc trúng đòn.
/// </summary>
/// <remarks>
/// Dịch chuyển bằng lệnh Move của <see cref="CharacterView"/> (MovementSystem), không dùng lực Rigidbody.
/// Không gây choáng hay khóa skill; cooldown vẫn do skill quản lý.
/// </remarks>
internal sealed class EnemyKnockback
{
    #region State

    private float remaining; // Giây còn lại của lần lùi hiện tại.
    private float distance;
    private float duration;
    private Vector2 direction; // Hướng lùi chốt lúc trúng, không đổi theo Player trong lúc lùi.

    /// <summary>Đang lùi: controller bỏ qua đuổi/tách cho tới khi xong.</summary>
    internal bool IsActive => remaining > 0f;

    #endregion

    #region Knockback

    /// <summary>
    /// Bắt đầu lùi ra xa theo <paramref name="away"/>. Trả false (không làm gì) nếu quãng lùi bằng 0
    /// hoặc vỏ đang tắt.
    /// </summary>
    internal bool Start(CharacterView view, Vector2 away, float knockbackDistance, float knockbackDuration)
    {
        if (knockbackDistance <= 0f || !view.isActiveAndEnabled) return false;

        distance = knockbackDistance;
        duration = Mathf.Max(0.01f, knockbackDuration);
        if (away.sqrMagnitude < 0.000001f) away = Vector2.right;
        direction = away.normalized;
        remaining = duration;
        return true;
    }

    /// <summary>Gửi một bước lùi của frame. Trả false khi không còn đang lùi.</summary>
    internal bool Apply(CharacterView view, CharacterView player)
    {
        if (remaining <= 0f) return false;

        float stepTime = Mathf.Min(Time.deltaTime, remaining);
        remaining = Mathf.Max(0f, remaining - stepTime);
        // Bước cuối chỉ dùng phần duration còn lại, tránh lùi quá quãng đường cấu hình.
        float speed = (distance / duration) * (stepTime / Time.deltaTime);
        // Vẫn quay mặt về Player trong lúc lùi.
        float facing = player != null ? player.transform.position.x - view.transform.position.x : -direction.x;
        view.Move(direction, speed, facingDirection: facing);
        return true;
    }

    /// <summary>Hủy lần lùi đang chạy (controller dispose).</summary>
    internal void Clear() => remaining = 0f;

    #endregion
}
