using UnityEditor;
using UnityEngine;
using Zenject;

// Công cụ kiểm tra skill và bảng chọn khi lên cấp trong Editor. Không thêm debug UI vào game.
public static class SkillProgressionMenu
{
    [MenuItem("Shooter Game/Skills/Grant One Run Level")]
    private static void GrantRunLevel()
    {
        var scene = Object.FindObjectOfType<SceneContext>();
        if (scene == null) return;
        LevelManager levels = scene.Container.Resolve<LevelManager>();
        if (!levels.IsRunMaxLevel)
            levels.AddRunExperience(levels.RunExperienceToNextLevel - levels.RunExperience);
    }

    [MenuItem("Shooter Game/Skills/Grant One Run Level", true)]
    private static bool CanGrantRunLevel() => Application.isPlaying;

    [MenuItem("Shooter Game/Skills/Grant Or Upgrade Selected Skill")]
    private static void GrantOrUpgradeSelected()
    {
        var scene = Object.FindObjectOfType<SceneContext>();
        if (scene == null) { Debug.LogWarning("Open the gameplay scene and enter Play Mode first."); return; }
        var definition = (SkillDataSO)Selection.activeObject;
        CharacterView owner = scene.Container.Resolve<PlayerManager>().CurrentPlayer;
        SkillManager manager = scene.Container.Resolve<SkillManager>();
        bool changed = manager.GrantOrUpgrade(definition, owner);
        if (manager.TryGetRuntime(definition, owner, out SkillRuntime skill))
            Debug.Log($"{definition.name}: run level {skill.RunLevel}/{definition.MaxLevel}, changed={changed}.");
    }

    [MenuItem("Shooter Game/Skills/Grant Or Upgrade Selected Skill", true)]
    private static bool CanGrant() => Application.isPlaying && Selection.activeObject is SkillDataSO;

    #region UI Flow Inspection

    // Import lại asset đúng đường dẫn sau khi chuyển folder, giữ nguyên GUID để không mất liên kết scene.
    [MenuItem("Shooter Game/UI/Reimport UIManager Script")]
    private static void ReimportUIManagerScript()
    {
        const string path = "Assets/_Project/Scripts/UI/Core/UIManager.cs"; // Asset hiện tại, không tạo script hoặc đổi GUID.
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
        EditorApplication.delayCall += SelectUIManagerScript;
    }

    // Chọn đúng script trong Project và báo GetClass thực tế thay vì chỉ dựa vào kết quả compiler.
    private static void SelectUIManagerScript()
    {
        const string path = "Assets/_Project/Scripts/UI/Core/UIManager.cs"; // Cùng đường dẫn với asset vừa được import lại.
        MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(path); // MonoScript phải resolve được class để Unity cho phép Add Component.
        Selection.activeObject = script;
        EditorGUIUtility.PingObject(script);
        if (script == null || script.GetClass() != typeof(UIManager))
            Debug.LogError("UIManager MonoScript did not resolve. Check the first compilation/import error in Console.");
        else Debug.Log("UIManager script resolved. Add Component > Shooter Game > UI > UI Manager, or drag the selected script onto Canvas.");
    }

    // Khôi phục component/reference trong scene đang mở nếu Editor đã giữ một bản scene cũ sau khi chuyển folder.
    [MenuItem("Shooter Game/UI/Repair UIManager On Canvas")]
    private static void RepairUIManagerOnCanvas()
    {
        if (Application.isPlaying)
        { Debug.LogWarning("Exit Play Mode before repairing scene references."); return; }
        foreach (SkillChoiceController choices in Resources.FindObjectsOfTypeAll<SkillChoiceController>())
        {
            if (choices.gameObject.scene.path != "Assets/_Project/Scenes/SampleScene.unity") continue;
            Canvas canvas = choices.GetComponentInParent<Canvas>(true); // Chỉ sửa Canvas chứa controller chọn skill của gameplay scene.
            if (canvas == null) continue;
            UIManager manager = canvas.GetComponent<UIManager>(); // Tái sử dụng manager hiện có, không gắn trùng.
            if (manager == null) manager = Undo.AddComponent<UIManager>(canvas.gameObject);
            var data = new SerializedObject(manager); // Chỉ cập nhật reference quản lý UI; không thay đổi slot hoặc gameplay data.
            SerializedProperty panels = data.FindProperty("panels"); // Đăng ký tất cả controller, bao gồm gameplay, không tách danh sách gameplay.
            foreach (UIPanelController controller in canvas.GetComponentsInChildren<UIPanelController>(true))
            {
                bool registered = false; // Giữ reference cũ và tránh thêm controller hai lần.
                for (int i = 0; i < panels.arraySize; i++)
                    if (panels.GetArrayElementAtIndex(i).objectReferenceValue == controller) registered = true;
                if (registered) continue;
                int index = panels.arraySize; // Thêm vào cuối, không thay thứ tự các panel đã được cấu hình.
                panels.InsertArrayElementAtIndex(index);
                panels.GetArrayElementAtIndex(index).objectReferenceValue = controller;
            }
            SerializedProperty routes = data.FindProperty("statePanels"); // Chỉ tạo mapping Playing còn thiếu, không chọn defaultPanel.
            bool hasPlaying = false;
            for (int i = 0; i < routes.arraySize; i++)
                if (routes.GetArrayElementAtIndex(i).FindPropertyRelative("state").enumValueIndex == (int)GameState.Playing) hasPlaying = true;
            if (!hasPlaying)
            {
                int index = routes.arraySize;
                routes.InsertArrayElementAtIndex(index);
                routes.GetArrayElementAtIndex(index).FindPropertyRelative("state").enumValueIndex = (int)GameState.Playing;
                routes.GetArrayElementAtIndex(index).FindPropertyRelative("panel").objectReferenceValue = canvas.GetComponentInChildren<GameplayController>(true);
            }
            data.ApplyModifiedProperties();
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
            Selection.activeGameObject = canvas.gameObject;
            Debug.Log("UIManager attached/configured on Canvas. Review its references and save the scene.");
            return;
        }
        Debug.LogWarning("Open SampleScene in the main stage before repairing its UIManager.");
    }

