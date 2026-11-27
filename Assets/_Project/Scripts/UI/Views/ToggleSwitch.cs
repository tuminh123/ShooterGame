using System;
using PrimeTween;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Công tắc bật/tắt dạng trượt (prefab Switch của GUI Pro): bấm để đổi, tay nắm trượt sang phải khi bật,
/// hiện phần Fill và chữ ON/OFF. Chỉ là view, không biết công tắc điều khiển gì.
/// </summary>
/// <remarks>Tween chạy unscaled time vì Settings/Pause mở khi game đang pause.</remarks>
public sealed class ToggleSwitch : MonoBehaviour
{
    #region References

    [SerializeField] private Button button;
    [Tooltip("Phần nền màu hiện khi bật.")]
    [SerializeField] private GameObject fill;
    [SerializeField] private RectTransform handle;
    [SerializeField] private TMP_Text label;
    [Tooltip("Vị trí X của tay nắm khi bật; khi tắt dùng số âm.")]
    [SerializeField] private float handleOffset = 56f;
    [SerializeField, Min(0f)] private float slideSeconds = 0.12f;

    #endregion

    #region State

    private Tween slide;

    /// <summary>Trạng thái đang hiện.</summary>
    public bool IsOn { get; private set; }

    /// <summary>Người chơi bấm đổi trạng thái; tham số là trạng thái mới.</summary>
    public event Action<bool> Toggled;

    #endregion

    #region Lifecycle

    private void OnEnable() => button.onClick.AddListener(OnClicked);

    private void OnDisable()
    {
        button.onClick.RemoveListener(OnClicked);
        slide.Stop();
    }

    #endregion

    #region Public API

    /// <summary>Hiện trạng thái mà không phát <see cref="Toggled"/>; animate = false để đặt ngay khi mở popup.</summary>
    public void SetIsOn(bool on, bool animate)
    {
        IsOn = on;
        fill.SetActive(on);
        label.text = on ? "ON" : "OFF";
        float x = on ? handleOffset : -handleOffset;
        slide.Stop();
        if (animate && slideSeconds > 0f && isActiveAndEnabled)
            slide = Tween.UIAnchoredPositionX(handle, x, slideSeconds, Ease.OutQuad, useUnscaledTime: true);
        else
            handle.anchoredPosition = new Vector2(x, handle.anchoredPosition.y);
    }

    #endregion

    #region Private Helpers

    private void OnClicked()
    {
        SetIsOn(!IsOn, animate: true);
        Toggled?.Invoke(IsOn);
    }

    #endregion
}
