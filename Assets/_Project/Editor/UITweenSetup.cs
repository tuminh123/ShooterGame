using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Gắn <see cref="ButtonClickTween"/> cho mọi Button trong scene đang mở và trong prefab của dự án,
/// để mọi nút có cùng cảm giác bấm như nút Play. Chạy lại an toàn: nút đã có thì bỏ qua.
/// </summary>
/// <remarks>
/// Bỏ qua nút đã có <see cref="PopupTweenBase"/> trên cùng object (ví dụ nút Play tự điều khiển tween của nó)
/// và nút dùng transition Animation (Animator sẽ ghi đè scale). Chỉ quét prefab trong Assets/_Project, không sửa asset bên thứ ba.
/// </remarks>
public static class UITweenSetup
{
    private const string PrefabRoot = "Assets/_Project/Prefabs";

    /// <summary>Gắn tween bấm cho mọi nút còn thiếu và báo số lượng trong Console.</summary>
    [MenuItem("Tools/Shooter Game/UI/Add Click Tween To All Buttons")]
    public static void AddClickTweenToAllButtons()
    {
        int sceneCount = 0;
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded) continue;
            int added = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
                added += AddMissing(root.GetComponentsInChildren<Button>(true));
            if (added > 0) EditorSceneManager.MarkSceneDirty(scene);
            sceneCount += added;
        }

        int prefabCount = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { PrefabRoot }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                int added = AddMissing(contents.GetComponentsInChildren<Button>(true));
                if (added > 0) PrefabUtility.SaveAsPrefabAsset(contents, path);
                prefabCount += added;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }
        Debug.Log($"[UITweenSetup] Đã gắn ButtonClickTween: {sceneCount} nút trong scene, {prefabCount} nút trong prefab. Nhớ Save scene.");
    }

    private static int AddMissing(IEnumerable<Button> buttons)
    {
        int added = 0;
        foreach (Button button in buttons)
        {
            if (button.GetComponent<ButtonClickTween>() != null) continue;
            if (button.GetComponent<PopupTweenBase>() != null) continue;
            if (button.transition == Selectable.Transition.Animation) continue;
            Undo.AddComponent<ButtonClickTween>(button.gameObject);
            added++;
        }
        return added;
    }
}
