/// <summary>
/// Strategy của một <b>cách hiển thị VFX</b> (animation clip, particle, object tĩnh), đặt trên gốc prefab visual.
/// </summary>
/// <remarks>
/// <see cref="EffectView"/> gọi qua interface này nên không cần biết bên trong là animation hay particle.
/// Visual chỉ điều khiển cách hiển thị; vòng đời và việc trả về pool thuộc <see cref="CombatVfx"/> (qua EffectView).
/// Không có event hoàn tất: CombatVfx hỏi <see cref="Advance"/> mỗi frame, nên không có callback trễ nào
/// có thể trỏ sang một lượt pool đã đổi chủ.
/// </remarks>
public interface IEffectVisual
{
    #region Playback Contract

    /// <summary>Phát lại từ đầu, không mang trạng thái của lượt dùng trước. Gọi khi object đã bật.</summary>
    void Play();

    /// <summary>Dừng và xóa trạng thái hiển thị trước khi trả về pool.</summary>
    void StopAndReset();

    /// <summary>
    /// Tiến một frame; trả về true khi đã phát xong tự nhiên (CombatVfx sẽ thu hồi). CombatVfx gọi mỗi frame ở LateTick
    /// (qua EffectView), nên visual không tự đăng ký tick. <paramref name="deltaTime"/> = 0 khi game pause.
    /// </summary>
    bool Advance(float deltaTime);

    #endregion
}
