using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LayerLab.ArtMakerUnity
{
    /// <summary>
    /// Represents a single parts category slot in the parts panel.
    /// Displays a thumbnail, handles visibility toggling, and responds to pointer events
    /// for selection and focus highlighting.
    /// </summary>
    public class PartsSlot : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        private const float HIDDEN_ALPHA = 0.3f;

        [SerializeField] private UICategory uiCategory;
        [SerializeField] private Button buttonVisible;
        [SerializeField] private Image imageIcon;
        [SerializeField] private Image imageBg;
        [SerializeField] private Image imageItem;


        /// <summary>
        /// Gets the UI category this slot represents.
        /// </summary>
        public UICategory UICategory => uiCategory;

        [SerializeField] private Image toolIndicator;
        [SerializeField] private Sprite toolOff;
        [SerializeField] private Sprite toolOn;
        private FishingEquip toolEquipment;
        private Image _imageVisible;
        private PartsManager _partsManager;
        private Sprite[] _bgSprites;
        private Sprite[] _visibleSprites;
        private bool _isHidden;
        private bool _hasItem;
        private PartsType? _lastVisibleType;

        private void OnValidate()
        {
            imageBg ??= GetComponent<Image>();
            buttonVisible ??= transform.Find("Button_Eye")?.GetComponent<Button>();
            imageIcon ??= transform.Find("Icon")?.GetComponent<Image>();
            imageItem ??= transform.Find("Item")?.GetComponent<Image>();
        }

        private void OnEnable()
        {
            if (buttonVisible != null)
                buttonVisible.onClick.AddListener(OnClickVisible);
        }

        private void OnDisable()
        {
            if (buttonVisible != null)
                buttonVisible.onClick.RemoveListener(OnClickVisible);
        }

        /// <summary>
        /// Initializes the slot with the given <see cref="PartsManager"/> and sprite sets.
        /// Subscribes to parts, visibility, and color change events, then refreshes the display.
        /// </summary>
        /// <param name="pm">The PartsManager controlling character customization.</param>
        /// <param name="bgSprites">Background sprites for empty, hidden, and visible states.</param>
        /// <param name="visibleSprites">Eye icon sprites for hidden and visible states.</param>
        public void Init(PartsManager pm, Sprite[] bgSprites, Sprite[] visibleSprites)
        {
            _partsManager = pm;
            _bgSprites = bgSprites;
            _visibleSprites = visibleSprites;
            _imageVisible = buttonVisible.GetComponent<Image>();

            if (_partsManager == null) return;

            if (toolIndicator || IsHandSlot)
            {
                toolEquipment = pm.GetComponent<FishingEquip>();
                if (toolEquipment) toolEquipment.ShownToolChanged += RefreshDisplay;
            }
            _partsManager.OnPartsChanged += OnPartsChanged;
            _partsManager.OnVisibilityChanged += OnVisibilityChanged;
            _partsManager.OnColorChanged += OnColorChanged;

            RefreshDisplay();
        }

        private bool IsHandSlot => uiCategory == UICategory.HandRight || uiCategory == UICategory.HandLeft;
        private bool ToolInHand => toolEquipment && (toolEquipment.IsPickaxeShown || toolEquipment.IsRodShown);

        private void RefreshToolIndicator()
        {
            bool shown = toolEquipment && (uiCategory == UICategory.Fishing
                ? toolEquipment.IsRodShown : toolEquipment.IsPickaxeShown);
            _isHidden = _hasItem && !shown;
            toolIndicator.sprite = shown ? toolOn : toolOff;
            toolIndicator.SetNativeSize();
        }

        private void OnDestroy()
        {
            if (_partsManager == null) return;
            if (toolEquipment) toolEquipment.ShownToolChanged -= RefreshDisplay;
            _partsManager.OnPartsChanged -= OnPartsChanged;
            _partsManager.OnVisibilityChanged -= OnVisibilityChanged;
            _partsManager.OnColorChanged -= OnColorChanged;
        }

        private void OnClickVisible()
        {
            if (_partsManager == null) return;

            var subTypes = UICategoryConfig.GetSubTypes(uiCategory);

            if (toolIndicator && toolEquipment)
            {
                if (!_hasItem) return;
                toolEquipment.SelectPreview(!_isHidden ? FishingEquip.Preview.Combat :
                    uiCategory == UICategory.Fishing ? FishingEquip.Preview.Rod : FishingEquip.Preview.Pickaxe);
                return;
            }
            if (IsHandSlot && ToolInHand)
            {
                foreach (var type in subTypes)
                {
                    if (!_partsManager.IsEquipped(type)) continue;
                    _partsManager.ToggleParts(type, true);
                    toolEquipment.SelectPreview(FishingEquip.Preview.Combat);
                    break;
                }
                return;
            }
            if (subTypes.Length == 1)
            {
                if (!_partsManager.CanToggle(subTypes[0])) return;
                _partsManager.ToggleParts(subTypes[0], _isHidden);
            }
            else if (UICategoryConfig.IsGroup(uiCategory))
            {
                if (_isHidden)
                {
                    // Hidden -> visible: restore the type that was previously hidden
                    if (_lastVisibleType.HasValue && _partsManager.CanToggle(_lastVisibleType.Value))
                        _partsManager.ToggleParts(_lastVisibleType.Value, true);
                }
                else
                {
                    // Visible -> hidden: remember the currently visible part and hide it
                    _lastVisibleType = null;
                    foreach (var type in subTypes)
                    {
                        if (_partsManager.IsPartsVisible(type) && _partsManager.CanToggle(type))
                        {
                            _lastVisibleType = type;
                            _partsManager.ToggleParts(type, false);
                        }
                    }
                }
            }
        }

        private void OnPartsChanged(PartsType type, int index)
        {
            if (IsRelevantType(type))
                RefreshDisplay();
        }

        private void OnVisibilityChanged(PartsType type, bool visible)
        {
            if (IsRelevantType(type))
                RefreshDisplay();
        }

        private void OnColorChanged(ColorTargetType target, Color color)
        {
            if (uiCategory == UICategory.Skin && target == ColorTargetType.Skin)
            {
                ApplyItemColor(color);
                return;
            }

            var subTypes = UICategoryConfig.GetSubTypes(uiCategory);
            if (subTypes.Length == 1 && _partsManager.CanChangeColor(subTypes[0]) &&
                _partsManager.GetColorTarget(subTypes[0]) == target)
            {
                ApplyItemColor(color);
            }
        }

        private bool IsRelevantType(PartsType type)
        {
            var subTypes = UICategoryConfig.GetSubTypes(uiCategory);
            foreach (var st in subTypes)
            {
                if (st == type) return true;
            }

            return false;
        }

        private void RefreshDisplay()
        {
            if (_partsManager == null) return;

            var subTypes = UICategoryConfig.GetSubTypes(uiCategory);

            bool hasItem = false;

            // Group category (e.g., HandRight/HandLeft with multiple sub-types)
            if (UICategoryConfig.IsGroup(uiCategory))
            {
                // Group: find the first sub-type that is both equipped and visible
                bool foundEquipped = false;
                bool foundVisible = false;
                foreach (var type in subTypes)
                {
                    if (!_partsManager.IsEquipped(type)) continue;
                    foundEquipped = true;

                    if (_partsManager.IsPartsVisible(type))
                    {
                        int idx = _partsManager.GetActiveIndex(type);
                        Sprite thumb = _partsManager.GetThumbnail(type, idx);
                        if (imageItem != null)
                        {
                            imageItem.sprite = thumb;
                            imageItem.SetNativeSize();
                        }
                        foundVisible = true;
                        hasItem = true;
                        break;
                    }
                }

                if (foundEquipped && !foundVisible)
                {
                    // Equipped but hidden -> show thumbnail of the last visible item
                    PartsType target = _lastVisibleType ?? subTypes[0];
                    if (_partsManager.IsEquipped(target))
                    {
                        int idx = _partsManager.GetActiveIndex(target);
                        Sprite thumb = _partsManager.GetThumbnail(target, idx);
                        if (imageItem != null)
                        {
                            imageItem.sprite = thumb;
                            imageItem.SetNativeSize();
                        }
                        hasItem = true;
                    }
                    _isHidden = true;
                }
                else if (!foundEquipped)
                {
                    _isHidden = false; // empty
                }
                else
                {
                    _isHidden = false; // visible
                }

                ApplyItemColor(Color.white);
            }
            // Single type (one sub-type per category)
            else if (subTypes.Length == 1)
            {
                var partsType = subTypes[0];
                bool equipped = _partsManager.IsEquipped(partsType);

                if (!equipped)
                {
                    // State 1: Empty
                    _isHidden = false;
                }
                else
                {
                    // State 2 or 3
                    _isHidden = !_partsManager.IsPartsVisible(partsType);

                    int idx = _partsManager.GetActiveIndex(partsType);
                    Sprite thumb = _partsManager.GetThumbnail(partsType, idx);
                    if (imageItem != null)
                    {
                        imageItem.sprite = thumb;
                        imageItem.SetNativeSize();
                    }
                    hasItem = true;

                    if (_partsManager.CanChangeColor(partsType))
                    {
                        var target = _partsManager.GetColorTarget(partsType);
                        ApplyItemColor(_partsManager.GetColor(target));
                    }
                    else
                    {
                        ApplyItemColor(Color.white);
                    }
                }
            }

            _hasItem = hasItem;
            if (toolIndicator) RefreshToolIndicator();
            if (IsHandSlot && hasItem && ToolInHand) _isHidden = true;

            if (imageIcon != null) imageIcon.gameObject.SetActive(!hasItem);
            if (imageItem != null) imageItem.gameObject.SetActive(hasItem);

            UpdateItemAlpha();
            UpdateBg();
            UpdateVisibleIcon();
        }

        private void ApplyItemColor(Color color)
        {
            if (imageItem == null) return;
            color.a = imageItem.color.a;
            imageItem.color = color;
        }

        private void UpdateBg()
        {
            if (imageBg == null || _bgSprites == null || _bgSprites.Length < 3) return;

            if (!_hasItem)
                imageBg.sprite = _bgSprites[0];  // Empty
            else if (_isHidden)
                imageBg.sprite = _bgSprites[1];  // Equipped + hidden
            else
                imageBg.sprite = _bgSprites[2];  // Equipped + visible
        }

        private void UpdateVisibleIcon()
        {
            if (buttonVisible == null) return;

            if (uiCategory == UICategory.Fishing || uiCategory == UICategory.Pickaxe)
            {
                buttonVisible.gameObject.SetActive(_hasItem);
                return;
            }

            bool equipped = false;
            var subTypes = UICategoryConfig.GetSubTypes(uiCategory);
            if (UICategoryConfig.IsGroup(uiCategory))
            {
                foreach (var type in subTypes)
                {
                    if (_partsManager != null && _partsManager.IsEquipped(type))
                    { equipped = true; break; }
                }
            }
            else if (subTypes.Length == 1)
            {
                equipped = _partsManager != null && _partsManager.IsEquipped(subTypes[0]);
            }

            // Skin, Eye -> always hide / Empty -> hide button / Equipped -> show button
            if (UICategoryConfig.IsSkin(uiCategory) || uiCategory == UICategory.Eye)
                equipped = false;
            buttonVisible.gameObject.SetActive(equipped);

            if (equipped && _imageVisible != null && _visibleSprites != null && _visibleSprites.Length >= 2)
                _imageVisible.sprite = _isHidden ? _visibleSprites[0] : _visibleSprites[1];
        }

        private void UpdateItemAlpha()
        {
            if (imageItem == null) return;
            var c = imageItem.color;
            c.a = _isHidden ? HIDDEN_ALPHA : 1f;
            imageItem.color = c;
        }

        /// <summary>
        /// Handles pointer click to select this slot in the parts panel.
        /// </summary>
        public void OnPointerClick(PointerEventData eventData)
        {
            PanelPartsControl.SelectSlot(this);
        }

        /// <summary>
        /// Handles pointer enter to apply focus highlighting on this slot.
        /// </summary>
        public void OnPointerEnter(PointerEventData eventData)
        {
            PanelPartsControl.FocusSlot(this);
        }

        /// <summary>
        /// Handles pointer exit to remove focus highlighting from this slot.
        /// </summary>
        public void OnPointerExit(PointerEventData eventData)
        {
            PanelPartsControl.UnfocusSlot(this);
        }
    }
}
