using System;

// Ranh giới của miền Clock, do ClockManager implement. Miền nào cần giờ (Mission, sau này Login Check-in)
// chỉ biết các interface này, không đọc DateTime.UtcNow trực tiếp và không biết giờ đến từ máy hay server.

#region Clock (Mission, Login Check-in)

/// <summary>Đọc giờ UTC theo nguồn mà data chọn (<see cref="ClockSource"/>).</summary>
public interface IGameClock
{
    /// <summary>
    /// Giờ hiện tại của nguồn <paramref name="source"/>. False = nguồn chưa có giờ (ví dụ chưa hỏi được server):
    /// bên gọi phải ẩn tính năng và không cho nhận thưởng. Một hàm duy nhất để không thể đọc giờ mà quên kiểm tra.
    /// </summary>
    bool TryGetUtcNow(ClockSource source, out DateTime utcNow);

    /// <summary>Một nguồn vừa có hoặc vừa mất giờ (server vừa trả lời, hoặc app quay lại từ nền): bên dùng tính lại kỳ.</summary>
    event Action Changed;
}

#endregion

#region Sync (GameSceneInstaller)

/// <summary>Hỏi lại giờ khi app quay lại từ nền. Dùng bởi GameSceneInstaller.</summary>
public interface IClockSync
{
    /// <summary>Bỏ giờ server cũ và hỏi lại; trong lúc chờ, nguồn Server báo chưa có giờ.</summary>
    void Resync();
}

#endregion
