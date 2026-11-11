using System.Collections.Generic;
using UnityEngine;

namespace LayerLab.ArtMakerUnity
{
    /// <summary>
    /// Stores character customization data (parts, colors, visibility) on a prefab.
    /// On Start, applies the stored configuration to the associated PartsManager.
    /// </summary>
    public class CharacterPrefabData : MonoBehaviour
    {
        [SerializeField] private List<PresetData.PartsEntry> parts = new();
        [SerializeField] private List<PresetData.ColorEntry> colors = new();
        [SerializeField] private List<PresetData.VisibilityEntry> visibility = new();

        /// <summary>
        /// Sets the character data from a preset item.
        /// </summary>
        /// <param name="item">The preset item containing parts, colors, and visibility data.</param>
        public void SetData(PresetData.PresetItem item)
        {
            parts = item.parts;
            colors = item.colors;
            visibility = item.visibility;
        }

        private void Start()
        {
            var pm = GetComponent<PartsManager>();
            if (pm == null) pm = GetComponentInChildren<PartsManager>();
            if (pm == null) return;

            pm.Init();

            var item = new PresetData.PresetItem
            {
                isEmpty = false,
                parts = parts,
                colors = colors,
                visibility = visibility
            };
            pm.ApplyPresetItem(item);
        }
    }
}
