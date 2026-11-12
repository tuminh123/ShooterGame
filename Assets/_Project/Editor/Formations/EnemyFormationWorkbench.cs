using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Ba tab tách hẳn việc dựng hình, dựng đường và xem thử. Không tạo enemy thật hoặc bật Play scene.
public sealed class EnemyFormationWorkbench : EditorWindow
{
    #region Editor State

    [SerializeField] private int tab;
    [SerializeField] private EnemyFormationSO formation;
    [SerializeField] private EnemyPathSO path;
    [SerializeField] private EnemyFormationSettings settings = new EnemyFormationSettings();
    [SerializeField] private WaveConfigSO targetWave;
    [SerializeField] private int targetWaveIndex;
    [SerializeField] private int targetFormationIndex;
    [SerializeField] private EnemySpawnManager spawnSource;
    [SerializeField] private Camera previewCamera;
    [SerializeField] private bool showSpawnArea = true;
    [SerializeField] private bool showCameraArea = true;
    [SerializeField] private Vector2 fallbackCameraPosition;
    private readonly List<Vector2> previewSpawnPoints = new List<Vector2>();
    [SerializeField] private Vector2 origin;
    [SerializeField] private Vector2 player = new Vector2(5f, 3f);
    [SerializeField] private bool showGizmos = true;
    [SerializeField] private bool simulateContact = true;
    [SerializeField] private bool showLabels;
    [SerializeField] private int selectedPoint;

    [SerializeField] private float previewChaseSpeed = 1f; // Chỉ minh họa chase; runtime đọc moveSpeed thực của từng enemy.

    private enum PreviewState { Moving, Stunned, Chasing }
    private PreviewState state;
    private EnemyFormationSettings activeSettings;
    private EnemyPathRoute route;
    private Vector2[] offsets = Array.Empty<Vector2>();
    private Vector2[] positions = Array.Empty<Vector2>();
    private Vector3[] line = Array.Empty<Vector3>();
    private float travelled;
    private float stunRemaining;
    private Vector2[] recoilDirections = System.Array.Empty<Vector2>();
    private float recoilRemaining;
    private bool running;
    private double lastUpdate;
    private Vector2 scroll;
    private string message;

    private static GUIContent Tip(string label, string help)
    {
        return new GUIContent(label, help);
    }

    #endregion

    #region Open And Editor Lifecycle

    [MenuItem("Tools/Shooter Game/Formation & Path Workbench")]
    private static void OpenMenu()
    {
        var window = GetWindow<EnemyFormationWorkbench>("Enemy Formation / Path");
        window.minSize = new Vector2(440f, 540f);

        if (Selection.activeObject is EnemyFormationSO selectedFormation)
        {
            window.formation = selectedFormation;
            window.tab = 0;
        }
        else if (Selection.activeObject is EnemyPathSO selectedPath)
        {
            window.path = selectedPath;
            window.tab = 1;
        }
        else if (Selection.activeObject is WaveConfigSO selectedWave)
        {
            Open(selectedWave, 0);
            return;
        }

        window.ResetPreview();
        window.Show();
    }

    public static EnemyFormationWorkbench Open(WaveConfigSO waves, int index, int formationIndex = 0)
    {
        var window = GetWindow<EnemyFormationWorkbench>("Enemy Formation / Path");
        window.minSize = new Vector2(440f, 540f);
        window.targetWave = waves;
        window.targetWaveIndex = index;
        window.targetFormationIndex = formationIndex;
        window.tab = 2;
        window.LoadFromWave();
        window.ResetPreview();
        window.Show();
        return window;
    }

    public void SetPreviewOrigin(Vector2 value)
    {
        fallbackCameraPosition = value;
        previewCamera = null;
        origin = value;
        ResetPreview();
    }

    private void OnEnable()
    {
        FindSpawnSource();
        SceneView.duringSceneGui += DrawScene;
        EditorApplication.update += UpdatePreview;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        Undo.undoRedoPerformed += ResetPreview;
        ResetPreview();
    }

    private void OnDisable()
    {
        running = false;
        SceneView.duringSceneGui -= DrawScene;
        EditorApplication.update -= UpdatePreview;
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        Undo.undoRedoPerformed -= ResetPreview;
        SceneView.RepaintAll();
    }

    private void OnPlayModeChanged(PlayModeStateChange change)
    {
        running = false;
        SceneView.RepaintAll();
        Repaint();
    }

    private void OnProjectChange()
    {
        ResetPreview();
    }

    #endregion

