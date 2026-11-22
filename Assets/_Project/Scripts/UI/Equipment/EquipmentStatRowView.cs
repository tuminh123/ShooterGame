using PrimeTween;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Một dòng chỉ số trong popup trang bị (khung BasicFrame_Round12): icon, tên, giá trị và chữ phụ
/// ("+X" xem trước cấp tới, hoặc "Mở ở Lv.X" khi dòng còn khóa). Dùng cho dòng chính, dòng phụ và popup Property.
/// </summary>
public sealed class EquipmentStatRowView : MonoBehaviour
{
    #region References

    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text valueText;
    [Tooltip("Chữ phụ bên phải giá trị; có thể để trống.")]
    [SerializeField] private TMP_Text extraText;
    [Tooltip("Làm mờ cả dòng khi còn khóa.")]
    [SerializeField] private CanvasGroup group;
    [SerializeField, Range(0f, 1f)] private float lockedAlpha = 0.45f;

    #endregion

    #region State

    private Sequence flash;

    #endregion

    #region Presentation

    /// <summary>Hiện một dòng chỉ số.</summary>
    public void Bind(EquipmentStatLineModel model)
    {
        icon.sprite = model.Icon;
        icon.enabled = model.Icon != null;
        nameText.text = model.Name;
        valueText.text = model.Value;
        if (extraText != null)
            extraText.text = model.Extra;
        if (group != null)
            group.alpha = model.Locked ? lockedAlpha : 1f;
    }

    /// <summary>Nảy dòng lên để báo dòng phụ vừa mở hoặc vừa được cộng.</summary>
    public void Flash()
    {
        StopFlash();
        // Thời gian không scale: màn Equipment mở ở Home, có thể đang pause.
        flash = Sequence.Create(useUnscaledTime: true)
            .Chain(Tween.Scale(transform, 1.08f, 0.1f, Ease.OutCubic))
            .Chain(Tween.Scale(transform, 1f, 0.25f, Ease.OutBack));
    }

    private void StopFlash()
    {
        flash.Stop();
        transform.localScale = Vector3.one;
    }

    #endregion

    #region Lifecycle

    private void OnDisable() => StopFlash();

    #endregion
}
