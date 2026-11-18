using System;
using UnityEditor;
using UnityEngine;

/// <summary>Inspector danh sách asset spawn trực tiếp và phần thưởng của Stage.</summary>
[CustomEditor(typeof(StageDataSO))]
public sealed class StageDataEditor : Editor
{
    #region Inspector Stage

    /// <summary>Vẽ cấu hình, chặn asset sai interface và áp dụng chỉnh sửa có Undo.</summary>
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        using (DataInspectorGUI.Section("STAGE INFO & MAP", "Stage ID là khóa save ổn định. Map Key phân biệt hoa/thường và phải khớp Map Database.", DataInspectorTone.Info))
        {
            Field("stageId"); Field("displayName"); Field("mapKey");
        }
        using (DataInspectorGUI.Section("SPAWN DATA", "Gán Waves, Pattern hoặc Boss Spawn. Waves tự quản lý chuỗi; Pattern và Elite dùng lịch riêng tính từ đầu Stage.", DataInspectorTone.Spawn))
            DrawSpawnData();
        using (DataInspectorGUI.Section("FIRST CLEAR REWARDS", "Chỉ nhận một lần khi thắng màn lần đầu; cộng thêm với phần thưởng thắng bên dưới.", DataInspectorTone.Rewards))
            Field("firstClearStageRewards");
        using (DataInspectorGUI.Section("VICTORY / DEFEAT REWARDS", "Thưởng theo kết quả mỗi lượt. Các dòng cùng loại tiền được cộng lại; số lượng phải từ 0 trở lên.", DataInspectorTone.Rewards))
        {
            Field("victoryRewards"); Field("defeatRewards");
        }
        serializedObject.ApplyModifiedProperties();
        DataInspectorGUI.Validate(() => ((StageDataSO)target).Validate());
    }

    // Bộ chọn giữ kiểm tra IStageSpawnData; không cho gán nhầm DataSO ngoài hệ spawn.
    private void DrawSpawnData()
    {
        var assets = serializedObject.FindProperty("spawnData");
        for (int i = 0; i < assets.arraySize; i++)
        {
            var item = assets.GetArrayElementAtIndex(i);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUI.BeginChangeCheck();
                var selected = EditorGUILayout.ObjectField(new GUIContent($"Data {i + 1}", assets.tooltip), item.objectReferenceValue, typeof(ScriptableObject), false);
                if (EditorGUI.EndChangeCheck())
                {
                    if (selected == null || selected is IStageSpawnData) item.objectReferenceValue = selected;
                    else Debug.LogWarning("Asset phải triển khai IStageSpawnData.", selected);
                }
                if (GUILayout.Button("Xóa", GUILayout.Width(50)))
                {
                    int oldSize = assets.arraySize;
                    assets.DeleteArrayElementAtIndex(i);
                    if (assets.arraySize == oldSize) assets.DeleteArrayElementAtIndex(i);
                    break;
                }
            }
        }
        if (GUILayout.Button("Thêm Spawn Data"))
        {
            assets.arraySize++;
            assets.GetArrayElementAtIndex(assets.arraySize - 1).objectReferenceValue = null;
        }
    }

    /// <summary>Vẽ một thuộc tính serialized theo tên, gồm cả các phần tử con.</summary>
    private void Field(string name) => DataInspectorGUI.Field(serializedObject, name);
    #endregion
}
