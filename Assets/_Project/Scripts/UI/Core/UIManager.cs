using System;
using System.Collections.Generic;
using UnityEngine;
using Zenject;

/// <summary>
/// Điều phối luồng giữa các UI; controller giữ logic nghiệp vụ riêng và gửi yêu cầu mở/đóng.
/// Không bind vào Zenject (không ai inject nó); SceneContext vẫn tự inject Construct vì nó nằm trong scene.
/// </summary>
/// <remarks>
/// GameManager quyết định luồng game và phát StateChanged; UIManager chỉ nghe và chọn "hiện gì":
/// màn gốc theo bảng route, các tab menu trong stack, và panel nền (chrome) theo <see cref="UIPanelController.ShowsInState"/>.
/// Panel được tự thu thập trong scene (kể cả đang tắt) và bị tắt hết khi bắt đầu, nên scene lưu bật hay tắt đều như nhau.
/// </remarks>
[AddComponentMenu("Shooter Game/UI/UI Manager")]
public sealed class UIManager : MonoBehaviour
{
    #region Scene References

    [Serializable]
    private sealed class StatePanelRoute
    {
        /// <summary>Trạng thái game mà route này trình bày.</summary>
        public GameState state;
        /// <summary>UI gốc cho trạng thái đó; null nghĩa là đóng UI hiện tại.</summary>
        public UIPanelController panel;
    }
    [SerializeField] private StatePanelRoute[] statePanels = Array.Empty<StatePanelRoute>(); // Mapping trạng thái → UI, không quyết định luồng game.

    #endregion

    #region Navigation State

    private readonly List<UIPanelController> panels = new List<UIPanelController>(); // Mọi panel trong scene, thu thập lúc đăng ký.
    private readonly List<UIPanelController> chromePanels = new List<UIPanelController>(); // Panel nền: không vào stack, hiện theo state.
    private readonly HashSet<UIPanelController> shownChrome = new HashSet<UIPanelController>(); // Panel nền đang hiện, để chỉ gọi khi đổi.
    private readonly List<UIPanelController> history = new List<UIPanelController>(); // Stack yêu cầu: đóng panel trên cùng thì quay về panel trước.
    private IGameState game; // Đọc state hiện tại khi UI được bật lại sau một khoảng tắt subscription.
    private UIPanelController rootPanel; // Gốc luồng hiện tại do SetRootPanel chỉ định lúc chạy, không phải cấu hình default.
    private UIPanelController activePanel; // Controller đang được manager cấp quyền hiển thị.
    private bool visible = true; // Ẩn toàn bộ UI vẫn bảo toàn lịch sử để tiếp tục khi hiện lại.
    private bool subscribed; // Chống đăng ký sự kiện lặp khi OnEnable và API điều hướng cùng chạy.
    private bool listeningToGame; // Tách khỏi `subscribed`: OnEnable có thể chạy trước Construct, lúc đó chưa có `game` để nghe.
    /// <summary>Chỉ đọc panel hiện tại để các hệ thống quan sát luồng UI.</summary>
    public UIPanelController ActivePanel => activePanel;

    #endregion

    #region Lifecycle And Registration

    // Lắng nghe yêu cầu của controller, kể cả yêu cầu đã phát trước khi manager được bật.
    private void OnEnable()
    {
        Subscribe();
        ListenToGame();
        if (game != null) ApplyGameState(game.State);
    }

    /// <summary>Zenject cung cấp nguồn state; UI chỉ đọc state để chọn màn hình, không tự chuyển state.</summary>
    [Inject]
    public void Construct(IGameState gameState)
    {
        this.game = gameState;
        if (!isActiveAndEnabled) return; // Chưa bật thì OnEnable sẽ tự đăng ký sau.
        Subscribe();
        ListenToGame();
        ApplyGameState(game.State);
    }

    // Mỗi state chỉ chọn route hiển thị, không spawn/reset hoặc quyết định thắng/thua.
    private void OnGameStateChanged(GameState previous, GameState current) => ApplyGameState(current);

    // Đăng ký đúng một lần, và chỉ khi Zenject đã cấp nguồn state.
    private void ListenToGame()
    {
        if (listeningToGame || game == null) return;
        listeningToGame = true;
        game.StateChanged += OnGameStateChanged;
    }

    private void StopListeningToGame()
    {
        if (!listeningToGame) return;
        listeningToGame = false;
        game.StateChanged -= OnGameStateChanged;
    }

    // Không có route thì đóng luồng UI cũ, không tự quay về Gameplay hoặc UI mặc định.
    // Panel nền không cần route: chúng tự khai báo state hiện qua ShowsInState.
    private void ApplyGameState(GameState state)
    {
        foreach (StatePanelRoute route in statePanels)
            if (route.state == state) { SetRootPanel(route.panel); return; }
        SetRootPanel(null);
    }

    // Thu thập panel trong scene (cả object đang tắt) để không phải đăng ký tay; tắt hết rồi dựng lại stack từ yêu cầu còn giữ.
    private void Subscribe()
    {
        if (subscribed) return;
        subscribed = true;
        CollectPanels();
        history.Clear();
        shownChrome.Clear();
        if (rootPanel != null) history.Add(rootPanel);
        foreach (UIPanelController panel in panels)
        {
            panel.VisibilityRequested -= OnVisibilityRequested;
            panel.VisibilityRequested += OnVisibilityRequested;
            panel.Present(false, false);
            if (!panel.IsChrome && panel != rootPanel && panel.WantsVisible && !history.Contains(panel)) history.Add(panel);
        }
        RefreshFlow(false);
    }

