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

    #region State

    private TextPunch punch; // Tạo khi bind lần đầu: lúc đó text đã ở dáng gốc trong prefab.

    #endregion

    #region Presentation

    /// <summary>
    /// Đặt icon và chữ của ô. <paramref name="animate"/> = true khi vẫn là nhân vật cũ (vừa nâng cấp),
    /// lúc đó số tăng sẽ nhảy lên; đổi nhân vật thì truyền false.
    /// </summary>
    public void Bind(StatRowModel model, bool animate)
    {
        icon.sprite = model.Icon;
        punch ??= new TextPunch(value);
        punch.Set(model.Value, model.Amount, animate);
    }

    #endregion

    #region Lifecycle

    private void OnDisable() => punch?.Reset();

    #endregion
}
