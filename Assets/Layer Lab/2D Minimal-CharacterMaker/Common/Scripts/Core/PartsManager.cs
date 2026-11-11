using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace LayerLab.ArtMakerUnity
{
    /// <summary>
    /// Manages character parts (sprites) including equipping, toggling visibility,
    /// color application, and preset serialization.
    /// </summary>
    public class PartsManager : MonoBehaviour
    {
        [SerializeField] private PartsCategory[] categories;
        [SerializeField] private Animator animator;

        [SerializeField] private SpriteRenderer[] skinRenderers;
        [SerializeField] private SpriteRenderer[] hairRenderers;
        [SerializeField] private SpriteRenderer[] eyeRenderers;
        [SerializeField] private SpriteRenderer[] beardRenderers;

        [Header("Themes")]
        [SerializeField] private List<ThemeType> selectedThemes = new();

        /// <summary>
        /// Current active sprite index for each parts type.
        /// A value of -1 means the part is unequipped.
        /// </summary>
        public Dictionary<PartsType, int> ActiveIndices { get; private set; } = new();

        /// <summary>
        /// Visibility state for each parts type. True if the part is visible.
        /// </summary>
        public Dictionary<PartsType, bool> Visibility { get; private set; } = new();

        /// <summary>
        /// Current color for each color target type (Skin, Hair, Eye, Beard).
        /// </summary>
        public Dictionary<ColorTargetType, Color> Colors { get; private set; } = new();

        /// <summary>
        /// Invoked when a parts type is equipped or changed. Parameters: parts type, new index.
        /// </summary>
        public event Action<PartsType, int> OnPartsChanged;

        /// <summary>
        /// Invoked when a parts type visibility is toggled. Parameters: parts type, visible state.
        /// </summary>
        public event Action<PartsType, bool> OnVisibilityChanged;

        /// <summary>
        /// Invoked when a color target color changes. Parameters: color target type, new color.
        /// </summary>
        public event Action<ColorTargetType, Color> OnColorChanged;

        private Dictionary<PartsType, PartsCategory> categoryMap;
        private MountSeat mountSeat;
        private FishingEquip fishingEquip;
        private Transform leftHand;
        private static readonly PartsType[] OptionalEquipment =
            { PartsType.Gun, PartsType.Fishing, PartsType.Pickaxe, PartsType.Mount };


        /// <summary>
        /// Initializes all categories, indices, visibility, colors, and applies default sprites.
        /// Must be called before any other operation.
        /// </summary>
        public void Init()
        {
            mountSeat = GetComponent<MountSeat>();
            fishingEquip = GetComponent<FishingEquip>();
            if (fishingEquip) fishingEquip.ResetPreview();
            if (fishingEquip) fishingEquip.RestoreEquipment();
            leftHand = animator ? animator.transform.Find("HandLeft") : null;
            // Initialize lookup dictionaries
            categoryMap = new Dictionary<PartsType, PartsCategory>();
            ActiveIndices.Clear();
            Visibility.Clear();
            Colors.Clear();

            if (categories == null) return;

            // Register each category and set default index/visibility
            foreach (var cat in categories)
            {
                categoryMap[cat.type] = cat;
                ActiveIndices[cat.type] = 0;
                Visibility[cat.type] = true;

                // Beard always supports color changes
                if (cat.type == PartsType.Beard && !cat.canChangeColor)
                {
                    cat.canChangeColor = true;
                    cat.colorTarget = ColorTargetType.Beard;
                }
                // Eye does not support color changes
                if (cat.type == PartsType.Eye)
                    cat.canChangeColor = false;
            }

            // For group categories, only the first sub-type is visible; the rest are hidden
            foreach (UICategory uiCat in Enum.GetValues(typeof(UICategory)))
            {
                if (!UICategoryConfig.IsGroup(uiCat)) continue;
                var subTypes = UICategoryConfig.GetSubTypes(uiCat);
                for (int i = 1; i < subTypes.Length; i++)
                {
                    if (Visibility.ContainsKey(subTypes[i]))
                        Visibility[subTypes[i]] = false;
                }
            }

            foreach (var type in OptionalEquipment)
            {
                if (!categoryMap.ContainsKey(type)) continue;
                bool stocked = IsTool(type) && GetPartsCount(type) > 0;
                ActiveIndices[type] = stocked ? 0 : -1;
                Visibility[type] = stocked;
            }

            // Arrow is hidden by default (only shown when Bow or Crossbow is equipped)
            if (Visibility.ContainsKey(PartsType.Arrow))
                Visibility[PartsType.Arrow] = false;

            // HelmetHair is hidden by default (only shown when Helmet is equipped)
            if (Visibility.ContainsKey(PartsType.HelmetHair))
                Visibility[PartsType.HelmetHair] = false;

            // Set default colors to white
            Colors[ColorTargetType.Skin] = Color.white;
            Colors[ColorTargetType.Hair] = Color.white;
            Colors[ColorTargetType.Eye] = Color.white;
            Colors[ColorTargetType.Beard] = Color.white;

            // Auto-detect color renderers if not assigned in the Inspector
            // AutoMapColorRenderers();

            // Apply initial sprites and visibility state to all categories
            foreach (var cat in categories)
            {
                ApplySprites(cat, 0);

                // Enable/disable GameObjects based on visibility state
                bool visible = Visibility.TryGetValue(cat.type, out var v) && v;
                if (cat.canToggle)
                    SetRenderersActive(cat, visible);
            }

            ResolveSavedHands();
            // Sync HelmetHair visibility based on initial Helmet and Hair state
            SyncHelmetHairVisibility();
        }

        /// <summary>
        /// Auto-detects and assigns color renderers (Skin, Hair, Eye, Beard) from child objects
        /// if they are not already assigned in the Inspector.
        /// </summary>
        private void AutoMapColorRenderers()
        {
            if (skinRenderers != null && skinRenderers.Length > 0 &&
                hairRenderers != null && hairRenderers.Length > 0 &&
                eyeRenderers != null && eyeRenderers.Length > 0 &&
                beardRenderers != null && beardRenderers.Length > 0)
                return;

            var allRenderers = GetComponentsInChildren<SpriteRenderer>(true);

            if (skinRenderers == null || skinRenderers.Length == 0)
                skinRenderers = FindRenderersByName(allRenderers, "Body", "Head");
            if (hairRenderers == null || hairRenderers.Length == 0)
                hairRenderers = FindRenderersByName(allRenderers, "Hair", "Hair_Helmet");
            if (eyeRenderers == null || eyeRenderers.Length == 0)
                eyeRenderers = FindRenderersByName(allRenderers, "Eye");
            if (beardRenderers == null || beardRenderers.Length == 0)
                beardRenderers = FindRenderersByName(allRenderers, "Beard");
        }

        /// <summary>
        /// Finds SpriteRenderers whose GameObject names match any of the given names.
        /// </summary>
        private SpriteRenderer[] FindRenderersByName(SpriteRenderer[] allRenderers, params string[] names)
        {
            var result = new List<SpriteRenderer>();
            foreach (var sr in allRenderers)
            {
                foreach (var n in names)
                {
                    if (sr.gameObject.name == n)
                    {
                        result.Add(sr);
                        break;
                    }
                }
            }
            return result.ToArray();
        }

        /// <summary>
        /// Returns the PartsCategory for the given type, or null if not found.
        /// </summary>
        private PartsCategory GetCategory(PartsType type)
        {
            if (categoryMap == null || !categoryMap.TryGetValue(type, out var cat)) return null;
            return cat;
        }

        /// <summary>
        /// Returns the total number of available sprites for the given parts type.
        /// </summary>
        /// <param name="type">The parts type to query.</param>
        /// <returns>The sprite count, or 0 if the type is not found.</returns>
        public int GetPartsCount(PartsType type)
        {
            var cat = GetCategory(type);
            if (cat == null) return 0;
            return cat.SpriteCount > 0 ? cat.SpriteCount : cat.ThumbnailCount;
        }

        /// <summary>
        /// Returns the current active sprite index for the given parts type.
        /// </summary>
        /// <param name="type">The parts type to query.</param>
        /// <returns>The active index, or 0 if not found.</returns>
        public int GetActiveIndex(PartsType type)
        {
            return ActiveIndices.TryGetValue(type, out var index) ? index : 0;
        }

        /// <summary>
        /// Returns the thumbnail sprite for the given parts type at the specified index.
        /// Falls back to the first renderer's sprite if no dedicated thumbnails exist.
        /// </summary>
        /// <param name="type">The parts type to query.</param>
        /// <param name="index">The sprite index.</param>
        /// <returns>The thumbnail sprite, or null if out of range.</returns>
        public Sprite GetThumbnail(PartsType type, int index)
        {
            var cat = GetCategory(type);
            if (cat == null) return null;

            if (cat.thumbnails != null && cat.thumbnails.Length > 0)
            {
                if (index < 0 || index >= cat.thumbnails.Length) return null;
                return cat.thumbnails[index];
            }

            if (cat.renderers == null || cat.renderers.Length == 0) return null;
            var sprites = cat.renderers[0].sprites;
            if (sprites == null || index < 0 || index >= sprites.Length) return null;
            return sprites[index];
        }

        /// <summary>
        /// Checks whether the given parts type is currently visible.
        /// </summary>
        /// <param name="type">The parts type to check.</param>
        /// <returns>True if visible, false otherwise.</returns>
        public bool IsPartsVisible(PartsType type)
        {
            return Visibility.TryGetValue(type, out var visible) && visible;
        }

        /// <summary>
        /// Checks whether the given parts type has an equipped sprite (index >= 0).
        /// </summary>
        /// <param name="type">The parts type to check.</param>
        /// <returns>True if equipped, false otherwise.</returns>
        public bool IsEquipped(PartsType type)
        {
            return ActiveIndices.TryGetValue(type, out var index) && index >= 0;
        }

        /// <summary>
        /// Equips a specific sprite index for the given parts type.
        /// Clamps the index to valid range and fires <see cref="OnPartsChanged"/>.
        /// </summary>
        /// <param name="type">The parts type to equip.</param>
        /// <param name="index">The sprite index to apply.</param>
        public void EquipParts(PartsType type, int index)
        {
            var cat = GetCategory(type);
            if (cat == null) return;

            int count = GetPartsCount(type);
            if (count == 0) return;

            PrepareEquipmentChange(type, true);
            index = Mathf.Clamp(index, 0, count - 1);
            ActiveIndices[type] = index;
            ApplySprites(cat, index);

            // Restore visibility if previously unequipped
            if (cat.canToggle && Visibility.TryGetValue(type, out var vis) && !vis)
            {
                Visibility[type] = true;
                SetRenderersActive(cat, true);
                OnVisibilityChanged?.Invoke(type, true);
            }

            if (IsHandRightWeapon(type)) SyncLeftHand();
            OnPartsChanged?.Invoke(type, index);

            // Sync Arrow visibility when any HandRight weapon is equipped
            if (IsHandRightWeapon(type))
                SyncArrowVisibility();

            // Sync HelmetHair when Hair style changes
            if (type == PartsType.Hair)
            {
                var helmetHairCat = GetCategory(PartsType.HelmetHair);
                if (helmetHairCat != null && index < helmetHairCat.SpriteCount)
                {
                    ActiveIndices[PartsType.HelmetHair] = index;
                    ApplySprites(helmetHairCat, index);
                }
            }

            // Sync HelmetHair visibility when Helmet is equipped
            if (type == PartsType.Helmet)
                SyncHelmetHairVisibility();
        }

        /// <summary>
        /// Unequips the given parts type by hiding it and setting index to -1.
        /// Only works if the category supports toggling.
        /// </summary>
        /// <param name="type">The parts type to unequip.</param>
        public void UnequipParts(PartsType type)
        {
            var cat = GetCategory(type);
            if (cat == null || !cat.canToggle) return;

            PrepareEquipmentChange(type, false);
            ActiveIndices[type] = -1;
            Visibility[type] = false;
            SetRenderersActive(cat, false);

            if (IsHandRightWeapon(type)) SyncLeftHand();
            OnPartsChanged?.Invoke(type, -1);
            OnVisibilityChanged?.Invoke(type, false);

            // Sync Arrow when Bow or Crossbow is unequipped
            if (type == PartsType.Bow || type == PartsType.Crossbow)
                SyncArrowVisibility();

            // Sync HelmetHair when Helmet is unequipped
            if (type == PartsType.Helmet)
                SyncHelmetHairVisibility();
        }

        /// <summary>
        /// Toggles the visibility of the given parts type.
        /// Only works if the category supports toggling.
        /// </summary>
        /// <param name="type">The parts type to toggle.</param>
        /// <param name="visible">Whether the part should be visible.</param>
        public void ToggleParts(PartsType type, bool visible)
        {
            var cat = GetCategory(type);
            if (cat == null || !cat.canToggle) return;

            if (visible && GetActiveIndex(type) < 0) return;
            PrepareEquipmentChange(type, visible);
            Visibility[type] = visible;
            SetRenderersActive(cat, visible);

            if (IsHandRightWeapon(type)) SyncLeftHand();
            OnVisibilityChanged?.Invoke(type, visible);

            // Sync Arrow when Bow or Crossbow visibility changes
            if (IsHandRightWeapon(type))
                SyncArrowVisibility();

            // Sync HelmetHair when Helmet or Hair visibility changes
            if (type == PartsType.Helmet || type == PartsType.Hair)
                SyncHelmetHairVisibility();
        }

        /// <summary>
        /// Equips the next sprite for the given parts type, wrapping around to the start.
        /// </summary>
        /// <param name="type">The parts type to cycle.</param>
        public void NextParts(PartsType type)
        {
            int count = GetPartsCount(type);
            if (count == 0) return;

            int current = GetActiveIndex(type);
            int next = (current + 1) % count;
            EquipParts(type, next);
        }

        /// <summary>
        /// Equips the previous sprite for the given parts type, wrapping around to the end.
        /// </summary>
        /// <param name="type">The parts type to cycle.</param>
        public void PrevParts(PartsType type)
        {
            int count = GetPartsCount(type);
            if (count == 0) return;

            int current = GetActiveIndex(type);
            int prev = (current - 1 + count) % count;
            EquipParts(type, prev);
        }

        /// <summary>
        /// Returns the current color for the specified color target.
        /// </summary>
        /// <param name="target">The color target to query.</param>
        /// <returns>The current color, or white if not found.</returns>
        public Color GetColor(ColorTargetType target)
        {
            return Colors.TryGetValue(target, out var color) ? color : Color.white;
        }

        /// <summary>
        /// Sets the color for the specified target and applies it to the corresponding renderers.
        /// Fires <see cref="OnColorChanged"/>.
        /// </summary>
        /// <param name="target">The color target to update.</param>
        /// <param name="color">The new color to apply.</param>
        public void SetColor(ColorTargetType target, Color color)
        {
            Colors[target] = color;
            ApplyColor(target, color);
            OnColorChanged?.Invoke(target, color);
        }

        /// <summary>
        /// Applies the given color to all SpriteRenderers associated with the color target.
        /// </summary>
        private void ApplyColor(ColorTargetType target, Color color)
        {
            SpriteRenderer[] renderers = target switch
            {
                ColorTargetType.Skin => skinRenderers,
                ColorTargetType.Hair => hairRenderers,
                ColorTargetType.Eye => eyeRenderers,
                ColorTargetType.Beard => beardRenderers,
                _ => null
            };

            if (renderers == null) return;

            foreach (var sr in renderers)
            {
                if (sr != null)
                    sr.color = color;
            }
        }

        /// <summary>
        /// Returns true if the given type is a right-hand weapon (Sword, Axe, Bow, etc.).
        /// </summary>
        private static bool IsHandRightWeapon(PartsType type)
        {
            return type == PartsType.Sword || type == PartsType.Axe ||
                   type == PartsType.Bow || type == PartsType.Wand ||
                   type == PartsType.Staff || type == PartsType.Spear ||
                   type == PartsType.Blunt || type == PartsType.Crossbow ||
                   type == PartsType.Gun;
        }

        /// <summary>
        /// Shows or hides Arrow and Bolt based on Bow/Crossbow visibility.
        /// Arrow is shown only when Bow is visible, Bolt only when Crossbow is visible.
        /// </summary>
        private bool restoringEquipment;

        private void PrepareEquipmentChange(PartsType type, bool select)
        {
            if (!IsHandRightWeapon(type) && type != PartsType.Shield && type != PartsType.SubItem) return;
            if (fishingEquip) fishingEquip.RestoreEquipment();
            if (select && fishingEquip) fishingEquip.ResetPreview();
            if (!select || restoringEquipment) return;
            bool right = IsHandRightWeapon(type);
            foreach (var part in categories)
            {
                var other = part.type;
                if (other == type || !IsEquipped(other)) continue;
                bool leftSlot = other == PartsType.Shield || other == PartsType.SubItem;
                bool conflicts = right
                    ? IsHandRightWeapon(other) || (IsTwoHanded(type) && leftSlot)
                    : leftSlot || IsTwoHanded(other);
                if (conflicts) UnequipParts(other);
            }
        }

        private static bool IsTool(PartsType type) => type == PartsType.Fishing || type == PartsType.Pickaxe;

        private static bool IsTwoHanded(PartsType type) =>
            type == PartsType.Bow || type == PartsType.Crossbow || type == PartsType.Spear ||
            type == PartsType.Staff ||
            type == PartsType.Gun;

        private void SyncLeftHand()
        {
            if (leftHand) leftHand.gameObject.SetActive(!ActiveIndices.Any(
                pair => pair.Value >= 0 && IsTwoHanded(pair.Key) && IsPartsVisible(pair.Key)));
        }

        // Older presets kept indices for every hidden weapon. Keep the visible choice first.
        private void ResolveSavedHands()
        {
            var equipped = categories.Where(c => IsHandRightWeapon(c.type) && IsEquipped(c.type));
            var right = equipped.FirstOrDefault(c => IsPartsVisible(c.type)) ?? equipped.FirstOrDefault();
            if (right != null) PrepareEquipmentChange(right.type, true);
            var left = UICategoryConfig.GetSubTypes(UICategory.HandLeft).Where(IsEquipped);
            foreach (var type in left.OrderByDescending(IsPartsVisible).Take(1).ToArray())
                PrepareEquipmentChange(type, true);
            SyncLeftHand();
        }

        private void SyncArrowVisibility()
        {
            var arrowCat = GetCategory(PartsType.Arrow);
            if (arrowCat == null || arrowCat.renderers == null) return;

            bool bowVisible = Visibility.TryGetValue(PartsType.Bow, out var bv) && bv;
            bool crossbowVisible = Visibility.TryGetValue(PartsType.Crossbow, out var cv) && cv;

            foreach (var pr in arrowCat.renderers)
            {
                if (pr.renderer == null) continue;
                string name = pr.renderer.gameObject.name;

                if (name == "Bolt")
                    pr.renderer.gameObject.SetActive(crossbowVisible);
                else
                    pr.renderer.gameObject.SetActive(bowVisible);
            }

            bool showAny = bowVisible || crossbowVisible;
            Visibility[PartsType.Arrow] = showAny;
            OnVisibilityChanged?.Invoke(PartsType.Arrow, showAny);
        }

        /// <summary>
        /// Shows or hides HelmetHair and Hair based on Helmet visibility and Hair toggle state.
        /// When Helmet is visible and Hair is wanted, Hair is hidden and HelmetHair is shown.
        /// When Helmet is not visible, Hair is shown and HelmetHair is hidden.
        /// When Hair is toggled off, both Hair and HelmetHair are hidden.
        /// </summary>
        private void SyncHelmetHairVisibility()
        {
            var helmetHairCat = GetCategory(PartsType.HelmetHair);
            if (helmetHairCat == null) return;

            var hairCat = GetCategory(PartsType.Hair);
            if (hairCat == null) return;

            bool hairWanted = Visibility.TryGetValue(PartsType.Hair, out var hv) && hv;
            bool helmetVisible = Visibility.TryGetValue(PartsType.Helmet, out var hmv) && hmv;

            if (hairWanted && helmetVisible)
            {
                // Helmet ON + Hair wanted → hide Hair, show HelmetHair
                SetRenderersActive(hairCat, false);
                SetRenderersActive(helmetHairCat, true);
                Visibility[PartsType.HelmetHair] = true;
            }
            else if (hairWanted && !helmetVisible)
            {
                // Helmet OFF + Hair wanted → show Hair, hide HelmetHair
                SetRenderersActive(hairCat, true);
                SetRenderersActive(helmetHairCat, false);
                Visibility[PartsType.HelmetHair] = false;
            }
            else
            {
                // Hair not wanted → hide both
                SetRenderersActive(hairCat, false);
                SetRenderersActive(helmetHairCat, false);
                Visibility[PartsType.HelmetHair] = false;
            }

            OnVisibilityChanged?.Invoke(PartsType.HelmetHair, Visibility[PartsType.HelmetHair]);
        }

        /// <summary>
        /// Activates or deactivates all renderer GameObjects in the given category.
        /// </summary>
        private void SetRenderersActive(PartsCategory cat, bool active)
        {
            if (cat?.renderers == null) return;
            if (IsTool(cat.type))
            {
                if (fishingEquip) fishingEquip.SetToolAvailable(cat.type == PartsType.Fishing, active);
                active = false; // Stored in its tool slot until the interaction begins.
            }
            if (cat.type == PartsType.Mount && mountSeat && mountSeat.Mount)
                mountSeat.Mount.gameObject.SetActive(active);
            foreach (var pr in cat.renderers)
            {
                if (pr.renderer != null)
                    pr.renderer.gameObject.SetActive(active);
            }
        }

        /// <summary>
        /// Assigns the sprite at the given index to all renderers in the category.
        /// </summary>
        private void ApplySprites(PartsCategory cat, int index)
        {
            if (cat.renderers == null) return;

            foreach (var pr in cat.renderers)
            {
                if (pr.renderer == null || pr.sprites == null || pr.sprites.Length == 0) continue;
                // Renderers with fewer sprites than the category max (e.g. static Bow_Line_*)
                // clamp to index 0 so they always show their single decoration sprite.
                int idx = index < 0 ? 0 : (index < pr.sprites.Length ? index : 0);
                pr.renderer.sprite = pr.sprites[idx];
            }
        }

        /// <summary>
        /// Plays the specified animation clip on the character's Animator.
        /// </summary>
        /// <param name="animName">The name of the animation clip to play.</param>
        public void PlayAnimation(string animName)
        {
            if (animator != null)
                animator.Play(animName);
        }

        /// <summary>
        /// Returns the name of the currently playing animation clip.
        /// </summary>
        /// <returns>The clip name, or an empty string if unavailable.</returns>
        public string GetCurrentAnimation()
        {
            if (animator == null) return string.Empty;

            var stateInfo = animator.GetCurrentAnimatorStateInfo(0);
            var clipInfo = animator.GetCurrentAnimatorClipInfo(0);
            if (clipInfo.Length > 0)
                return clipInfo[0].clip.name;

            return string.Empty;
        }

        /// <summary>
        /// Returns an array of all animation clip names available in the Animator controller.
        /// </summary>
        /// <returns>An array of clip names, or an empty array if no animator is assigned.</returns>
        public string[] GetAnimationNames()
        {
            if (animator == null || animator.runtimeAnimatorController == null)
                return Array.Empty<string>();

            return animator.runtimeAnimatorController.animationClips
                .Select(clip => clip.name)
                .ToArray();
        }

        /// <summary>
        /// Resets all parts indices, visibility, and colors to the initial state
        /// (equivalent to Init() output). Fires change events for all categories and
        /// color targets so UI listeners stay in sync. Does NOT touch Animator state,
        /// selectedThemes, or PanelPartsControl preset index.
        /// </summary>
        public void ResetAll()
        {
            Init();

            if (categories != null)
            {
                foreach (var cat in categories)
                {
                    if (ActiveIndices.TryGetValue(cat.type, out var idx))
                        OnPartsChanged?.Invoke(cat.type, idx);
                    if (Visibility.TryGetValue(cat.type, out var v))
                        OnVisibilityChanged?.Invoke(cat.type, v);
                }
            }

            foreach (var kvp in Colors)
                OnColorChanged?.Invoke(kvp.Key, kvp.Value);
        }

        /// <summary>
        /// Randomizes all parts by equipping a random sprite index for each category.
        /// </summary>
        public void RandomizeAll()
        {
            if (categories == null) return;

            // For each group category, randomly pick one sub-type
            bool previewPickaxe = false;
            var groupPicks = new Dictionary<UICategory, PartsType>();
            foreach (UICategory uiCat in Enum.GetValues(typeof(UICategory)))
            {
                if (!UICategoryConfig.IsGroup(uiCat)) continue;
                var subTypes = UICategoryConfig.GetSubTypes(uiCat);
                // Only include sub-types that have sprites as candidates
                var candidates = new List<PartsType>();
                foreach (var st in subTypes)
                {
                    if (GetPartsCount(st) > 0) candidates.Add(st);
                }
                // Roll the held preview separately so the combat loadout is retained.
                if (uiCat == UICategory.HandRight && GetPartsCount(PartsType.Pickaxe) > 0)
                    previewPickaxe = UnityEngine.Random.Range(0, candidates.Count + 1) == candidates.Count;
                if (candidates.Count > 0)
                    groupPicks[uiCat] = candidates[UnityEngine.Random.Range(0, candidates.Count)];
            }

            foreach (var cat in categories)
            {
                // Skip HelmetHair and Arrow — they sync automatically
                if (cat.type == PartsType.HelmetHair || cat.type == PartsType.Arrow) continue;

                int count = GetPartsCount(cat.type);
                if (count == 0) continue;

                // Beard and Helmet have 50% chance to be unequipped
                if (cat.type == PartsType.Beard || cat.type == PartsType.Helmet || cat.type == PartsType.Mount)
                {
                    if (UnityEngine.Random.value < 0.5f)
                    {
                        UnequipParts(cat.type);
                        continue;
                    }

                    // A previous UnequipParts call may have set Visibility to false; restore it
                    if (Visibility.TryGetValue(cat.type, out var v) && !v)
                        ToggleParts(cat.type, true);
                }

                // Group sub-types: only the picked one is visible, others are hidden
                if (IsHandRightWeapon(cat.type))
                {
                    bool picked = groupPicks.TryGetValue(UICategory.HandRight, out var hr) && hr == cat.type;
                    ToggleParts(cat.type, picked);
                    if (picked) EquipParts(cat.type, UnityEngine.Random.Range(0, count));
                    continue;
                }
                if (cat.type == PartsType.Shield || cat.type == PartsType.SubItem)
                {
                    if (groupPicks.TryGetValue(UICategory.HandRight, out var weapon) && IsTwoHanded(weapon))
                    {
                        UnequipParts(cat.type);
                        continue;
                    }
                    bool picked = groupPicks.TryGetValue(UICategory.HandLeft, out var hl) && hl == cat.type;
                    ToggleParts(cat.type, picked);
                    if (picked) EquipParts(cat.type, UnityEngine.Random.Range(0, count));
                    continue;
                }

                EquipParts(cat.type, UnityEngine.Random.Range(0, count));
            }

            if (fishingEquip)
            {
                if (previewPickaxe) fishingEquip.SelectPreview(FishingEquip.Preview.Pickaxe);
                else fishingEquip.ResetPreview();
            }

            // Randomize colors (Skin, Hair, Beard)
            RandomizeColors();
        }

        private void RandomizeColors()
        {
            foreach (ColorTargetType target in Enum.GetValues(typeof(ColorTargetType)))
            {
                if (target == ColorTargetType.Eye) continue;

                float h = UnityEngine.Random.Range(0f, 1f);
                float s = UnityEngine.Random.Range(0.4f, 1f);
                float v = UnityEngine.Random.Range(0.5f, 1f);
                SetColor(target, Color.HSVToRGB(h, s, v));
            }
        }

        /// <summary>
        /// Copies all parts indices, visibility, and colors from another PartsManager.
        /// </summary>
        /// <param name="other">The source PartsManager to copy from.</param>
        public void CopyFrom(PartsManager other)
        {
            if (other == null) return;

            ApplyPresetItem(other.ToPresetItem());
        }

        /// <summary>
        /// Applies a saved preset item to this character, restoring parts, colors, and visibility.
        /// </summary>
        /// <param name="item">The preset item to apply.</param>
        public void ApplyPresetItem(PresetData.PresetItem item)
        {
            if (item == null || item.isEmpty) return;

            restoringEquipment = true;
            try
            {
                // Older presets predate tools, guns and mounts.
                foreach (var type in OptionalEquipment)
                    if (!item.parts.Exists(entry => entry.type == type))
                    {
                        if (IsTool(type)) EquipParts(type, 0);
                        else UnequipParts(type);
                    }

                foreach (var entry in item.parts)
                {
                    if (entry.index < 0)
                        UnequipParts(entry.type);
                    else
                        EquipParts(entry.type, entry.index);
                }

                foreach (var entry in item.colors)
                    SetColor(entry.target, entry.color);

                foreach (var entry in item.visibility)
                {
                    // Arrow and HelmetHair are auto-synced, so do not set them directly from the preset
                    if (entry.type == PartsType.Arrow || entry.type == PartsType.HelmetHair)
                        continue;
                    ToggleParts(entry.type, IsTool(entry.type) ? IsEquipped(entry.type) : entry.visible);
                }
            }
            finally
            {
                restoringEquipment = false;
            }
            ResolveSavedHands();

            // Re-run auto-sync after applying the preset
            SyncArrowVisibility();
            SyncHelmetHairVisibility();
        }

        /// <summary>
        /// Serializes the current character state into a preset item for saving.
        /// </summary>
        /// <returns>A new <see cref="PresetData.PresetItem"/> containing the current state.</returns>
        public PresetData.PresetItem ToPresetItem()
        {
            var item = new PresetData.PresetItem { isEmpty = false };

            foreach (var kvp in ActiveIndices)
                item.parts.Add(new PresetData.PartsEntry { type = kvp.Key, index = kvp.Value });

            foreach (var kvp in Colors)
                item.colors.Add(new PresetData.ColorEntry { target = kvp.Key, color = kvp.Value });

            foreach (var kvp in Visibility)
                item.visibility.Add(new PresetData.VisibilityEntry { type = kvp.Key, visible = kvp.Value });

            return item;
        }

        #region Category Accessors

        /// <summary>
        /// Returns all registered parts types from the categories array.
        /// </summary>
        /// <returns>An array of <see cref="PartsType"/> values.</returns>
        public PartsType[] GetAllPartsTypes()
        {
            if (categories == null) return Array.Empty<PartsType>();
            return categories.Select(c => c.type).ToArray();
        }

        /// <summary>
        /// Checks whether the given parts type supports color changes.
        /// </summary>
        /// <param name="type">The parts type to check.</param>
        /// <returns>True if color change is supported.</returns>
        public bool CanChangeColor(PartsType type)
        {
            var cat = GetCategory(type);
            return cat != null && cat.canChangeColor;
        }

        /// <summary>
        /// Returns the color target type associated with the given parts type.
        /// </summary>
        /// <param name="type">The parts type to query.</param>
        /// <returns>The <see cref="ColorTargetType"/>, defaulting to Skin.</returns>
        public ColorTargetType GetColorTarget(PartsType type)
        {
            var cat = GetCategory(type);
            return cat?.colorTarget ?? ColorTargetType.Skin;
        }

        /// <summary>
        /// Checks whether the given parts type can be toggled on/off.
        /// </summary>
        /// <param name="type">The parts type to check.</param>
        /// <returns>True if toggling is supported.</returns>
        public bool CanToggle(PartsType type)
        {
            var cat = GetCategory(type);
            return cat != null && cat.canToggle;
        }

        /// <summary>
        /// Returns the display name for the given parts type.
        /// </summary>
        /// <param name="type">The parts type to query.</param>
        /// <returns>The display name, or the enum name if not set.</returns>
        public string GetDisplayName(PartsType type)
        {
            var cat = GetCategory(type);
            return cat?.displayName ?? type.ToString();
        }

        #endregion
    }
}
