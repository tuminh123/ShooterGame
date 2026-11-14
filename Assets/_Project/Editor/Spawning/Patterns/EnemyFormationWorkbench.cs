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
    [SerializeField] private int tab;
    [SerializeField] private float elapsed;
    private bool playing;
    private double lastTime;
    [SerializeField] private Vector2 previewPlayerOffset;
    private Vector2 scroll;
    private Vector2 lastCameraPosition;
    private bool cameraPoseCached;
    private EnemySpawnManager previewSpawner;
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
        if (Selection.activeObject is EnemyPatternSO selected) { Open(selected); return; }
        if (Selection.activeObject is EnemyFormationSO formation) { Open(formation); return; }
        if (Selection.activeObject is EnemyPathSO selectedPath) { Open(selectedPath); return; }
        window.LoadPattern();
        window.Show();
        window.FramePreview();
    }

    /// <summary>Mở Workbench cho Pattern được truyền vào và chuyển sang tab xem trước.</summary>
    public static EnemyFormationWorkbench Open(EnemyPatternSO data)
    {
        var window = GetWindow<EnemyFormationWorkbench>("Enemy Pattern");
        window.pattern = data; window.tab = 2; window.LoadPattern(); window.Show();
        window.FramePreview();
        return window;
    }

    public static EnemyFormationWorkbench Open(EnemyFormationSO data)
    {
        var window = GetWindow<EnemyFormationWorkbench>("Enemy Pattern");
        // Bỏ Pattern cũ để LoadPattern không ghi đè hình vừa được chọn.
        window.pattern = null; window.shape = data; window.path = null; window.tab = 0;
        window.LoadPattern(); window.Show(); window.FramePreview();
        return window;
    }

    public static EnemyFormationWorkbench Open(EnemyPathSO data)
    {
        var window = GetWindow<EnemyFormationWorkbench>("Enemy Pattern");
        window.pattern = null; window.shape = null; window.path = data; window.tab = 1;
        window.LoadPattern(); window.Show(); window.FramePreview();
        return window;
    }

    /// <summary>Lấy hình/Path từ Pattern và đặt lại thời gian cùng bộ đệm preview.</summary>
    private void LoadPattern()
    {
        if (pattern != null) { shape = pattern.formation; path = pattern.type == EnemyPatternType.Area ? null : pattern.path; }
        elapsed = 0f; playing = false; previewPlayerOffset = Vector2.zero; Rebuild();
    }
    #endregion

    #region Vòng đời cửa sổ và đồng hồ

    /// <summary>Đăng ký vẽ Scene và cập nhật preview khi cửa sổ được bật.</summary>
    private void OnEnable()
    {
        minSize = new Vector2(360f, 420f);
        SceneView.duringSceneGui += DrawScene;
        EditorApplication.update += UpdatePreview;
        // Asset được giữ qua domain reload, nhưng slot/route runtime phải được dựng lại.
        EditorApplication.delayCall += RestorePreview;
    }
    private void RestorePreview() { Rebuild(); Repaint(); }
    /// <summary>Gỡ callback Scene/Editor khi cửa sổ ngừng hoạt động.</summary>
    private void OnDisable()
    {
        SceneView.duringSceneGui -= DrawScene;
        EditorApplication.update -= UpdatePreview;
        EditorApplication.delayCall -= RestorePreview;
    }
    /// <summary>Tiến thời gian preview bằng đồng hồ Editor; không điều khiển Play mode.</summary>
    private void UpdatePreview()
    {
        double now = EditorApplication.timeSinceStartup;
        if (!EditorApplication.isPlayingOrWillChangePlaymode && (!cameraPoseCached || CameraPosition != lastCameraPosition))
        {
            Rebuild(); FramePreview(); Repaint();
        }
        if (playing && !EditorApplication.isPlaying)
        {
            elapsed += (float)(now - lastTime);
            float duration = PreviewDuration;
            if (duration > 0f && elapsed >= duration) { elapsed = duration; playing = false; }
            Repaint(); SceneView.RepaintAll();
        }
        lastTime = now;
    }

    #endregion

    #region Giao diện và chỉnh asset

    /// <summary>Vẽ các tab Pattern/Boss, preview và chỉnh asset bằng Undo.</summary>
    private void OnGUI()
    {
        using (var view = new EditorGUILayout.ScrollViewScope(scroll))
        {
            scroll = view.scrollPosition;
            DrawWindow();
        }
    }

    private void DrawWindow()
    {
        var selected = (EnemyPatternSO)EditorGUILayout.ObjectField("Pattern", pattern, typeof(EnemyPatternSO), false);
        if (selected != pattern) { pattern = selected; LoadPattern(); FramePreview(); }
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
            EditorGUILayout.HelpBox("Preview hiển thị trong SCENE VIEW, không phải Game View. Nút Preview tự căn camera về hình; kéo marker Player để thử Area bám tâm. Không sinh quái thật.", MessageType.Info);
            DrawTestSettings();
            EditorGUILayout.LabelField("Camera Anchor", PreviewCamera != null ? PreviewCamera.name : "Không có camera — dùng gốc tọa độ");
            previewPlayerOffset = EditorGUILayout.Vector2Field("Player Offset from Camera", previewPlayerOffset);
            if (GUILayout.Button("Center Player on Camera")) { previewPlayerOffset = Vector2.zero; FramePreview(); }
            elapsed = Mathf.Max(0f, EditorGUILayout.FloatField("Elapsed", elapsed));
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(pattern == null || !string.IsNullOrEmpty(error)))
                    if (GUILayout.Button(playing ? "Pause" : "Preview"))
                    {
                        if (!playing)
                        {
                            if (PreviewDuration > 0f && elapsed >= PreviewDuration) elapsed = 0f;
                            Rebuild(); FramePreview();
                        }
                        playing = !playing; lastTime = EditorApplication.timeSinceStartup;
                    }
                if (GUILayout.Button("Reset")) { elapsed = 0f; playing = false; Rebuild(); FramePreview(); }
            }
            if (PreviewDuration > 0f && elapsed >= PreviewDuration)
                EditorGUILayout.HelpBox("Preview đã kết thúc nên marker được ẩn. Nhấn Reset hoặc Preview để xem lại.", MessageType.Info);
        }
        else
        {
            bossSpawn = (BossSpawnDataSO)EditorGUILayout.ObjectField("Boss Spawn Data", bossSpawn, typeof(BossSpawnDataSO), false);
            if (bossSpawn == null)
                EditorGUILayout.HelpBox("Chọn BossSpawnDataSO để xem trước kích thước đấu trường.", MessageType.Info);
            else
            {
                DrawAsset(bossSpawn);
                previewPlayerOffset = EditorGUILayout.Vector2Field("Arena Offset from Camera", previewPlayerOffset);
                EditorGUILayout.HelpBox("Viền đỏ là kích thước đấu trường. Kéo marker trong Scene để đổi tâm xem trước.", MessageType.Info);
            }
        }
        if (GUILayout.Button("Rebuild / Frame in Scene")) { Rebuild(); FramePreview(); }
        if (pattern != null && GUILayout.Button("Apply Shape / Path to Pattern"))
        {
            Undo.RecordObject(pattern, "Assign pattern geometry");
            pattern.formation = shape;
            if (pattern.type != EnemyPatternType.Area) pattern.path = path;
            EditorUtility.SetDirty(pattern); Rebuild();
        }
        if (!string.IsNullOrEmpty(error)) EditorGUILayout.HelpBox(error, MessageType.Error);
    }

    // Cho chỉnh các thông số mô phỏng ngay trong tab Test; vẫn dùng serialized Undo của asset.
    private void DrawTestSettings()
    {
        if (pattern == null) { EditorGUILayout.HelpBox("Chọn EnemyPatternSO để thử Formation, Path hoặc Area.", MessageType.Info); return; }
        EditorGUILayout.LabelField("Preview Type", pattern.type.ToString());
        var data = new SerializedObject(pattern);
        data.Update();
        EditorGUI.BeginChangeCheck();
        if (pattern.type == EnemyPatternType.Area)
        {
            EditorGUILayout.PropertyField(data.FindProperty("areaShrinkMode"));
            EditorGUILayout.PropertyField(data.FindProperty("shrinkDuration"));
            if (data.FindProperty("areaShrinkMode").enumValueIndex == (int)AreaShrinkMode.Burst)
            {
                EditorGUILayout.PropertyField(data.FindProperty("shrinkBurstDuration"));
                EditorGUILayout.PropertyField(data.FindProperty("shrinkPauseDuration"));
            }
        }
        else
        {
            EditorGUILayout.PropertyField(data.FindProperty("speed"));
            EditorGUILayout.PropertyField(data.FindProperty("passes"));
            if (pattern.type == EnemyPatternType.SequentialPath)
            {
                EditorGUILayout.PropertyField(data.FindProperty("spawnInterval"));
            }
        }
        if (EditorGUI.EndChangeCheck()) { data.ApplyModifiedProperties(); Rebuild(); }
        EditorGUILayout.LabelField("Geometry Slots / Enemies", slots.Length.ToString());
        if (pattern.type == EnemyPatternType.Area && string.IsNullOrEmpty(error))
        {
            var timing = pattern.ShrinkTiming;
            EditorGUILayout.LabelField("Total Area Duration", timing.TotalDuration.ToString("0.##") + " s");
            EditorGUILayout.LabelField("Current Phase", elapsed >= timing.TotalDuration ? "Đã thu hết" :
                timing.IsResting(elapsed) ? "Đang nghỉ — giữ bán kính" : "Đang thu");
        }
    }

    /// <summary>Vẽ các trường của asset, lưu thay đổi serialized rồi dựng lại hình học.</summary>
    private void DrawAsset(UnityEngine.Object asset)
    {
        if (asset == null) return;
        var data = new SerializedObject(asset);
        data.Update();
        EditorGUI.BeginChangeCheck();
        bool geometry = asset is EnemyFormationSO;
        if (geometry) DataInspectorGUI.FormationShapeFields(data);
        var field = data.GetIterator();
        bool children = true;
        while (field.NextVisible(children))
        {
            children = false;
            if (geometry && (field.name == "shape" || field.name == "fillMode")) continue;
            if (field.name != "m_Script") EditorGUILayout.PropertyField(field, true);
        }
        if (EditorGUI.EndChangeCheck()) { data.ApplyModifiedProperties(); Rebuild(); }
    }

    #endregion

    #region Dựng hình học xem trước

    // Dùng cùng camera với hệ spawn; Scene View chỉ là nơi vẽ, không phải mốc gameplay.
    private Camera PreviewCamera
    {
        get
        {
            if (previewSpawner == null) previewSpawner = FindObjectOfType<EnemySpawnManager>();
            return previewSpawner != null ? previewSpawner.FormationCamera : Camera.main;
        }
    }
    private Vector2 CameraPosition => PreviewCamera != null ? (Vector2)PreviewCamera.transform.position : Vector2.zero;
    private Vector2 PreviewPlayer
    {
        get => CameraPosition + previewPlayerOffset;
        set => previewPlayerOffset = value - CameraPosition;
    }

    private Bounds CameraPreviewBounds
    {
        get
        {
            var bounds = new Bounds(CameraPosition, Vector3.zero);
            var camera = PreviewCamera;
            if (camera == null) return new Bounds(Vector3.zero, new Vector3(6f, 6f, 0f));
            var plane = new Plane(Vector3.forward, Vector3.zero);
            foreach (var corner in new[] { Vector2.zero, Vector2.up, Vector2.one, Vector2.right })
            {
                Ray ray = camera.ViewportPointToRay(corner);
                if (plane.Raycast(ray, out float distance)) bounds.Encapsulate(ray.GetPoint(distance));
            }
            return bounds;
        }
    }

    private float PreviewDuration
    {
        get
        {
            if (pattern == null) return 0f;
            if (pattern.type == EnemyPatternType.Area) return pattern.ShrinkTiming.TotalDuration;
            if (route == null || pattern.speed <= 0f) return 0f;
            float duration = route.Length * pattern.passes / pattern.speed;
            if (pattern.type == EnemyPatternType.SequentialPath)
                duration += Mathf.Max(0, slots.Length - 1) * pattern.spawnInterval;
            return duration;
        }
    }

    /// <summary>Kiểm tra asset và dựng cache slot/route; giữ thông báo lỗi để hiển thị.</summary>
    private void Rebuild()
    {
        error = null; slots = Array.Empty<Vector2>(); route = null;
        lastCameraPosition = CameraPosition; cameraPoseCached = true;
        try
        {
            if (pattern != null) pattern.Validate();
            slots = shape == null ? Array.Empty<Vector2>() : FormationBuilder.Build(shape);
            if (path != null && (pattern == null || pattern.type != EnemyPatternType.Area))
                route = new EnemyPathRoute(EnemyPathBuilder.Build(path, CameraPosition));
        }
        catch (Exception e) { error = e.Message; }
        SceneView.RepaintAll();
    }

    // Căn theo toàn bộ hình/đường thực tế, kể cả khi camera game ở xa gốc tọa độ.
    private Bounds GetPreviewBounds()
    {
        Vector2 center = tab == 3 || (pattern != null && pattern.type == EnemyPatternType.Area) ? PreviewPlayer : CameraPosition;
        var bounds = CameraPreviewBounds;
        if (tab == 3 && bossSpawn != null)
        {
            bounds.Encapsulate((Vector3)(PreviewPlayer - bossSpawn.arenaSize * 0.5f));
            bounds.Encapsulate((Vector3)(PreviewPlayer + bossSpawn.arenaSize * 0.5f));
        }
        else if (route != null)
        {
            foreach (var point in route.Points)
            {
                bounds.Encapsulate(point);
                foreach (var slot in slots) bounds.Encapsulate((Vector3)(point + slot));
            }
        }
        else foreach (var slot in slots) bounds.Encapsulate((Vector3)(center + slot));
        // Luôn giữ tâm camera; chỉ mở rộng zoom đủ nhìn thấy hình/Path xung quanh camera đó.
        Vector2 cameraCenter = CameraPosition;
        float halfWidth = Mathf.Max(Mathf.Abs(bounds.min.x - cameraCenter.x), Mathf.Abs(bounds.max.x - cameraCenter.x)) + 1f;
        float halfHeight = Mathf.Max(Mathf.Abs(bounds.min.y - cameraCenter.y), Mathf.Abs(bounds.max.y - cameraCenter.y)) + 1f;
        return new Bounds(cameraCenter, new Vector3(Mathf.Max(6f, halfWidth * 2f), Mathf.Max(6f, halfHeight * 2f), 1f));
    }

    private void FramePreview()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var view = SceneView.lastActiveSceneView != null ? SceneView.lastActiveSceneView : GetWindow<SceneView>();
        view.in2DMode = true;
        view.Frame(GetPreviewBounds(), true);
        view.Repaint();
    }

    #endregion

    #region Vẽ và tương tác Scene

    /// <summary>Vẽ đường, slot và marker; cho kéo điểm Custom, waypoint hoặc player minh họa.</summary>
    private void DrawScene(SceneView scene)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        Bounds cameraBounds = CameraPreviewBounds;
        Handles.color = new Color(1f, 1f, 1f, 0.45f);
        Vector3 cameraA = new Vector3(cameraBounds.min.x, cameraBounds.min.y, 0f);
        Vector3 cameraB = new Vector3(cameraBounds.min.x, cameraBounds.max.y, 0f);
        Vector3 cameraC = new Vector3(cameraBounds.max.x, cameraBounds.max.y, 0f);
        Vector3 cameraD = new Vector3(cameraBounds.max.x, cameraBounds.min.y, 0f);
        Handles.DrawLine(cameraA, cameraB); Handles.DrawLine(cameraB, cameraC);
        Handles.DrawLine(cameraC, cameraD); Handles.DrawLine(cameraD, cameraA);
        Handles.Label(cameraB, "Gameplay Camera");
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
            Vector3 moved = Handles.PositionHandle(PreviewPlayer, Quaternion.identity);
            if (EditorGUI.EndChangeCheck()) { PreviewPlayer = moved; Repaint(); }
            Handles.Label(PreviewPlayer, "Player (Camera + Offset)");
        }
        if (tab == 3 && bossSpawn != null)
        {
            Vector2 half = bossSpawn.arenaSize * 0.5f;
            Vector3 a = PreviewPlayer + new Vector2(-half.x, -half.y);
            Vector3 b = PreviewPlayer + new Vector2(-half.x, half.y);
            Vector3 c = PreviewPlayer + new Vector2(half.x, half.y);
            Vector3 d = PreviewPlayer + new Vector2(half.x, -half.y);
            Handles.color = Color.red;
            Handles.DrawLine(a, b); Handles.DrawLine(b, c); Handles.DrawLine(c, d); Handles.DrawLine(d, a);
            Handles.Label(c, $"Boss Arena {bossSpawn.arenaSize.x:0.##} × {bossSpawn.arenaSize.y:0.##}");
        }
        Handles.color = Color.yellow;
        if (pattern != null && pattern.type == EnemyPatternType.SequentialPath && route != null)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                float age = elapsed - i * pattern.spawnInterval;
                if (age >= 0f && age * pattern.speed < route.Length * pattern.passes)
                    Marker(route.Sample(age * pattern.speed, pattern.passes) + slots[i]);
            }
        }
        else foreach (Vector2 slot in slots)
        {
            if (pattern != null && pattern.type == EnemyPatternType.Area)
            {
                if (elapsed < pattern.ShrinkTiming.TotalDuration) Marker(PreviewPlayer + slot * pattern.ShrinkTiming.GetScale(elapsed));
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
