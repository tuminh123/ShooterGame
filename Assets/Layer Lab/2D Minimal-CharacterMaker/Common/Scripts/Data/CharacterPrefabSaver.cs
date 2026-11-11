#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace LayerLab.ArtMakerUnity
{
    /// <summary>
    /// Editor-only utility for saving the current character as a prefab with thumbnail.
    /// </summary>
    public static class CharacterPrefabSaver
    {
        private const string PREFAB_SAVE_FOLDER = "Assets/CharacterPrefabs";

        /// <summary>
        /// Saves the current character state as a prefab asset with an optional thumbnail.
        /// </summary>
        /// <param name="partsManager">The PartsManager to capture state from.</param>
        /// <param name="thumbnailCameraSize">Orthographic camera size for thumbnail capture.</param>
        /// <param name="thumbnailCameraOffset">Camera offset from character position.</param>
        public static void Save(PartsManager partsManager, float thumbnailCameraSize, Vector3 thumbnailCameraOffset)
        {
            if (partsManager == null || partsManager.gameObject == null)
            {
                Debug.LogError("[SavePrefab] PartsManager not found.");
                return;
            }

            var presetItem = partsManager.ToPresetItem();

            var sourceObj = partsManager.gameObject;
            var prefabObj = Object.Instantiate(sourceObj);
            prefabObj.name = sourceObj.name + "_Prefab";

            CleanupComponents(prefabObj);

            var prefabData = prefabObj.AddComponent<CharacterPrefabData>();
            prefabData.SetData(presetItem);

            // Capture thumbnail
            prefabObj.SetActive(false);
            var map = GameObject.Find("Map");
            if (map != null) map.SetActive(false);

            var thumbnail = CaptureThumbnail(sourceObj, thumbnailCameraSize, thumbnailCameraOffset);

            if (map != null) map.SetActive(true);
            prefabObj.SetActive(true);

            // Ensure folder exists
            if (!AssetDatabase.IsValidFolder(PREFAB_SAVE_FOLDER))
                AssetDatabase.CreateFolder("Assets", "CharacterPrefabs");

            // Save prefab
            string timestamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string prefabPath = $"{PREFAB_SAVE_FOLDER}/Character_{timestamp}.prefab";

            var savedPrefab = PrefabUtility.SaveAsPrefabAsset(prefabObj, prefabPath, out var success);

            if (success && savedPrefab != null)
            {
                Debug.Log($"[SavePrefab] Prefab saved: {prefabPath}");
                SaveThumbnail(thumbnail, prefabPath);

                EditorUtility.SetDirty(savedPrefab);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                Selection.activeObject = savedPrefab;
                EditorGUIUtility.PingObject(savedPrefab);
            }
            else
            {
                Debug.LogError("[SavePrefab] Failed to save prefab!");
            }

            Object.Destroy(prefabObj);
            if (thumbnail != null) Object.Destroy(thumbnail);
        }

        private static void CleanupComponents(GameObject prefabObj)
        {
            var player = prefabObj.GetComponent<Player>();
            if (player != null) Object.Destroy(player);

            var rb = prefabObj.GetComponent<Rigidbody2D>();
            if (rb != null) Object.Destroy(rb);

            var cols = prefabObj.GetComponents<Collider2D>();
            foreach (var col in cols) Object.Destroy(col);
        }

        private static Texture2D CaptureThumbnail(GameObject characterObj, float cameraSize, Vector3 cameraOffset)
        {
            // Calculate character bounds center for proper framing
            var renderers = characterObj.GetComponentsInChildren<SpriteRenderer>(false);
            Vector3 center = characterObj.transform.position;
            if (renderers.Length > 0)
            {
                var bounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++)
                    bounds.Encapsulate(renderers[i].bounds);
                center = bounds.center;
            }

            var camObj = new GameObject("ThumbnailCamera");
            var cam = camObj.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0, 0, 0, 0);
            cam.orthographic = true;
            cam.orthographicSize = cameraSize;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 1000f;
            cam.transform.position = new Vector3(center.x, center.y, cameraOffset.z);
            // Only render Default layer (character), exclude background/map
            cam.cullingMask = 1 << 0;

            const int resolution = 512;
            var rt = new RenderTexture(resolution, resolution, 24, RenderTextureFormat.ARGB32);
            rt.antiAliasing = 4;
            cam.targetTexture = rt;
            cam.Render();

            RenderTexture.active = rt;
            var tex = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, resolution, resolution), 0, 0);
            tex.Apply();
            RenderTexture.active = null;

            Object.Destroy(camObj);
            Object.Destroy(rt);

            return tex;
        }

        private static void SaveThumbnail(Texture2D thumbnail, string prefabPath)
        {
            if (thumbnail == null) return;

            string imagePath = prefabPath.Replace(".prefab", "_Thumbnail.png");
            var pngData = thumbnail.EncodeToPNG();
            System.IO.File.WriteAllBytes(imagePath, pngData);
            AssetDatabase.ImportAsset(imagePath);

            var importer = AssetImporter.GetAtPath(imagePath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }

            Debug.Log($"[SavePrefab] Thumbnail saved: {imagePath}");
        }
    }
}
#endif
