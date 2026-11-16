#region UIPanelNavigation

/// <summary>Mở/đóng panel và quay lại theo lịch sử điều hướng.</summary>
public interface IUIPanelNavigation
{
    UIPanelController ActivePanel { get; }
    void OpenPanel(UIPanelController panel);
    void ClosePanel(UIPanelController panel);
    void Back();
}

#endregion

#region UIRootNavigation

/// <summary>Chọn panel gốc hoặc ẩn/hiện toàn bộ luồng UI.</summary>
public interface IUIRootNavigation
{
    void SetRootPanel(UIPanelController panel);
    void SetVisible(bool value);
}

#endregion

#region System Contract

/// <summary>Gom hợp đồng của UIManager; consumer nhận vai trò nhỏ cần dùng.</summary>
public interface IUINavigation
{
    IUIPanelNavigation GetUIPanelNavigation();
    IUIRootNavigation GetUIRootNavigation();
}

#endregion
