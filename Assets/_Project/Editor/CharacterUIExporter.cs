using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Tool xuất nhân vật cho UI từ <see cref="CharacterDatabaseSO"/>: (1) ảnh chân dung PNG nền trong suốt cho thẻ Collection,
/// (2) prefab <see cref="CharacterUIVisual"/> chia bộ phận như prefab gameplay nhưng dùng Image, đứng Idle cho màn Detail.
/// </summary>
/// <remarks>
/// Chỉ đọc kiểu có sẵn của Unity (Transform, SpriteRenderer, Animator) nên không phụ thuộc pack tạo nhân vật;
/// pack chỉ dùng để thiết kế ngoại hình. Mọi thứ dựng trong một Preview Scene riêng, không đụng scene đang mở.
/// </remarks>
public sealed class CharacterUIExporter : EditorWindow
{
    #region Constants

    private const string PortraitFolder = "Assets/_Project/Graphic/Portraits";
    private const string PrefabFolder = "Assets/_Project/Prefabs/UI/Characters";
    private const string UIControllerPath = "Assets/_Project/Animations/UI/UI_CharacterController.controller";
    private const string IdleClipName = "Idle";
    private const string VictoryClipName = "Victory";
    private const string CelebrateTrigger = "Celebrate"; // Khớp CharacterUIVisual.Celebrate.
    private const int PortraitSize = 512;
    private const int PreviewSize = 256;
    private const string PrefsPrefix = "ShooterGame.CharacterUIExporter.";

    #endregion

    #region Window State

    private CharacterDatabaseSO database;
    private bool playersOnly = true;
    private int selectedIndex;
    private readonly List<CharacterDataSO> characters = new List<CharacterDataSO>();

    // Khung chụp ảnh chân dung, lưu theo từng nhân vật trong EditorPrefs (không phải dữ liệu game).
    // Mặc định toàn thân có lề: nhân vật chibi đầu to nên toàn thân đã đủ rõ mặt trên thẻ.
    private const float DefaultZoom = 1.1f;
    private float zoom = DefaultZoom;   // Phần chiều cao nhân vật lọt vào khung: 1 = vừa khít toàn thân.
    private Vector2 offset;             // Lệch tâm khung, tính theo chiều cao nhân vật.
    private float idleTime;         // Giây trong clip Idle dùng làm tư thế chụp.

    private Scene previewScene;
    private GameObject previewInstance;
    private CharacterDataSO previewFor;
    private Camera previewCamera;
    private RenderTexture previewTexture;
    private Bounds previewBounds;
    private string status;

    #endregion

    #region Window Lifecycle

    /// <summary>Mở tool từ menu.</summary>
    [MenuItem("Tools/Shooter Game/Character UI Exporter")]
    public static void Open() => GetWindow<CharacterUIExporter>("Character UI Exporter");

    private void OnEnable()
    {
        if (database == null)
        {
            string[] guids = AssetDatabase.FindAssets("t:CharacterDatabaseSO");
            if (guids.Length > 0) database = AssetDatabase.LoadAssetAtPath<CharacterDatabaseSO>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }
        RefreshCharacters();
    }

    private void OnDisable() => ClosePreview();

    #endregion

    #region GUI

