using UnityEditor;
using UnityEngine;

// Nối công cụ authoring vào Inspector spawner; không thêm dữ liệu preview vào scene/runtime.
[CustomEditor(typeof(EnemySpawnManager))]
public sealed class EnemySpawnManagerEditor : Editor
{
    private WaveConfigSO previewWaves;
    private int previewWave;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Formation / Path Preview", EditorStyles.boldLabel);
        if (previewWaves == null)
        {
            foreach (GameSceneInstaller installer in Object.FindObjectsByType<GameSceneInstaller>(FindObjectsSortMode.None))
            {
                if (installer.gameObject.scene != ((EnemySpawnManager)target).gameObject.scene)
                {
                    continue;
                }
                var data = new SerializedObject(installer).FindProperty("stageData").objectReferenceValue as StageDataSO;
                if (data != null)
                {
                    previewWaves = data.Waves;
                    break;
                }
            }
        }
        previewWaves = (WaveConfigSO)EditorGUILayout.ObjectField(new GUIContent("Wave DataSO", "Asset wave dùng để mở bản xem thử. Tool tự tìm từ Stage của scene nếu có; bạn có thể chọn asset khác để kiểm tra."), previewWaves, typeof(WaveConfigSO), false);
        if (previewWaves != null && previewWaves.Count > 0)
        {
            previewWave = EditorGUILayout.IntSlider(new GUIContent("Wave", "Số thứ tự wave sẽ mở trong Workbench, tính từ 1. Không thay wave đang chơi trong game."), previewWave + 1, 1, previewWaves.Count) - 1;
        }
        if (GUILayout.Button(new GUIContent("Edit Path / Preview Formations", "Mở Workbench cho wave đã chọn, lấy một điểm cạnh vùng spawn làm gốc minh họa. Dùng Frame in Scene, rồi Preview để xem đường/đội hình.")))
        {
            var window = EnemyFormationWorkbench.Open(previewWaves, previewWave);
            window.SetSpawnSource((EnemySpawnManager)target);
        }
    }
}
