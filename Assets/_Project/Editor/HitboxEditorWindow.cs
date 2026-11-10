using UnityEditor;
using UnityEngine;
using System;
using System.Linq;
using Object = UnityEngine.Object;

// Cửa sổ chỉnh hitbox nhân vật và cấu hình hitbox riêng cho từng BulletType trong catalog.
public sealed class HitboxEditorWindow : EditorWindow
{
    [SerializeField] private int tab;
    [SerializeField] private CharacterDataSO characterAsset;
    [SerializeField] private string filter = "";
    private CharacterVisualEditor characterEditor;
    private CharacterDataSO[] characters = Array.Empty<CharacterDataSO>();
    private Vector2 scroll;
    [SerializeField] private BulletType bulletType;
    [SerializeField] private CombatAssetsSO combatAssets;
    private GameObject previewRoot;
    private GameObject previewVisual;
    private GameObject previewPrefab;

    [MenuItem("Tools/Shooter Game/Hitbox Editor")]
    public static void Open() => GetWindow<HitboxEditorWindow>("Hitbox Editor").Show();

    private void OnEnable()
    {
        ReloadAssets();
        SceneView.duringSceneGui += DrawScene;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        Undo.undoRedoPerformed += Refresh;
    }

    private void OnGUI()
    {
        int nextTab = GUILayout.Toolbar(tab, new[] { "Nhân vật", "Đạn" });
        if (nextTab != tab)
        {
            ClosePreview();
            CloseCharacterEditor();
            tab = nextTab;
            filter = "";
        }
        EditorGUILayout.BeginHorizontal();
        filter = EditorGUILayout.TextField("Tìm tên", filter);
        if (GUILayout.Button("Làm mới", GUILayout.Width(75))) ReloadAssets();
        EditorGUILayout.EndHorizontal();
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorGUILayout.HelpBox("Thoát Play Mode để chỉnh hitbox.", MessageType.Info);
            return;
        }
        scroll = EditorGUILayout.BeginScrollView(scroll);
        if (tab == 0) DrawCharacters();
        else DrawProjectiles();
        EditorGUILayout.EndScrollView();
        if (GUILayout.Button("Lưu thay đổi")) AssetDatabase.SaveAssets();
    }

    private void ReloadAssets()
    {
        characters = LoadAssets<CharacterDataSO>();
        if (combatAssets == null)
        {
            CombatAssetsSO[] catalogs = LoadAssets<CombatAssetsSO>();
            if (catalogs.Length == 1) combatAssets = catalogs[0];
        }
    }

    private static T[] LoadAssets<T>() where T : Object
        => AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { "Assets" })
            .Select(guid => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid)))
            .Where(asset => asset != null).OrderBy(asset => AssetDatabase.GetAssetPath(asset)).ToArray();

    private T PickAsset<T>(string label, T current, T[] assets) where T : Object
    {
        T[] visible = assets.Where(asset => asset != null &&
            asset.name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0).ToArray();
        if (visible.Length == 0)
        {
            EditorGUILayout.HelpBox("Không có dữ liệu khớp tên tìm kiếm.", MessageType.Info);
            return null;
        }
        int index = Array.IndexOf(visible, current);
        int selected = EditorGUILayout.Popup(label, Mathf.Max(0, index),
            visible.Select(asset => AssetDatabase.GetAssetPath(asset).Replace("Assets/_Project/", "")).ToArray());
        return visible[selected];
    }

    private void DrawCharacters()
    {
        CharacterDataSO selected = PickAsset("Nhân vật", characterAsset, characters);
        if (selected != characterAsset) { CloseCharacterEditor(); characterAsset = selected; }
        if (characterAsset == null) return;
        if (characterEditor == null)
        {
            characterEditor = (CharacterVisualEditor)UnityEditor.Editor.CreateEditor(characterAsset, typeof(CharacterVisualEditor));
            characterEditor.OpenHitboxPreview();
        }
        characterEditor.OnInspectorGUI();
    }

    private void CloseCharacterEditor()
    {
        if (characterEditor != null) DestroyImmediate(characterEditor);
        characterEditor = null;
    }

    private void DrawProjectiles()
    {
        var selectedCatalog = (CombatAssetsSO)EditorGUILayout.ObjectField("Combat Assets", combatAssets, typeof(CombatAssetsSO), false);
        if (selectedCatalog != combatAssets)
        {
            ClosePreview();
            GUI.FocusControl(null);
            EditorGUIUtility.editingTextField = false;
            combatAssets = selectedCatalog;
        }
        if (combatAssets == null)
        {
            ClosePreview();
            EditorGUILayout.HelpBox("Chọn Combat Assets đang dùng trong game.", MessageType.Info);
            return;
        }
        BulletType[] types = combatAssets.BulletEntries.Select(entry => entry.type).Distinct()
            .Where(type => type.ToString().IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0).ToArray();
        if (types.Length == 0)
        {
            ClosePreview();
            EditorGUILayout.HelpBox("Không có loại đạn khớp tên tìm kiếm.", MessageType.Info);
            return;
        }
        int index = EditorGUILayout.Popup("Loại đạn", Mathf.Max(0, Array.IndexOf(types, bulletType)), types.Select(type => type.ToString()).ToArray());
        if (bulletType != types[index])
        {
            GUI.FocusControl(null);
            EditorGUIUtility.editingTextField = false;
            ClosePreview();
            bulletType = types[index];
        }
        DrawProjectileSettings();
    }

    private void DrawProjectileSettings()
    {
        EditorGUILayout.HelpBox("Kéo mũi tên để dịch tâm, kéo tay nắm vòng tròn để chỉnh bán kính. " +
            "Hit Offset xoay cùng hướng bay: X phía trước, Y bên trái; không theo scale hình ảnh. " +
            "Mỗi loại đạn có hitbox riêng. Tất cả skill bắn loại đạn đang chọn dùng chung cấu hình này. Hỗ trợ Undo/Redo.", MessageType.Info);
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorGUILayout.HelpBox("Thoát Play Mode để chỉnh hitbox.", MessageType.Info);
            return;
        }
        if (!TryGetBullet(out SerializedObject data, out SerializedProperty shooting))
        {
            ClosePreview();
            EditorGUILayout.HelpBox("Chọn lại loại đạn hoặc bấm Làm mới nếu catalog đã thay đổi.", MessageType.Warning);
            return;
        }
        using (new EditorGUI.DisabledScope(true))
            EditorGUILayout.PropertyField(shooting.FindPropertyRelative("visualPrefab"));
        SerializedProperty offset = shooting.FindPropertyRelative("hitOffset");
        EditorGUI.BeginChangeCheck();
        Vector2 movedOffset = EditorGUILayout.Vector2Field("Hit Offset", offset.vector2Value);
        if (EditorGUI.EndChangeCheck() && IsValid(movedOffset.x) && IsValid(movedOffset.y))
            offset.vector2Value = movedOffset;
        SerializedProperty radius = shooting.FindPropertyRelative("hitRadius");
        EditorGUI.BeginChangeCheck();
        float value = EditorGUILayout.FloatField("Hit Radius", radius.floatValue);
        if (EditorGUI.EndChangeCheck() && IsValid(value)) radius.floatValue = Mathf.Max(0.001f, value);
        data.ApplyModifiedProperties();
        UpdatePreview(shooting);
        if (previewVisual == null)
            EditorGUILayout.HelpBox("Chọn Combat Assets có visual prefab tương ứng với Bullet Type.", MessageType.Warning);
        if (GUILayout.Button("Đưa preview vào khung nhìn")) FramePreview(radius.floatValue);
        using (new EditorGUI.DisabledScope(previewVisual == null))
            if (GUILayout.Button("Khớp vòng tròn bao quanh hình ảnh")) FitRadius();
        SceneView.RepaintAll();
    }

    private bool TryGetBullet(out SerializedObject data, out SerializedProperty shooting)
    {
        data = null;
        shooting = null;
        if (combatAssets == null) return false;
        data = new SerializedObject(combatAssets);
        data.Update();
        SerializedProperty bullets = data.FindProperty("bullets");
        for (int index = 0; index < bullets.arraySize; index++)
        {
            SerializedProperty entry = bullets.GetArrayElementAtIndex(index);
            if (entry.FindPropertyRelative("type").intValue != (int)bulletType) continue;
            shooting = entry;
            return true;
        }
        return false;
    }

    private void UpdatePreview(SerializedProperty shooting)
    {
        GameObject prefab = shooting.FindPropertyRelative("visualPrefab").objectReferenceValue as GameObject;
        if (previewRoot != null && previewPrefab == prefab) return;
        ClosePreview();
        if (prefab == null) return;
        previewRoot = new GameObject("Projectile Hitbox Preview") { hideFlags = HideFlags.HideAndDontSave };
        previewRoot.SetActive(false);
        previewPrefab = prefab;
        previewVisual = Instantiate(prefab, previewRoot.transform);
        previewVisual.hideFlags = HideFlags.HideAndDontSave;
        // Chỉ hiển thị hình ảnh; không chạy gameplay, particle hay audio trên bản sao.
        foreach (var script in previewVisual.GetComponentsInChildren<MonoBehaviour>(true))
            if (script != null) DestroyImmediate(script);
        foreach (var animator in previewVisual.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
        foreach (var audio in previewVisual.GetComponentsInChildren<AudioSource>(true)) audio.enabled = false;
        foreach (var particle in previewVisual.GetComponentsInChildren<ParticleSystem>(true))
        {
            var settings = particle.main;
            settings.playOnAwake = false;
            settings.stopAction = ParticleSystemStopAction.None;
            particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
        previewVisual.SetActive(true);
        previewRoot.SetActive(true);
        FramePreview(shooting.FindPropertyRelative("hitRadius").floatValue);
    }

    private void DrawScene(SceneView scene)
    {
        if (tab != 1 || EditorApplication.isPlayingOrWillChangePlaymode || previewRoot == null) return;
        if (!TryGetBullet(out SerializedObject data, out SerializedProperty shooting)) { ClosePreview(); return; }
        SerializedProperty radius = shooting.FindPropertyRelative("hitRadius");
        SerializedProperty offset = shooting.FindPropertyRelative("hitOffset");
        Vector3 center = offset.vector2Value;
        using (new Handles.DrawingScope(Color.green))
        {
            Handles.DrawWireDisc(center, Vector3.forward, radius.floatValue);
            Handles.Label(center + Vector3.up * radius.floatValue, $"Hit Radius: {radius.floatValue:0.###}");
            EditorGUI.BeginChangeCheck();
            Vector3 moved = Handles.PositionHandle(center, Quaternion.identity);
            float value = Handles.RadiusHandle(Quaternion.identity, moved, radius.floatValue, true);
            if (EditorGUI.EndChangeCheck() && IsValid(value) && IsValid(moved.x) && IsValid(moved.y))
            {
                offset.vector2Value = new Vector2(moved.x, moved.y);
                radius.floatValue = Mathf.Max(0.001f, value);
                data.ApplyModifiedProperties();
                Repaint();
            }
        }
    }

    private Bounds VisualBounds()
    {
        var bounds = new Bounds(Vector3.zero, Vector3.zero);
        bool first = true;
        if (previewVisual != null)
            foreach (var renderer in previewVisual.GetComponentsInChildren<SpriteRenderer>())
                if (renderer.sprite != null)
                {
                    if (first) { bounds = renderer.bounds; first = false; }
                    else bounds.Encapsulate(renderer.bounds);
                }
        return bounds;
    }

    private void FitRadius()
    {
        if (!TryGetBullet(out SerializedObject data, out SerializedProperty shooting)) return;
        Bounds bounds = VisualBounds();
        if (bounds.size.sqrMagnitude <= 0f) return;
        shooting.FindPropertyRelative("hitOffset").vector2Value = bounds.center;
        shooting.FindPropertyRelative("hitRadius").floatValue = Mathf.Max(0.001f, ((Vector2)bounds.extents).magnitude);
        data.ApplyModifiedProperties();
        Refresh();
    }

    private void FramePreview(float radius)
    {
        SceneView scene = SceneView.lastActiveSceneView;
        if (scene == null) scene = GetWindow<SceneView>();
        Bounds bounds = VisualBounds();
        Vector3 center = Vector3.zero;
        if (TryGetBullet(out _, out SerializedProperty shooting))
            center = shooting.FindPropertyRelative("hitOffset").vector2Value;
        bounds.Encapsulate(new Bounds(center, Vector3.one * Mathf.Max(0.01f, radius * 2f)));
        bounds.Expand(Mathf.Max(bounds.size.x, bounds.size.y) * 0.4f);
        scene.in2DMode = true;
        scene.Frame(bounds, false);
    }

    private static bool IsValid(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    private void Refresh() { Repaint(); SceneView.RepaintAll(); }
    private void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode) { ClosePreview(); CloseCharacterEditor(); }
    }
    private void ClosePreview()
    {
        if (previewRoot != null) DestroyImmediate(previewRoot);
        previewRoot = previewVisual = previewPrefab = null;
        SceneView.RepaintAll();
    }
    private void OnDisable()
    {
        SceneView.duringSceneGui -= DrawScene;
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        Undo.undoRedoPerformed -= Refresh;
        ClosePreview();
        CloseCharacterEditor();
    }
}
