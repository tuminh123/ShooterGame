using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

[CustomEditor(typeof(CharacterDataSO))]
public sealed class CharacterVisualEditor : Editor
{
    private readonly BoxBoundsHandle hitboxHandle = new BoxBoundsHandle();
    private readonly BoxBoundsHandle barHandle = new BoxBoundsHandle();
    private GameObject previewRoot;
    private GameObject previewVisual;
    private GameObject previewPrefab;
    private RectTransform previewVitals;
    private BarView previewBar;
    private int tool;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.Space();
        EditorGUILayout.HelpBox("Scale/Offset chỉ đổi hình ảnh. Hitbox và thanh máu có kích thước riêng. " +
            "Mở preview rồi kéo các tay nắm trong Scene; thay đổi được ghi vào asset và hỗ trợ Undo.", MessageType.Info);

        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
        {
            if (GUILayout.Button(previewRoot == null ? "Căn chỉnh visual trong Scene" : "Đóng preview"))
            {
                if (previewRoot == null) OpenPreview();
                else ClosePreview();
            }
        }
        if (previewRoot == null) return;
        tool = GUILayout.Toolbar(tool, new[] { "Visual", "Hitbox", "Thanh máu" });
        if (GUILayout.Button("Khớp hitbox với hình ảnh")) FitHitbox();
        UpdatePreview();
        SceneView.RepaintAll();
    }

    private void OpenPreview()
    {
        var settings = ((CharacterDataSO)target).VisualSettings;
        if (settings.Prefab == null) return;
        previewRoot = new GameObject("Character Layout Preview") { hideFlags = HideFlags.HideAndDontSave };
        previewRoot.SetActive(false);
        CreateVisual(settings.Prefab);

        // Clone riêng Canvas đã thiết kế, không tạo CharacterView/gameplay trong chế độ chỉnh sửa.
        var shell = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Character.prefab");
        var shellUI = shell != null ? shell.GetComponent<CharacterUI>() : null;
        if (shellUI != null && shellUI.VitalsRoot != null)
        {
            var canvas = Instantiate(shellUI.VitalsRoot.gameObject, previewRoot.transform);
            canvas.hideFlags = HideFlags.HideAndDontSave;
            canvas.SetActive(true);
            previewVitals = (RectTransform)canvas.transform;
            previewBar = canvas.GetComponentInChildren<BarView>(true);
        }
        UpdatePreview();
        previewRoot.SetActive(true);
        SceneView.duringSceneGui += DrawScene;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        var scene = SceneView.lastActiveSceneView;
        if (scene != null)
        {
            scene.in2DMode = true;
            var bounds = GetVisualBounds();
            bounds.Encapsulate(settings.HealthBarOffset);
            bounds.Expand(Mathf.Max(bounds.size.x, bounds.size.y) * 0.4f + 0.1f);
            scene.Frame(bounds, false);
            scene.Focus();
        }
    }

    private void CreateVisual(GameObject prefab)
    {
        if (previewVisual != null) DestroyImmediate(previewVisual);
        previewPrefab = prefab;
        if (prefab == null) return;
        // Root đang tắt: bỏ scripts trên bản sao trước khi bật, tránh chạy gameplay của visual prefab.
        bool active = previewRoot.activeSelf;
        previewRoot.SetActive(false);
        previewVisual = Instantiate(prefab, previewRoot.transform);
        previewVisual.hideFlags = HideFlags.HideAndDontSave;
        foreach (var script in previewVisual.GetComponentsInChildren<MonoBehaviour>(true))
            if (script != null) DestroyImmediate(script);
        foreach (var animator in previewVisual.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
        foreach (var audio in previewVisual.GetComponentsInChildren<AudioSource>(true)) audio.enabled = false;
        previewVisual.SetActive(true);
        previewRoot.SetActive(active);
    }

    private void UpdatePreview()
    {
        if (previewRoot == null || target == null) return;
        var settings = ((CharacterDataSO)target).VisualSettings;
        if (previewPrefab != settings.Prefab) CreateVisual(settings.Prefab);
        if (previewVisual != null) settings.ApplyVisual(previewVisual.transform);
        if (previewVitals != null)
        {
            previewVitals.localPosition = settings.HealthBarOffset;
            previewVitals.localScale = Vector3.one * Mathf.Max(0.0001f, settings.HealthBarScale);
            previewVitals.sizeDelta = settings.HealthBarSize;
        }
        if (previewBar != null)
        {
            previewBar.ApplyLayout(settings.HealthBarSize, settings.ShieldThickness);
            previewBar.SetValue(1f, 1f);
            previewBar.SetShield(1f, 1f);
        }
    }

    private void DrawScene(SceneView scene)
    {
        if (previewRoot == null || target == null) return;
        var settings = ((CharacterDataSO)target).VisualSettings;
        using (new Handles.DrawingScope(previewRoot.transform.localToWorldMatrix))
        {
            Handles.color = Color.green;
            Handles.DrawWireCube(settings.Hitbox.Offset, settings.Hitbox.Size);
            Handles.color = Color.yellow;
            Handles.DrawWireCube(settings.HealthBarOffset, settings.HealthBarSize * settings.HealthBarScale);
            Handles.Label(settings.HealthBarOffset + Vector3.down * 0.03f, "Thanh máu + giáp");
            EditorGUI.BeginChangeCheck();
            if (tool == 0) DrawVisualHandles(settings);
            else if (tool == 1)
            {
                ConfigureBox(hitboxHandle, settings.Hitbox.Offset, settings.Hitbox.Size, Color.green);
                hitboxHandle.center = Handles.PositionHandle(hitboxHandle.center, Quaternion.identity);
                hitboxHandle.DrawHandle();
            }
            else if (tool == 2)
            {
                ConfigureBox(barHandle, settings.HealthBarOffset,
                    settings.HealthBarSize * settings.HealthBarScale, Color.yellow);
                barHandle.center = Handles.PositionHandle(barHandle.center, Quaternion.identity);
                barHandle.DrawHandle();
            }
            if (!EditorGUI.EndChangeCheck()) return;
            serializedObject.Update();
            var visual = serializedObject.FindProperty("visualSettings");
            if (tool == 1)
            {
                var hitbox = visual.FindPropertyRelative("Hitbox");
                hitbox.FindPropertyRelative("Offset").vector2Value = hitboxHandle.center;
                hitbox.FindPropertyRelative("Size").vector2Value = PositiveXY(hitboxHandle.size);
            }
            else if (tool == 2)
            {
                visual.FindPropertyRelative("HealthBarOffset").vector3Value = barHandle.center;
                visual.FindPropertyRelative("HealthBarSize").vector2Value =
                    PositiveXY(barHandle.size) / Mathf.Max(0.0001f, settings.HealthBarScale);
            }
            // Visual handles ghi riêng qua SerializedProperty để Undo hoạt động giống Inspector.
            if (tool != 0) serializedObject.ApplyModifiedProperties();
            UpdatePreview();
            Repaint();
        }
    }

    private void DrawVisualHandles(CharacterVisualSettings settings)
    {
        if (settings.Prefab == null) return;
        Vector3 origin = settings.Prefab.transform.localPosition;
        Vector3 position = origin + settings.Offset;
        Vector3 moved = Handles.PositionHandle(position, Quaternion.identity);
        Vector3 scaled = Handles.ScaleHandle(settings.Scale, position, Quaternion.identity,
            HandleUtility.GetHandleSize(position) * 0.65f);
        if (moved == position && scaled == settings.Scale) return;
        serializedObject.Update();
        var visual = serializedObject.FindProperty("visualSettings");
        visual.FindPropertyRelative("Offset").vector3Value = moved - origin;
        visual.FindPropertyRelative("Scale").vector3Value = new Vector3(
            Mathf.Max(0.001f, scaled.x), Mathf.Max(0.001f, scaled.y), Mathf.Max(0.001f, scaled.z));
        serializedObject.ApplyModifiedProperties();
    }

    private static void ConfigureBox(BoxBoundsHandle handle, Vector3 center, Vector2 size, Color color)
    {
        handle.axes = PrimitiveBoundsHandle.Axes.X | PrimitiveBoundsHandle.Axes.Y;
        handle.center = center;
        handle.size = new Vector3(size.x, size.y, 0f);
        handle.wireframeColor = color;
        handle.handleColor = color;
    }

    private Bounds GetVisualBounds()
    {
        var bounds = new Bounds(Vector3.zero, Vector3.zero);
        bool first = true;
        if (previewVisual == null) return bounds;
        foreach (var renderer in previewVisual.GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (renderer.sprite == null) continue;
            if (first) { bounds = renderer.bounds; first = false; }
            else bounds.Encapsulate(renderer.bounds);
        }
        return bounds;
    }

    private void FitHitbox()
    {
        var bounds = GetVisualBounds();
        if (bounds.size.x <= 0f || bounds.size.y <= 0f) return;
        serializedObject.Update();
        var hitbox = serializedObject.FindProperty("visualSettings").FindPropertyRelative("Hitbox");
        hitbox.FindPropertyRelative("Offset").vector2Value = bounds.center;
        hitbox.FindPropertyRelative("Size").vector2Value = bounds.size;
        serializedObject.ApplyModifiedProperties();
        SceneView.RepaintAll();
    }

    private static Vector2 PositiveXY(Vector3 value)
        => new Vector2(Mathf.Max(0.001f, value.x), Mathf.Max(0.001f, value.y));

    private void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode) ClosePreview();
    }

    private void OnDisable() => ClosePreview();

    private void ClosePreview()
    {
        SceneView.duringSceneGui -= DrawScene;
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        if (previewRoot != null) DestroyImmediate(previewRoot);
        previewRoot = null;
        previewVisual = null;
        previewPrefab = null;
        previewVitals = null;
        previewBar = null;
        SceneView.RepaintAll();
    }
}
