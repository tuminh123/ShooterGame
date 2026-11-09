using System;
using UnityEngine;

// Tween nhiều phần tử theo thứ tự, dùng cho thẻ lựa chọn, phần thưởng hoặc bất kỳ danh sách popup nào.
// Số phần tử không cố định là ba; UI truyền số mục đang hiển thị trong lần mở hiện tại.
public sealed class StaggeredPopupTween : PopupTweenBase
{
    #region Ordered Item Settings

    [SerializeField] private Transform[] items = Array.Empty<Transform>(); // Thứ tự cũng là thứ tự xuất hiện.
    [SerializeField, Min(0f)] private float stagger = 0.12f; // Giây thực lệch giữa hai phần tử lúc mở/đóng.
    private int visibleCount; // Chỉ tween đóng các mục đã mở; không tác động mục ẩn còn lại trong danh sách.

    #endregion

    #region Initialization

    // UI cấp danh sách bằng code; hoặc gán Items trong Inspector để dùng PlayOpen không tham số count.
    public void Initialize(Transform[] targets)
    {
        InitializeTargets(targets);
        visibleCount = TargetCount;
    }

    private void EnsureInitialized()
    {
        if (!IsInitialized) Initialize(items);
    }

    #endregion

    #region Open All Or Visible Items

    // Mở toàn bộ danh sách đã cấu hình, phù hợp popup có số mục cố định.
    public override float PlayOpen(Action onComplete = null)
    {
        EnsureInitialized();
        return PlayOpen(TargetCount, onComplete);
    }

    // Mở count mục đầu tiên; các mục ngoài count phải được UI ẩn theo dữ liệu của popup.
    public float PlayOpen(int count, Action onComplete = null)
    {
        EnsureInitialized();
        visibleCount = Mathf.Clamp(count, 0, TargetCount);
        return OpenTargets(visibleCount, stagger, onComplete);
    }

    #endregion

    #region Close Visible Items

    // Thu nhỏ lần lượt cùng thứ tự; callback chỉ chạy sau mục cuối, để UI tắt popup đúng lúc.
    public override float PlayClose(Action onComplete = null)
    {
        EnsureInitialized();
        return CloseTargets(visibleCount, stagger, onComplete);
    }

    #endregion
}
