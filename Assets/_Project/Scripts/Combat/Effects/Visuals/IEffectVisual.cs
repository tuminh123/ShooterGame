using System;

// Hợp đồng chung cho component trên gốc prefab visual.
// EffectView hoặc đối tượng sở hữu khác gọi qua interface, không cần biết nội dung là GameObject hay particle.
public interface IEffectVisual
{
    #region Playback Contract

    event Action Completed; // Chỉ báo kết thúc tự nhiên, không báo khi hủy/cleanup.

    // Đối tượng sở hữu quản lý lifetime và pooling; component visual chỉ điều khiển cách hiển thị.
    void Play(); // Phát lại từ đầu, không mang trạng thái của lượt dùng trước.
    void StopAndReset(); // Dừng và xóa trạng thái hiển thị trước khi trả về pool.

    #endregion
}
