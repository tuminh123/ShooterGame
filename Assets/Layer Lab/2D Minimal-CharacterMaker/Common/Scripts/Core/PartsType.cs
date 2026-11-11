using System;
using System.Collections.Generic;

namespace LayerLab.ArtMakerUnity
{
    /// <summary>
    /// Defines the available character part types (e.g., Eye, Hair, weapons).
    /// </summary>
    public enum PartsType
    {
        Eye,
        Hair,
        Helmet,
        Beard,
        Chest,
        Sword,
        Axe,
        Bow,
        Shield,
        Wand,
        Staff,
        Spear,
        Blunt,
        Crossbow,
        SubItem,
        Arrow,
        HelmetHair,
        Skin,
        Gun,
        Fishing,
        Pickaxe,
        Mount
    }

    /// <summary>
    /// Defines UI panel categories used to group related parts types together.
    /// </summary>
    public enum UICategory
    {
        Hair,
        Eye,
        Beard,
        Skin,
        Helmet,
        Chest,
        HandRight,
        HandLeft,
        Fishing,
        Pickaxe,
        Mount,
    }

    /// <summary>
    /// Defines color target types for character customization (Skin, Hair, Eye, Beard).
    /// </summary>
    public enum ColorTargetType
    {
        Skin,
        Hair,
        Eye,
        Beard
    }

    /// <summary>
    /// Defines the visual theme types for character assets.
    /// </summary>
    public enum ThemeType
    {
        Fantasy,
        Fantasy1,
    }

    /// <summary>
    /// Provides configuration and lookup utilities for <see cref="UICategory"/> groupings.
    /// Maps each UI category to its associated <see cref="PartsType"/> sub-types.
    /// </summary>
    public static class UICategoryConfig
    {
        private static readonly Dictionary<UICategory, PartsType[]> SubTypes = new()
        {
            { UICategory.Hair, new[] { PartsType.Hair } },
            { UICategory.Eye, new[] { PartsType.Eye } },
            { UICategory.Beard, new[] { PartsType.Beard } },
            { UICategory.Skin, new[] { PartsType.Skin } },
            { UICategory.Helmet, new[] { PartsType.Helmet } },
            { UICategory.Chest, new[] { PartsType.Chest } },
            {
                UICategory.HandRight,
                new[]
                {
                    PartsType.Sword, PartsType.Axe, PartsType.Bow,
                    PartsType.Wand, PartsType.Staff, PartsType.Spear,
                    PartsType.Blunt, PartsType.Crossbow, PartsType.Gun
                }
            },
            { UICategory.Fishing, new[] { PartsType.Fishing } },
            { UICategory.Pickaxe, new[] { PartsType.Pickaxe } },
            { UICategory.Mount, new[] { PartsType.Mount } },
            { UICategory.HandLeft, new[] { PartsType.Shield, PartsType.SubItem } },
        };

        /// <summary>
        /// Returns the array of <see cref="PartsType"/> sub-types for the given UI category.
        /// </summary>
        /// <param name="category">The UI category to query.</param>
        /// <returns>An array of associated parts types, or empty if not found.</returns>
        public static PartsType[] GetSubTypes(UICategory category) =>
            SubTypes.TryGetValue(category, out var types) ? types : Array.Empty<PartsType>();

        /// <summary>
        /// Checks whether the given UI category is a group containing multiple sub-types.
        /// </summary>
        /// <param name="category">The UI category to check.</param>
        /// <returns>True if the category has more than one sub-type.</returns>
        public static bool IsGroup(UICategory category) =>
            SubTypes.TryGetValue(category, out var types) && types.Length > 1;

        /// <summary>
        /// Checks whether the given UI category represents the Skin category.
        /// Skin is always equipped and cannot be toggled or unequipped.
        /// </summary>
        /// <param name="category">The UI category to check.</param>
        /// <returns>True if the category is Skin.</returns>
        public static bool IsSkin(UICategory category) => category == UICategory.Skin;
    }
}
