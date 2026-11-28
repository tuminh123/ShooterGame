using System;
using UnityEngine;

/// <summary>
/// Strategy <b>hành vi của một nhân vật</b>: cách di chuyển (input hay AI), cách nhận/gây sát thương và phản ứng khi chết.
/// </summary>
/// <remarks>
/// Implementation: <see cref="PlayerController"/> (joystick) và <see cref="EnemyController"/> (AI đuổi Player, tách đàn, đội hình).
/// Cả hai thay thế được cho nhau hoàn toàn: <see cref="CharacterView"/> không bao giờ kiểm tra kiểu controller.
/// Những khả năng chỉ một loại có (khóa đòn đánh, va chạm đội hình, gizmo tách đàn) đều có mặc định
/// trong <see cref="CharacterControllerBase"/>, nên Player không phải viết hàm rỗng.
///
/// Mọi hàm nhận (view, runtime) của đúng lượt spawn; controller tự bỏ qua lời gọi thuộc lượt spawn cũ.
/// Một controller chỉ phục vụ một lượt spawn và bị <see cref="IDisposable.Dispose"/> khi lượt đó kết thúc.
/// </remarks>
public interface ICharacterController : IDisposable
{
    #region Frame Update

    /// <summary>Mỗi frame: xử lý chết do giảm max HP, rồi quyết định hướng di chuyển.</summary>
    void Tick(CharacterView view, CharacterRuntimeData runtime);

    /// <summary>
    /// Gọi sau khi skill của frame đã chạy xong. Enemy trong đội hình dùng để xử lý va chạm với Player
    /// (tách khỏi đội hình và bị đẩy lùi); Player không làm gì.
    /// </summary>
    /// <param name="spawn">Lượt spawn đã được tick trong frame này; bỏ qua nếu nó không còn sống hoặc đã đổi.</param>
    void CompletePatternContact(CharacterHandle spawn);

    /// <summary>Dừng hoặc tiếp tục state riêng của controller (ví dụ đếm thời gian bị đẩy lùi).</summary>
    void SetPaused(bool paused);

    /// <summary>Nhân vật có được dùng skill/đánh lúc này không. Mặc định luôn được.</summary>
    bool CanAttack { get; }

    #endregion

    #region Combat

    /// <summary>Nhận một đòn: giáp hấp thụ trước, rồi tới HP. Đòn chí mạng có thể kết thúc lượt spawn ngay trong lời gọi này.</summary>
    DamageResult ReceiveDamage(CharacterView view, CharacterRuntimeData runtime, DamageInfo damage);

    /// <summary>Gây sát thương lên đúng lượt spawn của <paramref name="target"/>, tính từ chỉ số của nhân vật này.</summary>
    DamageResult DealDamage(CharacterView view, CharacterRuntimeData runtime, CharacterHandle target, float multiplier = 1f);

    /// <summary>Cộng hoặc trừ HP trực tiếp, bỏ qua giáp. Không hồi sinh nhân vật đã chết.</summary>
    void ChangeHealth(CharacterView view, CharacterRuntimeData runtime, float amount);

    /// <summary>Cấp thêm giáp (tăng cả max và current).</summary>
    void GrantShield(CharacterView view, CharacterRuntimeData runtime, float amount);

    /// <summary>Hồi HP/giáp đã mất, không vượt max (hút máu, vùng hồi máu, hồi giáp).</summary>
    void Restore(CharacterView view, CharacterRuntimeData runtime, float health, float shield);

    /// <summary>Bị skill đẩy/kéo một quãng theo hướng cho trước; controller không hỗ trợ thì bỏ qua.</summary>
    void Push(CharacterView view, Vector2 direction, float distance, float duration);

    #endregion

    #region Feedback

    /// <summary>(loại, đang bật). View nghe để đổi màu; controller không biết màu cụ thể.</summary>
    event Action<CharacterFeedbackKind, bool> FeedbackChanged;

    /// <summary>EffectSystem báo một effect trạng thái bắt đầu hoặc kết thúc trên đúng lượt spawn.</summary>
    /// <param name="target">Lượt spawn mà effect đang tác động; bỏ qua nếu không còn đúng lượt spawn của controller.</param>
    void NotifyEffect(CharacterHandle target, CharacterFeedbackKind kind, bool active);

    #endregion

    #region Editor Debug

    /// <summary>Vẽ phạm vi tách đàn khi chọn nhân vật trong Scene view. Chỉ có tác dụng trong Editor.</summary>
    void DrawSeparationGizmo(Vector3 position);

    #endregion
}
