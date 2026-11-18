using System;
using UnityEngine;

/// <summary>
/// Strategy của một <b>cách hiển thị VFX</b> (animation clip, particle, object tĩnh), đặt trên gốc prefab visual.
/// </summary>
/// <remarks>
/// <see cref="EffectView"/> gọi qua interface này nên không cần biết bên trong là animation hay particle.
/// Visual chỉ điều khiển cách hiển thị; vòng đời và việc trả về pool thuộc bên sở hữu (EffectView).
/// </remarks>
public interface IEffectVisual
{
    #region Playback Contract

    event Action Completed; // Chỉ báo kết thúc tự nhiên, không báo khi hủy/cleanup.

    // Đối tượng sở hữu quản lý lifetime và pooling; component visual chỉ điều khiển cách hiển thị.
    void Play(); // Phát lại từ đầu, không mang trạng thái của lượt dùng trước.
    void StopAndReset(); // Dừng và xóa trạng thái hiển thị trước khi trả về pool.

    #endregion
}