    private void CollectPanels()
    {
        panels.Clear();
        chromePanels.Clear();
        foreach (UIPanelController panel in FindObjectsOfType<UIPanelController>(true))
        {
            panels.Add(panel);
            if (panel.IsChrome) chromePanels.Add(panel);
        }
    }

    // Ngừng điều phối, ẩn modal và trả tài nguyên hiển thị qua controller; giữ ý định mở để khôi phục lúc bật lại.
    private void OnDisable()
    {
        StopListeningToGame();
        foreach (UIPanelController panel in panels)
        {
            if (panel == null) continue;
            panel.VisibilityRequested -= OnVisibilityRequested;
            panel.Present(false, false);
        }
        activePanel = null;
        history.Clear();
        shownChrome.Clear();
        subscribed = false;
    }

    #endregion

    #region Panel Navigation

    // Nhận yêu cầu chung; controller không gọi ngược UIManager và không biết panel nào đang đứng trước nó.
    private void OnVisibilityRequested(UIPanelController panel, bool open)
    {
        if (open) OpenPanel(panel);
        else ClosePanel(panel);
    }

    /// <summary>
    /// Đưa một panel lên đầu luồng, tạm ẩn panel trước và giữ vị trí để quay lại.
    /// Tab menu thay thế tab menu đang đứng trên cùng trong một lần cập nhật (không bật lại Home giữa hai tab).
    /// </summary>
    public void OpenPanel(UIPanelController panel)
    {
        Subscribe();
        EnsureNavigable(panel);
        if (panel == rootPanel) { SetRootPanel(panel); return; }
        panel.RecordVisibilityRequest(true);
        if (panel.IsMenuTab && history.Count > 0)
        {
            UIPanelController top = history[history.Count - 1];
            if (top != rootPanel && top != panel && top.IsMenuTab)
            {
                top.RecordVisibilityRequest(false);
                history.RemoveAt(history.Count - 1);
            }
        }
        history.Remove(panel);
        history.Add(panel);
        RefreshFlow(true);
    }

    /// <summary>Đóng một bước trong luồng; nếu là panel đang hiện thì phục hồi bước trước hoặc gốc luồng hiện tại.</summary>
    public void ClosePanel(UIPanelController panel)
    {
        Subscribe();
        if (panel != null && panel == rootPanel) return;
        if (panel != null) panel.RecordVisibilityRequest(false);
        history.Remove(panel);
        RefreshFlow(true);
    }

    /// <summary>Quay về UI trước thông qua cùng chính sách điều hướng; không tự bỏ queue nghiệp vụ của panel.</summary>
    public void Back()
    {
        if (activePanel != null && activePanel != rootPanel) ClosePanel(activePanel);
    }

    /// <summary>
    /// Chuyển gốc khi state game đổi: bỏ lịch sử cũ, không tự chọn màn hình hoặc giữ default panel.
    /// Truyền null để đóng mọi panel khi trạng thái chưa có UI tương ứng.
    /// </summary>
    public void SetRootPanel(UIPanelController panel)
    {
        Subscribe();
        if (panel != null) EnsureNavigable(panel);
        foreach (UIPanelController previous in history) previous.RecordVisibilityRequest(false);
        history.Clear();
        rootPanel = panel;
        if (panel != null)
        {
            panel.RecordVisibilityRequest(true);
            history.Add(panel);
        }
        RefreshFlow(false); // Đổi state: màn đang tối nên bật/tắt ngay, không trượt.
    }

    // Panel nền không vào stack (chúng hiện theo state); panel lạ chưa có trong scene lúc thu thập cũng bị từ chối.
    private void EnsureNavigable(UIPanelController panel)
    {
        if (panel == null || !panels.Contains(panel) || panel.IsChrome)
            throw new ArgumentException("Only a screen panel that exists in the scene can be opened or set as root.", nameof(panel));
    }

    // Dùng chung một đường hiển thị cho mọi controller; chỉ một màn hoạt động tại một thời điểm, panel nền đi kèm theo state.
    // animate = true khi điều hướng trong menu (Open/Close/Back); false khi đổi state hoặc khởi động.
    private void RefreshFlow(bool animate)
    {
        UIPanelController next = visible && history.Count > 0 ? history[history.Count - 1] : null; // Lịch sử rỗng thì không có UI; GameState quyết định luồng tiếp theo.
        if (activePanel != next)
        {
            UIPanelController previous = activePanel; // Đổi state trước callback để không giữ panel cũ trong lúc nó đóng.
            activePanel = next;
            if (previous != null) previous.Present(false, animate);
            if (next != null) next.Present(true, animate);
        }
        RefreshChrome(animate);
    }

    // Panel nền hiện khi UI đang hiện và state hiện tại nằm trong ShowsInState của nó; chỉ gọi panel khi kết quả đổi.
    private void RefreshChrome(bool animate)
    {
        foreach (UIPanelController chrome in chromePanels)
        {
            bool want = visible && game != null && chrome.ShowsInState(game.State);
            if (want == shownChrome.Contains(chrome)) continue;
            if (want) shownChrome.Add(chrome);
            else shownChrome.Remove(chrome);
            chrome.Present(want, animate);
        }
    }

    #endregion

    #region Global Visibility

    /// <summary>Tạm ẩn/hiện toàn bộ luồng; không xóa lịch sử hay phần thưởng đang chờ của controller.</summary>
    public void SetVisible(bool value)
    {
        Subscribe();
        visible = value;
        RefreshFlow(false);
    }

    #endregion
}
