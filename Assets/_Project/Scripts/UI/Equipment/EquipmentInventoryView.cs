using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Phần dưới của màn Equipment: tab "Tất cả" + 6 tab lọc theo ô, và lưới thẻ đồ chưa mặc của tab đang chọn.
/// Tự sinh và tái dùng thẻ từ prefab; chỉ hiển thị model và phát event.
/// </summary>
public sealed class EquipmentInventoryView : MonoBehaviour
{
    #region Types

    /// <summary>Một nút tab và ô mà nó lọc.</summary>
    [Serializable]
    private sealed class TabBinding
    {
        public EquipmentSlot slot;
        public Button button;
    }

    #endregion

    #region References

    [Tooltip("Tab hiện đồ chưa mặc của mọi ô.")]
    [SerializeField] private Button allTab;
    [Tooltip("Đủ 6 tab, mỗi EquipmentSlot một lần.")]
    [SerializeField] private TabBinding[] tabs = Array.Empty<TabBinding>();
    [Tooltip("Khung sáng nằm dưới tab đang chọn; được dời theo tab.")]
    [SerializeField] private RectTransform tabFocus;
    [SerializeField] private EquipmentCardView cardPrefab;
    [Tooltip("Content của Scroll View (có GridLayoutGroup).")]
    [SerializeField] private RectTransform content;
    [Tooltip("Chữ hiện khi tab đang chọn không có món nào trong túi (có thể để trống).")]
    [SerializeField] private GameObject emptyHint;

    #endregion

    #region Events And State

    /// <summary>Bấm vào tab của một ô.</summary>
    public event Action<EquipmentSlot> TabClicked;

    /// <summary>Bấm vào tab "Tất cả".</summary>
    public event Action AllTabClicked;

    /// <summary>Chạm vào thẻ thứ <c>index</c> (theo thứ tự model truyền vào).</summary>
    public event Action<int> CardClicked;

    private readonly List<EquipmentCardView> cards = new List<EquipmentCardView>(); // Thẻ đã sinh, tái dùng giữa các lần Bind.
    private readonly List<Action> tabHandlers = new List<Action>();

    #endregion

    #region Lifecycle

    private void OnEnable()
    {
        allTab.onClick.AddListener(OnAllTab);
        tabHandlers.Clear();
        foreach (TabBinding tab in tabs)
        {
            EquipmentSlot slot = tab.slot;
            UnityEngine.Events.UnityAction handler = () => TabClicked?.Invoke(slot);
            tab.button.onClick.AddListener(handler);
            tabHandlers.Add(() => tab.button.onClick.RemoveListener(handler));
        }
    }

    private void OnDisable()
    {
        allTab.onClick.RemoveListener(OnAllTab);
        foreach (Action remove in tabHandlers)
            remove();
        tabHandlers.Clear();
    }

    #endregion

    #region Presentation

    /// <summary>Vẽ lại tab đang chọn (null = tab "Tất cả") và lưới thẻ.</summary>
    public void Bind(EquipmentSlot? selected, IReadOnlyList<EquipmentCardModel> models)
    {
        MoveFocus(selected);
        EnsureCards(models.Count);
        for (int i = 0; i < cards.Count; i++)
        {
            bool used = i < models.Count;
            cards[i].gameObject.SetActive(used);
            if (used)
                cards[i].Bind(models[i]);
        }
        if (emptyHint != null)
            emptyHint.SetActive(models.Count == 0);
    }

    // Khung sáng đi theo tab: chỉ cần đặt cùng vị trí X với nút tab.
    private void MoveFocus(EquipmentSlot? selected)
    {
        if (tabFocus == null)
            return;
        Button target = allTab;
        foreach (TabBinding tab in tabs)
        {
            if (tab.slot == selected)
                target = tab.button;
        }
        // Lần mở đầu tiên layout của hàng tab có thể chưa tính: ép tính để đọc đúng vị trí nút.
        LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)target.transform.parent);
        Vector3 position = tabFocus.position;
        position.x = target.transform.position.x;
        tabFocus.position = position;
    }

    private void OnAllTab() => AllTabClicked?.Invoke();

    // Sinh thêm thẻ khi thiếu; mỗi thẻ giữ chỉ số vị trí của nó để báo CardClicked.
    private void EnsureCards(int count)
    {
        while (cards.Count < count)
        {
            EquipmentCardView card = Instantiate(cardPrefab, content);
            int index = cards.Count;
            card.Clicked += () => CardClicked?.Invoke(index);
            cards.Add(card);
        }
    }

    #endregion
}
