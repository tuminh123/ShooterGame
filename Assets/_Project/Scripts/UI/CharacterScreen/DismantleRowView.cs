using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Một dòng trong popup phân rã: ảnh + tên nhân vật kèm số bản trùng, nút "+N [icon mảnh]" để phân rã hết bản trùng.
/// Chỉ hiển thị model và phát event; không gọi service.
/// </summary>
public sealed class DismantleRowView : MonoBehaviour
{
    #region References

    [SerializeField] private Image portrait;
    [SerializeField] private TMP_Text nameText;
    [Tooltip("Nút phân rã hết bản trùng của nhân vật này.")]
    [SerializeField] private Button dismantleButton;
    [SerializeField] private Image shardIcon;
    [Tooltip("Số mảnh nhận được, ví dụ \"+30\".")]
    [SerializeField] private TMP_Text shardText;
    [Tooltip("Tên + số bản trùng; {0} = tên, {1} = số bản trùng.")]
    [SerializeField] private string nameFormat = "{0}  <color=#FFD23A>x{1}</color>";

    #endregion

    #region Events

    /// <summary>Bấm nút phân rã của dòng này.</summary>
    public event Action Clicked;

    #endregion

    #region Lifecycle

    private void OnEnable() => dismantleButton.onClick.AddListener(OnClick);
    private void OnDisable() => dismantleButton.onClick.RemoveListener(OnClick);

    #endregion

    #region Presentation

    /// <summary>Hiện một nhân vật có bản trùng.</summary>
    public void Bind(DismantleRowModel model)
    {
        if (portrait != null && model.Portrait != null) portrait.sprite = model.Portrait;
        nameText.text = string.Format(CultureInfo.InvariantCulture, nameFormat, model.Name, model.Copies);
        if (shardIcon != null && model.ShardIcon != null) shardIcon.sprite = model.ShardIcon;
        shardText.text = "+" + model.ShardTotal.ToString("N0", CultureInfo.InvariantCulture);
    }

    private void OnClick() => Clicked?.Invoke();

    #endregion
}
