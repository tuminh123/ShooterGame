using System;
using System.Collections.Generic;
using UnityEngine;

namespace LayerLab.ArtMakerUnity
{
    /// <summary>
    /// ScriptableObject that stores a collection of character preset configurations.
    /// Each preset contains parts, colors, and visibility settings for character customization.
    /// </summary>
    [CreateAssetMenu(fileName = "PresetData", menuName = "LayerLab/ArtMakerUnity/PresetData")]
    public class PresetData : ScriptableObject
    {
        public List<PresetItem> items = new();

        /// <summary>
        /// Represents a single character preset configuration with parts, colors, and visibility data.
        /// </summary>
        [Serializable]
        public class PresetItem
        {
            public List<PartsEntry> parts = new();
            public List<ColorEntry> colors = new();
            public List<VisibilityEntry> visibility = new();
            public bool isEmpty = true;
        }

        /// <summary>
        /// Stores the selected sprite index for a specific parts type.
        /// </summary>
        [Serializable]
        public class PartsEntry
        {
            public PartsType type;
            public int index;
        }

        /// <summary>
        /// Stores the color value for a specific color target type.
        /// </summary>
        [Serializable]
        public class ColorEntry
        {
            public ColorTargetType target;
            public Color color;
        }

        /// <summary>
        /// Stores the visibility state for a specific parts type.
        /// </summary>
        [Serializable]
        public class VisibilityEntry
        {
            public PartsType type;
            public bool visible;
        }

        /// <summary>Total number of preset slots.</summary>
        public int SlotCount => items.Count;

        /// <summary>
        /// Returns the preset item at the given slot index, or null if out of range.
        /// </summary>
        /// <param name="slot">The slot index.</param>
        /// <returns>The preset item, or null if the index is invalid.</returns>
        public PresetItem GetItem(int slot)
        {
            if (slot < 0 || slot >= items.Count) return null;
            return items[slot];
        }

        /// <summary>
        /// Saves the given preset item to the specified slot, expanding the list if necessary.
        /// </summary>
        /// <param name="slot">The slot index to save to.</param>
        /// <param name="item">The preset item to save.</param>
        public void SaveItem(int slot, PresetItem item)
        {
            if (slot < 0) return;

            while (items.Count <= slot)
                items.Add(new PresetItem());

            item.isEmpty = false;
            items[slot] = item;
        }
    }
}
