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
        Field("stageId"); Field("displayName"); Field("mapKey");
        EditorGUILayout.LabelField("Spawn Data", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Gán trực tiếp bộ Waves, Pattern hoặc Boss Spawn. Mỗi bộ tự quản lý cách spawn của nó.", MessageType.Info);
        var assets = serializedObject.FindProperty("spawnData");
        for (int i = 0; i < assets.arraySize; i++)
        {
            var item = assets.GetArrayElementAtIndex(i);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUI.BeginChangeCheck();
                var selected = EditorGUILayout.ObjectField($"Data {i + 1}", item.objectReferenceValue, typeof(ScriptableObject), false);
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
        EditorGUILayout.Space();
        Field("firstClearStageRewards"); Field("victoryRewards"); Field("defeatRewards");
        serializedObject.ApplyModifiedProperties();
        try { ((StageDataSO)target).Validate(); }
        catch (Exception e) { EditorGUILayout.HelpBox(e.Message, MessageType.Error); }
    }

    /// <summary>Vẽ một thuộc tính serialized theo tên, gồm cả các phần tử con.</summary>
    private void Field(string name) => EditorGUILayout.PropertyField(serializedObject.FindProperty(name), true);
    #endregion
}
