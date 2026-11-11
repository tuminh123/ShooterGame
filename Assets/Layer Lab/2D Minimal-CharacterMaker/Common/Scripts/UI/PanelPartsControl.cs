using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace LayerLab.ArtMakerUnity
{
    /// <summary>
    /// Controls the parts selection panel. Manages slot selection, focus highlighting,
    /// previous/next navigation, and equipment preset runtime navigation.
    /// </summary>
    public class PanelPartsControl : MonoBehaviour
    {
        public static PanelPartsControl Instance;

        private const string ASSET_STORE_URL = "https://assetstore.unity.com/publishers/81675";

        [SerializeField] private GameObject focusFrame;
        [SerializeField] private GameObject selectFrame;
        [SerializeField] private GameObject groupArrow;
        [SerializeField] private Button buttonPrevious;
        [SerializeField] private Button buttonNext;
        [SerializeField] private Button buttonSavePrefab;
        [SerializeField] private Button buttonAssetStore;
        [SerializeField] private Sprite[] spriteBgs;
        [SerializeField] private Sprite[] spriteVisibles;

        [Header("Equipment Preset")]
        [SerializeField] private Button buttonPresetPrev;
        [SerializeField] private Button buttonPresetNext;
        [SerializeField] private TMP_Text textPresetNumber;
        [SerializeField] private PresetData equipmentPresetData;

        [Header("Save Prefab")]
        [SerializeField] private float thumbnailCameraSize = 1.8f;
        [SerializeField] private Vector3 thumbnailCameraOffset = new Vector3(0, 0, -10);

        private const int PRESET_SLOT_COUNT = 10;

        private PanelPartsListControl _panelPartsList;
        private PartsSlot[] _partsSlots;
        private PartsSlot _selectedSlot;
        private PartsSlot _focusedSlot;
        private PartsManager _partsManager;
        private int _currentPresetIndex;

        #region Internal Accessors (for EquipmentPresetEditorGUI)

        internal PartsManager CurrentPartsManager => _partsManager;

        internal PresetData EquipmentPresetData
        {
            get => equipmentPresetData;
            set => equipmentPresetData = value;
        }

        internal int CurrentPresetIndex
        {
            get => _currentPresetIndex;
            set => _currentPresetIndex = value;
        }

        #endregion

        private void Awake()
        {
            Instance = this;
        }

        private void OnEnable()
        {
            buttonPrevious.onClick.AddListener(OnClickPrevious);
            buttonNext.onClick.AddListener(OnClickNext);
            buttonSavePrefab.onClick.AddListener(OnClickSavePrefab);
            buttonAssetStore.onClick.AddListener(OnClickAssetStore);

            if (buttonPresetPrev != null)
                buttonPresetPrev.onClick.AddListener(OnClickPresetPrev);
            if (buttonPresetNext != null)
                buttonPresetNext.onClick.AddListener(OnClickPresetNext);
        }

        private void OnDisable()
        {
            buttonPrevious.onClick.RemoveListener(OnClickPrevious);
            buttonNext.onClick.RemoveListener(OnClickNext);
            buttonSavePrefab.onClick.RemoveListener(OnClickSavePrefab);
            buttonAssetStore.onClick.RemoveListener(OnClickAssetStore);

            if (buttonPresetPrev != null)
                buttonPresetPrev.onClick.RemoveListener(OnClickPresetPrev);
            if (buttonPresetNext != null)
                buttonPresetNext.onClick.RemoveListener(OnClickPresetNext);
        }

        /// <summary>
        /// Initializes the parts panel with the given <see cref="PartsManager"/> and linked parts list panel.
        /// Sets up all child <see cref="PartsSlot"/> instances and selects the first slot.
        /// </summary>
        /// <param name="pm">The PartsManager controlling character customization.</param>
        /// <param name="panelPartsList">The companion parts list panel for displaying items.</param>
        public void Init(PartsManager pm, PanelPartsListControl panelPartsList,
                         PresetData presetData = null, int startupPresetSlot = -1)
        {
            _partsManager = pm;
            _panelPartsList = panelPartsList;

            // Caller-provided preset (e.g. from DemoControl) takes precedence over the
            // inspector field so the scene only needs to wire it in one place.
            if (presetData != null)
                equipmentPresetData = presetData;

            if (startupPresetSlot >= 0 && startupPresetSlot < PRESET_SLOT_COUNT)
                _currentPresetIndex = startupPresetSlot;

            _partsSlots = GetComponentsInChildren<PartsSlot>();
            foreach (var slot in _partsSlots)
                slot.Init(pm, spriteBgs, spriteVisibles);

            if (groupArrow != null)
                groupArrow.SetActive(false);

            if (_partsSlots.Length > 0)
                DoSelectSlot(_partsSlots[0]);

            UpdatePresetDisplay();
            LoadCurrentPreset();
        }

        /// <summary>
        /// Selects the specified slot via the singleton instance.
        /// </summary>
        public static void SelectSlot(PartsSlot slot) => Instance?.DoSelectSlot(slot);

        /// <summary>
        /// Applies hover focus to the specified slot via the singleton instance.
        /// </summary>
        public static void FocusSlot(PartsSlot slot) => Instance?.DoFocusSlot(slot);

        /// <summary>
        /// Removes hover focus from the specified slot via the singleton instance.
        /// </summary>
        public static void UnfocusSlot(PartsSlot slot) => Instance?.DoUnfocusSlot(slot);

        private void DoSelectSlot(PartsSlot slot)
        {
            if (slot == null || slot == _selectedSlot) return;
            _selectedSlot = slot;

            selectFrame.transform.SetParent(slot.transform, false);
            selectFrame.transform.localPosition = Vector3.zero;
            selectFrame.transform.SetAsLastSibling();
            selectFrame.SetActive(true);

            _panelPartsList.Show(slot.UICategory);
        }

        private void DoFocusSlot(PartsSlot slot)
        {
            if (slot == null) return;
            _focusedSlot = slot;

            focusFrame.transform.SetParent(slot.transform, false);
            focusFrame.transform.localPosition = Vector3.zero;
            focusFrame.transform.SetAsLastSibling();
            focusFrame.SetActive(true);

            if (groupArrow != null)
            {
                bool hasItems = HasSwappableItems(slot);
                groupArrow.transform.SetParent(slot.transform, false);
                groupArrow.transform.localPosition = Vector3.zero;
                groupArrow.transform.SetAsLastSibling();
                groupArrow.SetActive(hasItems);
            }
        }

        private void DoUnfocusSlot(PartsSlot slot)
        {
            if (slot != _focusedSlot) return;
            _focusedSlot = null;

            if (focusFrame != null)
                focusFrame.SetActive(false);

            if (groupArrow != null)
                groupArrow.SetActive(false);
        }

        private bool HasSwappableItems(PartsSlot slot)
        {
            if (_partsManager == null) return false;
            var subTypes = UICategoryConfig.GetSubTypes(slot.UICategory);
            foreach (var type in subTypes)
            {
                if (_partsManager.GetPartsCount(type) > 1) return true;
            }
            return false;
        }

        private PartsType? GetActiveTypeForSlot(PartsSlot slot)
        {
            if (slot == null || _partsManager == null) return null;

            var subTypes = UICategoryConfig.GetSubTypes(slot.UICategory);
            if (subTypes.Length == 1) return subTypes[0];

            // Group: first visible sub-type
            foreach (var type in subTypes)
            {
                if (_partsManager.IsPartsVisible(type))
                    return type;
            }
            return subTypes.Length > 0 ? subTypes[0] : null;
        }

        private void OnClickPrevious()
        {
            if (_partsManager == null) return;
            var slot = _focusedSlot ?? _selectedSlot;
            if (slot == null) return;

            if (UICategoryConfig.IsGroup(slot.UICategory))
                NavigateGroup(slot.UICategory, -1);
            else
            {
                var type = GetActiveTypeForSlot(slot);
                if (type.HasValue) _partsManager.PrevParts(type.Value);
            }
        }

        private void OnClickNext()
        {
            if (_partsManager == null) return;
            var slot = _focusedSlot ?? _selectedSlot;
            if (slot == null) return;

            if (UICategoryConfig.IsGroup(slot.UICategory))
                NavigateGroup(slot.UICategory, 1);
            else
            {
                var type = GetActiveTypeForSlot(slot);
                if (type.HasValue) _partsManager.NextParts(type.Value);
            }
        }

        /// <summary>
        /// Iterates through all items across the group category.
        /// Wraps from the last item of the current type to the first item of the next type.
        /// </summary>
        private void NavigateGroup(UICategory category, int direction)
        {
            var subTypes = UICategoryConfig.GetSubTypes(category);

            // Build a list of all items as (type, index) pairs
            var allItems = new System.Collections.Generic.List<(PartsType type, int index)>();
            foreach (var type in subTypes)
            {
                int count = _partsManager.GetPartsCount(type);
                for (int i = 0; i < count; i++)
                    allItems.Add((type, i));
            }
            if (allItems.Count == 0) return;

            // Find the current position (based on the visible type within this group)
            PartsType? activeType = null;
            foreach (var type in subTypes)
            {
                if (_partsManager.IsPartsVisible(type))
                { activeType = type; break; }
            }
            if (!activeType.HasValue && subTypes.Length > 0)
                activeType = subTypes[0];

            int activeIndex = activeType.HasValue ? _partsManager.GetActiveIndex(activeType.Value) : 0;

            int currentPos = 0;
            for (int i = 0; i < allItems.Count; i++)
            {
                if (allItems[i].type == activeType && allItems[i].index == activeIndex)
                { currentPos = i; break; }
            }

            // Move
            int newPos = (currentPos + direction + allItems.Count) % allItems.Count;
            var (newType, newIndex) = allItems[newPos];

            // If the type changes, switch visibility
            if (activeType.HasValue && newType != activeType.Value)
            {
                _partsManager.ToggleParts(activeType.Value, false);
                _partsManager.ToggleParts(newType, true);
            }

            _partsManager.EquipParts(newType, newIndex);
        }

        /// <summary>
        /// Refreshes the currently selected slot's parts list display.
        /// </summary>
        public void RefreshCurrentSlot()
        {
            if (_selectedSlot != null && _panelPartsList != null)
                _panelPartsList.Show(_selectedSlot.UICategory);
        }

        private void OnClickSavePrefab()
        {
#if UNITY_EDITOR
            CharacterPrefabSaver.Save(_partsManager, thumbnailCameraSize, thumbnailCameraOffset);
#endif
        }

        private void OnClickAssetStore()
        {
            Application.OpenURL(ASSET_STORE_URL);
        }

        #region Equipment Preset

        private void OnClickPresetPrev()
        {
            _currentPresetIndex = (_currentPresetIndex - 1 + PRESET_SLOT_COUNT) % PRESET_SLOT_COUNT;
            UpdatePresetDisplay();
            LoadCurrentPreset();
        }

        private void OnClickPresetNext()
        {
            _currentPresetIndex = (_currentPresetIndex + 1) % PRESET_SLOT_COUNT;
            UpdatePresetDisplay();
            LoadCurrentPreset();
        }

        internal void UpdatePresetDisplay()
        {
            if (textPresetNumber != null)
                textPresetNumber.text = (_currentPresetIndex + 1).ToString("D2");
        }

        private void LoadCurrentPreset()
        {
            if (equipmentPresetData == null || _partsManager == null) return;

            var item = equipmentPresetData.GetItem(_currentPresetIndex);
            if (item == null || item.isEmpty) return;

            _partsManager.ApplyPresetItem(item);
            RefreshCurrentSlot();
        }

#if UNITY_EDITOR
        private EquipmentPresetEditorGUI _editorGUI;

        private void OnGUI()
        {
            _editorGUI ??= new EquipmentPresetEditorGUI(this);
            _editorGUI.DrawGUI();
        }
#endif

        #endregion
    }
}
