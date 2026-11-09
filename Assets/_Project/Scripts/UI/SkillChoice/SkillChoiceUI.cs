using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;

// View nằm trên LevelUpPanel; SkillChoiceController luôn hoạt động quản lý vòng đời.
// Chỉ hiển thị và phát sự kiện chọn; không sở hữu gameplay hoặc pause.
public sealed class SkillChoiceUI : MonoBehaviour
{
    #region Card Presentation

    [Serializable]
    private sealed class Card
    {
        public Button button; // Nút chọn cả thẻ; khóa ngay sau click để tránh nhận thưởng lặp.
        public Image icon; // Icon lấy từ definition của skill được offer.
        public TMP_Text title; // Tên hiển thị; fallback về tên asset nếu chưa nhập.
        public TMP_Text description; // Nhãn NEW/UPGRADE và mô tả của cấp sẽ nhận.
        public Transform gemsOff; // Các sao nền, bật theo trần cấp thực tế của offer.
        public Transform gemsOn; // Các sao sáng, bật theo cấp nhận sau khi chọn.

        // Đổ dữ liệu một offer lên thẻ; sao sáng là cấp sẽ nhận sau khi chọn.
        public void Show(SkillChoiceOffer offer)
        {
            button.gameObject.SetActive(true);
            button.interactable = true;
            icon.sprite = offer.Definition.Icon;
            icon.enabled = icon.sprite != null;
            icon.preserveAspect = true;
            title.text = string.IsNullOrWhiteSpace(offer.Definition.DisplayName)
                ? offer.Definition.name : offer.Definition.DisplayName;
            string action = offer.CurrentLevel == 0 ? "NEW SKILL - Lv. 1" : // Phân biệt nhận mới và nâng từ cấp đang có.
                $"UPGRADE - Lv. {offer.CurrentLevel} > {offer.OfferedLevel}";
            description.text = action + "\n" + offer.Definition.GetRunLevelDescription(offer.OfferedLevel);
            // Group_GreadGems đã có Off/On, mỗi hàng 5 Gem. Không nhân bản cả group.
            // Off = số cấp tối đa; On = cấp sẽ nhận nếu chọn. Skill mới luôn bắt đầu một sao.
            for (int i = 0; i < gemsOff.childCount; i++) gemsOff.GetChild(i).gameObject.SetActive(i < offer.MaxLevel);
            for (int i = 0; i < gemsOn.childCount; i++) gemsOn.GetChild(i).gameObject.SetActive(i < offer.OfferedLevel);
        }
    }

    #endregion

    #region Owned Skill Slot State

    [SerializeField] private RectTransform ownedSlotsRoot; // Group_SkillFrmae02 dùng GridLayoutGroup gốc, ban đầu không có slot con.
    [SerializeField] private OwnedSkillSlot ownedSlotTemplate; // Prefab asset có script riêng; pool tạo instance khi có skill đầu tiên.
    private readonly List<OwnedSkillSlot> ownedSlots = new List<OwnedSkillSlot>(); // Chỉ giữ instance đang dùng; phần rảnh thuộc PoolManager.
    private PoolManager pools; // Dùng pool chung của dự án, không tự tạo pool hoặc prewarm slot.

    #endregion

    #region References And Input State

    [SerializeField] private TMP_Text heading; // Tiêu đề bảng, kèm số lượt còn lại nếu lớn hơn một.
    [SerializeField] private Card[] cards = new Card[3]; // Ba thẻ có sẵn trong scene; không tạo thêm object lúc chơi.
    public event Action<int> Selected; // Gửi index thẻ được chọn cho controller xử lý gameplay.
    public event Action Hidden; // Báo panel tắt để controller trả token pause.
    private UnityAction[] clicks; // Giữ delegate đã đăng ký để gỡ chính xác khi view bị hủy.
    private GameObject previousSelection; // Nút được focus trước khi mở modal, phục hồi khi đóng.
    private int openedFrame; // Chặn input rơi từ frame mở bảng sang nút đầu tiên.
    private float nextInputAt; // Mốc nhận click theo unscaled time, vẫn chạy lúc game pause.
    private int offerCount; // Số thẻ còn được nhận click; về 0 ngay sau một lựa chọn.
    private bool ready; // Đảm bảo reference và listener chỉ được khởi tạo một lần.

    #endregion

    #region Initialization

    // Inject pool cả khi panel đang tắt; không spawn slot trong bước khởi tạo.
    [Zenject.Inject]
    public void Construct(PoolManager pools) => this.pools = pools;


    // Kiểm tra đủ reference rồi gắn listener; gọi được ngay cả khi GameObject panel đang tắt.
    private void Initialize()
    {
        if (ready) return;
        if (cards == null || cards.Length != 3)
            throw new InvalidOperationException("SkillChoiceUI requires exactly three cards.");
        foreach (Card card in cards)
            if (card == null || card.button == null || card.icon == null || card.title == null ||
                card.description == null || card.gemsOff == null || card.gemsOn == null)
                throw new InvalidOperationException("Assign all SkillChoiceUI card references.");
        clicks = new UnityAction[cards.Length];
        for (int i = 0; i < cards.Length; i++)
        {
            int index = i; // Chụp index riêng cho closure của từng button.
            clicks[i] = () => Choose(index);
            cards[i].button.onClick.AddListener(clicks[i]);
        }
        ready = true;
    }

