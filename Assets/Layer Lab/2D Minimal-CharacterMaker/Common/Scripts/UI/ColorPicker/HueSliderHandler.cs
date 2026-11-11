using UnityEngine;
using UnityEngine.EventSystems;

namespace LayerLab.ArtMakerUnity
{
    /// <summary>
    /// Handles pointer input on the hue slider bar, converting drag position
    /// to a hue value and updating the associated ColorPicker.
    /// </summary>
    public class HueSliderHandler : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [SerializeField] private ColorPicker colorPicker;
        [SerializeField] private RectTransform handleArea;

        private RectTransform _rectTransform;
        private bool _isDragging;

        private void Awake()
        {
            _rectTransform = handleArea != null ? handleArea : GetComponent<RectTransform>();
        }

        /// <inheritdoc />
        public void OnPointerDown(PointerEventData eventData)
        {
            _isDragging = true;
            UpdateHue(eventData);
        }

        /// <inheritdoc />
        public void OnDrag(PointerEventData eventData)
        {
            if (_isDragging)
                UpdateHue(eventData);
        }

        /// <inheritdoc />
        public void OnPointerUp(PointerEventData eventData)
        {
            _isDragging = false;
        }

        private void UpdateHue(PointerEventData eventData)
        {
            if (colorPicker == null || _rectTransform == null) return;

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _rectTransform, eventData.position, eventData.pressEventCamera, out Vector2 localPoint))
            {
                Rect rect = _rectTransform.rect;
                float hue;

                if (rect.width > rect.height)
                    hue = Mathf.Clamp01((localPoint.x - rect.x) / rect.width);
                else
                    hue = Mathf.Clamp01((localPoint.y - rect.y) / rect.height);

                colorPicker.SetHSV(hue, colorPicker.Saturation, colorPicker.Value);
            }
        }
    }
}