    // Đọc object thực sự trong scene đang chạy, giúp phân biệt reference trong Editor với file scene trên đĩa.
    [MenuItem("Shooter Game/UI/Inspect Level Up Flow")]
    private static void InspectLevelUpFlow()
    {
        var report = new System.Text.StringBuilder(); // Chỉ ghi Console, không tạo file hoặc sửa dữ liệu scene.
        report.AppendLine($"[LevelUp UI] playing={Application.isPlaying}");
        MonoScript managerScript = AssetDatabase.LoadAssetAtPath<MonoScript>("Assets/_Project/Scripts/UI/Core/UIManager.cs"); // Trạng thái import của script trong Editor hiện tại.
        report.AppendLine("UIManager MonoScript class=" + (managerScript == null ? "ASSET MISSING" : managerScript.GetClass()?.FullName ?? "UNRESOLVED"));
        int managers = 0; // Phải có UIManager hoạt động để nhận yêu cầu mở panel từ controller.
        foreach (UIManager manager in Resources.FindObjectsOfTypeAll<UIManager>())
        {
            if (!manager.gameObject.scene.IsValid()) continue;
            managers++;
            var data = new SerializedObject(manager); // Reference đang nạp trong Editor có thể khác scene trên đĩa chưa reload.
            report.AppendLine($"Manager scene={manager.gameObject.scene.path}, enabled={manager.isActiveAndEnabled}, " +
                $"registeredPanels={data.FindProperty("panels").arraySize}, active={manager.ActivePanel}");
        }
        report.AppendLine($"Manager count={managers}");
        foreach (SkillChoiceController controller in Resources.FindObjectsOfTypeAll<SkillChoiceController>())
        {
            if (!controller.gameObject.scene.IsValid()) continue;
            var data = new SerializedObject(controller); // Kiểm tra panel gắn trên controller của scene thực tế.
            var panel = data.FindProperty("levelUpPanel").objectReferenceValue as SkillChoiceUI;
            report.AppendLine($"Controller enabled={controller.isActiveAndEnabled}, pending={controller.PendingSkillChoices}, " +
                $"requested={controller.WantsVisible}, panel={panel}, panelActive={panel != null && panel.gameObject.activeInHierarchy}");
        }
        Debug.Log(report.ToString());
    }

    // Lấy singleton của scene hiện tại; chỉ dùng để thử luồng khi các màn Home/kết quả chưa được làm.
    private static GameManager CurrentGame() => Object.FindObjectOfType<SceneContext>()?.Container.Resolve<GameManager>();

    // Thử nút Play/chơi lại khi chưa có Home/kết quả UI.
    [MenuItem("Shooter Game/Game Flow/Start Run")]
    private static void StartRun() { if (Application.isPlaying) CurrentGame()?.StartRun(); }

    // Giả lập điều kiện thắng, vẫn đi qua guard/state của GameManager.
    [MenuItem("Shooter Game/Game Flow/Win Run")]
    private static void WinRun() { if (Application.isPlaying) CurrentGame()?.WinRun(); }

    // Giả lập kết thúc thua mà không phải can thiệp trực tiếp HP/scene.
    [MenuItem("Shooter Game/Game Flow/Lose Run")]
    private static void LoseRun() { if (Application.isPlaying) CurrentGame()?.LoseRun(); }

    // Thử rời lượt hiện tại, dọn world và chuyển Home trong cùng scene.
    [MenuItem("Shooter Game/Game Flow/Return Home")]
    private static void ReturnHome() { if (Application.isPlaying) CurrentGame()?.ReturnHome(); }
    #endregion
}
