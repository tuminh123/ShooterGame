// Dịch vụ dùng chung giữa các panel UI. UI là tầng lá: không miền nào khác dùng các interface này.
// Panel UI là MonoBehaviour trong scene (Zenject inject tự động), nên UI không có một Manager C# thuần như các miền khác;
// UIManager chỉ điều phối hiển thị theo GameState. ScreenTransitionController là binding duy nhất của UI
// vì Home và menu pause cùng cần nó.
// Xem Docs/Architecture/Refactor-Architecture.md.

#region Screen Transition (Home, menu pause)

/// <summary>Che màn hình rồi gửi lệnh bắt đầu, chơi lại hoặc về Home. Dùng bởi HomeController, PauseUI và ResultController.</summary>
public interface IScreenTransition
{
    /// <summary>Đang che/mở màn; các nút nên bỏ qua click trong lúc này.</summary>
    bool IsTransitioning { get; }

    bool StartRun();
    bool RestartRun();
    bool ReturnHome();
}

#endregion
