using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace LayerLab.ArtMakerUnity.Editor
{
    /// <summary>
    /// Custom inspector for PartsManager that provides auto-setup, sprite loading,
    /// theme selection, color mapping, and preview functionality in the Unity Editor.
    /// </summary>
    [CustomEditor(typeof(PartsManager))]
    public class PartsManagerEditor : UnityEditor.Editor
    {
        private const string DEFAULT_SPRITE_FOLDER = "Assets/Layer Lab/2D Minimal-CharacterMaker/Extenstions";
        private const string THUMBNAIL_SUBFOLDER = "Thumbnail";
        private const string PREFS_KEY_SPRITE_FOLDER = "PartsManagerEditor_SpriteFolderPath";
        private const string PREFS_KEY_SHOW_DETAILS = "PartsManagerEditor_ShowDetails";

        private SerializedProperty categoriesProp;
        private SerializedProperty animatorProp;
        private SerializedProperty skinRenderersProp;
        private SerializedProperty hairRenderersProp;
        private SerializedProperty eyeRenderersProp;
        private SerializedProperty beardRenderersProp;

        private Dictionary<int, bool> foldoutStates = new();
        private bool categoriesFoldout = true;
        private bool colorTargetsFoldout = true;
        private bool showDetails;
        private string spriteFolderPath = DEFAULT_SPRITE_FOLDER;
        private int previewCategoryIndex = -1;
        private int previewSpriteIndex;

        private static readonly Dictionary<PartsType, string> RendererNameMap = new()
        {
            { PartsType.Eye, "Eye" },
            { PartsType.Hair, "Hair" },
            { PartsType.Helmet, "Helmet" },
            { PartsType.Beard, "Beard" },
            { PartsType.Chest, "Chest" },
            { PartsType.Gun, "Gun" },
            { PartsType.Fishing, "Tool_Fishing" },
            { PartsType.Pickaxe, "Tool_Pick" },
            { PartsType.Mount, "Mount" },
            { PartsType.Sword, "Sword" },
            { PartsType.Axe, "Axe" },
            { PartsType.Bow, "Bow" },
            { PartsType.Shield, "Shield" },
            { PartsType.Wand, "Wand" },
            { PartsType.Staff, "Staff" },
            { PartsType.Spear, "Spear" },
            { PartsType.Blunt, "Blunt" },
            { PartsType.Crossbow, "Crossbow" },
            { PartsType.SubItem, "Sub_Item" },
            { PartsType.Arrow, "Arrow" },
            { PartsType.HelmetHair, "Hair_Helmet" },
            { PartsType.Skin, "Body" },
        };

        private static readonly Dictionary<string, PartsType> PartsFolderMap = new()
        {
            { "Chest", PartsType.Chest },
            { "Helmet", PartsType.Helmet },
            { "Shield", PartsType.Shield },
            { "Sub_Item", PartsType.SubItem },
            { "Gun", PartsType.Gun },
            { "Mount", PartsType.Mount },
            { "Sword", PartsType.Sword },
            { "Axe", PartsType.Axe },
            { "Bow", PartsType.Bow },
            { "Wand", PartsType.Wand },
            { "Staff", PartsType.Staff },
            { "Spear", PartsType.Spear },
            { "Blunt", PartsType.Blunt },
            { "Crossbow", PartsType.Crossbow },
            { "Arrow", PartsType.Arrow },
            { "Eye", PartsType.Eye },
            { "Hair", PartsType.Hair },
            { "Beard", PartsType.Beard },
            { "Helmet_Hair", PartsType.HelmetHair },
            { "Skin", PartsType.Skin },
        };

        private static readonly Dictionary<PartsType, string> ThumbnailFolderMap = new()
        {
            { PartsType.Eye, "Eye" },
            { PartsType.Hair, "Hair" },
            { PartsType.Helmet, "Helmet" },
            { PartsType.Beard, "Beard" },
            { PartsType.Chest, "Chest" },
            { PartsType.Gun, "Gun" },
            { PartsType.Fishing, "Tool_Fishing" },
            { PartsType.Pickaxe, "Tool_Pick" },
            { PartsType.Mount, "Mount" },
            { PartsType.Sword, "Sword" },
            { PartsType.Axe, "Axe" },
            { PartsType.Bow, "Bow" },
            { PartsType.Shield, "Shield" },
            { PartsType.Wand, "Wand" },
            { PartsType.Staff, "Staff" },
            { PartsType.Spear, "Spear" },
            { PartsType.Blunt, "Blunt" },
            { PartsType.Crossbow, "Crossbow" },
            { PartsType.SubItem, "Sub_Item" },
            { PartsType.Arrow, "Arrow" },
            { PartsType.HelmetHair, "Helmet_Hair" },
            { PartsType.Skin, "Skin" },
        };

        private static readonly HashSet<PartsType> CommonPartsTypes = new()
        {
            PartsType.Eye,
            PartsType.Hair,
            PartsType.Beard,
            PartsType.HelmetHair,
            PartsType.Skin,
        };

        private void OnEnable()
        {
            categoriesProp = serializedObject.FindProperty("categories");
            animatorProp = serializedObject.FindProperty("animator");
            skinRenderersProp = serializedObject.FindProperty("skinRenderers");
            hairRenderersProp = serializedObject.FindProperty("hairRenderers");
            eyeRenderersProp = serializedObject.FindProperty("eyeRenderers");
            beardRenderersProp = serializedObject.FindProperty("beardRenderers");

            spriteFolderPath = EditorPrefs.GetString(PREFS_KEY_SPRITE_FOLDER, DEFAULT_SPRITE_FOLDER);
            showDetails = EditorPrefs.GetBool(PREFS_KEY_SHOW_DETAILS, false);
        }

        private void SetSpriteFolderPath(string path)
        {
            spriteFolderPath = path;
            EditorPrefs.SetString(PREFS_KEY_SPRITE_FOLDER, path);
        }

        /// <summary>
        /// Draws the custom inspector GUI for PartsManager.
        /// </summary>
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawAnimatorField();
            EditorGUILayout.Space(4);
            DrawAutoSetupSection();
            EditorGUILayout.Space(4);

            // Color Targets / Parts Categories are populated by Auto Setup. Hide them
            // by default to keep this single click the canonical entry point;
            // expose them behind a foldout for inspection / manual tweaking.
            EditorGUI.BeginChangeCheck();
            bool newShowDetails = EditorGUILayout.Foldout(
                showDetails,
                showDetails ? "Hide Details" : "Show Details (Color Targets & Parts Categories)",
                true, EditorStyles.foldoutHeader);
            if (EditorGUI.EndChangeCheck())
            {
                showDetails = newShowDetails;
                EditorPrefs.SetBool(PREFS_KEY_SHOW_DETAILS, showDetails);
            }

            if (showDetails)
            {
                EditorGUILayout.Space(4);
                DrawColorTargetSection();
                EditorGUILayout.Space(8);
                DrawCategoriesSection();
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawAnimatorField()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Animator", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(animatorProp);
            EditorGUILayout.EndVertical();
        }

        private void DrawAutoSetupSection()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Auto Setup", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Maps renderers, loads sprites and thumbnails in one step.\n" +
                "Renderers are matched by child object name, sprites from the Base Folder.",
                MessageType.Info);

            // Object field for drag & drop folder assignment
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("Base Folder");
            var folderAsset = AssetDatabase.IsValidFolder(spriteFolderPath)
                ? AssetDatabase.LoadAssetAtPath<DefaultAsset>(spriteFolderPath)
                : null;
            EditorGUI.BeginChangeCheck();
            var newFolder = EditorGUILayout.ObjectField(folderAsset, typeof(DefaultAsset), false);
            if (EditorGUI.EndChangeCheck() && newFolder != null)
            {
                string path = AssetDatabase.GetAssetPath(newFolder);
                if (AssetDatabase.IsValidFolder(path))
                    SetSpriteFolderPath(path);
            }
            if (GUILayout.Button("Browse", GUILayout.Width(60)))
            {
                string absoluteStart = Path.GetFullPath(spriteFolderPath);
                if (!Directory.Exists(absoluteStart))
                    absoluteStart = Path.GetFullPath("Assets");
                string selected = EditorUtility.OpenFolderPanel("Select Sprite Folder", absoluteStart, "");
                if (!string.IsNullOrEmpty(selected))
                {
                    int assetsIndex = selected.IndexOf("Assets", StringComparison.Ordinal);
                    if (assetsIndex >= 0)
                        SetSpriteFolderPath(selected.Substring(assetsIndex));
                }
                GUIUtility.ExitGUI();
                return;
            }
            EditorGUILayout.EndHorizontal();
            EditorGUI.BeginChangeCheck();
            string editedPath = EditorGUILayout.TextField("Path", spriteFolderPath);
            if (EditorGUI.EndChangeCheck())
                SetSpriteFolderPath(editedPath);

            if (GUILayout.Button("Auto Setup", GUILayout.Height(28)))
            {
                AutoSetupRenderers();
                LoadSpritesFromFolder();
                LoadThumbnails();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawColorTargetSection()
        {
            EditorGUILayout.BeginVertical("box");

            colorTargetsFoldout = EditorGUILayout.Foldout(colorTargetsFoldout, "Color Targets", true, EditorStyles.foldoutHeader);

            if (colorTargetsFoldout)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(skinRenderersProp, true);
                EditorGUILayout.PropertyField(hairRenderersProp, true);
                EditorGUILayout.PropertyField(eyeRenderersProp, true);
                EditorGUILayout.PropertyField(beardRenderersProp, true);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawCategoriesSection()
        {
            EditorGUILayout.BeginVertical("box");

            categoriesFoldout = EditorGUILayout.Foldout(categoriesFoldout, "Parts Categories", true, EditorStyles.foldoutHeader);

            if (!categoriesFoldout)
            {
                EditorGUILayout.EndVertical();
                return;
            }

            EditorGUILayout.Space(2);

            if (categoriesProp == null || !categoriesProp.isArray)
            {
                EditorGUILayout.HelpBox("Cannot find the 'categories' field.", MessageType.Warning);
                EditorGUILayout.EndVertical();
                return;
            }

            EditorGUI.indentLevel++;
            for (int i = 0; i < categoriesProp.arraySize; i++)
            {
                DrawCategoryElement(i);
            }
            EditorGUI.indentLevel--;

            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("+ Add Category"))
            {
                categoriesProp.InsertArrayElementAtIndex(categoriesProp.arraySize);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void DrawCategoryElement(int index)
        {
            var element = categoriesProp.GetArrayElementAtIndex(index);
            var typeProp = element.FindPropertyRelative("type");
            var displayNameProp = element.FindPropertyRelative("displayName");
            var renderersProp = element.FindPropertyRelative("renderers");
            var canToggleProp = element.FindPropertyRelative("canToggle");
            var canChangeColorProp = element.FindPropertyRelative("canChangeColor");
            var colorTargetProp = element.FindPropertyRelative("colorTarget");

            if (!foldoutStates.ContainsKey(index))
                foldoutStates[index] = false;

            string typeName = typeProp != null ? ((PartsType)typeProp.enumValueIndex).ToString() : "Unknown";
            int spriteCount = GetSpriteCount(renderersProp);
            int rendererCount = renderersProp != null ? renderersProp.arraySize : 0;
            var thumbnailsProp = element.FindPropertyRelative("thumbnails");
            int thumbnailCount = thumbnailsProp != null ? thumbnailsProp.arraySize : 0;
            bool isReady = rendererCount > 0 && (spriteCount > 0 || thumbnailCount > 0);
            string statusIcon = isReady ? "[OK]" : "[--]";

            string info = spriteCount > 0
                ? $"Renderers: {rendererCount}, Sprites: {spriteCount}"
                : $"Renderers: {rendererCount}, Thumbnails: {thumbnailCount}";

            EditorGUILayout.BeginVertical("helpbox");

            EditorGUILayout.BeginHorizontal();
            foldoutStates[index] = EditorGUILayout.Foldout(foldoutStates[index],
                $"{statusIcon} {typeName} - {info}", true);

            if (GUILayout.Button("X", GUILayout.Width(22)))
            {
                categoriesProp.DeleteArrayElementAtIndex(index);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                return;
            }
            EditorGUILayout.EndHorizontal();

            if (foldoutStates[index])
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(typeProp);
                EditorGUILayout.PropertyField(displayNameProp);
                EditorGUILayout.PropertyField(canToggleProp);
                EditorGUILayout.PropertyField(canChangeColorProp);
                if (canChangeColorProp != null && canChangeColorProp.boolValue)
                    EditorGUILayout.PropertyField(colorTargetProp);
                EditorGUILayout.PropertyField(renderersProp, true);

                if (spriteCount > 0)
                {
                    EditorGUILayout.Space(2);
                    DrawPreviewSection(index, renderersProp);
                }

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawPreviewSection(int catIndex, SerializedProperty renderersProp)
        {
            if (renderersProp == null || renderersProp.arraySize == 0) return;

            int maxCount = GetSpriteCount(renderersProp);
            if (maxCount == 0) return;

            // Pick the first renderer that actually has the full sprite list — used as the
            // representative for the thumbnail preview.
            SerializedProperty representativeSprites = null;
            for (int i = 0; i < renderersProp.arraySize; i++)
            {
                var sp = renderersProp.GetArrayElementAtIndex(i).FindPropertyRelative("sprites");
                if (sp != null && sp.arraySize == maxCount) { representativeSprites = sp; break; }
            }

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Preview", GUILayout.Width(50));

            bool isActive = previewCategoryIndex == catIndex;
            int currentIdx = isActive ? previewSpriteIndex : 0;

            EditorGUI.BeginChangeCheck();
            int newIdx = EditorGUILayout.IntSlider(currentIdx, 0, maxCount - 1);
            if (EditorGUI.EndChangeCheck())
            {
                previewCategoryIndex = catIndex;
                previewSpriteIndex = newIdx;
                ApplyPreviewInEditor(renderersProp, newIdx);
            }

            if (GUILayout.Button("Apply", GUILayout.Width(50)))
            {
                previewCategoryIndex = catIndex;
                previewSpriteIndex = newIdx;
                ApplyPreviewInEditor(renderersProp, newIdx);
            }

            EditorGUILayout.EndHorizontal();

            if (isActive && representativeSprites != null
                && previewSpriteIndex >= 0 && previewSpriteIndex < representativeSprites.arraySize)
            {
                var spriteProp = representativeSprites.GetArrayElementAtIndex(previewSpriteIndex);
                Sprite sprite = spriteProp.objectReferenceValue as Sprite;
                if (sprite != null)
                {
                    Rect previewRect = GUILayoutUtility.GetRect(64, 64, GUILayout.ExpandWidth(false));
                    EditorGUI.DrawPreviewTexture(previewRect, AssetPreview.GetAssetPreview(sprite) ?? sprite.texture);
                }
            }
        }

        private void ApplyPreviewInEditor(SerializedProperty renderersProp, int spriteIndex)
        {
            for (int r = 0; r < renderersProp.arraySize; r++)
            {
                var rendererElement = renderersProp.GetArrayElementAtIndex(r);
                var srProp = rendererElement.FindPropertyRelative("renderer");
                var spritesProp = rendererElement.FindPropertyRelative("sprites");

                if (srProp == null || spritesProp == null || spritesProp.arraySize == 0) continue;

                SpriteRenderer sr = srProp.objectReferenceValue as SpriteRenderer;
                if (sr == null) continue;

                // Match runtime ApplySprites: clamp short sprite lists (e.g. static
                // Bow_Line_*, Crossbow_Line_*) to index 0 so they always show their decoration.
                int idx = spriteIndex < 0 ? 0 : (spriteIndex < spritesProp.arraySize ? spriteIndex : 0);

                Sprite sprite = spritesProp.GetArrayElementAtIndex(idx).objectReferenceValue as Sprite;
                if (sprite != null)
                {
                    Undo.RecordObject(sr, "Preview Parts");
                    sr.sprite = sprite;
                    EditorUtility.SetDirty(sr);
                }
            }

            SceneView.RepaintAll();
        }

        private int GetSpriteCount(SerializedProperty renderersProp)
        {
            if (renderersProp == null || renderersProp.arraySize == 0) return 0;

            // Match PartsCategory.SpriteCount — take the max across all sub-renderers
            // so static decoration renderers (Bow_Line_*, Crossbow_Line_*) don't mask the real count.
            int max = 0;
            for (int i = 0; i < renderersProp.arraySize; i++)
            {
                var spritesProp = renderersProp.GetArrayElementAtIndex(i).FindPropertyRelative("sprites");
                if (spritesProp != null && spritesProp.arraySize > max)
                    max = spritesProp.arraySize;
            }
            return max;
        }

        private void AutoSetupRenderers()
        {
            PartsManager pm = (PartsManager)target;
            Transform root = pm.transform;
            Undo.RecordObject(pm, "Auto Setup Renderers");

            var allRenderers = root.GetComponentsInChildren<SpriteRenderer>(true);
            var partsTypes = Enum.GetValues(typeof(PartsType)).Cast<PartsType>().ToArray();

            categoriesProp.arraySize = partsTypes.Length;

            for (int i = 0; i < partsTypes.Length; i++)
            {
                PartsType pType = partsTypes[i];
                var element = categoriesProp.GetArrayElementAtIndex(i);

                element.FindPropertyRelative("type").enumValueIndex = (int)pType;
                element.FindPropertyRelative("displayName").stringValue = pType.ToString();
                element.FindPropertyRelative("canToggle").boolValue = pType != PartsType.Eye && pType != PartsType.Skin;
                element.FindPropertyRelative("isCommon").boolValue = CommonPartsTypes.Contains(pType);

                bool hasColor = pType == PartsType.Hair || pType == PartsType.Beard
                    || pType == PartsType.HelmetHair || pType == PartsType.Skin;
                element.FindPropertyRelative("canChangeColor").boolValue = hasColor;

                if (hasColor)
                {
                    ColorTargetType ct = pType switch
                    {
                        PartsType.Eye => ColorTargetType.Eye,
                        PartsType.Beard => ColorTargetType.Beard,
                        PartsType.Skin => ColorTargetType.Skin,
                        _ => ColorTargetType.Hair
                    };
                    element.FindPropertyRelative("colorTarget").enumValueIndex = (int)ct;
                }

                if (!RendererNameMap.TryGetValue(pType, out string searchName)) continue;

                var matchedRenderers = new List<SpriteRenderer>();
                foreach (var sr in allRenderers)
                {
                    if (MatchRendererName(sr.gameObject.name, searchName, pType))
                        matchedRenderers.Add(sr);
                }

                var rendProp = element.FindPropertyRelative("renderers");

                if (matchedRenderers.Count > 0)
                {
                    rendProp.arraySize = matchedRenderers.Count;
                    for (int r = 0; r < matchedRenderers.Count; r++)
                    {
                        var rendElement = rendProp.GetArrayElementAtIndex(r);
                        rendElement.FindPropertyRelative("renderer").objectReferenceValue = matchedRenderers[r];

                        var spritesProp = rendElement.FindPropertyRelative("sprites");
                        spritesProp.arraySize = 0;
                    }
                }
                else
                {
                    rendProp.arraySize = 0;
                }
            }

            // Auto-map Animator
            if (animatorProp.objectReferenceValue == null)
            {
                Animator anim = root.GetComponent<Animator>();
                if (anim == null)
                    anim = root.GetComponentInChildren<Animator>(true);
                if (anim != null)
                    animatorProp.objectReferenceValue = anim;
            }

            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(pm);
            AutoMapColors();
        }

        private bool MatchRendererName(string objName, string searchName, PartsType pType)
        {
            if (objName == searchName) return true;

            if (pType == PartsType.Bow)
                return objName == "Bow_Line_Up" || objName == "Bow_Line_Down";

            if (pType == PartsType.Crossbow)
                return objName == "Crossbow_Down" || objName == "Crossbow_Line_Up" || objName == "Crossbow_Line_Down";

            if (pType == PartsType.Arrow)
                return objName == "Bolt";

            if (pType == PartsType.Mount) return objName == "Mount_Down";

            if (pType == PartsType.Skin)
                return objName == "Body" || objName == "Head";

            return false;
        }

        private void AutoMapColors()
        {
            PartsManager pm = (PartsManager)target;
            Transform root = pm.transform;
            Undo.RecordObject(pm, "Auto Map Colors");

            var allRenderers = root.GetComponentsInChildren<SpriteRenderer>(true);
            var skinList = new List<SpriteRenderer>();
            var hairList = new List<SpriteRenderer>();
            var eyeList = new List<SpriteRenderer>();
            var beardList = new List<SpriteRenderer>();

            foreach (var sr in allRenderers)
            {
                string name = sr.gameObject.name;

                if (name == "Body" || name == "Head")
                    skinList.Add(sr);
                else if (name == "Hair" || name == "Hair_Helmet")
                    hairList.Add(sr);
                else if (name == "Eye")
                    eyeList.Add(sr);
                else if (name == "Beard")
                    beardList.Add(sr);
            }

            SetRendererArray(skinRenderersProp, skinList);
            SetRendererArray(hairRenderersProp, hairList);
            SetRendererArray(eyeRenderersProp, eyeList);
            SetRendererArray(beardRenderersProp, beardList);

            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(pm);
        }

        private void SetRendererArray(SerializedProperty arrayProp, List<SpriteRenderer> renderers)
        {
            arrayProp.arraySize = renderers.Count;
            for (int i = 0; i < renderers.Count; i++)
                arrayProp.GetArrayElementAtIndex(i).objectReferenceValue = renderers[i];
        }

        private void LoadSpritesFromFolder()
        {
            if (!AssetDatabase.IsValidFolder(spriteFolderPath))
            {
                EditorUtility.DisplayDialog("Error", $"Folder not found:\n{spriteFolderPath}", "OK");
                return;
            }

            PartsManager pm = (PartsManager)target;
            Undo.RecordObject(pm, "Load Sprites");

            // Recursively scan from the base folder, regardless of how many pack/grouping
            // levels are in between (e.g. "Extenstions/Parts Pack Vol.1/Parts/Chest" or just
            // "Parts Demo Pack/Parts/Chest"). PartsFolderMap-matching folders are collected,
            // "Thumbnail" folders are skipped, and any other intermediate folder is descended into.
            var spritesByType = new Dictionary<PartsType, List<Sprite>>();
            ScanPartsFolder(spriteFolderPath, spritesByType);

            for (int i = 0; i < categoriesProp.arraySize; i++)
            {
                var catElement = categoriesProp.GetArrayElementAtIndex(i);
                var typeProp = catElement.FindPropertyRelative("type");
                if (typeProp == null) continue;

                PartsType pType = (PartsType)typeProp.enumValueIndex;

                var rendProp = catElement.FindPropertyRelative("renderers");
                if (rendProp == null || rendProp.arraySize == 0) continue;

                Sprite[] allSprites = spritesByType.TryGetValue(pType, out var list)
                    ? list.ToArray()
                    : Array.Empty<Sprite>();

                for (int r = 0; r < rendProp.arraySize; r++)
                {
                    var rendElement = rendProp.GetArrayElementAtIndex(r);
                    var srProp = rendElement.FindPropertyRelative("renderer");
                    var spritesProp = rendElement.FindPropertyRelative("sprites");

                    string rendererName = "";
                    if (srProp?.objectReferenceValue is SpriteRenderer sr)
                        rendererName = sr.gameObject.name;

                    Sprite[] filtered = FilterSpritesForRenderer(pType, rendererName, allSprites);

                    spritesProp.arraySize = filtered.Length;
                    for (int s = 0; s < filtered.Length; s++)
                        spritesProp.GetArrayElementAtIndex(s).objectReferenceValue = filtered[s];
                }
            }

            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(pm);
        }

        private void ScanPartsFolder(string folderPath, Dictionary<PartsType, List<Sprite>> spritesByType)
        {
            foreach (string subFolder in AssetDatabase.GetSubFolders(folderPath))
            {
                string folderName = Path.GetFileName(subFolder);

                // Thumbnail folders are handled by LoadThumbnails, never as part sprites.
                if (string.Equals(folderName, THUMBNAIL_SUBFOLDER, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (folderName == "Tool")
                {
                    foreach (var sprite in LoadSpritesFromPath(subFolder))
                    {
                        PartsType tool;
                        if (sprite.name.IndexOf("Tool_Fishing_", StringComparison.OrdinalIgnoreCase) >= 0) tool = PartsType.Fishing;
                        else if (sprite.name.IndexOf("Tool_Pick_", StringComparison.OrdinalIgnoreCase) >= 0) tool = PartsType.Pickaxe;
                        else continue;
                        if (!spritesByType.TryGetValue(tool, out var items)) spritesByType[tool] = items = new List<Sprite>();
                        items.Add(sprite);
                    }
                    continue;
                }

                if (PartsFolderMap.TryGetValue(folderName, out PartsType partsType))
                {
                    Sprite[] sprites = LoadSpritesFromPath(subFolder);
                    if (sprites.Length == 0) continue;

                    if (!spritesByType.ContainsKey(partsType))
                        spritesByType[partsType] = new List<Sprite>();

                    spritesByType[partsType].AddRange(sprites);
                }
                else
                {
                    // Recursively descend into any other intermediate folder
                    // (Pack, Parts, Character, HandRight, HandLeft, ...).
                    ScanPartsFolder(subFolder, spritesByType);
                }
            }
        }

        private Sprite[] FilterSpritesForRenderer(PartsType type, string rendererName, Sprite[] allSprites)
        {
            if (type == PartsType.Mount)
                return PairCrossbowBodyAndDown(allSprites, rendererName == "Mount_Down");
            if (type != PartsType.Crossbow && type != PartsType.Bow)
                return allSprites;

            var filtered = new List<Sprite>();
            foreach (var sprite in allSprites)
            {
                string name = sprite.name;
                bool isLine = name.Contains("Line");

                if (type == PartsType.Bow)
                {
                    if (rendererName == "Bow_Line_Up")
                    {
                        if (isLine && name.Contains("Up"))
                            filtered.Add(sprite);
                    }
                    else if (rendererName == "Bow_Line_Down")
                    {
                        if (isLine && name.Contains("Down"))
                            filtered.Add(sprite);
                    }
                    else
                    {
                        if (!isLine)
                            filtered.Add(sprite);
                    }
                }
                else if (type == PartsType.Crossbow)
                {
                    if (rendererName == "Crossbow_Line_Up")
                    {
                        if (isLine && name.Contains("Up"))
                            filtered.Add(sprite);
                    }
                    else if (rendererName == "Crossbow_Line_Down")
                    {
                        if (isLine && name.Contains("Down"))
                            filtered.Add(sprite);
                    }
                    // Crossbow body / Crossbow_Down are handled by PairCrossbowBodyAndDown
                    // below, not by this per-sprite loop.
                }
            }

            // For Crossbow body / Crossbow_Down sub-renderers, pair sprites by base name
            // (".._ColorName" body ↔ ".._ColorName_Down" variant) so a missing or misnamed
            // _Down file does not silently shift every subsequent index.
            if (type == PartsType.Crossbow
                && (rendererName == "Crossbow" || rendererName == "Crossbow_Down"))
            {
                return PairCrossbowBodyAndDown(allSprites, wantDown: rendererName == "Crossbow_Down");
            }

            return filtered.ToArray();
        }

        private static Sprite[] PairCrossbowBodyAndDown(Sprite[] allSprites, bool wantDown)
        {
            // Group every sprite by its base name (with the "_Down" suffix stripped),
            // ignoring static "Line" decoration sprites. The first time a base name is
            // seen we record its order so the resulting body/down arrays share the same
            // index for the same color.
            var bodyByBase = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
            var downByBase = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
            var order = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var sprite in allSprites)
            {
                string name = sprite.name;
                if (name.Contains("Line")) continue;

                bool isDown = name.EndsWith("_Down", StringComparison.OrdinalIgnoreCase);
                string baseKey = isDown ? name.Substring(0, name.Length - "_Down".Length) : name;

                if (seen.Add(baseKey)) order.Add(baseKey);

                if (isDown) downByBase[baseKey] = sprite;
                else        bodyByBase[baseKey] = sprite;
            }

            var result = new List<Sprite>(order.Count);
            foreach (var key in order)
            {
                bodyByBase.TryGetValue(key, out var body);
                downByBase.TryGetValue(key, out var down);

                // Skip orphan entries entirely so the two arrays stay in lock-step:
                // a base name with no body AND no down (impossible by construction) is dropped,
                // a base name missing one side falls back to the other side so both arrays
                // are the same length and the user notices the duplicated visual.
                Sprite chosen = wantDown ? (down ?? body) : (body ?? down);
                if (chosen != null) result.Add(chosen);
            }
            return result.ToArray();
        }

        private Sprite[] LoadSpritesFromPath(string folderPath)
        {
            string[] guids = AssetDatabase.FindAssets("t:Sprite", new[] { folderPath });
            var sprites = new List<Sprite>();

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);

                var assets = AssetDatabase.LoadAllAssetsAtPath(path);
                foreach (var asset in assets)
                {
                    if (asset is Sprite sprite)
                        sprites.Add(sprite);
                }
            }

            // Sort by base name (stripping the "_Down" suffix) and place body before _Down variant
            // for the same base. This keeps a body sprite and its matching "_Down" variant aligned
            // at the same index across the two sub-renderers (e.g. Crossbow / Crossbow_Down)
            // even when alphabetic order would otherwise interleave them
            // (e.g. "Dark" / "DarkGreen" / "DarkGreen_Down" / "Dark_Down").
            sprites.Sort((a, b) =>
            {
                string aKey = StripDownSuffix(a.name);
                string bKey = StripDownSuffix(b.name);
                int cmp = string.Compare(aKey, bKey, StringComparison.OrdinalIgnoreCase);
                if (cmp != 0) return cmp;
                bool aDown = a.name.EndsWith("_Down", StringComparison.OrdinalIgnoreCase);
                bool bDown = b.name.EndsWith("_Down", StringComparison.OrdinalIgnoreCase);
                return aDown.CompareTo(bDown);
            });
            return sprites.ToArray();
        }

        private static string StripDownSuffix(string name)
        {
            const string suffix = "_Down";
            return name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)
                ? name.Substring(0, name.Length - suffix.Length)
                : name;
        }

        private void LoadThumbnails()
        {
            if (!AssetDatabase.IsValidFolder(spriteFolderPath))
            {
                EditorUtility.DisplayDialog("Error", $"Folder not found:\n{spriteFolderPath}", "OK");
                return;
            }

            PartsManager pm = (PartsManager)target;
            Undo.RecordObject(pm, "Load Thumbnails");

            // Recursively scan from the base folder for any "Thumbnail" folder, regardless of how
            // many pack/grouping levels are in between. Inside a Thumbnail folder, each PartsType
            // sub-folder is matched via ThumbnailFolderMap.
            var thumbsByType = new Dictionary<PartsType, List<Sprite>>();
            ScanThumbnailFolder(spriteFolderPath, thumbsByType);

            for (int i = 0; i < categoriesProp.arraySize; i++)
            {
                var catElement = categoriesProp.GetArrayElementAtIndex(i);
                var typeProp = catElement.FindPropertyRelative("type");
                if (typeProp == null) continue;

                PartsType pType = (PartsType)typeProp.enumValueIndex;
                if (!thumbsByType.TryGetValue(pType, out var list)) continue;

                var thumbnailsProp = catElement.FindPropertyRelative("thumbnails");
                thumbnailsProp.arraySize = list.Count;
                for (int s = 0; s < list.Count; s++)
                    thumbnailsProp.GetArrayElementAtIndex(s).objectReferenceValue = list[s];
            }

            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(pm);
        }

        private void ScanThumbnailFolder(string folderPath, Dictionary<PartsType, List<Sprite>> thumbsByType)
        {
            foreach (string subFolder in AssetDatabase.GetSubFolders(folderPath))
            {
                string folderName = Path.GetFileName(subFolder);

                if (string.Equals(folderName, THUMBNAIL_SUBFOLDER, StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var kvp in ThumbnailFolderMap)
                    {
                        string thumbPath = $"{subFolder}/{kvp.Value}";
                        if (!AssetDatabase.IsValidFolder(thumbPath)) continue;

                        Sprite[] sprites = LoadSpritesFromPath(thumbPath);
                        if (sprites.Length == 0) continue;

                        if (!thumbsByType.ContainsKey(kvp.Key))
                            thumbsByType[kvp.Key] = new List<Sprite>();
                        thumbsByType[kvp.Key].AddRange(sprites);
                    }
                }
                else if (!PartsFolderMap.ContainsKey(folderName))
                {
                    // PartsType leaf folders never contain a Thumbnail child; skip descending into them.
                    ScanThumbnailFolder(subFolder, thumbsByType);
                }
            }
        }
    }
}
