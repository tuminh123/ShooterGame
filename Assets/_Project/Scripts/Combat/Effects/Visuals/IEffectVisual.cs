// Component trên gốc prefab visual; EffectView gọi khi bắt đầu và khi dọn lượt phát.
public interface IEffectVisual
{
    #region Playback Contract

    // EffectView sở hữu lifetime và pooling; component visual chỉ điều khiển cách hiển thị.
    void Play(); // Phát lại từ đầu, không mang trạng thái của lượt dùng trước.
    void StopAndReset(); // Dừng và xóa trạng thái hiển thị trước khi trả về pool.

    #endregion
}
