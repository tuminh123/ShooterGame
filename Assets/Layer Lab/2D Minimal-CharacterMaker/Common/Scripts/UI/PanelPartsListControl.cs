using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace LayerLab.ArtMakerUnity
{
    /// <summary>
    /// Controls the scrollable parts list panel. Displays thumbnails for the currently selected
    /// <see cref="UICategory"/>, handles slot selection, color synchronization, and reset functionality.
    /// </summary>
    public class PanelPartsListControl : MonoBehaviour
    {
        [SerializeField] private PartsListSlot slotTemplate;
        [SerializeField] private Transform contentParent;
        [SerializeField] private TMP_Text textTitle;
        [SerializeField] private ColorPicker colorPicker;
        [SerializeField] private Image imgSelectFrame;
        [SerializeField] private Button buttonReset;

        private readonly List<PartsListSlot> _slots = new();
        private PartsType _activeType;
        private UICategory _activeCategory;
        private PartsType[] _currentSubTypes;
        private PartsManager _partsManager;

        /// <summary>
        /// Gets the currently active parts type.
        /// </summary>
        public PartsType? ActiveType => _activeType;


        /// <summary>
        /// Initializes the parts list panel with the given <see cref="PartsManager"/>.
        /// Subscribes to parts and color change events.
        /// </summary>
        /// <param name="pm">The PartsManager that provides parts data.</param>
        public void Init(PartsManager pm)
        {
            _partsManager = pm;
            if (slotTemplate != null)
                slotTemplate.gameObject.SetActive(false);
            if (buttonReset != null)
                buttonReset.onClick.AddListener(OnClickReset);

            if (_partsManager != null)
            {
                _partsManager.OnPartsChanged += OnPartsChanged;
                _partsManager.OnColorChanged += OnColorChanged;
            }
        }

        /// <summary>
        /// Displays the parts list for the specified UI category.
        /// Routes to skin, group, or single-type display logic accordingly.
        /// </summary>
        /// <param name="category">The UI category to display.</param>
        public void Show(UICategory category)
        {
            _activeCategory = category;
            _currentSubTypes = UICategoryConfig.GetSubTypes(category);

            textTitle.text = category switch
            {
                UICategory.HandRight => "HAND RIGHT",
                UICategory.HandLeft => "HAND LEFT",
                _ => category.ToString().ToUpper(),
            };

            if (UICategoryConfig.IsGroup(category))
            {
                ShowGroup(category);
                return;
            }

            if (_currentSubTypes.Length == 1)
                ShowPartsType(_currentSubTypes[0]);
        }

        private void ShowGroup(UICategory category)
        {
            // Hide all existing slots before repopulating
            foreach (var slot in _slots)
                slot.gameObject.SetActive(false);

            // Create or reuse slots for all sub-types in the group
            int slotIdx = 0;
            foreach (var type in _currentSubTypes)
            {
                int count = _partsManager.GetPartsCount(type);
                for (int i = 0; i < count; i++)
                {
                    while (_slots.Count <= slotIdx)
                    {
                        var newSlot = Instantiate(slotTemplate, contentParent);
                        _slots.Add(newSlot);
                    }

                    _slots[slotIdx].SetSlot(this, _partsManager.GetThumbnail(type, i), i, type);
                    _slots[slotIdx].gameObject.SetActive(true);
                    slotIdx++;
                }
            }

            // Groups don't support per-slot coloring; reset to white
            ChangeColorList(Color.white);
            if (colorPicker != null)
                colorPicker.gameObject.SetActive(false);

            // Show reset button at the top of the list
            if (buttonReset != null)
            {
                buttonReset.gameObject.SetActive(true);
                buttonReset.transform.SetAsFirstSibling();
            }

            UpdateSelectFrame();
        }

        private void ShowPartsType(PartsType type)
        {
            _activeType = type;

            foreach (var slot in _slots)
                slot.gameObject.SetActive(false);

            int count = _partsManager.GetPartsCount(type);

            while (_slots.Count < count)
            {
                var newSlot = Instantiate(slotTemplate, contentParent);
                _slots.Add(newSlot);
            }

            for (int i = 0; i < count; i++)
            {
                _slots[i].SetSlot(this, _partsManager.GetThumbnail(type, i), i, type);
                _slots[i].gameObject.SetActive(true);
            }

            if (_partsManager.CanChangeColor(type))
            {
                var target = _partsManager.GetColorTarget(type);
                Color color = _partsManager.GetColor(target);
                ChangeColorList(color);
                if (colorPicker != null)
                {
                    colorPicker.gameObject.SetActive(true);
                    colorPicker.SetTarget(target);
                }
            }
            else
            {
                ChangeColorList(Color.white);
                if (colorPicker != null)
                    colorPicker.gameObject.SetActive(false);
            }

            if (buttonReset != null)
            {
                buttonReset.gameObject.SetActive(_partsManager.CanToggle(type));
                if (_partsManager.CanToggle(type))
                    buttonReset.transform.SetAsFirstSibling();
            }

            UpdateSelectFrame();
        }

        /// <summary>
        /// Handles selection of a parts list slot. For groups, shows only the selected sub-type
        /// and hides others. For single types, ensures visibility before equipping.
        /// </summary>
        /// <param name="slot">The slot that was clicked.</param>
        public void SelectSlot(PartsListSlot slot)
        {
            PartsType slotType = slot.PartsType;

            if (UICategoryConfig.IsGroup(_activeCategory))
            {
                // Group: only the selected type is visible, others are hidden
                foreach (var type in _currentSubTypes)
                {
                    if (type == slotType)
                        _partsManager.ToggleParts(type, true);
                    else if (_partsManager.CanToggle(type))
                        _partsManager.ToggleParts(type, false);
                }
                _activeType = slotType;
            }
            else
            {
                if (!_partsManager.IsPartsVisible(_activeType) && _partsManager.CanToggle(_activeType))
                    _partsManager.ToggleParts(_activeType, true);
            }

            _partsManager.EquipParts(slotType, slot.SlotIndex);
            UpdateSelectFrame();
        }

        /// <summary>
        /// Resets (unequips) the currently active parts type or all sub-types in a group.
        /// </summary>
        public void OnClickReset()
        {
            if (UICategoryConfig.IsGroup(_activeCategory))
            {
                foreach (var type in _currentSubTypes)
                    _partsManager.UnequipParts(type);
            }
            else
            {
                _partsManager.UnequipParts(_activeType);
            }
            UpdateSelectFrame();
        }

        private void HideAllSlots()
        {
            foreach (var slot in _slots)
                slot.gameObject.SetActive(false);
        }

        private void UpdateSelectFrame()
        {
            if (imgSelectFrame == null) return;

            if (UICategoryConfig.IsGroup(_activeCategory))
            {
                // Find the slot matching the equipped sub-type in the group
                // Keep the selection frame on equipped items even when visibility is off
                foreach (var type in _currentSubTypes)
                {
                    if (!_partsManager.IsEquipped(type)) continue;
                    // Prefer the visible type; fall back to _activeType if none is visible
                    if (!_partsManager.IsPartsVisible(type) && type != _activeType)
                        continue;

                    int idx = _partsManager.GetActiveIndex(type);
                    foreach (var slot in _slots)
                    {
                        if (slot.gameObject.activeSelf && slot.PartsType == type && slot.SlotIndex == idx)
                        {
                            imgSelectFrame.gameObject.SetActive(true);
                            MoveFrameTo(slot.transform as RectTransform);
                            return;
                        }
                    }
                }
                imgSelectFrame.gameObject.SetActive(false);
            }
            else
            {
                // Single type: match by active index
                int activeIndex = _partsManager.GetActiveIndex(_activeType);

                if (!_partsManager.IsEquipped(_activeType))
                {
                    imgSelectFrame.gameObject.SetActive(false);
                    return;
                }

                if (activeIndex >= 0 && activeIndex < _slots.Count && _slots[activeIndex].gameObject.activeSelf)
                {
                    imgSelectFrame.gameObject.SetActive(true);
                    MoveFrameTo(_slots[activeIndex].transform as RectTransform);
                }
                else
                {
                    imgSelectFrame.gameObject.SetActive(false);
                }
            }
        }

        private void MoveFrameTo(RectTransform target)
        {
            if (target == null) return;
            imgSelectFrame.transform.SetParent(target, false);
            imgSelectFrame.transform.localPosition = Vector3.zero;
            imgSelectFrame.transform.SetAsFirstSibling();
        }

        private void ChangeColorList(Color color)
        {
            foreach (var slot in _slots)
            {
                if (slot.gameObject.activeSelf)
                    slot.ChangeColor(color);
            }
        }

        private void OnPartsChanged(PartsType type, int index)
        {
            if (UICategoryConfig.IsGroup(_activeCategory))
            {
                foreach (var st in _currentSubTypes)
                {
                    if (st == type) { UpdateSelectFrame(); return; }
                }
            }
            else if (type == _activeType)
            {
                UpdateSelectFrame();
            }
        }

        private void OnColorChanged(ColorTargetType target, Color color)
        {
            if (_partsManager.CanChangeColor(_activeType) &&
                _partsManager.GetColorTarget(_activeType) == target)
            {
                ChangeColorList(color);
            }
        }

        private void OnDestroy()
        {
            if (buttonReset != null)
                buttonReset.onClick.RemoveListener(OnClickReset);

            if (_partsManager != null)
            {
                _partsManager.OnPartsChanged -= OnPartsChanged;
                _partsManager.OnColorChanged -= OnColorChanged;
            }
        }
    }
}
