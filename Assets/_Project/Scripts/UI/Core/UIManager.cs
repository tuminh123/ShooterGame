using System;
using System.Collections.Generic;
using UnityEngine;
using Zenject;

// Điều phối luồng giữa các UI; controller giữ logic nghiệp vụ riêng và gửi yêu cầu mở/đóng.
[AddComponentMenu("Shooter Game/UI/UI Manager")]
public sealed class UIManager : MonoBehaviour
{
    #region Scene References

    [SerializeField] private UIPanelController[] panels = Array.Empty<UIPanelController>(); // Tất cả controller có thể tham gia điều hướng; không có panel mặc định.

    [Serializable]
    private sealed class StatePanelRoute
    {
        public GameState state; // Trạng thái game mà route này trình bày.
        public UIPanelController panel; // UI gốc cho trạng thái đó; null nghĩa là đóng UI hiện tại.
    }
    [SerializeField] private StatePanelRoute[] statePanels = Array.Empty<StatePanelRoute>(); // Mapping trạng thái → UI, không quyết định luồng game.

    #endregion

    #region Navigation State

    private readonly List<UIPanelController> history = new List<UIPanelController>(); // Stack yêu cầu: đóng panel trên cùng thì quay về panel trước.
    private GameManager game; // Đọc state hiện tại khi UI được bật lại sau một khoảng tắt subscription.
    private UIPanelController rootPanel; // Gốc luồng hiện tại do SetRootPanel chỉ định lúc chạy, không phải cấu hình default.
    private UIPanelController activePanel; // Controller đang được manager cấp quyền hiển thị.
    private bool visible = true; // Ẩn toàn bộ UI vẫn bảo toàn lịch sử để tiếp tục khi hiện lại.
    private bool subscribed; // Chống đăng ký sự kiện lặp khi OnEnable và API điều hướng cùng chạy.
    public UIPanelController ActivePanel => activePanel; // Chỉ đọc panel hiện tại để các hệ thống quan sát luồng UI.

    #endregion

    #region Lifecycle And Registration

    // Lắng nghe yêu cầu của controller, kể cả yêu cầu đã phát trước khi manager được bật.
    private void OnEnable()
    {
        Subscribe();
        if (game != null) ApplyGameState(game.State);
    }

    // Zenject cung cấp nguồn state; UI không tự gọi GameManager chuyển trạng thái để chọn màn hình.
    [Inject]
    public void Construct(GameManager game)
    {
        this.game = game;
        if (isActiveAndEnabled) { Subscribe(); ApplyGameState(game.State); }
    }

    // Bus phục vụ UI: mỗi trạng thái chỉ chọn route hiển thị, không spawn/reset hoặc quyết định thắng/thua.
    private void OnGameStateChanged(GameState previous, GameState current) => ApplyGameState(current);

    // Không có route thì đóng luồng UI cũ, không tự quay về Gameplay hoặc UI mặc định.
    private void ApplyGameState(GameState state)
    {
        foreach (StatePanelRoute route in statePanels)
            if (route.state == state) { SetRootPanel(route.panel); return; }
        SetRootPanel(null);
    }

    // Đồng bộ yêu cầu hiện có; không đọc bất kỳ dữ liệu level/skill nào của controller.
    private void Subscribe()
    {
        if (subscribed) return;
        subscribed = true;
        GameUIEventBus.StateChanged += OnGameStateChanged;
        history.Clear();
        if (rootPanel != null) history.Add(rootPanel);
        foreach (UIPanelController panel in panels)
        {
            if (panel == null) continue;
            panel.VisibilityRequested -= OnVisibilityRequested;
            panel.VisibilityRequested += OnVisibilityRequested;
            panel.SetVisible(false);
            if (panel != rootPanel && panel.WantsVisible && !history.Contains(panel)) history.Add(panel);
        }
        RefreshFlow();
    }

    // Ngừng điều phối, ẩn modal và trả tài nguyên hiển thị qua controller; giữ ý định mở để khôi phục lúc bật lại.
    private void OnDisable()
    {
        GameUIEventBus.StateChanged -= OnGameStateChanged;
        foreach (UIPanelController panel in panels)
        {
            if (panel == null) continue;
            panel.VisibilityRequested -= OnVisibilityRequested;
            panel.SetVisible(false);
        }
        activePanel = null;
        history.Clear();
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

    // Đưa một panel đã đăng ký lên đầu luồng, tạm ẩn panel trước và giữ vị trí để quay lại.
    public void OpenPanel(UIPanelController panel)
    {
        Subscribe();
        if (panel == null || Array.IndexOf(panels, panel) < 0)
            throw new ArgumentException("Register the panel in UIManager before opening it.", nameof(panel));
        if (panel == rootPanel) { SetRootPanel(panel); return; }
        panel.RecordVisibilityRequest(true);
        history.Remove(panel);
        history.Add(panel);
        RefreshFlow();
    }

    // Đóng một bước trong luồng; nếu là panel đang hiện thì phục hồi bước trước hoặc gốc luồng hiện tại.
    public void ClosePanel(UIPanelController panel)
    {
        Subscribe();
        if (panel != null && panel == rootPanel) return;
        if (panel != null) panel.RecordVisibilityRequest(false);
        history.Remove(panel);
        RefreshFlow();
    }

    // Quay về UI trước thông qua cùng chính sách điều hướng; không tự bỏ queue nghiệp vụ của panel.
    public void Back()
    {
        if (activePanel != null && activePanel != rootPanel) ClosePanel(activePanel);
    }

    // Chuyển gốc khi state game đổi: bỏ lịch sử cũ, không tự chọn màn hình hoặc giữ default panel.
    // Truyền null để đóng mọi panel khi trạng thái chưa có UI tương ứng.
    public void SetRootPanel(UIPanelController panel)
    {
        Subscribe();
        if (panel != null && Array.IndexOf(panels, panel) < 0)
            throw new ArgumentException("Register the root panel in Panels before opening it.", nameof(panel));
        foreach (UIPanelController previous in history) previous.RecordVisibilityRequest(false);
        history.Clear();
        rootPanel = panel;
        if (panel != null)
        {
            panel.RecordVisibilityRequest(true);
            history.Add(panel);
        }
        RefreshFlow();
    }
    // Dùng chung một đường hiển thị cho mọi controller; chỉ một panel hoạt động tại một thời điểm.
    private void RefreshFlow()
    {
        UIPanelController next = visible && history.Count > 0 ? history[history.Count - 1] : null; // Lịch sử rỗng thì không có UI; GameManager quyết định luồng tiếp theo.
        if (activePanel != next)
        {
            UIPanelController previous = activePanel; // Đổi state trước callback để không giữ panel cũ trong lúc nó đóng.
            activePanel = next;
            if (previous != null) previous.SetVisible(false);
            if (next != null) next.SetVisible(true);
        }
    }

    #endregion

    #region Global Visibility

    // Tạm ẩn/hiện toàn bộ luồng; không xóa lịch sử hay phần thưởng đang chờ của controller.
    public void SetVisible(bool value)
    {
        Subscribe();
        visible = value;
        RefreshFlow();
    }

    #endregion
}
