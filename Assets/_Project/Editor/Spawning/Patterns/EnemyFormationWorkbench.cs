using System;
using UnityEditor;
using UnityEngine;

/// <summary>Công cụ chỉnh hình/Path và xem trước bằng marker, không sinh quái thật.</summary>
public sealed class EnemyFormationWorkbench : EditorWindow
{
    #region Dữ liệu chọn và trạng thái preview

    [SerializeField] private EnemyPatternSO pattern;
    [SerializeField] private BossSpawnDataSO bossSpawn;
    [SerializeField] private EnemyFormationSO shape;
    [SerializeField] private EnemyPathSO path;
    private int tab;
    private float elapsed;
    private bool playing;
    private double lastTime;
    private Vector2 previewPlayer;
    private Vector2[] slots = Array.Empty<Vector2>();
    private EnemyPathRoute route;
    private string error;

    #endregion

    #region Mở cửa sổ và nạp asset

    /// <summary>Mở cửa sổ từ menu và đọc asset đang được chọn.</summary>
    [MenuItem("Tools/Shooter Game/Formation & Path Workbench")]
    private static void OpenMenu()
    {
        var window = GetWindow<EnemyFormationWorkbench>("Enemy Pattern");
        if (Selection.activeObject is EnemyPatternSO selected) window.pattern = selected;
        if (Selection.activeObject is EnemyFormationSO formation) { window.shape = formation; window.tab = 0; }
        if (Selection.activeObject is EnemyPathSO selectedPath) { window.path = selectedPath; window.tab = 1; }
        window.LoadPattern();
        window.Show();
    }

    /// <summary>Mở Workbench cho Pattern được truyền vào và chuyển sang tab xem trước.</summary>
    public static EnemyFormationWorkbench Open(EnemyPatternSO data)
    {
        var window = GetWindow<EnemyFormationWorkbench>("Enemy Pattern");
        window.pattern = data; window.tab = 2; window.LoadPattern(); window.Show();
        return window;
    }

    /// <summary>Lấy hình/Path từ Pattern và đặt lại thời gian cùng bộ đệm preview.</summary>
    private void LoadPattern()
    {
        if (pattern != null) { shape = pattern.formation; path = pattern.type == EnemyPatternType.Area ? null : pattern.path; }
        elapsed = 0f; playing = false; Rebuild();
    }
    #endregion

    #region Vòng đời cửa sổ và đồng hồ

    /// <summary>Đăng ký vẽ Scene và cập nhật preview khi cửa sổ được bật.</summary>
    private void OnEnable() { SceneView.duringSceneGui += DrawScene; EditorApplication.update += UpdatePreview; }
    /// <summary>Gỡ callback Scene/Editor khi cửa sổ ngừng hoạt động.</summary>
    private void OnDisable() { SceneView.duringSceneGui -= DrawScene; EditorApplication.update -= UpdatePreview; }
    /// <summary>Tiến thời gian preview bằng đồng hồ Editor; không điều khiển Play mode.</summary>
    private void UpdatePreview()
    {
        double now = EditorApplication.timeSinceStartup;
        if (playing && !EditorApplication.isPlaying)
        {
            elapsed += (float)(now - lastTime);
            Repaint(); SceneView.RepaintAll();
        }
        lastTime = now;
    }

    #endregion

    #region Giao diện và chỉnh asset

