using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>View một ô chỉ số (prefab StatRow). Chỉ hiện dữ liệu đã định dạng; Controller định dạng theo Theme.</summary>
public sealed class StatRowView : MonoBehaviour
{
    #region References

    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text value;

    #endregion

    #region Presentation

    /// <summary>Đặt icon và chữ của ô.</summary>
    public void Bind(StatRowModel model)
    {
        icon.sprite = model.Icon;
        value.text = model.Value;
    }

    #endregion
}
