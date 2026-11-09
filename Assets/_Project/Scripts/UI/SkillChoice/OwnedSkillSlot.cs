using UnityEngine;
using UnityEngine.UI;

// View của một ô skill; prefab được PoolManager cấp khi có skill và thu hồi khi panel đóng/reset.
public sealed class OwnedSkillSlot : MonoBehaviour
{
    #region Prefab References

    [SerializeField] private Image icon; // Icon riêng của skill được gán vào ô hiện tại.
    [SerializeField] private Transform gemsOff; // Sao nền biểu diễn trần cấp thực tế của skill.
    [SerializeField] private Transform gemsOn; // Sao sáng biểu diễn cấp runtime đang sở hữu.

    #endregion

    #region Presentation

    // Ghi đè toàn bộ nội dung khi lấy từ pool hoặc nâng cấp; không giữ icon/sao của skill trước.
    public void Show(SkillRuntime skill)
    {
        icon.sprite = skill.Definition.Icon;
        icon.enabled = icon.sprite != null;
        icon.preserveAspect = true;
        int max = Mathf.Max(1, skill.Definition.MaxRunLevel); // Skill đơn cấp chỉ hiện một sao, không dựa vào MaxLevel cũ.
        ShowStars(gemsOff, max, max);
        ShowStars(gemsOn, max, skill.RunLevel);
    }

    // Bổ sung sao khi cần, tái sử dụng ở lần sau; cả hai hàng có cùng vị trí và số ô.
    private static void ShowStars(Transform row, int count, int lit)
    {
        while (row.childCount < count) Instantiate(row.GetChild(0), row);
        row.gameObject.SetActive(true);
        for (int i = 0; i < row.childCount; i++)
        {
            row.GetChild(i).gameObject.SetActive(i < lit);
            ((RectTransform)row.GetChild(i)).anchoredPosition = new Vector2((i - (count - 1) * 0.5f) * 30f, 0f);
        }
    }

    // Xóa dữ liệu trước khi trả pool; không dùng OnDisable vì panel cha có thể chỉ đang tạm ẩn.
    public void Clear()
    {
        icon.sprite = null;
        icon.enabled = false;
        foreach (Transform gem in gemsOff) gem.gameObject.SetActive(false);
        foreach (Transform gem in gemsOn) gem.gameObject.SetActive(false);
    }

    #endregion
}