    #region Three Tab Window

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Enemy Formation / Path", EditorStyles.boldLabel);

        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorGUILayout.HelpBox("Đang Play: công cụ tạm dừng và ẩn marker để không nhầm với enemy thật. Quay về Edit mode để chỉnh asset hoặc xem thử.", MessageType.Info);
            return;
        }

        EditorGUI.BeginChangeCheck();
        tab = GUILayout.Toolbar(tab, new[]
        {
            Tip("1. Formation", "Tạo và chỉnh hình khối đặc. Tab này không chỉnh đường đi."),
            Tip("2. Path", "Bắt đầu bằng A–B, chèn và kéo waypoint để tạo đường riêng."),
            Tip("3. Test", "Ghép hai asset để tự xem chuyển động, va chạm, choáng và chase.")
        });

        if (EditorGUI.EndChangeCheck())
        {
            ResetPreview();
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            showGizmos = EditorGUILayout.ToggleLeft(Tip("Hiện trong Scene", "Bật để thấy điểm, đường và marker trong Scene view. Bật cả nút Gizmos của Scene view."), showGizmos);

            if (GUILayout.Button(Tip("Đưa vào khung nhìn", "Chuyển Scene view sang 2D và phóng vừa nội dung tab hiện tại.")))
            {
                FramePreview();
            }
        }

        EditorGUI.BeginChangeCheck();
        using (new EditorGUI.DisabledScope(true))
        {
            origin = EditorGUILayout.Vector2Field(Tip("Tâm Path hiện tại", "Camera + Center Offset trong Path asset. Chỉnh offset tại tab Path."), origin);
        }

        if (EditorGUI.EndChangeCheck())
        {
            ResetPreview();
        }

        scroll = EditorGUILayout.BeginScrollView(scroll);
        DrawSpawnContext();

        if (tab == 0)
        {
            DrawFormationTab();
        }
        else if (tab == 1)
        {
            DrawPathTab();
        }
        else
        {
            DrawTestTab();
        }

        if (!string.IsNullOrEmpty(message))
        {
            EditorGUILayout.HelpBox(message, MessageType.Info);
        }

        EditorGUILayout.EndScrollView();
    }

    #region Spawn Context And Coordinate Mapping

    // Có thể mở từ menu/Wave Inspector; tự lấy spawner trong scene đang mở nếu chưa chọn.
    private Vector2 CameraPosition => previewCamera != null ? (Vector2)previewCamera.transform.position : fallbackCameraPosition;
    private float PreviewFormationSpeed => formation != null ? formation.Speed : 0f;
    private float PreviewKnockbackDistance => spawnSource != null ? spawnSource.BaseKnockbackDistance : 0.35f;

    private void FindSpawnSource()
    {
        if (spawnSource == null)
        {
            spawnSource = UnityEngine.Object.FindFirstObjectByType<EnemySpawnManager>();
        }

        if (previewCamera == null)
        {
            previewCamera = spawnSource != null ? spawnSource.FormationCamera : Camera.main;
        }
    }

    public void SetSpawnSource(EnemySpawnManager source)
    {
        spawnSource = source;
        previewCamera = source != null ? source.FormationCamera : Camera.main;
        SyncPathCenter(true);
        ResetPreview();
    }

    private void DrawSpawnContext()
    {
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("Camera và vùng spawn tham chiếu", EditorStyles.boldLabel);
        EditorGUI.BeginChangeCheck();
        spawnSource = (EnemySpawnManager)EditorGUILayout.ObjectField(Tip("Spawner trong scene", "Vùng vàng chỉ dành cho quái thường. Đội hình xuất hiện tại A của Path, không dùng các điểm vàng."), spawnSource, typeof(EnemySpawnManager), true);
        previewCamera = (Camera)EditorGUILayout.ObjectField(Tip("Camera tham chiếu", "Dùng đúng Formation Camera trên spawner. Path chỉ bám vị trí camera, không lấy zoom hoặc góc xoay."), previewCamera, typeof(Camera), true);

        if (previewCamera == null)
        {
            fallbackCameraPosition = EditorGUILayout.Vector2Field(Tip("Tâm camera mô phỏng", "Không có camera: nhập vị trí tâm để chỉnh đường trong Edit mode."), fallbackCameraPosition);
        }

        showSpawnArea = EditorGUILayout.Toggle(Tip("Hiện vùng spawn thường", "Vùng/điểm vàng của quái thường để đối chiếu. Không quyết định vị trí đội hình."), showSpawnArea);
        showCameraArea = EditorGUILayout.Toggle(Tip("Hiện khung camera", "Khung xanh lá là phần màn hình game nhìn thấy. Tâm Path màu tím; A là nơi đội hình xuất hiện."), showCameraArea);
        bool changed = EditorGUI.EndChangeCheck();

        if (GUILayout.Button(Tip("Nạp từ scene", "Lấy Formation Camera từ spawner; dùng tâm camera làm player minh họa.")))
        {
            FindSpawnSource();

            if (spawnSource != null)
            {
                previewCamera = spawnSource.FormationCamera;
            }

            changed = true;
        }

        if (changed)
        {
            SyncPathCenter(true);
            ResetPreview();
        }

        EditorGUILayout.HelpBox("Đội hình: spawn tại A, chạy trên Path bám camera. Quái thường: dùng vùng vàng. Đổi tâm tùy chỉnh ở tab Path.", MessageType.None);
        EditorGUILayout.EndVertical();
    }

    private void SyncPathCenter(bool resetPlayer)
    {
        origin = path != null && tab != 0 ? path.GetCenter(CameraPosition) : CameraPosition;

        if (resetPlayer)
        {
            player = CameraPosition;
        }
    }

    private Vector2 PathPointToWorld(Vector2 local)
    {
        return origin + local;
    }

    private Vector2 WorldPointToPath(Vector2 world)
    {
        return world - origin;
    }

    #endregion

    private void DrawFormationTab()
    {
        EditorGUILayout.HelpBox("Hình tự quyết định số enemy theo cấp đối xứng. Size chỉ giãn hình. Custom cho phép thêm, xóa và kéo từng điểm trong Scene.", MessageType.Info);
        EditorGUI.BeginChangeCheck();
        formation = (EnemyFormationSO)EditorGUILayout.ObjectField(Tip("Formation asset", "Asset chỉ chứa hình dạng, kích thước và góc xoay. Kéo asset này vào Wave để dùng."), formation, typeof(EnemyFormationSO), false);

        if (EditorGUI.EndChangeCheck())
        {
            ResetPreview();
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button(Tip("Tạo Formation", "Tạo asset hình mới; dùng lại được trong nhiều Wave.")))
            {
                formation = CreateAsset<EnemyFormationSO>("EnemyFormation", "Assets/_Project/DataSO/Wave/EnemyFormations");
                ResetPreview();
            }

            SaveButton(formation);
        }

        if (formation == null)
        {
            return;
        }

        if (DrawAssetFields(formation, "shape", "speed"))
        {
            ResetPreview();
        }

        if (formation.Shape == FormationType.Custom)
        {
            if (DrawAssetFields(formation, "customScale", "rotation", "symmetry", "customPoints"))
            {
                ResetPreview();
            }

            EditorGUILayout.HelpBox("Kéo điểm gốc màu trắng trong Scene. Các điểm đối xứng được sinh tự động. Dùng +/− trong danh sách để thêm/xóa điểm gốc.", MessageType.Info);
        }
        else
        {
            if (DrawAssetFields(formation, "level", "size", "rotation"))
            {
                ResetPreview();
            }

            if (GUILayout.Button(Tip("Tạo bản Custom từ hình này", "Tạo asset mới chứa đúng các vị trí hiện tại để chỉnh tự do; không thay hình gốc.")))
            {
                ConvertFormationToCustom();
            }
        }

        try
        {
            EditorGUILayout.LabelField("Số enemy / đội hình", formation.SlotCount.ToString());
        }
        catch (Exception exception)
        {
            EditorGUILayout.HelpBox(exception.Message, MessageType.Error);
        }
    }

    // Tạo bản mới để người thiết kế tùy biến hình chuẩn mà không làm thay đổi các Wave dùng hình gốc.
    private void ConvertFormationToCustom()
    {
        Vector2[] points = FormationBuilder.Build(formation);
        float speed = formation.Speed;
        EnemyFormationSO custom = CreateAsset<EnemyFormationSO>("CustomFormation", "Assets/_Project/DataSO/Wave/EnemyFormations");

        if (custom == null)
        {
            return;
        }

        var serialized = new SerializedObject(custom);
        serialized.FindProperty("shape").intValue = (int)FormationType.Custom;
        serialized.FindProperty("speed").floatValue = speed;
        SerializedProperty array = serialized.FindProperty("customPoints");
        array.arraySize = points.Length;

        for (int i = 0; i < points.Length; i++)
        {
            array.GetArrayElementAtIndex(i).vector2Value = points[i];
        }

        serialized.ApplyModifiedProperties();
        AssetDatabase.SaveAssetIfDirty(custom);
        formation = custom;
        ResetPreview();
    }

    private void DrawPathTab()
    {
        EditorGUILayout.HelpBox("Path mới chỉ có A và B. Kéo cả hai đầu hoặc chèn điểm giữa. Shift + click trong Scene để chèn lên đoạn gần nhất.", MessageType.Info);
        EditorGUI.BeginChangeCheck();
        path = (EnemyPathSO)EditorGUILayout.ObjectField(Tip("Path asset", "Đường riêng chỉ chứa waypoint và cách nối điểm, không chứa enemy hoặc tốc độ."), path, typeof(EnemyPathSO), false);

        if (EditorGUI.EndChangeCheck())
        {
            selectedPoint = 0;
            ResetPreview();
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button(Tip("Tạo Path A–B", "Tạo đường thẳng với đúng hai điểm ban đầu. Sau đó thêm và kéo điểm để tự tạo đường.")))
            {
                path = CreateAsset<EnemyPathSO>("EnemyPath", "Assets/_Project/DataSO/Wave/EnemyPaths");
                selectedPoint = 0;
                ResetPreview();
            }

            SaveButton(path);
        }

        if (path == null)
        {
            return;
        }

        if (DrawAssetFields(path, path.UseCustomCenter ? new[] { "useCustomCenter", "centerOffset" } : new[] { "useCustomCenter" }))
        {
            ResetPreview();
        }

        if (DrawAssetFields(path, path.Smooth ? new[] { "smooth", "samplesPerSegment" } : new[] { "smooth" }))
        {
            ResetPreview();
        }

        var serialized = new SerializedObject(path);
        serialized.Update();
        SerializedProperty points = serialized.FindProperty("waypoints");
        selectedPoint = Mathf.Clamp(selectedPoint, 0, Mathf.Max(0, points.arraySize - 1));

        for (int i = 0; i < points.arraySize; i++)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                string label = i == 0 ? "A" : i == points.arraySize - 1 ? "B" : "P" + i;

                if (GUILayout.Toggle(selectedPoint == i, Tip(label, "Chọn điểm để chèn, xóa hoặc đổi thứ tự. Điểm chọn có màu vàng trong Scene."), EditorStyles.miniButton, GUILayout.Width(40f)))
                {
                    selectedPoint = i;
                }

                EditorGUILayout.PropertyField(points.GetArrayElementAtIndex(i), Tip("Vị trí", "Tọa độ local của điểm. Có thể kéo trực tiếp trong Scene. A/B không bị khóa."));
            }
        }

        if (serialized.ApplyModifiedProperties())
        {
            ResetPreview();
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(points.arraySize < 2 || points.arraySize >= 128))
            {
                if (GUILayout.Button(Tip("Chèn sau điểm chọn", "Chèn điểm giữa điểm đang chọn và điểm kế tiếp. Nếu chọn B, nối dài thêm một điểm cuối.")))
                {
                    InsertWaypoint(selectedPoint, null);
                }
            }

            using (new EditorGUI.DisabledScope(points.arraySize <= 2))
            {
                if (GUILayout.Button(Tip("Xóa điểm chọn", "Luôn giữ ít nhất hai điểm. Có thể Undo để khôi phục.")))
                {
                    EditWaypoints(array => array.DeleteArrayElementAtIndex(selectedPoint));
                }
            }
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(selectedPoint <= 0))
            {
                if (GUILayout.Button("Đưa lên"))
                {
                    int index = selectedPoint;
                    EditWaypoints(array => array.MoveArrayElement(index, index - 1));
                    selectedPoint = index - 1;
                }
            }

            using (new EditorGUI.DisabledScope(selectedPoint >= points.arraySize - 1))
            {
                if (GUILayout.Button("Đưa xuống"))
                {
                    int index = selectedPoint;
                    EditWaypoints(array => array.MoveArrayElement(index, index + 1));
                    selectedPoint = index + 1;
                }
            }
        }

        DrawRouteLength();
    }

    private void DrawTestTab()
    {
        EditorGUILayout.HelpBox("Chọn một Formation và một Path. Preview chỉ dùng marker, không sinh enemy thật và không mô phỏng damage.", MessageType.Info);
        EditorGUI.BeginChangeCheck();
        formation = (EnemyFormationSO)EditorGUILayout.ObjectField(Tip("Formation", "Hình xếp quái cần xem thử."), formation, typeof(EnemyFormationSO), false);
        path = (EnemyPathSO)EditorGUILayout.ObjectField(Tip("Path", "Đường sẽ đưa cả đội hình di chuyển."), path, typeof(EnemyPathSO), false);

        if (EditorGUI.EndChangeCheck())
        {
            ResetPreview();
        }

        if (formation != null && DrawAssetFields(formation, "speed"))
        {
            ResetPreview();
        }

        EditorGUILayout.HelpBox("Speed thuộc Formation asset. Chỉnh ở đây sẽ đổi tốc độ cho mọi Wave dùng cùng asset; thông số lượt chạy bên dưới không chứa Speed.", MessageType.None);
        EditorGUILayout.LabelField("Số enemy trong preview", positions.Length.ToString());
        var serialized = new SerializedObject(this);
        serialized.Update();
        EditorGUILayout.PropertyField(serialized.FindProperty("previewChaseSpeed"), Tip("Tốc độ đuổi minh họa", "Tốc độ sau khi nhóm tan trong preview. Gameplay dùng moveSpeed thực của enemy, không dùng tốc độ của đội hình."));
        EditorGUILayout.PropertyField(serialized.FindProperty("settings"), Tip("Thông số lượt chạy", "Dùng cùng kiểu dữ liệu với Wave: tốc độ, số lượt và phản ứng va chạm. Số quái lấy từ Formation."), true);

        if (serialized.ApplyModifiedProperties())
        {
            ResetPreview();
        }

        player = EditorGUILayout.Vector2Field(Tip("Player giả lập", "Có thể kéo vòng xanh trong Scene để thử chạm nhóm. Di chuyển player không bẻ lại path đã dựng; Reset để lấy hướng mới."), player);
        simulateContact = EditorGUILayout.Toggle(Tip("Thử va chạm", "Tắt nếu chỉ muốn xem nhóm đi hết đường. Bật để thử cả nhóm văng rồi choáng."), simulateContact);
        showLabels = EditorGUILayout.Toggle(Tip("Hiện số thứ tự", "Giúp phân biệt các slot và đầu/đuôi."), showLabels);

        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(route == null || positions.Length == 0))
            {
                if (GUILayout.Button(Tip(running ? "Tạm dừng" : "Chạy preview", "Chạy mô phỏng trong Edit mode, không bật Play scene.")))
                {
                    running = !running;
                    lastUpdate = EditorApplication.timeSinceStartup;
                }
            }

            if (GUILayout.Button(Tip("Về đầu", "Dựng lại nhóm, lấy lại hướng đi và xóa trạng thái choáng/knockback của preview.")))
            {
                ResetPreview();
            }

            using (new EditorGUI.DisabledScope(state != PreviewState.Moving || route == null))
            {
                if (GUILayout.Button(Tip("Phá nhóm", "Giả lập cả nhóm knockback, choáng rồi trở lại chase. Không phải thao tác thay đổi Wave.")))
                {
                    BreakPreview(-1);
                }
            }
        }

        if (route != null && activeSettings != null)
        {
            float total = route.CompletionDistance(activeSettings, positions.Length);
            EditorGUI.BeginChangeCheck();
            float progress = EditorGUILayout.Slider(Tip("Tiến trình đường", "Tua qua toàn bộ số lượt, tính cả đuôi nếu nối đuôi. Tua sẽ dừng mô phỏng."), Mathf.Clamp01(travelled / total), 0f, 1f);

            if (EditorGUI.EndChangeCheck())
            {
                running = false;
                state = PreviewState.Moving;
                recoilDirections = System.Array.Empty<Vector2>();
                travelled = progress * total;
                SampleMembers();
                SceneView.RepaintAll();
            }
        }

        EditorGUILayout.LabelField("Trạng thái", state.ToString());
        DrawRouteLength();
        DrawWaveBinding();
    }

    #endregion

    #region Assets And Wave Binding

    private static T CreateAsset<T>(string name, string folder) where T : ScriptableObject
    {
        string[] parts = folder.Split('/');
        string current = parts[0];

        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];

            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }

            current = next;
        }

        string destination = EditorUtility.SaveFilePanelInProject("Lưu asset", name, "asset", "Chọn nơi lưu asset.", folder);

        if (string.IsNullOrEmpty(destination))
        {
            return null;
        }

        T asset = CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, destination);
        AssetDatabase.SaveAssetIfDirty(asset);
        return asset;
    }

    private static void SaveButton(UnityEngine.Object asset)
    {
        using (new EditorGUI.DisabledScope(asset == null))
        {
            if (GUILayout.Button(Tip("Lưu asset", "Chỉ lưu asset đang chỉnh; không lưu các asset khác hoặc sửa scene.")))
            {
                AssetDatabase.SaveAssetIfDirty(asset);
            }
        }
    }

    private static bool DrawAssetFields(UnityEngine.Object asset, params string[] names)
    {
        var serialized = new SerializedObject(asset);
        serialized.Update();

        foreach (string name in names)
        {
            SerializedProperty property = serialized.FindProperty(name);
            EditorGUILayout.PropertyField(property, Tip(property.displayName, property.tooltip), true);
        }

        return serialized.ApplyModifiedProperties();
    }

    private void DrawWaveBinding()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Gán cấu hình vào Wave", EditorStyles.boldLabel);
        targetWave = (WaveConfigSO)EditorGUILayout.ObjectField(Tip("Wave DataSO", "Wave đích nhận hai asset và thông số lượt chạy. Không thay danh sách enemy hoặc tổng quái."), targetWave, typeof(WaveConfigSO), false);

        if (targetWave == null || targetWave.Count == 0)
        {
            return;
        }

        targetWaveIndex = EditorGUILayout.IntSlider("Wave", targetWaveIndex + 1, 1, targetWave.Count) - 1;
        EnemyFormationSpawn[] entries = targetWave.GetWave(targetWaveIndex).formationSpawns ?? Array.Empty<EnemyFormationSpawn>();
        var labels = new string[entries.Length + 1];

        for (int i = 0; i < entries.Length; i++)
        {
            labels[i] = "Lượt " + (i + 1);
        }

        labels[entries.Length] = "Thêm lượt mới";
        targetFormationIndex = EditorGUILayout.Popup("Lượt đội hình", Mathf.Clamp(targetFormationIndex, 0, entries.Length), labels);
        EditorGUILayout.HelpBox("Chọn lượt cần đọc/gán hoặc thêm lượt mới. Gán chỉ thay lượt đã chọn; giữ nguyên các lượt khác và Spawn After. Tab Test xem một nhóm.", MessageType.Info);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button(Tip("Lấy từ Wave", "Đọc hai tham chiếu asset và sao chép thông số sang tab Test.")))
            {
                LoadFromWave();
                ResetPreview();
            }

            using (new EditorGUI.DisabledScope(formation == null || path == null))
            {
                if (GUILayout.Button(Tip("Gán và lưu Wave", "Ghi hai asset đang chọn và thông số lượt chạy vào wave này. Hỗ trợ Undo.")))
                {
                    ApplyToWave();
                }
            }
        }
    }

    private void LoadFromWave()
    {
        if (targetWave == null || targetWave.Count == 0)
        {
            return;
        }

        targetWaveIndex = Mathf.Clamp(targetWaveIndex, 0, targetWave.Count - 1);
        WaveConfigSO.Wave wave = targetWave.GetWave(targetWaveIndex);
        EnemyFormationSpawn[] entries = wave.formationSpawns ?? Array.Empty<EnemyFormationSpawn>();
        targetFormationIndex = Mathf.Clamp(targetFormationIndex, 0, entries.Length);
        EnemyFormationSpawn entry = targetFormationIndex < entries.Length ? entries[targetFormationIndex] : null;
        formation = entry == null ? null : entry.formation;
        path = entry == null ? null : entry.path;
        settings = entry == null || entry.settings == null ? new EnemyFormationSettings() : entry.settings.Copy();

    }

    private void ApplyToWave()
    {
        try
        {
            formation.Validate();
            path.Validate();
            settings.Validate();
            WaveConfigSO.Wave wave = targetWave.GetWave(targetWaveIndex);

            // Bản sao mảng chỉ thay phần tử đích; không sửa lượt khác trước khi ngân sách hợp lệ.
            EnemyFormationSpawn[] current = wave.formationSpawns ?? Array.Empty<EnemyFormationSpawn>();
            targetFormationIndex = Mathf.Clamp(targetFormationIndex, 0, current.Length);
            bool append = targetFormationIndex == current.Length;
            var entries = new EnemyFormationSpawn[current.Length + (append ? 1 : 0)];
            Array.Copy(current, entries, current.Length);
            entries[targetFormationIndex] = new EnemyFormationSpawn
            {
                formation = formation,
                path = path,
                settings = settings.Copy()
            };
            var candidate = new WaveConfigSO.Wave
            {
                spawnCount = wave.spawnCount,
                formationSpawns = entries,
                formationSpawnAfter = wave.formationSpawnAfter
            };
            candidate.GetFormationSpawnPlan();
            Undo.RecordObject(targetWave, "Gán lượt đội hình vào Wave");
            wave.formationSpawns = entries;
            EditorUtility.SetDirty(targetWave);
            AssetDatabase.SaveAssetIfDirty(targetWave);
            message = "Đã gán và lưu cấu hình vào Wave.";
        }
        catch (Exception exception)
        {
            message = exception.Message;
        }
    }

    #endregion

    #region Waypoint Editing

    private void EditWaypoints(Action<SerializedProperty> edit)
    {
        var serialized = new SerializedObject(path);
        serialized.Update();
        edit(serialized.FindProperty("waypoints"));
        serialized.ApplyModifiedProperties();
        ResetPreview();
    }

    private void InsertWaypoint(int after, Vector2? position)
    {
        EditWaypoints(array =>
        {
            int index = Mathf.Clamp(after, 0, array.arraySize - 1);
            Vector2 current = array.GetArrayElementAtIndex(index).vector2Value;
            Vector2 next = index + 1 < array.arraySize ? array.GetArrayElementAtIndex(index + 1).vector2Value : current + Vector2.right * 4f;
            array.InsertArrayElementAtIndex(index + 1);
            array.GetArrayElementAtIndex(index + 1).vector2Value = position ?? (current + next) * 0.5f;
            selectedPoint = index + 1;
        });
    }

    #endregion

    #region Preview State And Movement

    private void ResetPreview()
    {
        running = false;
        SyncPathCenter(false);
        message = null;
        travelled = 0f;
        stunRemaining = 0f;
        recoilRemaining = 0f;
        recoilDirections = System.Array.Empty<Vector2>();
        state = PreviewState.Moving;
        route = null;
        positions = Array.Empty<Vector2>();
        line = Array.Empty<Vector3>();
        lastUpdate = EditorApplication.timeSinceStartup;

        try
        {
            if (tab == 0 && formation != null)
            {
                offsets = FormationBuilder.Build(formation);
                positions = new Vector2[offsets.Length];

                for (int i = 0; i < positions.Length; i++)
                {
                    positions[i] = origin + offsets[i];
                }
            }
            else if (tab == 1 && path != null)
            {
                Vector2[] points = EnemyPathBuilder.Build(path, CameraPosition);

                route = new EnemyPathRoute(points, previewCamera != null ? previewCamera.transform : null);
                BuildLine();
            }
            else if (tab == 2 && formation != null && path != null)
            {
                settings.Validate();

                activeSettings = settings.Copy();
                route = new EnemyPathRoute(EnemyPathBuilder.Build(path, CameraPosition), previewCamera != null ? previewCamera.transform : null);
                offsets = FormationBuilder.Build(formation);
                positions = new Vector2[offsets.Length];
                SampleMembers();
                BuildLine();
            }
        }
        catch (Exception exception)
        {
            message = exception.Message;
        }

        Repaint();
        SceneView.RepaintAll();
    }

    private void BuildLine()
    {
        line = new Vector3[route.Points.Count];

        for (int i = 0; i < line.Length; i++)
        {
            line[i] = route.Points[i] + route.AnchorOffset;
        }
    }

    private void UpdatePreview()
    {
        double now = EditorApplication.timeSinceStartup;
        float deltaTime = Mathf.Clamp((float)(now - lastUpdate), 0f, 0.1f);
        lastUpdate = now;

        Vector2 previousOrigin = origin;
        SyncPathCenter(false);

        if (tab == 0 && origin != previousOrigin && !EditorApplication.isPlayingOrWillChangePlaymode)
        {
            for (int i = 0; i < positions.Length; i++)
            {
                positions[i] = origin + offsets[i];
            }

            Repaint();
            SceneView.RepaintAll();
        }

        if (route != null && origin != previousOrigin && !EditorApplication.isPlayingOrWillChangePlaymode)
        {
            BuildLine();

            if (tab == 2 && state == PreviewState.Moving)
            {
                SampleMembers();
            }

            Repaint();
            SceneView.RepaintAll();
        }

        if (!running || tab != 2 || route == null || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        AdvancePreview(deltaTime);
        Repaint();
        SceneView.RepaintAll();
    }

    private void AdvancePreview(float deltaTime)
    {
        if (state == PreviewState.Moving)
        {
            travelled = Mathf.Min(route.CompletionDistance(activeSettings, positions.Length), travelled + PreviewFormationSpeed * deltaTime);
            SampleMembers();

            if (travelled >= route.CompletionDistance(activeSettings, positions.Length))
            {
                state = PreviewState.Chasing;
            }
            else if (simulateContact)
            {
                for (int i = 0; i < positions.Length; i++)
                {
                    if (Vector2.Distance(positions[i], player) <= 0.35f)
                    {
                        BreakPreview(i);
                        break;
                    }
                }
            }
        }
        else if (state == PreviewState.Stunned)
        {
            stunRemaining -= deltaTime;

            if (stunRemaining <= 0f)
            {
                state = PreviewState.Chasing;
            }
        }
        else
        {
            for (int i = 0; i < positions.Length; i++)
            {
                if (recoilRemaining <= 0f)
                {
                    positions[i] = Vector2.MoveTowards(positions[i], player, Mathf.Max(0f, previewChaseSpeed) * deltaTime);
                }
            }
        }

        if (recoilRemaining > 0f)
        {
            float step = Mathf.Min(deltaTime, recoilRemaining);
            float duration = Mathf.Max(0.01f, activeSettings.knockbackDuration);

            for (int i = 0; i < positions.Length; i++)
            {
                positions[i] += recoilDirections[i] * (PreviewKnockbackDistance / duration) * step;
            }

            recoilRemaining -= step;
        }
    }

    private void SampleMembers()
    {
        for (int i = 0; i < positions.Length; i++)
        {
            positions[i] = route.MemberPosition(activeSettings, travelled, i, offsets[i]);
        }
    }

    private void BreakPreview(int source)
    {
        if (state != PreviewState.Moving || activeSettings == null)
        {
            return;
        }

        // Nút phá nhóm và va chạm mô phỏng đều áp dụng phản lực cho toàn bộ marker.
        recoilRemaining = PreviewKnockbackDistance > 0f ? Mathf.Max(0.01f, activeSettings.knockbackDuration) : 0f;
        stunRemaining = recoilRemaining + activeSettings.stunDuration;
        state = stunRemaining > 0f ? PreviewState.Stunned : PreviewState.Chasing;
        recoilDirections = new Vector2[positions.Length];

        for (int i = 0; i < positions.Length; i++)
        {
            Vector2 away = positions[i] - player;
            recoilDirections[i] = away.sqrMagnitude > 0.0001f ? away.normalized : Vector2.right;
        }
    }

    private void DrawRouteLength()
    {
        if (route != null)
        {
            EditorGUILayout.LabelField(Tip("Độ dài đường thực", "Tổng chiều dài đường sau khi nối/mượt, theo world unit. Thay đổi bằng cách kéo waypoint."), route.Length.ToString("0.##"));
        }
    }

    #endregion

    #region Scene Handles

    private void DrawScene(SceneView scene)
    {
        if (!showGizmos || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        DrawSpawnContextGizmos();

        using (new Handles.DrawingScope(Color.cyan))
        {
            if (line.Length > 1)
            {
                Handles.DrawAAPolyLine(3f, line);
            }

            if (tab == 0 && formation != null && formation.Shape == FormationType.Custom)
            {
                DrawCustomHandles();
            }

            Handles.color = Color.gray;
            Handles.color = Color.magenta;
            Handles.DrawWireDisc(origin, Vector3.forward, 0.15f);
            Handles.Label(origin + Vector2.down * 0.3f, "Tâm Path · bám camera");

            if (tab == 1 && path != null)
            {
                DrawWaypointHandles();
                InsertFromSceneClick();
            }

            for (int i = 0; i < positions.Length; i++)
            {
                Handles.color = state == PreviewState.Stunned ? Color.yellow : new Color(1f, 0.45f, 0.15f);
                Handles.SphereHandleCap(0, positions[i], Quaternion.identity, 0.25f, EventType.Repaint);

                if (tab == 0 || showLabels)
                {
                    Handles.Label(positions[i] + Vector2.up * 0.15f, i.ToString());
                }
            }

            if (tab == 2)
            {
                if (route != null)
                {
                    Handles.color = Color.white;
                    Handles.Label(route.Points[0] + route.AnchorOffset, "A · nơi spawn đội hình");
                    Handles.DrawWireDisc(route.Points[0] + route.AnchorOffset, Vector3.forward, 0.2f);
                }

                Handles.color = Color.green;
                EditorGUI.BeginChangeCheck();
                Vector3 next = Handles.FreeMoveHandle(player, HandleUtility.GetHandleSize(player) * 0.1f, Vector3.zero, Handles.CircleHandleCap);

                if (EditorGUI.EndChangeCheck())
                {
                    player = new Vector2(next.x, next.y);
                    Repaint();
                }

                Handles.Label(player, "Player giả lập");
            }
        }
    }

    // Handle chỉ sửa điểm gốc. Điểm đối xứng được dựng lại từ cùng dữ liệu như runtime.
    private void DrawCustomHandles()
    {
        if (formation.CustomPoints == null || formation.CustomScale <= 0f)
        {
            return;
        }

        for (int i = 0; i < formation.CustomPoints.Count; i++)
        {
            Vector2 point = origin + FormationBuilder.Rotate(formation.CustomPoints[i] * formation.CustomScale, formation.Rotation);
            Handles.color = Color.white;
            Handles.Label(point + Vector2.up * 0.3f, "Gốc " + i);
            EditorGUI.BeginChangeCheck();
            Vector3 moved = Handles.FreeMoveHandle(point, HandleUtility.GetHandleSize(point) * 0.08f, Vector3.zero, Handles.DotHandleCap);

            if (EditorGUI.EndChangeCheck())
            {
                var serialized = new SerializedObject(formation);
                Vector2 local = FormationBuilder.Rotate((Vector2)moved - origin, -formation.Rotation) / formation.CustomScale;
                serialized.FindProperty("customPoints").GetArrayElementAtIndex(i).vector2Value = local;
                serialized.ApplyModifiedProperties();
                ResetPreview();
                break;
            }
        }
    }

    private void DrawWaypointHandles()
    {
        for (int i = 0; i < path.Waypoints.Count; i++)
        {
            Vector2 point = PathPointToWorld(path.Waypoints[i]);
            string label = i == 0 ? "A" : i == path.Waypoints.Count - 1 ? "B" : "P" + i;
            Handles.color = selectedPoint == i ? Color.yellow : Color.white;
            Handles.Label(point, label);
            EditorGUI.BeginChangeCheck();
            Vector3 moved = Handles.FreeMoveHandle(point, HandleUtility.GetHandleSize(point) * 0.07f, Vector3.zero, Handles.DotHandleCap);

            if (EditorGUI.EndChangeCheck())
            {
                int index = i;
                Vector2 value = WorldPointToPath(new Vector2(moved.x, moved.y));
                selectedPoint = index;
                EditWaypoints(array => array.GetArrayElementAtIndex(index).vector2Value = value);
                break;
            }
        }
    }

    private void InsertFromSceneClick()
    {
        Event current = Event.current;

        if (!current.shift || current.type != EventType.MouseDown || current.button != 0 || path.Waypoints.Count < 2 || path.Waypoints.Count >= 128)
        {
            return;
        }

        Ray ray = HandleUtility.GUIPointToWorldRay(current.mousePosition);
        var plane = new Plane(Vector3.forward, Vector3.zero);

        if (!plane.Raycast(ray, out float distance))
        {
            return;
        }

        Vector2 point = WorldPointToPath((Vector2)ray.GetPoint(distance));
        int nearest = 0;
        float best = float.PositiveInfinity;

        for (int i = 0; i < path.Waypoints.Count - 1; i++)
        {
            Vector2 start = path.Waypoints[i];
            Vector2 segment = path.Waypoints[i + 1] - start;
            float ratio = segment.sqrMagnitude <= 0f ? 0f : Mathf.Clamp01(Vector2.Dot(point - start, segment) / segment.sqrMagnitude);
            float squareDistance = (point - start - segment * ratio).sqrMagnitude;

            if (squareDistance < best)
            {
                best = squareDistance;
                nearest = i;
            }
        }

        InsertWaypoint(nearest, point);
        current.Use();
    }

    // Cắt bốn tia viewport với mặt phẳng gameplay, hỗ trợ cả camera orthographic và perspective.
    private bool TryCameraCorners(out Vector3[] corners)
    {
        corners = new Vector3[4];

        if (previewCamera == null)
        {
            return false;
        }

        float z = spawnSource != null ? spawnSource.SpawnPlaneZ : 0f;
        var plane = new Plane(Vector3.forward, new Vector3(0f, 0f, z));
        var viewport = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };

        for (int i = 0; i < 4; i++)
        {
            Ray ray = previewCamera.ViewportPointToRay(new Vector3(viewport[i].x, viewport[i].y, 0f));

            if (!plane.Raycast(ray, out float distance))
            {
                return false;
            }

            corners[i] = ray.GetPoint(distance);
            corners[i].z = 0f; // Tool chỉnh đường trên mặt phẳng XY, cùng tọa độ với marker.
        }

        return true;
    }

    private void DrawSpawnContextGizmos()
    {
        using (new Handles.DrawingScope(Color.yellow))
        {
            if (showSpawnArea && spawnSource != null)
            {
                Vector2 center = spawnSource.SpawnCenterPosition;
                Vector2 half = spawnSource.SpawnAreaSize * 0.5f;
                Handles.DrawWireCube(center, new Vector3(half.x * 2f, half.y * 2f, 0f));
                Handles.Label(center + new Vector2(-half.x, half.y), "Vùng spawn");
                spawnSource.GetSpawnPoints(previewSpawnPoints);

                for (int i = 0; i < previewSpawnPoints.Count; i++)
                {
                    Vector2 point = previewSpawnPoints[i];
                    Handles.color = Color.yellow;
                    float radius = HandleUtility.GetHandleSize(point) * 0.045f;
                    Handles.DotHandleCap(0, point, Quaternion.identity, radius, EventType.Repaint);
                }
            }

            if (showCameraArea && TryCameraCorners(out Vector3[] corners))
            {
                Handles.color = Color.green;
                Handles.DrawAAPolyLine(2f, corners[0], corners[1], corners[2], corners[3], corners[0]);
                Handles.Label(corners[3], "Khung camera game");
            }
        }
    }

    private void FramePreview()
    {
        var bounds = new Bounds(origin, Vector3.one * 4f);

        if (showSpawnArea && spawnSource != null)
        {
            Vector2 center = spawnSource.SpawnCenterPosition;
            Vector2 half = spawnSource.SpawnAreaSize * 0.5f;
            bounds.Encapsulate(center - half);
            bounds.Encapsulate(center + half);
        }

        if (showCameraArea && TryCameraCorners(out Vector3[] corners))
        {
            foreach (Vector3 corner in corners)
            {
                bounds.Encapsulate(corner);
            }
        }

        foreach (Vector3 point in line)
        {
            bounds.Encapsulate(point);
        }

        foreach (Vector2 point in positions)
        {
            bounds.Encapsulate(point);
        }

        if (tab == 2)
        {
            bounds.Encapsulate(player);
        }

        SceneView scene = SceneView.lastActiveSceneView != null ? SceneView.lastActiveSceneView : GetWindow<SceneView>();
        scene.in2DMode = true;
        scene.Frame(bounds, false);
        scene.Repaint();
    }

    #endregion
}
