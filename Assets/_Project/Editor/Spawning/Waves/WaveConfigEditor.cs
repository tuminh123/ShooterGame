using System;
using UnityEditor;
using UnityEngine;

/// <summary>Inspector chỉnh danh sách wave và điều kiện chuyển wave.</summary>
[CustomEditor(typeof(WaveConfigSO))]
public sealed class WaveConfigEditor : Editor
{
    #region Inspector chuỗi wave

    /// <summary>Vẽ từng wave, xử lý thêm/xóa và chỉ hiện thời gian nghỉ khi có dùng.</summary>
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        var condition = serializedObject.FindProperty("advanceCondition");
        EditorGUILayout.PropertyField(condition);
        var waves = serializedObject.FindProperty("waves");
        for (int i = 0; i < waves.arraySize; i++)
        {
            var wave = waves.GetArrayElementAtIndex(i);
            using (new EditorGUILayout.VerticalScope("box"))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField($"Wave {i + 1}", EditorStyles.boldLabel);
                    if (GUILayout.Button("Xóa", GUILayout.Width(50))) { waves.DeleteArrayElementAtIndex(i); break; }
                }
                EditorGUILayout.PropertyField(wave.FindPropertyRelative("spawnCount"));
                EditorGUILayout.PropertyField(wave.FindPropertyRelative("spawnInterval"));
                EditorGUILayout.PropertyField(wave.FindPropertyRelative("enemies"), true);
                if (i < waves.arraySize - 1 && condition.enumValueIndex == (int)WaveAdvanceCondition.AfterDuration)
                    EditorGUILayout.PropertyField(wave.FindPropertyRelative("delayAfterSpawn"));
            }
        }
        if (GUILayout.Button("Thêm Wave"))
        {
            bool first = waves.arraySize == 0;
            waves.arraySize++;
            if (first)
            {
                var added = waves.GetArrayElementAtIndex(0);
                added.FindPropertyRelative("spawnCount").intValue = 20;
                added.FindPropertyRelative("spawnInterval").floatValue = 0.5f;
                added.FindPropertyRelative("delayAfterSpawn").floatValue = 20f;
            }
        }
        serializedObject.ApplyModifiedProperties();
        try { ((WaveConfigSO)target).Validate(); }
        catch (Exception e) { EditorGUILayout.HelpBox(e.Message, MessageType.Error); }
    }
    #endregion
}
