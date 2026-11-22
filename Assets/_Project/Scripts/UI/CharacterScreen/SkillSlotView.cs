using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Một ô skill trên tab Level (prefab, CharacterDetailView tạo theo đúng số skill của nhân vật).
/// Hiện icon, cấp hoặc điều kiện mở, lớp phủ khóa; chạm vào thì báo lên để mở popup thông tin skill.
/// </summary>
/// <remarks>Chỉ hiển thị model; không đọc service. Dùng Button.onClick nên chạy như nhau với chạm và chuột.</remarks>
public sealed class SkillSlotView : MonoBehaviour
{
    #region References

    [SerializeField] private Button button;
    [SerializeField] private Image icon;
    [Tooltip("Chữ dưới ô: cấp skill khi đã mở, điều kiện mở khi còn khóa.")]
    [SerializeField] private TMP_Text label;
    [Tooltip("Bật khi skill còn khóa (ổ khóa / lớp tối); có thể để trống.")]
    [SerializeField] private GameObject lockOverlay;
    [Tooltip("Màu icon khi skill còn khóa (làm tối icon gốc).")]
    [SerializeField] private Color lockedIconColor = new Color(0.35f, 0.35f, 0.4f, 1f);

    #endregion

    #region State And Events

    /// <summary>Model của lần Bind gần nhất; popup đọc khi ô được chạm.</summary>
    public SkillSlotModel Model { get; private set; }

    /// <summary>Ô được chạm.</summary>
    public event Action<SkillSlotView> Clicked;

    #endregion

    #region Lifecycle

    private void OnEnable() => button.onClick.AddListener(OnClick);

    private void OnDisable() => button.onClick.RemoveListener(OnClick);

    #endregion

    #region Presentation

    /// <summary>Hiện một skill theo trạng thái mở/khóa.</summary>
    public void Bind(SkillSlotModel model)
    {
        Model = model;
        icon.sprite = model.Icon;
        icon.enabled = model.Icon != null;
        if (model.Unlocked)
            icon.color = Color.white;
        else
            icon.color = lockedIconColor;
        label.text = model.Label;
        if (lockOverlay != null)
            lockOverlay.SetActive(!model.Unlocked);
    }

    private void OnClick() => Clicked?.Invoke(this);

    #endregion
}
