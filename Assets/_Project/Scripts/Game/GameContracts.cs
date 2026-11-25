using System;

// Ranh giới của miền Game: đây là TẤT CẢ những gì miền khác được phép dùng.
// Mọi interface trong file này do GameManager implement. GameStateMachine, GamePause và GamePrewarm nằm bên trong
// miền, không được bind vào Zenject.
// Game nằm trên mọi miền gameplay (gọi xuống trực tiếp); chỉ UI nằm trên Game và dùng các vai trò dưới đây.
// Xem Docs/Architecture/Refactor-Architecture.md.

#region State (UI)

/// <summary>Đọc trạng thái luồng game. Dùng bởi UIManager và các panel UI.</summary>
public interface IGameState
{
    GameState State { get; }

    /// <summary>Lỗi khiến game vào <see cref="GameState.Faulted"/>; null nếu chưa có lỗi.</summary>
    Exception LastError { get; }

    /// <summary>
    /// (state trước, state sau), phát sau khi <see cref="State"/> đã đổi. Chỉ tầng UI nghe event này;
    /// các miền gameplay được GameManager gọi trực tiếp.
    /// </summary>
    event Action<GameState, GameState> StateChanged;
}

#endregion

#region Flow Commands (UI)

/// <summary>Bắt đầu, chơi lại hoặc rời lượt về Home. Dùng bởi ScreenTransitionController và popup kết quả.</summary>
public interface IGameFlow
{
    /// <summary>Từ Home/Won/Lost vào trận mới; false nếu đang chuyển state hoặc chưa sẵn sàng.</summary>
    bool StartBattle();

    /// <summary>Chơi lại khi đang Playing (từ menu pause).</summary>
    bool RestartBattle();

    /// <summary>Dọn trận và về Home trong cùng scene.</summary>
    bool ReturnHome();
}

#endregion

#region Pause (UI)

/// <summary>
/// Token pause/slow motion nhiều nguồn: mỗi nguồn giữ token riêng, mức chậm nhất có hiệu lực tới khi token được trả.
/// Dùng bởi menu pause, bảng chọn skill và chuyển cảnh.
/// </summary>
public interface IGamePause
{
    /// <summary>Dừng gameplay (timeScale = 0) tới khi Dispose token.</summary>
    IDisposable Acquire();

    /// <summary>Làm chậm theo hệ số (0, 1] tới khi Dispose token; không gỡ được pause của nguồn khác.</summary>
    IDisposable AcquireSlowMotion(float multiplier);
}

#endregion

#region Loading (UI)

/// <summary>Tiến độ chuẩn bị tài nguyên lúc khởi động. Dùng bởi màn Loading.</summary>
public interface ILoadingProgress
{
    /// <summary>Mọi pool và buffer đã sẵn sàng.</summary>
    bool IsReady { get; }

    /// <summary>Tỉ lệ 0..1.</summary>
    float Progress { get; }

    event Action<float> ProgressChanged;
}

#endregion
