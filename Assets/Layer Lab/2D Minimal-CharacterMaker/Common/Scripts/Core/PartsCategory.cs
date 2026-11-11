using System;
using UnityEngine;

namespace LayerLab.ArtMakerUnity
{
    /// <summary>
    /// Defines a character parts category containing sprite renderers, thumbnails,
    /// and configuration for toggling and color changes.
    /// </summary>
    [Serializable]
    public class PartsCategory
    {
        /// <summary>The parts type this category represents.</summary>
        public PartsType type;

        /// <summary>The human-readable display name shown in the UI.</summary>
        public string displayName;

        /// <summary>Array of part renderers, each mapping a SpriteRenderer to its available sprites.</summary>
        public PartRenderer[] renderers;

        /// <summary>Whether this category can be toggled on/off (equipped/unequipped).</summary>
        public bool canToggle = true;

        /// <summary>Whether this category supports color customization.</summary>
        public bool canChangeColor;

        /// <summary>The color target type used when applying color changes.</summary>
        public ColorTargetType colorTarget;

        /// <summary>Whether this category is shared across all themes.</summary>
        public bool isCommon;

        /// <summary>Optional dedicated thumbnail sprites for the parts list UI.</summary>
        public Sprite[] thumbnails;

        /// <summary>
        /// Returns the maximum sprites length across all renderers in this category.
        /// Static sub-renderers (e.g. Bow_Line_Up holding a single decoration sprite)
        /// would otherwise mask the real count when stored at index 0. Variable-length
        /// renderers fall back to index 0 in <c>ApplySprites</c>.
        /// </summary>
        public int SpriteCount
        {
            get
            {
                if (renderers == null) return 0;
                int max = 0;
                for (int i = 0; i < renderers.Length; i++)
                {
                    var s = renderers[i]?.sprites;
                    if (s != null && s.Length > max) max = s.Length;
                }
                return max;
            }
        }

        /// <summary>
        /// Returns the number of available thumbnail sprites.
        /// </summary>
        public int ThumbnailCount => thumbnails != null ? thumbnails.Length : 0;
    }

    /// <summary>
    /// Maps a <see cref="SpriteRenderer"/> to an array of interchangeable sprites
    /// representing different visual options for a character part.
    /// </summary>
    [Serializable]
    public class PartRenderer
    {
        /// <summary>The target SpriteRenderer component on the character.</summary>
        public SpriteRenderer renderer;

        /// <summary>Array of sprite options that can be assigned to the renderer.</summary>
        public Sprite[] sprites;
    }
}