    /// <summary>Vẽ các tab Pattern/Boss, preview và chỉnh asset bằng Undo.</summary>
    private void OnGUI()
    {
        var selected = (EnemyPatternSO)EditorGUILayout.ObjectField("Pattern", pattern, typeof(EnemyPatternSO), false);
        if (selected != pattern) { pattern = selected; LoadPattern(); }
        tab = GUILayout.Toolbar(tab, new[] { "Formation / Area", "Path", "Test", "Boss" });
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            playing = false; EditorGUILayout.HelpBox("Dừng Play mode trước khi chỉnh hoặc preview.", MessageType.Info); return;
        }
        if (tab == 0)
        {
            shape = (EnemyFormationSO)EditorGUILayout.ObjectField("Shape", shape, typeof(EnemyFormationSO), false);
            DrawAsset(shape);
        }
        else if (tab == 1)
        {
            if (pattern != null && pattern.type == EnemyPatternType.Area)
                EditorGUILayout.HelpBox("Area bám tâm player và thu nhỏ, không sử dụng Path.", MessageType.Info);
            else
            {
                path = (EnemyPathSO)EditorGUILayout.ObjectField("Path", path, typeof(EnemyPathSO), false);
                DrawAsset(path);
            }
        }
        else if (tab == 2)
        {
            EditorGUILayout.HelpBox("Kéo marker player trong Scene để xem Area bám tâm. Preview chỉ hiển thị hình học.", MessageType.Info);
            previewPlayer = EditorGUILayout.Vector2Field("Player", previewPlayer);
            elapsed = Mathf.Max(0f, EditorGUILayout.FloatField("Elapsed", elapsed));
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(playing ? "Pause" : "Preview")) { playing = !playing; lastTime = EditorApplication.timeSinceStartup; }
                if (GUILayout.Button("Reset")) { elapsed = 0f; playing = false; }
            }
        }
        else
        {
            bossSpawn = (BossSpawnDataSO)EditorGUILayout.ObjectField("Boss Spawn Data", bossSpawn, typeof(BossSpawnDataSO), false);
            if (bossSpawn == null)
                EditorGUILayout.HelpBox("Chọn BossSpawnDataSO để xem trước kích thước đấu trường.", MessageType.Info);
            else
            {
                DrawAsset(bossSpawn);
                previewPlayer = EditorGUILayout.Vector2Field("Arena Center / Player", previewPlayer);
                EditorGUILayout.HelpBox("Viền đỏ là kích thước đấu trường. Kéo marker trong Scene để đổi tâm xem trước.", MessageType.Info);
            }
        }
        if (GUILayout.Button("Rebuild / Frame"))
        {
            Rebuild();
            if (SceneView.lastActiveSceneView != null)
                SceneView.lastActiveSceneView.Frame(new Bounds(new Vector3(previewPlayer.x, previewPlayer.y, 0f), Vector3.one * 15f), false);
        }
        if (pattern != null && GUILayout.Button("Apply Shape / Path to Pattern"))
        {
            Undo.RecordObject(pattern, "Assign pattern geometry");
            pattern.formation = shape;
            if (pattern.type != EnemyPatternType.Area) pattern.path = path;
            EditorUtility.SetDirty(pattern); Rebuild();
        }
        if (!string.IsNullOrEmpty(error)) EditorGUILayout.HelpBox(error, MessageType.Error);
    }

    /// <summary>Vẽ các trường của asset, lưu thay đổi serialized rồi dựng lại hình học.</summary>
    private void DrawAsset(UnityEngine.Object asset)
    {
        if (asset == null) return;
        var data = new SerializedObject(asset);
        data.Update();
        var field = data.GetIterator();
        bool children = true;
        EditorGUI.BeginChangeCheck();
        while (field.NextVisible(children))
        {
            children = false;
            if (field.name != "m_Script") EditorGUILayout.PropertyField(field, true);
        }
        if (EditorGUI.EndChangeCheck()) { data.ApplyModifiedProperties(); Rebuild(); }
    }

    #endregion

    #region Dựng hình học xem trước

    /// <summary>Lấy tâm XY của Main Camera, hoặc gốc nếu chưa có camera.</summary>
    private Vector2 CameraPosition => Camera.main != null ? (Vector2)Camera.main.transform.position : Vector2.zero;

    /// <summary>Kiểm tra asset và dựng cache slot/route; giữ thông báo lỗi để hiển thị.</summary>
    private void Rebuild()
    {
        error = null; slots = Array.Empty<Vector2>(); route = null;
        try
        {
            if (pattern != null) pattern.Validate();
            slots = shape == null ? Array.Empty<Vector2>() :
                pattern != null && pattern.type == EnemyPatternType.Area ? EnemyAreaBuilder.Build(shape, pattern.enemyCount) : FormationBuilder.Build(shape);
            if (path != null && (pattern == null || pattern.type != EnemyPatternType.Area))
                route = new EnemyPathRoute(EnemyPathBuilder.Build(path, CameraPosition));
        }
        catch (Exception e) { error = e.Message; }
        SceneView.RepaintAll();
    }

    #endregion

    #region Vẽ và tương tác Scene

    /// <summary>Vẽ đường, slot và marker; cho kéo điểm Custom, waypoint hoặc player minh họa.</summary>
    private void DrawScene(SceneView scene)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (tab == 0 && shape != null && shape.Shape == FormationType.Custom)
        {
            var data = new SerializedObject(shape);
            var points = data.FindProperty("customPoints");
            for (int i = 0; i < points.arraySize; i++)
            {
                var point = points.GetArrayElementAtIndex(i);
                Vector2 local = FormationBuilder.Rotate(point.vector2Value * shape.CustomScale, shape.Rotation);
                EditorGUI.BeginChangeCheck();
                Vector3 moved = Handles.PositionHandle(CameraPosition + local, Quaternion.identity);
                if (EditorGUI.EndChangeCheck())
                {
                    point.vector2Value = FormationBuilder.Rotate((Vector2)moved - CameraPosition, -shape.Rotation) / shape.CustomScale;
                    data.ApplyModifiedProperties(); Rebuild();
                }
            }
        }
        if (tab == 1 && path != null && (pattern == null || pattern.type != EnemyPatternType.Area))
        {
            var data = new SerializedObject(path);
            var points = data.FindProperty("waypoints");
            Vector2 center = path.GetCenter(CameraPosition);
            for (int i = 0; i < points.arraySize; i++)
            {
                var point = points.GetArrayElementAtIndex(i);
                EditorGUI.BeginChangeCheck();
                Vector3 moved = Handles.PositionHandle(center + point.vector2Value, Quaternion.identity);
                if (EditorGUI.EndChangeCheck())
                {
                    point.vector2Value = (Vector2)moved - center;
                    data.ApplyModifiedProperties(); Rebuild();
                }
                Handles.Label(center + point.vector2Value, i == 0 ? "A" : i == points.arraySize - 1 ? "B" : i.ToString());
            }
        }
        if (route != null)
        {
            Handles.color = Color.cyan;
            for (int i = 1; i < route.Points.Count; i++) Handles.DrawLine(route.Points[i-1], route.Points[i]);
        }
        if (tab == 2 || tab == 3)
        {
            EditorGUI.BeginChangeCheck();
            Vector3 moved = Handles.PositionHandle(previewPlayer, Quaternion.identity);
            if (EditorGUI.EndChangeCheck()) { previewPlayer = moved; Repaint(); }
            Handles.Label(previewPlayer, "Player");
        }
        if (tab == 3 && bossSpawn != null)
        {
            Vector2 half = bossSpawn.arenaSize * 0.5f;
            Vector3 a = previewPlayer + new Vector2(-half.x, -half.y);
            Vector3 b = previewPlayer + new Vector2(-half.x, half.y);
            Vector3 c = previewPlayer + new Vector2(half.x, half.y);
            Vector3 d = previewPlayer + new Vector2(half.x, -half.y);
            Handles.color = Color.red;
            Handles.DrawLine(a, b); Handles.DrawLine(b, c); Handles.DrawLine(c, d); Handles.DrawLine(d, a);
            Handles.Label(c, $"Boss Arena {bossSpawn.arenaSize.x:0.##} × {bossSpawn.arenaSize.y:0.##}");
        }
        Handles.color = Color.yellow;
        if (pattern != null && pattern.type == EnemyPatternType.SequentialPath && route != null)
        {
            for (int i = 0; i < pattern.enemyCount; i++)
            {
                float age = elapsed - i * pattern.spawnInterval;
                if (age >= 0f && age * pattern.speed < route.Length * pattern.passes)
                    Marker(route.Sample(age * pattern.speed, pattern.passes));
            }
        }
        else foreach (Vector2 slot in slots)
        {
            if (pattern != null && pattern.type == EnemyPatternType.Area)
            {
                if (elapsed < pattern.shrinkDuration) Marker(previewPlayer + slot * (1f - elapsed / pattern.shrinkDuration));
            }
            else if (route != null)
            {
                float distance = pattern != null ? elapsed * pattern.speed : 0f;
                int passes = pattern != null ? pattern.passes : 1;
                if (distance < route.Length * passes) Marker(route.Sample(distance, passes) + slot);
            }
            else Marker(CameraPosition + slot);
        }
    }
    /// <summary>Vẽ một vòng tròn nhỏ tại vị trí xem trước của enemy.</summary>
    private static void Marker(Vector2 position) => Handles.DrawWireDisc(position, Vector3.forward, 0.09f);
    #endregion
}
