using System;
using System.Collections.Generic;
using PrimeTween;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Popup phân rã bản trùng (mở từ nút DISMANTLE ở màn Collection): mỗi nhân vật có bản trùng một dòng,
/// bấm nút của dòng để đổi hết bản trùng thành mảnh cùng độ hiếm. Chỉ hiển thị model và phát event.
/// </summary>
/// <remarks>Dòng được sinh từ một dòng mẫu và tái dùng giữa các lần vẽ; chạm nền tối hoặc nút X thì đóng.</remarks>
public sealed class CharacterDismantlePopupView : MonoBehaviour
{
    #region References

    [SerializeField] private Button dimmer;
    [SerializeField] private RectTransform window;
    [SerializeField] private Button closeButton;
    [Tooltip("Dòng mẫu (đang tắt); các dòng thật được nhân bản từ đây vào cùng cha.")]
    [SerializeField] private DismantleRowView rowTemplate;
    [Tooltip("Chữ hiện khi không có nhân vật nào có bản trùng.")]
    [SerializeField] private GameObject emptyText;
    [SerializeField, Min(0.01f)] private float openDuration = 0.18f;

    #endregion

    #region Events And State

    /// <summary>Bấm phân rã ở dòng thứ <c>index</c> (theo thứ tự model truyền vào).</summary>
    public event Action<int> RowClicked;

    private readonly List<DismantleRowView> rows = new List<DismantleRowView>();
    private Sequence openSequence;

    /// <summary>Popup đang mở.</summary>
    public bool IsOpen => window.gameObject.activeSelf;

    #endregion

    #region Lifecycle

    private void Awake()
    {
        rowTemplate.gameObject.SetActive(false);
        SetVisible(false);
    }

    private void OnEnable()
    {
        dimmer.onClick.AddListener(Hide);
        closeButton.onClick.AddListener(Hide);
    }

    private void OnDisable()
    {
        dimmer.onClick.RemoveListener(Hide);
        closeButton.onClick.RemoveListener(Hide);
        openSequence.Stop();
        window.localScale = Vector3.one;
    }

    #endregion

    #region Presentation

    /// <summary>Mở popup (nảy nhẹ) với danh sách nhân vật có bản trùng.</summary>
    public void Show(IReadOnlyList<DismantleRowModel> models)
    {
        Bind(models);
        SetVisible(true);
        LayoutRebuilder.ForceRebuildLayoutImmediate(window);
        openSequence.Stop();
        window.localScale = Vector3.one * 0.9f;
        openSequence = Sequence.Create(useUnscaledTime: true)
            .Chain(Tween.Scale(window, 1f, openDuration, Ease.OutBack));
    }

    /// <summary>Vẽ lại tại chỗ khi đang mở (sau khi phân rã), không nảy lại.</summary>
    public void Refresh(IReadOnlyList<DismantleRowModel> models)
    {
        if (!IsOpen) return;
        Bind(models);
        LayoutRebuilder.ForceRebuildLayoutImmediate(window);
    }

    /// <summary>Đóng popup.</summary>
    public void Hide()
    {
        openSequence.Stop();
        window.localScale = Vector3.one;
        SetVisible(false);
    }

    private void Bind(IReadOnlyList<DismantleRowModel> models)
    {
        while (rows.Count < models.Count)
        {
            DismantleRowView row = Instantiate(rowTemplate, rowTemplate.transform.parent);
            int index = rows.Count;
            row.Clicked += () => RowClicked?.Invoke(index);
            rows.Add(row);
        }
        for (int i = 0; i < rows.Count; i++)
        {
            bool used = i < models.Count;
            rows[i].gameObject.SetActive(used);
            if (used) rows[i].Bind(models[i]);
        }
        if (emptyText != null) emptyText.SetActive(models.Count == 0);
    }

    private void SetVisible(bool visible)
    {
        dimmer.gameObject.SetActive(visible);
        window.gameObject.SetActive(visible);
    }

    #endregion
}
