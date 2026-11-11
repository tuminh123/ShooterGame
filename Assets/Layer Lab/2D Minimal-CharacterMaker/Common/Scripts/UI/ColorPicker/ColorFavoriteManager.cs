using UnityEngine;
using UnityEngine.UI;

namespace LayerLab.ArtMakerUnity
{
    /// <summary>
    /// Manages user-saved favorite colors with add, delete, and persistence via PlayerPrefs.
    /// </summary>
    public class ColorFavoriteManager : MonoBehaviour
    {
        [SerializeField] private ColorFavoriteSlot[] slots;
        [SerializeField] private Button addButton;
        [SerializeField] private Button deleteButton;
        [SerializeField] private ColorPicker colorPicker;

        private Color[] _savedColors;
        private int _count;
        private int _selectedIndex = -1;
        private const string PREFS_KEY_FAVORITES = "ArtMakerUnity_FavoriteColors";

        /// <summary>
        /// Initializes the favorite color slots, loads saved colors, and sets up button listeners.
        /// </summary>
        public void Init()
        {
            _savedColors = new Color[slots != null ? slots.Length : 0];
            Load();

            if (slots != null)
            {
                for (int i = 0; i < slots.Length; i++)
                    slots[i].Init(i, this);
            }

            if (addButton != null)
                addButton.onClick.AddListener(OnClickAdd);
            if (deleteButton != null)
                deleteButton.onClick.AddListener(OnClickDelete);

            RefreshAll();
        }

        /// <summary>
        /// Selects the favorite slot at the given index and updates the selection frame.
        /// </summary>
        /// <param name="index">The slot index to select.</param>
        public void SelectSlot(int index)
        {
            if (_selectedIndex >= 0 && _selectedIndex < slots.Length)
                slots[_selectedIndex].SetSelected(false);

            _selectedIndex = index;

            if (_selectedIndex >= 0 && _selectedIndex < slots.Length)
                slots[_selectedIndex].SetSelected(true);
        }

        /// <summary>
        /// Applies the saved favorite color at the given index to the color picker.
        /// </summary>
        /// <param name="index">The index of the saved color to apply.</param>
        public void ApplyFavoriteColor(int index)
        {
            if (index < 0 || index >= _count) return;
            if (colorPicker != null)
                colorPicker.SetColor(_savedColors[index]);
        }

        private void OnClickAdd()
        {
            if (colorPicker == null || _selectedIndex < 0) return;
            if (_selectedIndex >= _savedColors.Length) return;

            Color color = colorPicker.GetCurrentColor();
            _savedColors[_selectedIndex] = color;

            if (_selectedIndex >= _count)
                _count = _selectedIndex + 1;

            Save();
            RefreshAll();
        }

        private void OnClickDelete()
        {
            if (_selectedIndex < 0 || _selectedIndex >= _count) return;

            for (int i = _selectedIndex; i < _count - 1; i++)
                _savedColors[i] = _savedColors[i + 1];

            _savedColors[_count - 1] = Color.clear;
            _count--;

            if (_selectedIndex >= _count && _selectedIndex > 0)
                _selectedIndex = _count - 1;

            Save();
            RefreshAll();
            SelectSlot(_selectedIndex);
        }

        private void RefreshAll()
        {
            if (slots == null) return;

            for (int i = 0; i < slots.Length; i++)
            {
                if (i < _count)
                    slots[i].SetColor(_savedColors[i]);
                else
                    slots[i].SetColor(Color.clear);
            }
        }

        private void Save()
        {
            string data = "";
            for (int i = 0; i < _count; i++)
            {
                if (i > 0) data += "|";
                data += ColorUtility.ToHtmlStringRGBA(_savedColors[i]);
            }
            PlayerPrefs.SetString(PREFS_KEY_FAVORITES, data);
            PlayerPrefs.SetInt(PREFS_KEY_FAVORITES + "_Count", _count);
            PlayerPrefs.Save();
        }

        private void Load()
        {
            string data = PlayerPrefs.GetString(PREFS_KEY_FAVORITES, "");
            if (string.IsNullOrEmpty(data))
            {
                _count = 0;
                return;
            }

            string[] entries = data.Split('|');
            _count = Mathf.Min(entries.Length, _savedColors.Length);

            for (int i = 0; i < _count; i++)
            {
                if (ColorUtility.TryParseHtmlString("#" + entries[i], out Color color))
                    _savedColors[i] = color;
                else
                    _savedColors[i] = Color.clear;
            }
        }

        private void OnDestroy()
        {
            if (addButton != null)
                addButton.onClick.RemoveListener(OnClickAdd);
            if (deleteButton != null)
                deleteButton.onClick.RemoveListener(OnClickDelete);
        }
    }
}
