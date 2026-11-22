using System;
using System.Collections.Generic;
using PrimeTween;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Popup Property của màn Equipment: đủ 8 chỉ số cuối của nhân vật đang chọn (đã gồm level và trang bị).
/// Chỉ hiển thị model; chạm nền tối hoặc nút X thì đóng.
/// </summary>
public sealed class EquipmentStatsPopupView : MonoBehaviour
{
    #region References

    [SerializeField] private Button dimmer;
    [SerializeField] private RectTransform window;
    [SerializeField] private Button closeButton;
    [Tooltip("Đủ 8 dòng, theo thứ tự chỉ số trong theme màn Character.")]
    [SerializeField] private EquipmentStatRowView[] rows = Array.Empty<EquipmentStatRowView>();
    [SerializeField, Min(0.01f)] private float openDuration = 0.18f;

    #endregion

    #region State

    private Sequence openSequence;

    #endregion

    #region Lifecycle

    private void Awake() => SetVisible(false);

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

    /// <summary>Mở popup với các dòng chỉ số đã định dạng.</summary>
    public void Show(IReadOnlyList<EquipmentStatLineModel> lines)
    {
        for (int i = 0; i < rows.Length; i++)
        {
            bool used = i < lines.Count;
            rows[i].gameObject.SetActive(used);
            if (used)
                rows[i].Bind(lines[i]);
        }
        SetVisible(true);
        LayoutRebuilder.ForceRebuildLayoutImmediate(window);
        openSequence.Stop();
        window.localScale = Vector3.one * 0.9f;
        openSequence = Sequence.Create(useUnscaledTime: true)
            .Chain(Tween.Scale(window, 1f, openDuration, Ease.OutBack));
    }

    /// <summary>Đóng popup.</summary>
    public void Hide()
    {
        openSequence.Stop();
        window.localScale = Vector3.one;
        SetVisible(false);
    }

    private void SetVisible(bool visible)
    {
        dimmer.gameObject.SetActive(visible);
        window.gameObject.SetActive(visible);
    }

    #endregion
}
