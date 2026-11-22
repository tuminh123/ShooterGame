using System;
using PrimeTween;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Phần trên của màn Equipment: nhân vật đang chọn (đứng Idle như màn Character Detail), 6 ô mặc, danh sách set và các nút.
/// Chỉ hiển thị model và phát event; không gọi service.
/// </summary>
public sealed class EquipmentLoadoutView : MonoBehaviour
{
    #region Types

    /// <summary>Một ô mặc trên màn: loại ô và thẻ hiển thị.</summary>
    [Serializable]
    private sealed class SlotBinding
    {
        public EquipmentSlot slot;
        public EquipmentCardView card;
    }

    #endregion

    #region References

    [Tooltip("Đủ 6 ô, mỗi EquipmentSlot một lần.")]
    [SerializeField] private SlotBinding[] slots = Array.Empty<SlotBinding>();
    [Tooltip("Khung nhân vật. Có prefab UI thì nhân vật UI (Idle) đặt vừa khung này và ẩn ảnh; chưa có thì hiện ảnh tĩnh.")]
    [SerializeField] private Image portrait;
    [SerializeField] private TMP_Text characterName;
    [Tooltip("Danh sách set đang mặc (mỗi set một dòng).")]
    [SerializeField] private TMP_Text setsText;
    [SerializeField] private Button unequipAllButton;
    [SerializeField] private Button propertyButton;
    [Tooltip("Nút Fuse của mockup; chưa có luật ghép nên luôn ẩn.")]
    [SerializeField] private GameObject fuseButton;

    #endregion

    #region State

    private Sequence equipPunch;
    private Transform punchedSlot;
    private CharacterPreviewSlot preview; // Dùng chung cách hiện nhân vật với màn Character Detail; tạo khi bind lần đầu.

    #endregion

    #region Events

    /// <summary>Chạm vào ô mặc.</summary>
    public event Action<EquipmentSlot> SlotClicked;

    /// <summary>Bấm Unequip All.</summary>
    public event Action UnequipAllClicked;

    /// <summary>Bấm Property (xem đủ 8 chỉ số).</summary>
    public event Action PropertyClicked;

    #endregion

    #region Lifecycle

    private void Awake()
    {
        if (fuseButton != null)
            fuseButton.SetActive(false);
        foreach (SlotBinding binding in slots)
        {
            EquipmentSlot slot = binding.slot;
            binding.card.Clicked += () => SlotClicked?.Invoke(slot);
        }
    }

    private void OnEnable()
    {
        unequipAllButton.onClick.AddListener(OnUnequipAll);
        propertyButton.onClick.AddListener(OnProperty);
    }

    private void OnDisable()
    {
        unequipAllButton.onClick.RemoveListener(OnUnequipAll);
        propertyButton.onClick.RemoveListener(OnProperty);
        StopPunch();
    }

    #endregion

    #region Presentation

    /// <summary>Vẽ lại phần trên của màn.</summary>
    public void Bind(EquipmentLoadoutModel model)
    {
        preview ??= new CharacterPreviewSlot(portrait);
        preview.Show(model.UIVisual, model.Portrait);
        characterName.text = model.CharacterName;
        setsText.text = model.Sets;
        unequipAllButton.interactable = model.CanUnequipAll;
        foreach (SlotBinding binding in slots)
            binding.card.Bind(model.Slots[(int)binding.slot]);
    }

    /// <summary>Ô vừa được mặc đồ nảy nhẹ để người chơi thấy món đã lên ô nào.</summary>
    public void PlayEquipped(EquipmentSlot slot)
    {
        foreach (SlotBinding binding in slots)
        {
            if (binding.slot != slot)
                continue;
            StopPunch();
            punchedSlot = binding.card.transform;
            // Thời gian không scale: màn Equipment mở ở Home, có thể đang pause.
            equipPunch = Sequence.Create(useUnscaledTime: true)
                .Chain(Tween.Scale(punchedSlot, 1.15f, 0.1f, Ease.OutCubic))
                .Chain(Tween.Scale(punchedSlot, 1f, 0.2f, Ease.OutBack));
        }
    }

    // Dừng giữa chừng thì trả ô về kích thước gốc, không để ô bị phóng to.
    private void StopPunch()
    {
        equipPunch.Stop();
        if (punchedSlot != null)
            punchedSlot.localScale = Vector3.one;
        punchedSlot = null;
    }

    private void OnUnequipAll() => UnequipAllClicked?.Invoke();

    private void OnProperty() => PropertyClicked?.Invoke();

    #endregion
}
