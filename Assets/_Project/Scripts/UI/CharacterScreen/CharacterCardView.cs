using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>View một thẻ nhân vật (prefab CharacterCard): ảnh, tên, level, sao và trạng thái Normal/Selected/Empty.</summary>
public sealed class CharacterCardView : MonoBehaviour
{
    #region References

    [SerializeField] private Button button;
    [SerializeField] private Image portrait;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text levelText;
    [Tooltip("5 ô sao theo thứ tự; bật đúng số sao hiện có.")]
    [SerializeField] private GameObject[] stars = Array.Empty<GameObject>();
    [Tooltip("Nhãn IN USE, chỉ bật khi thẻ là nhân vật đang chọn.")]
    [SerializeField] private GameObject inUseLabel;
    [Tooltip("Icon thẻ trống, chỉ bật ở trạng thái Empty.")]
    [SerializeField] private GameObject emptyIcon;
    [Tooltip("Các phần tắt khi thẻ trống để thẻ tối như mockup (Gradient, Glow, Bottom, ảnh, sao, level, tên).")]
    [SerializeField] private GameObject[] hiddenWhenEmpty = Array.Empty<GameObject>();

    #endregion

    #region Events

    /// <summary>Người dùng chạm vào thẻ (thẻ trống không phát).</summary>
    public event Action Clicked;

    #endregion

    #region Lifecycle

    private void OnEnable() => button.onClick.AddListener(OnClick);
    private void OnDisable() => button.onClick.RemoveListener(OnClick);

    #endregion

    #region Presentation

    /// <summary>Hiện dữ liệu một thẻ.</summary>
    public void Bind(CharacterCardModel model)
    {
        bool empty = model.State == CharacterCardState.Empty;
        foreach (GameObject part in hiddenWhenEmpty)
            if (part != null) part.SetActive(!empty);
        if (emptyIcon != null) emptyIcon.SetActive(empty);
        if (inUseLabel != null) inUseLabel.SetActive(model.State == CharacterCardState.Selected);
        button.interactable = !empty;
        if (empty) return;

        if (model.Portrait != null) portrait.sprite = model.Portrait;
        nameText.text = model.Name;
        levelText.text = model.Level.ToString(CultureInfo.InvariantCulture);
        for (int i = 0; i < stars.Length; i++)
            if (stars[i] != null) stars[i].SetActive(i < model.Stars);
    }

    private void OnClick() => Clicked?.Invoke();

    #endregion
}
