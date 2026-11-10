using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

// View của một ô skill; prefab được PoolManager cấp khi có skill và thu hồi khi panel đóng/reset.
public sealed class OwnedSkillSlot : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerMoveHandler
{
    #region Prefab References

    [SerializeField] private Image icon; // Icon riêng của skill được gán vào ô hiện tại.
    [SerializeField] private Transform gemsOff; // Sao nền biểu diễn trần cấp thực tế của skill.
    [SerializeField] private Transform gemsOn; // Sao sáng biểu diễn cấp runtime đang sở hữu.
    [SerializeField] private RectTransform tooltip;
    [SerializeField] private TMP_Text tooltipText;
    private SkillRuntime currentSkill;
    private bool hovered;
    private Vector2 pointerPosition;
    private Camera pointerCamera;

    #endregion

    #region Presentation

    // Ghi đè toàn bộ nội dung khi lấy từ pool hoặc nâng cấp; không giữ icon/sao của skill trước.
    public void Show(SkillRuntime skill)
    {
        currentSkill = skill;
        icon.sprite = skill.Definition.Icon;
        icon.enabled = icon.sprite != null;
        icon.preserveAspect = true;
        int max = Mathf.Max(1, skill.Definition.MaxRunLevel); // Skill đơn cấp chỉ hiện một sao, không dựa vào MaxLevel cũ.
        ShowStars(gemsOff, max, max);
        ShowStars(gemsOn, max, skill.RunLevel);
        if (hovered) ShowTooltip();
    }

    // Bổ sung sao khi cần, tái sử dụng ở lần sau; cả hai hàng có cùng vị trí và số ô.
    private static void ShowStars(Transform row, int count, int lit)
    {
        // Hai hàng phải dùng cùng vị trí theo count tối đa, kể cả hàng On chỉ sáng một phần.
        // LayoutGroup căn theo số con đang bật sẽ đẩy hàng sao sáng lệch khỏi hàng nền.
        if (row.TryGetComponent(out HorizontalLayoutGroup layout)) layout.enabled = false;
        while (row.childCount < count) Instantiate(row.GetChild(0), row);
        row.gameObject.SetActive(true);
        for (int i = 0; i < row.childCount; i++)
        {
            RectTransform gem = (RectTransform)row.GetChild(i);
            gem.gameObject.SetActive(i < lit);
            // Prefab cũ có anchor ở mép trái; đưa về tâm trước khi tính độ lệch quanh icon.
            gem.anchorMin = gem.anchorMax = gem.pivot = new Vector2(0.5f, 0.5f);
            gem.anchoredPosition = new Vector2((i - (count - 1) * 0.5f) * 30f, 0f);
        }
    }

    // Xóa dữ liệu trước khi trả pool; không dùng OnDisable vì panel cha có thể chỉ đang tạm ẩn.
    public void Clear()
    {
        HideTooltip();
        currentSkill = null;
        icon.sprite = null;
        icon.enabled = false;
        foreach (Transform gem in gemsOff) gem.gameObject.SetActive(false);
        foreach (Transform gem in gemsOn) gem.gameObject.SetActive(false);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        hovered = true;
        pointerPosition = eventData.position;
        pointerCamera = eventData.enterEventCamera;
        ShowTooltip();
    }

    public void OnPointerMove(PointerEventData eventData)
    {
        pointerPosition = eventData.position;
        if (hovered && tooltip != null && tooltip.gameObject.activeSelf) PositionTooltip();
    }

    public void OnPointerExit(PointerEventData eventData) => HideTooltip();

    // Đọc mô tả cùng nguồn với thẻ nâng cấp, nhưng dùng cấp đang sở hữu.
    private void ShowTooltip()
    {
        if (currentSkill == null || !currentSkill.IsActive || tooltip == null || tooltipText == null) return;
        SkillDataSO definition = currentSkill.Definition;
        string title = string.IsNullOrWhiteSpace(definition.DisplayName) ? definition.name : definition.DisplayName;
        tooltipText.text = $"<b>{title}</b>\nLv. {currentSkill.RunLevel} / {definition.MaxRunLevel}\n\n" +
            definition.GetRunLevelDescription(currentSkill.RunLevel);
        tooltip.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,
            Mathf.Max(140f, tooltipText.GetPreferredValues(tooltipText.text, tooltip.rect.width - 32f, 0f).y + 32f));
        tooltip.gameObject.SetActive(true);
        PositionTooltip();
    }

    private void PositionTooltip()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return;
        RectTransform bounds = (RectTransform)canvas.rootCanvas.transform;
        Camera camera = canvas.rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : pointerCamera;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(bounds, pointerPosition, camera, out Vector2 position)) return;
        float scale = tooltip.lossyScale.x / bounds.lossyScale.x;
        float width = tooltip.rect.width * scale;
        float height = tooltip.rect.height * scale;
        position += new Vector2(18f, -18f);
        position.x = Mathf.Clamp(position.x, bounds.rect.xMin + 8f, Mathf.Max(bounds.rect.xMin + 8f, bounds.rect.xMax - width - 8f));
        position.y = Mathf.Clamp(position.y, Mathf.Min(bounds.rect.yMax - 8f, bounds.rect.yMin + height + 8f), bounds.rect.yMax - 8f);
        tooltip.position = bounds.TransformPoint(position);
    }

    private void HideTooltip()
    {
        hovered = false;
        if (tooltip != null) tooltip.gameObject.SetActive(false);
    }

    private void OnDisable() => HideTooltip();

    #endregion
}
