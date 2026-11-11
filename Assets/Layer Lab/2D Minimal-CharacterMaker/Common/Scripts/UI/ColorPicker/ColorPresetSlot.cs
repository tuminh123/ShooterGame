using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LayerLab.ArtMakerUnity
{
    /// <summary>
    /// Represents a single color preset slot that displays a color swatch
    /// and applies it to the color picker when clicked.
    /// </summary>
    public class ColorPresetSlot : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private Image colorImage;

        private Color _color;
        private ColorPresetManager _manager;

        /// <summary>
        /// Initializes the slot with a color and its parent manager.
        /// </summary>
        /// <param name="color">The preset color to display.</param>
        /// <param name="manager">The parent ColorPresetManager.</param>
        public void Init(Color color, ColorPresetManager manager)
        {
            _color = color;
            _manager = manager;

            if (colorImage != null)
                colorImage.color = color;
        }

        /// <inheritdoc />
        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left && _manager != null)
                _manager.ApplyColor(_color);
        }
    }
}
