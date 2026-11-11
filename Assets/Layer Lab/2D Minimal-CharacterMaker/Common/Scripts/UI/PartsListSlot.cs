using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LayerLab.ArtMakerUnity
{
    /// <summary>
    /// Represents a single item slot in the parts list scroll view.
    /// Displays a thumbnail sprite and handles click selection to equip the corresponding part.
    /// </summary>
    public class PartsListSlot : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private Image imageItem;

        /// <summary>
        /// Gets the index of this slot within its parts type.
        /// </summary>
        public int SlotIndex { get; private set; }

        /// <summary>
        /// Gets the parts type this slot belongs to.
        /// </summary>
        public PartsType PartsType { get; private set; }
        private PanelPartsListControl _parent;

        /// <summary>
        /// Configures this slot with display data and associates it with the parent list control.
        /// </summary>
        /// <param name="parent">The parent <see cref="PanelPartsListControl"/> managing this slot.</param>
        /// <param name="sprite">The thumbnail sprite to display.</param>
        /// <param name="index">The index of this part within its type (used for equipping).</param>
        /// <param name="type">The <see cref="PartsType"/> this slot represents.</param>
        public void SetSlot(PanelPartsListControl parent, Sprite sprite, int index, PartsType type = default)
        {
            _parent = parent;
            SlotIndex = index;
            PartsType = type;
            imageItem.sprite = sprite;
            imageItem.SetNativeSize();
        }

        /// <summary>
        /// Applies a tint color to the item thumbnail image.
        /// </summary>
        /// <param name="color">The color to apply.</param>
        public void ChangeColor(Color color)
        {
            imageItem.color = color;
        }

        /// <summary>
        /// Handles pointer click to select this slot, triggering part equipping via the parent panel.
        /// </summary>
        public void OnPointerClick(PointerEventData eventData)
        {
            _parent.SelectSlot(this);
        }
    }
}
