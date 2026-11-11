using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace LayerLab.ArtMakerUnity
{
    /// <summary>
    /// HSV color picker UI component that provides saturation-value area and hue slider
    /// for selecting colors interactively. Applies selected colors to character parts via PartsManager.
    /// </summary>
    public class ColorPicker : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        /// <summary>
        /// Singleton instance of the ColorPicker.
        /// </summary>
        public static ColorPicker Instance { get; private set; }

        [Header("HSV Color Picker Components")]
        [SerializeField] private Image svArea;
        [SerializeField] private Image hueSlider;
        [SerializeField] private RectTransform svIndicator;
        [SerializeField] private RectTransform hueIndicator;
        [SerializeField] private Image previewColor;
        [SerializeField] private TMP_InputField hexInput;
        [SerializeField] private Button buttonCopy;

        /// <summary>
        /// Event fired whenever the selected color changes.
        /// </summary>
        public event Action<Color> OnColorChanged;

        private float _hue;
        private float _saturation = 1f;
        private float _value = 1f;
        private ColorTargetType _currentTarget;
        private PartsManager _partsManager;

        private Texture2D _svTexture;
        private Texture2D _hueTexture;
        private Sprite _svSprite;
        private Sprite _hueSprite;
        private bool _isDraggingSV;
        private bool _isDraggingHue;
        private Color[] _svColors;

        private void Awake()
        {
            Instance = this;
        }

        /// <summary>
        /// Initializes the color picker with the given PartsManager and generates initial textures.
        /// </summary>
        /// <param name="pm">The PartsManager used to apply color changes to character parts.</param>
        public void Init(PartsManager pm)
        {
            _partsManager = pm;
            CreateHueTexture();
            RegenerateSVTexture();
            UpdateIndicators();
            UpdateColorPreview();

            if (hexInput != null)
                hexInput.onEndEdit.AddListener(OnHexInputEndEdit);

            if (buttonCopy != null)
                buttonCopy.onClick.AddListener(CopyColorToClipboard);

            if (_partsManager != null)
                _partsManager.OnColorChanged += OnPartsManagerColorChanged;
        }

        /// <summary>
        /// Sets the current color target type and updates the picker to reflect the target's current color.
        /// </summary>
        /// <param name="target">The color target type (e.g., Skin, Hair, Eye).</param>
        public void SetTarget(ColorTargetType target)
        {
            _currentTarget = target;

            if (_partsManager != null)
            {
                Color current = _partsManager.GetColor(target);
                Color.RGBToHSV(current, out _hue, out _saturation, out _value);
                RegenerateSVTexture();
                UpdateIndicators();
                UpdateColorPreview();
            }
        }

        /// <summary>
        /// Sets the color picker to the specified HSV values and applies the resulting color.
        /// </summary>
        /// <param name="h">Hue value (0-1).</param>
        /// <param name="s">Saturation value (0-1).</param>
        /// <param name="v">Brightness value (0-1).</param>
        public void SetHSV(float h, float s, float v)
        {
            _hue = Mathf.Clamp01(h);
            _saturation = Mathf.Clamp01(s);
            _value = Mathf.Clamp01(v);

            RegenerateSVTexture();
            UpdateIndicators();
            ApplyColor();
        }

        /// <summary>
        /// Sets the color picker to match the given RGB color.
        /// </summary>
        /// <param name="color">The color to set.</param>
        public void SetColor(Color color)
        {
            Color.RGBToHSV(color, out float h, out float s, out float v);
            SetHSV(h, s, v);
        }

        /// <summary>
        /// Returns the currently selected color as an RGB Color.
        /// </summary>
        public Color GetCurrentColor()
        {
            return Color.HSVToRGB(_hue, _saturation, _value);
        }

        /// <summary>Current hue value (0-1).</summary>
        public float Hue => _hue;
        /// <summary>Current saturation value (0-1).</summary>
        public float Saturation => _saturation;
        /// <summary>Current brightness value (0-1).</summary>
        public float Value => _value;

        /// <inheritdoc />
        public void OnPointerDown(PointerEventData eventData)
        {
            TryHandleInput(eventData);
        }

        /// <inheritdoc />
        public void OnDrag(PointerEventData eventData)
        {
            if (_isDraggingSV)
                HandleSVInput(eventData);
            else if (_isDraggingHue)
                HandleHueInput(eventData);
        }

        /// <inheritdoc />
        public void OnPointerUp(PointerEventData eventData)
        {
            _isDraggingSV = false;
            _isDraggingHue = false;
        }

        private void TryHandleInput(PointerEventData eventData)
        {
            _isDraggingSV = false;
            _isDraggingHue = false;

            if (svArea != null)
            {
                RectTransform svRect = svArea.rectTransform;
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    svRect, eventData.position, eventData.pressEventCamera, out Vector2 localPoint))
                {
                    if (IsPointInRect(localPoint, svRect.rect))
                    {
                        _isDraggingSV = true;
                        UpdateSV(localPoint, svRect.rect);
                        return;
                    }
                }
            }

            if (hueSlider != null)
            {
                RectTransform hueRect = hueSlider.rectTransform;
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    hueRect, eventData.position, eventData.pressEventCamera, out Vector2 localPoint))
                {
                    if (IsPointInRect(localPoint, hueRect.rect))
                    {
                        _isDraggingHue = true;
                        UpdateHueFromInput(localPoint, hueRect.rect);
                    }
                }
            }
        }

        private void HandleSVInput(PointerEventData eventData)
        {
            if (svArea == null) return;
            RectTransform svRect = svArea.rectTransform;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                svRect, eventData.position, eventData.pressEventCamera, out Vector2 localPoint))
            {
                UpdateSV(localPoint, svRect.rect);
            }
        }

        private void HandleHueInput(PointerEventData eventData)
        {
            if (hueSlider == null) return;
            RectTransform hueRect = hueSlider.rectTransform;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                hueRect, eventData.position, eventData.pressEventCamera, out Vector2 localPoint))
            {
                UpdateHueFromInput(localPoint, hueRect.rect);
            }
        }

        private void UpdateSV(Vector2 localPoint, Rect rect)
        {
            _saturation = Mathf.Clamp01((localPoint.x - rect.x) / rect.width);
            _value = Mathf.Clamp01((localPoint.y - rect.y) / rect.height);
            UpdateIndicators();
            ApplyColor();
        }

        private void UpdateHueFromInput(Vector2 localPoint, Rect rect)
        {
            if (rect.width > rect.height)
                _hue = Mathf.Clamp01((localPoint.x - rect.x) / rect.width);
            else
                _hue = Mathf.Clamp01((localPoint.y - rect.y) / rect.height);

            RegenerateSVTexture();
            UpdateIndicators();
            ApplyColor();
        }

        private void ApplyColor()
        {
            Color color = GetCurrentColor();
            UpdateColorPreview();

            if (_partsManager != null)
                _partsManager.SetColor(_currentTarget, color);

            OnColorChanged?.Invoke(color);
        }

        private void UpdateColorPreview()
        {
            Color color = GetCurrentColor();

            if (previewColor != null)
                previewColor.color = color;

            if (hexInput != null)
                hexInput.SetTextWithoutNotify("#" + ColorUtility.ToHtmlStringRGB(color));
        }

        private void UpdateIndicators()
        {
            if (svIndicator != null && svArea != null)
            {
                Rect svRect = svArea.rectTransform.rect;
                float x = svRect.x + svRect.width * _saturation;
                float y = svRect.y + svRect.height * _value;
                svIndicator.localPosition = new Vector3(x, y, 0);
            }

            if (hueIndicator != null && hueSlider != null)
            {
                Rect hueRect = hueSlider.rectTransform.rect;
                if (hueRect.width > hueRect.height)
                {
                    float x = hueRect.x + hueRect.width * _hue;
                    hueIndicator.localPosition = new Vector3(x, hueRect.center.y, 0);
                }
                else
                {
                    float y = hueRect.y + hueRect.height * _hue;
                    hueIndicator.localPosition = new Vector3(hueRect.center.x, y, 0);
                }
            }
        }

        private void CreateHueTexture()
        {
            if (hueSlider == null) return;

            _hueTexture = new Texture2D(1, 256) { wrapMode = TextureWrapMode.Clamp };

            for (int y = 0; y < 256; y++)
            {
                float h = (float)y / 255;
                _hueTexture.SetPixel(0, y, Color.HSVToRGB(h, 1f, 1f));
            }

            _hueTexture.Apply();
            _hueSprite = Sprite.Create(_hueTexture,
                new Rect(0, 0, _hueTexture.width, _hueTexture.height), new Vector2(0.5f, 0.5f));
            hueSlider.sprite = _hueSprite;
        }

        private void RegenerateSVTexture()
        {
            if (svArea == null) return;

            const int size = 256;

            if (_svTexture == null)
                _svTexture = new Texture2D(size, size) { wrapMode = TextureWrapMode.Clamp };

            _svColors ??= new Color[size * size];

            for (int y = 0; y < size; y++)
            {
                float v = y / 255f;
                for (int x = 0; x < size; x++)
                {
                    float s = x / 255f;
                    _svColors[y * size + x] = Color.HSVToRGB(_hue, s, v);
                }
            }

            _svTexture.SetPixels(_svColors);
            _svTexture.Apply();

            if (_svSprite != null) Destroy(_svSprite);
            _svSprite = Sprite.Create(_svTexture,
                new Rect(0, 0, _svTexture.width, _svTexture.height), new Vector2(0.5f, 0.5f));
            svArea.sprite = _svSprite;
        }

        private void OnHexInputEndEdit(string value)
        {
            string hex = value.Trim();
            if (!hex.StartsWith("#"))
                hex = "#" + hex;

            if (ColorUtility.TryParseHtmlString(hex, out Color color))
            {
                SetColor(color);
            }
            else
            {
                UpdateColorPreview();
            }
        }

        private bool IsPointInRect(Vector2 point, Rect rect)
        {
            return point.x >= rect.xMin && point.x <= rect.xMax &&
                   point.y >= rect.yMin && point.y <= rect.yMax;
        }

        private void CopyColorToClipboard()
        {
            string hex = "#" + ColorUtility.ToHtmlStringRGB(GetCurrentColor());
            GUIUtility.systemCopyBuffer = hex;
        }

        /// <summary>
        /// Handles external color changes from PartsManager (e.g. ResetAll).
        /// Updates sliders, hex input, and color preview without calling ApplyColor,
        /// so the feedback loop (SetColor → OnColorChanged → SetColor) cannot occur.
        /// </summary>
        private void OnPartsManagerColorChanged(ColorTargetType target, Color color)
        {
            if (target != _currentTarget) return;
            Color.RGBToHSV(color, out _hue, out _saturation, out _value);
            RegenerateSVTexture();
            UpdateIndicators();
            UpdateColorPreview();
        }

        private void OnDestroy()
        {
            if (_partsManager != null)
                _partsManager.OnColorChanged -= OnPartsManagerColorChanged;
            if (buttonCopy != null)
                buttonCopy.onClick.RemoveListener(CopyColorToClipboard);
            if (_svSprite != null) Destroy(_svSprite);
            if (_hueSprite != null) Destroy(_hueSprite);
            if (_svTexture != null) Destroy(_svTexture);
            if (_hueTexture != null) Destroy(_hueTexture);
        }
    }
}