    #endregion

    #region Panel Presentation

    // Mở panel, cập nhật ba thẻ và giới hạn điều hướng trong modal; bắt đầu khoảng chặn input ngắn.
    public void Show(IReadOnlyList<SkillChoiceOffer> offers, int pending)
    {
        Initialize();
        if (!gameObject.activeSelf && EventSystem.current != null)
            previousSelection = EventSystem.current.currentSelectedGameObject;
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        offerCount = offers.Count;
        if (heading != null) heading.text = pending > 1 ? $"SELECT A SKILL ({pending})" : "SELECT A SKILL";
        for (int i = 0; i < cards.Length; i++)
        {
            if (i < offerCount) cards[i].Show(offers[i]);
            else cards[i].button.gameObject.SetActive(false);
        }
        for (int i = 0; i < offerCount; i++)
            cards[i].button.navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnLeft = cards[(i + offerCount - 1) % offerCount].button,
                selectOnRight = cards[(i + 1) % offerCount].button
            };
        if (offerCount > 0 && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(cards[0].button.gameObject);
        openedFrame = Time.frameCount;
        nextInputAt = Time.unscaledTime + 0.15f;
    }

    #endregion

    #region Owned Skill Slot Presentation

    // Đồng bộ số slot đúng với snapshot; chưa có skill thì không spawn, nâng cấp chỉ đổi nội dung ô đang dùng.
    public void ShowOwnedSkills(IReadOnlyList<SkillRuntime> skills)
    {
        while (ownedSlots.Count > skills.Count) ReleaseLastOwnedSlot();
        if (skills.Count == 0) return;
        if (ownedSlotsRoot == null || ownedSlotTemplate == null || pools == null)
            throw new InvalidOperationException("Assign owned slot prefab, group and inject PoolManager in SkillChoiceUI.");
        while (ownedSlots.Count < skills.Count)
        {
            GameObject clone = pools.Spawn(ownedSlotTemplate.gameObject, Vector3.zero, Quaternion.identity,
                parent: ownedSlotsRoot); // Pool tăng tự nhiên khi thiếu, không lấy cắp các ô đang dùng.
            if (clone == null) throw new InvalidOperationException("Could not spawn owned skill slot.");
            OwnedSkillSlot slot = clone.GetComponent<OwnedSkillSlot>(); // Prefab đã gán sẵn icon/sao, không tìm theo tên object.
            RectTransform rect = (RectTransform)clone.transform; // Pool spawn theo world space nên chuẩn hóa lại UI local transform.
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
            rect.anchoredPosition3D = Vector3.zero;
            ownedSlots.Add(slot);
        }
        for (int i = 0; i < skills.Count; i++) ownedSlots[i].Show(skills[i]);
    }

    // Trả một ô cuối list về pool; pool giữ instance để dùng lại ở lần mở bảng hoặc Player tiếp theo.
    private void ReleaseLastOwnedSlot()
    {
        int index = ownedSlots.Count - 1; // Bỏ khỏi list trước khi Despawn có thể gọi callback vòng đời Unity.
        OwnedSkillSlot slot = ownedSlots[index];
        ownedSlots.RemoveAt(index);
        if (slot == null) return;
        slot.Clear();
        pools.Despawn(slot.gameObject);
    }

    // Thu hồi toàn bộ instance đang dùng khi đóng panel; không Destroy hoặc tạo lại prefab thủ công.
    private void ReleaseOwnedSlots()
    {
        while (ownedSlots.Count > 0) ReleaseLastOwnedSlot();
    }
    #endregion

    #region Selection Input

    // Kiểm tra input và khóa toàn bộ thẻ trước khi phát sự kiện; controller quyết định nhận/nâng hoặc roll lại.
    private void Choose(int index)
    {
        if (Time.frameCount <= openedFrame || Time.unscaledTime < nextInputAt || index < 0 || index >= offerCount) return;
        offerCount = 0; // Chặn click lặp cho tới khi manager render lại.
        foreach (Card card in cards) card.button.interactable = false;
        Selected?.Invoke(index);
    }

    #endregion

    #region Visibility And Cleanup

    // Đóng panel qua vòng đời Unity để OnDisable luôn trả focus và thông báo controller.
    public void Hide()
    {
        ReleaseOwnedSlots();
        gameObject.SetActive(false);
    }

    // Vô hiệu hóa offer cũ, phục hồi focus nếu object còn hoạt động và báo panel đã ẩn.
    private void OnDisable()
    {
        offerCount = 0;
        ReleaseOwnedSlots();
        if (EventSystem.current != null && previousSelection != null && previousSelection.activeInHierarchy)
            EventSystem.current.SetSelectedGameObject(previousSelection);
        previousSelection = null;
        Hidden?.Invoke();
    }

    // Gỡ đúng các delegate của view, tránh giữ reference khi panel bị hủy.
    private void OnDestroy()
    {
        ReleaseOwnedSlots();
        if (clicks == null) return;
        for (int i = 0; i < clicks.Length; i++)
            if (cards[i]?.button != null) cards[i].button.onClick.RemoveListener(clicks[i]);
    }
    #endregion
}
