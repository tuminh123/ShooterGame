using UnityEngine;

namespace LayerLab.ArtMakerUnity
{
    /// <summary>
    /// Manages a set of predefined color preset slots for quick color selection.
    /// Provides preset skin tones, hair colors, and a random color generator.
    /// </summary>
    public class ColorPresetManager : MonoBehaviour
    {
        [SerializeField] private ColorPresetSlot[] slots;
        [SerializeField] private Color[] presetColors = new[]
        {
            // Skin tones
            new Color(1.00f, 0.89f, 0.77f),
            new Color(0.96f, 0.80f, 0.65f),
            new Color(0.87f, 0.68f, 0.51f),
            new Color(0.72f, 0.53f, 0.34f),
            new Color(0.55f, 0.38f, 0.24f),
            new Color(0.40f, 0.26f, 0.15f),
            // Hair colors
            new Color(0.10f, 0.10f, 0.10f),
            new Color(0.35f, 0.20f, 0.10f),
            new Color(0.90f, 0.75f, 0.45f),
            new Color(0.80f, 0.25f, 0.15f),
            new Color(0.20f, 0.35f, 0.70f),
            new Color(0.60f, 0.30f, 0.60f),
            new Color(0.95f, 0.60f, 0.20f),
            new Color(0.85f, 0.85f, 0.85f),
            new Color(0.95f, 0.55f, 0.70f),
            new Color(0.25f, 0.65f, 0.45f),
        };
        [SerializeField] private ColorPicker colorPicker;

        [Header("Random Color Range")]
        [SerializeField] private float minRandomSaturation = 0.4f;
        [SerializeField] private float maxRandomSaturation = 1f;
        [SerializeField] private float minRandomValue = 0.5f;
        [SerializeField] private float maxRandomValue = 1f;

        private PartsManager _partsManager;

        /// <summary>
        /// Initializes the preset manager and assigns colors to each slot.
        /// </summary>
        /// <param name="pm">The PartsManager used for color application.</param>
        public void Init(PartsManager pm)
        {
            _partsManager = pm;

            if (slots == null || presetColors == null) return;

            int count = Mathf.Min(slots.Length, presetColors.Length);
            for (int i = 0; i < count; i++)
            {
                if (slots[i] == null) continue;
                slots[i].Init(presetColors[i], this);
            }
        }

        /// <summary>
        /// Applies the given color to the color picker.
        /// </summary>
        /// <param name="color">The color to apply.</param>
        public void ApplyColor(Color color)
        {
            if (colorPicker != null)
                colorPicker.SetColor(color);
        }

        /// <summary>
        /// Generates a random color within the configured HSV range and applies it to the specified target.
        /// </summary>
        /// <param name="target">The color target type to apply the random color to.</param>
        public void SetRandomColor(ColorTargetType target)
        {
            float h = Random.Range(0f, 1f);
            float s = Random.Range(minRandomSaturation, maxRandomSaturation);
            float v = Random.Range(minRandomValue, maxRandomValue);
            Color color = Color.HSVToRGB(h, s, v);

            if (_partsManager != null)
                _partsManager.SetColor(target, color);

            if (colorPicker != null)
                colorPicker.SetColor(color);
        }
    }
}
