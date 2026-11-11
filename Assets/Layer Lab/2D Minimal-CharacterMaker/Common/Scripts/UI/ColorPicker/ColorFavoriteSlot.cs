using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LayerLab.ArtMakerUnity
{
    /// <summary>
    /// Represents a single favorite color slot that can display a saved color,
    /// show an empty indicator, and handle click events for selection and application.
    /// </summary>
    public class ColorFavoriteSlot : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private Image colorImage;
        [SerializeField] private GameObject emptyIndicator;
        [SerializeField] private GameObject selectionFrame;

        private int _slotIndex;
        private ColorFavoriteManager _manager;
        private bool _isEmpty = true;

        /// <summary>
        /// Initializes the slot with its index and parent manager.
        /// </summary>
        /// <param name="index">The slot index within the favorite color list.</param>
        /// <param name="manager">The parent ColorFavoriteManager.</param>
        public void Init(int index, ColorFavoriteManager manager)
        {
            _slotIndex = index;
            _manager = manager;
            SetColor(Color.clear);
            SetSelected(false);
        }

        /// <summary>
        /// Sets the displayed color. A fully transparent color is treated as empty.
        /// </summary>
        /// <param name="color">The color to display, or Color.clear for an empty slot.</param>
        public void SetColor(Color color)
        {
            _isEmpty = (color.a == 0);

            if (colorImage != null)
            {
                colorImage.gameObject.SetActive(!_isEmpty);
                if (!_isEmpty)
                    colorImage.color = color;
            }

            if (emptyIndicator != null)
                emptyIndicator.SetActive(_isEmpty);
        }

        /// <summary>
        /// Sets the visual selection state of this slot.
        /// </summary>
        /// <param name="selected">True to show the selection frame, false to hide it.</param>
        public void SetSelected(bool selected)
        {
            if (selectionFrame != null)
                selectionFrame.SetActive(selected);
        }

        /// <inheritdoc />
        public void OnPointerClick(PointerEventData eventData)
        {
            if (_manager == null || eventData.button != PointerEventData.InputButton.Left) return;

            _manager.SelectSlot(_slotIndex);

            if (!_isEmpty)
                _manager.ApplyFavoriteColor(_slotIndex);
        }
    }
}