    private void OnGUI()
    {
        EditorGUI.BeginChangeCheck();
        database = (CharacterDatabaseSO)EditorGUILayout.ObjectField("Character Database", database, typeof(CharacterDatabaseSO), false);
        playersOnly = EditorGUILayout.Toggle("Chỉ nhân vật Player", playersOnly);
        if (EditorGUI.EndChangeCheck()) RefreshCharacters();

        if (characters.Count == 0)
        {
            EditorGUILayout.HelpBox("Không có nhân vật nào có Visual Prefab trong database.", MessageType.Info);
            return;
        }

        string[] names = characters.ConvertAll(c => c.CharacterType + " — " + c.DisplayName).ToArray();
        selectedIndex = EditorGUILayout.Popup("Nhân vật", Mathf.Clamp(selectedIndex, 0, characters.Count - 1), names);
        CharacterDataSO character = characters[selectedIndex];
        if (previewFor != character) OpenPreview(character);
        if (previewInstance == null) return;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Khung ảnh chân dung (thẻ Collection)", EditorStyles.boldLabel);
        EditorGUI.BeginChangeCheck();
        zoom = EditorGUILayout.Slider("Zoom (1 = toàn thân)", zoom, 0.2f, 1.3f);
        offset.x = EditorGUILayout.Slider("Lệch ngang", offset.x, -0.5f, 0.5f);
        offset.y = EditorGUILayout.Slider("Lệch dọc", offset.y, -0.5f, 0.5f);
        AnimationClip idle = FindClip(previewInstance, IdleClipName);
        idleTime = EditorGUILayout.Slider("Frame Idle (giây)", idleTime, 0f, idle != null ? idle.length : 0f);
        if (EditorGUI.EndChangeCheck())
        {
            SaveFraming(character);
            RenderPreview();
        }

        Rect rect = GUILayoutUtility.GetRect(PreviewSize, PreviewSize, GUILayout.ExpandWidth(false));
        if (previewTexture != null) EditorGUI.DrawPreviewTexture(rect, previewTexture);

        EditorGUILayout.Space();
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Xuất ảnh")) Run(() => ExportPortrait(character));
            if (GUILayout.Button("Xuất prefab UI")) Run(() => ExportUIPrefab(character));
            if (GUILayout.Button("Xuất cả hai")) Run(() => { ExportPortrait(character); ExportUIPrefab(character); });
        }
        if (GUILayout.Button($"Xuất cả hai cho tất cả ({characters.Count} nhân vật)")) Run(ExportAll);
        if (!string.IsNullOrEmpty(status)) EditorGUILayout.HelpBox(status, MessageType.None);
    }

    // Gom lỗi của mọi thao tác xuất vào một chỗ để tool không để lại preview dở dang.
    private void Run(Action action)
    {
        try { action(); }
        catch (Exception exception)
        {
            status = "Lỗi: " + exception.Message;
            Debug.LogException(exception);
        }
        finally
        {
            AssetDatabase.SaveAssets();
            if (previewFor != null) OpenPreview(previewFor);
        }
    }

    private void RefreshCharacters()
    {
        characters.Clear();
        if (database == null) return;
        foreach (CharacterDataSO character in database.Characters)
            if (character != null && character.VisualPrefab != null && (!playersOnly || character.Team == CharacterTeam.Player))
                characters.Add(character);
    }

    #endregion

    #region Preview Scene

    // Dựng bản sao prefab ngoại hình trong scene ẩn, ép tư thế Idle; MonoBehaviour không chạy trong Edit Mode
    // nên sprite đã lưu sẵn trên prefab chính là ngoại hình được chụp.
    private void OpenPreview(CharacterDataSO character)
    {
        ClosePreview();
        previewFor = character;
        LoadFraming(character);
        previewScene = EditorSceneManager.NewPreviewScene();
        previewInstance = (GameObject)PrefabUtility.InstantiatePrefab(character.VisualPrefab, previewScene);
        previewInstance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

        var cameraObject = new GameObject("Portrait Camera");
        SceneManager.MoveGameObjectToScene(cameraObject, previewScene);
        previewCamera = cameraObject.AddComponent<Camera>();
        previewCamera.enabled = false;
        previewCamera.scene = previewScene;
        previewCamera.orthographic = true;
        previewCamera.clearFlags = CameraClearFlags.SolidColor;
        previewCamera.nearClipPlane = 0.01f;
        previewCamera.farClipPlane = 100f;
        previewTexture = new RenderTexture(PreviewSize, PreviewSize, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        RenderPreview();
    }

    private void ClosePreview()
    {
        if (previewInstance != null) DestroyImmediate(previewInstance);
        if (previewCamera != null) DestroyImmediate(previewCamera.gameObject);
        if (previewScene.IsValid()) EditorSceneManager.ClosePreviewScene(previewScene);
        if (previewTexture != null) previewTexture.Release();
        previewTexture = null;
        previewInstance = null;
        previewCamera = null;
        previewFor = null;
    }

    private void RenderPreview()
    {
        if (previewInstance == null) return;
        ApplyIdlePose(idleTime);
        FrameCamera();
        previewCamera.backgroundColor = new Color(0.18f, 0.18f, 0.2f, 1f);
        previewCamera.targetTexture = previewTexture;
        previewCamera.Render();
        previewCamera.targetTexture = null;
        Repaint();
    }

    private void ApplyIdlePose(float time)
    {
        AnimationClip idle = FindClip(previewInstance, IdleClipName);
        Animator animator = previewInstance.GetComponentInChildren<Animator>(true);
        if (idle != null && animator != null) idle.SampleAnimation(animator.gameObject, time);
        previewBounds = CalculateBounds(previewInstance);
    }

    private void FrameCamera()
    {
        float height = Mathf.Max(0.01f, previewBounds.size.y);
        Vector3 center = previewBounds.center + new Vector3(offset.x * height, offset.y * height, 0f);
        previewCamera.orthographicSize = height * zoom * 0.5f;
        previewCamera.transform.position = new Vector3(center.x, center.y, -10f);
    }

    #endregion

    #region Portrait Export

    private void ExportPortrait(CharacterDataSO character)
    {
        if (previewFor != character) OpenPreview(character);
        ApplyIdlePose(idleTime);
        FrameCamera();
        Texture2D portrait = CaptureTransparent(PortraitSize);
        try
        {
            EnsureFolder(PortraitFolder);
            string path = $"{PortraitFolder}/{character.CharacterType}_Portrait.png";
            File.WriteAllBytes(path, portrait.EncodeToPNG());
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
            // Tên file cố định: xuất lại ghi đè cùng GUID nên mọi reference giữ nguyên.
            AssignReference(character, "portrait", AssetDatabase.LoadAssetAtPath<Sprite>(path));
            status = $"Đã xuất ảnh: {path}";
        }
        finally
        {
            DestroyImmediate(portrait);
        }
    }

    // Chụp hai lần trên nền đen và trắng rồi suy ra alpha thật: chụp thẳng trên nền trong suốt sẽ bị viền tối
    // vì sprite blend cả kênh alpha. Tính trong không gian linear vì project dùng Linear color space.
    private Texture2D CaptureTransparent(int size)
    {
        Color[] black = Capture(size, Color.black);
        Color[] white = Capture(size, Color.white);
        var pixels = new Color[black.Length];
        for (int i = 0; i < pixels.Length; i++)
        {
            Color b = black[i].linear;
            Color w = white[i].linear;
            float alpha = Mathf.Clamp01(1f - ((w.r - b.r) + (w.g - b.g) + (w.b - b.b)) / 3f);
            pixels[i] = alpha <= 0.0001f
                ? Color.clear
                : new Color(Mathf.Clamp01(b.r / alpha), Mathf.Clamp01(b.g / alpha), Mathf.Clamp01(b.b / alpha), 1f).gamma;
            pixels[i].a = alpha;
        }
        var result = new Texture2D(size, size, TextureFormat.RGBA32, false);
        result.SetPixels(pixels);
        result.Apply();
        return result;
    }

    private Color[] Capture(int size, Color background)
    {
        var target = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        RenderTexture previous = RenderTexture.active;
        try
        {
            previewCamera.backgroundColor = background;
            previewCamera.targetTexture = target;
            previewCamera.Render();
            RenderTexture.active = target;
            texture.ReadPixels(new Rect(0, 0, size, size), 0, 0);
            texture.Apply();
            return texture.GetPixels();
        }
        finally
        {
            previewCamera.targetTexture = null;
            RenderTexture.active = previous;
            target.Release();
            DestroyImmediate(target);
            DestroyImmediate(texture);
        }
    }

    #endregion

    #region UI Prefab Export

    // Dựng Rig (xương rỗng cùng tên/đường dẫn để clip gốc chạy được) + Layers (Image xếp theo sortingOrder).
    private void ExportUIPrefab(CharacterDataSO character)
    {
        if (previewFor != character) OpenPreview(character);
        ApplyIdlePose(0f); // Tư thế nghỉ của prefab = frame đầu Idle.

        Animator sourceAnimator = previewInstance.GetComponentInChildren<Animator>(true);
        if (sourceAnimator == null) throw new InvalidOperationException($"{character.name}: Visual Prefab không có Animator.");
        RuntimeAnimatorController controller = EnsureUIController(sourceAnimator.runtimeAnimatorController);
        List<SpriteRenderer> renderers = CollectVisibleRenderers(previewInstance);
        if (renderers.Count == 0) throw new InvalidOperationException($"{character.name}: không có bộ phận nào đang hiện.");
        float pixelsPerUnit = renderers[0].sprite.pixelsPerUnit;

        var root = new GameObject($"UI_{character.CharacterType}", typeof(RectTransform));
        SceneManager.MoveGameObjectToScene(root, previewScene);
        try
        {
            // Animation tính bằng world unit, UI tính bằng pixel: phóng Content theo pixelsPerUnit của sprite.
            // Khung UI ôm vừa bounds nhân vật. Rig chép từ các node con nên bỏ qua scale ở gốc prefab gameplay
            // (scale đó do CharacterVisualSettings dùng trong trận), vì vậy quy đổi bounds về không gian của gốc.
            Vector3 rootScale = previewInstance.transform.lossyScale;
            var min = new Vector2(previewBounds.min.x / Mathf.Abs(rootScale.x), previewBounds.min.y / Mathf.Abs(rootScale.y));
            var size = new Vector2(previewBounds.size.x / Mathf.Abs(rootScale.x), previewBounds.size.y / Mathf.Abs(rootScale.y));
            // Pivot đặt tại gốc tọa độ của nhân vật (chân) tính theo tỉ lệ trong khung.
            var pivot = new Vector2(-min.x / size.x, -min.y / size.y);
            var rootRect = (RectTransform)root.transform;
            rootRect.pivot = pivot;
            rootRect.sizeDelta = size * pixelsPerUnit;

            RectTransform content = CreateRect("Content", root.transform);
            // Neo trùng pivot của gốc; neo giữa mặc định sẽ làm lệch nhân vật khỏi khung.
            content.anchorMin = content.anchorMax = pivot;
            content.localScale = Vector3.one * pixelsPerUnit;
            RectTransform rig = CreateRect("Rig", content);
            var copies = new Dictionary<Transform, Transform>();
            foreach (Transform child in previewInstance.transform) CopyBones(child, rig, copies);

            Transform uiAnimatorNode = copies[sourceAnimator.transform];
            Animator uiAnimator = uiAnimatorNode.gameObject.AddComponent<Animator>();
            uiAnimator.runtimeAnimatorController = controller;
            uiAnimator.applyRootMotion = false;
            uiAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            RectTransform layers = CreateRect("Layers", content);
            var bones = new Transform[renderers.Count];
            var parts = new RectTransform[renderers.Count];
            for (int i = 0; i < renderers.Count; i++)
            {
                bones[i] = copies[renderers[i].transform];
                parts[i] = CreateImage(renderers[i], layers);
            }

            var visual = root.AddComponent<CharacterUIVisual>();
            visual.SetupForExport(uiAnimator, layers, bones, parts);
            visual.SyncParts(); // Prefab đúng tư thế ngay trong Edit Mode.

            EnsureFolder(PrefabFolder);
            string path = $"{PrefabFolder}/UI_{character.CharacterType}.prefab";
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path, out bool success);
            if (!success || saved == null) throw new InvalidOperationException("Không lưu được prefab: " + path);
            AssignReference(character, "uiVisualPrefab", saved.GetComponent<CharacterUIVisual>());
            status = $"Đã xuất prefab UI: {path} ({renderers.Count} bộ phận).";
        }
        finally
        {
            DestroyImmediate(root);
        }
    }

    private static void CopyBones(Transform source, Transform parent, Dictionary<Transform, Transform> copies)
    {
        RectTransform copy = CreateRect(source.name, parent);
        copy.localPosition = source.localPosition;
        copy.localRotation = source.localRotation;
        copy.localScale = source.localScale;
        copy.gameObject.SetActive(source.gameObject.activeSelf);
        copies.Add(source, copy);
        foreach (Transform child in source) CopyBones(child, copy, copies);
    }

    private static RectTransform CreateImage(SpriteRenderer source, RectTransform parent)
    {
        RectTransform rect = CreateRect(source.name, parent);
        Sprite sprite = source.sprite;
        // Kích thước theo world unit (Content đã nhân pixelsPerUnit); pivot khớp pivot sprite để xoay đúng tâm.
        rect.sizeDelta = sprite.rect.size / sprite.pixelsPerUnit;
        rect.pivot = new Vector2(sprite.pivot.x / sprite.rect.width, sprite.pivot.y / sprite.rect.height);
        var image = rect.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.color = source.color;
        image.raycastTarget = false; // Không chặn click của nút trên màn Character.
        if (source.flipX || source.flipY)
            Debug.LogWarning($"[CharacterUIExporter] {source.name} dùng flipX/flipY; bản UI chưa hỗ trợ lật sprite.", source);
        return rect;
    }

    private static RectTransform CreateRect(string name, Transform parent)
    {
        var node = new GameObject(name, typeof(RectTransform));
        var rect = (RectTransform)node.transform;
        rect.SetParent(parent, false);
        rect.sizeDelta = Vector2.zero;
        return rect;
    }

    // Controller riêng cho UI: Idle mặc định, trigger Celebrate phát Victory một lần rồi về Idle.
    // Dùng lại clip gốc (không copy) nên sửa clip thì gameplay và UI cùng nhận.
    private static RuntimeAnimatorController EnsureUIController(RuntimeAnimatorController source)
    {
        var existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(UIControllerPath);
        if (existing != null) return existing;
        AnimationClip idle = FindClip(source, IdleClipName);
        AnimationClip victory = FindClip(source, VictoryClipName);
        if (idle == null || victory == null)
            throw new InvalidOperationException($"Controller '{source.name}' thiếu clip {IdleClipName} hoặc {VictoryClipName}.");
        WarnIfNotTransformOnly(idle);
        WarnIfNotTransformOnly(victory);

        EnsureFolder(Path.GetDirectoryName(UIControllerPath).Replace('\\', '/'));
        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(UIControllerPath);
        controller.AddParameter(CelebrateTrigger, AnimatorControllerParameterType.Trigger);
        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        AnimatorState idleState = machine.AddState(IdleClipName);
        idleState.motion = idle;
        AnimatorState victoryState = machine.AddState(VictoryClipName);
        victoryState.motion = victory;
        machine.defaultState = idleState;

        AnimatorStateTransition toVictory = idleState.AddTransition(victoryState);
        toVictory.hasExitTime = false;
        toVictory.duration = 0.1f;
        toVictory.AddCondition(AnimatorConditionMode.If, 0f, CelebrateTrigger);
        AnimatorStateTransition toIdle = victoryState.AddTransition(idleState);
        toIdle.hasExitTime = true;
        toIdle.exitTime = 1f;
        toIdle.duration = 0.15f;
        AssetDatabase.SaveAssets();
        return controller;
    }

    // Image không nhận curve của SpriteRenderer, và rig UI không có hàm nhận Animation Event.
    private static void WarnIfNotTransformOnly(AnimationClip clip)
    {
        if (clip.events.Length > 0)
            Debug.LogWarning($"[CharacterUIExporter] Clip {clip.name} có Animation Event; trên UI sẽ báo thiếu receiver.", clip);
        foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(clip))
            if (binding.type != typeof(Transform))
            {
                Debug.LogWarning($"[CharacterUIExporter] Clip {clip.name} đổi {binding.type.Name}.{binding.propertyName}; UI chỉ theo được Transform.", clip);
                return;
            }
    }

    #endregion

    #region Batch Export

    private void ExportAll()
    {
        int done = 0;
        foreach (CharacterDataSO character in characters)
        {
            EditorUtility.DisplayProgressBar("Character UI Exporter", character.name, (float)done / characters.Count);
            OpenPreview(character);
            ExportPortrait(character);
            ExportUIPrefab(character);
            done++;
        }
        EditorUtility.ClearProgressBar();
        status = $"Đã xuất {done} nhân vật.";
    }

    #endregion

    #region Helpers

    // Bộ phận đang hiện trong Idle: bật, có sprite, alpha > 0 và mọi node cha đang bật.
    // Bộ phận ẩn (xương lúc chết, mắt Stun...) không xuất để prefab UI nhẹ.
    private static List<SpriteRenderer> CollectVisibleRenderers(GameObject root)
    {
        var visible = new List<(SpriteRenderer renderer, int index)>();
        SpriteRenderer[] all = root.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < all.Length; i++)
            if (all[i].enabled && all[i].sprite != null && all[i].color.a > 0.001f && all[i].gameObject.activeInHierarchy)
                visible.Add((all[i], i));
        // UI vẽ theo thứ tự con: xếp theo sortingOrder, cùng order thì giữ thứ tự hierarchy (giống SortingGroup).
        visible.Sort((a, b) => a.renderer.sortingOrder != b.renderer.sortingOrder
            ? a.renderer.sortingOrder.CompareTo(b.renderer.sortingOrder)
            : a.index.CompareTo(b.index));
        return visible.ConvertAll(item => item.renderer);
    }

    private static Bounds CalculateBounds(GameObject root)
    {
        List<SpriteRenderer> renderers = CollectVisibleRenderers(root);
        if (renderers.Count == 0) return new Bounds(root.transform.position, Vector3.one);
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Count; i++) bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }

    private static AnimationClip FindClip(GameObject root, string clipName)
    {
        Animator animator = root != null ? root.GetComponentInChildren<Animator>(true) : null;
        return animator != null ? FindClip(animator.runtimeAnimatorController, clipName) : null;
    }

    private static AnimationClip FindClip(RuntimeAnimatorController controller, string clipName)
    {
        if (controller == null) return null;
        foreach (AnimationClip clip in controller.animationClips)
            if (clip != null && clip.name == clipName) return clip;
        return null;
    }

    private static void AssignReference(CharacterDataSO character, string field, UnityEngine.Object value)
    {
        var serialized = new SerializedObject(character);
        serialized.FindProperty(field).objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(character);
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    private void LoadFraming(CharacterDataSO character)
    {
        string key = PrefsPrefix + character.CharacterType;
        zoom = EditorPrefs.GetFloat(key + ".zoom", DefaultZoom);
        offset = new Vector2(EditorPrefs.GetFloat(key + ".offsetX", 0f), EditorPrefs.GetFloat(key + ".offsetY", 0f));
        idleTime = EditorPrefs.GetFloat(key + ".idleTime", 0f);
    }

    private void SaveFraming(CharacterDataSO character)
    {
        string key = PrefsPrefix + character.CharacterType;
        EditorPrefs.SetFloat(key + ".zoom", zoom);
        EditorPrefs.SetFloat(key + ".offsetX", offset.x);
        EditorPrefs.SetFloat(key + ".offsetY", offset.y);
        EditorPrefs.SetFloat(key + ".idleTime", idleTime);
    }

    #endregion
}
