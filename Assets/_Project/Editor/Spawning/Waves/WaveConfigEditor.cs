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
        using (DataInspectorGUI.Section("WAVE PROGRESSION", "Các wave chạy lần lượt. Cấp Enemy bằng số thứ tự wave; quota không bao gồm quái Pattern hoặc Boss.", DataInspectorTone.Info))
            EditorGUILayout.PropertyField(condition);
        var waves = serializedObject.FindProperty("waves");
        using (DataInspectorGUI.Section($"WAVE SEQUENCE · {waves.arraySize} WAVES", "Sequential sinh theo nhịp; Simultaneous sinh cả wave cùng một lượt. Wave cuối cần dọn sạch quái trước khi hoàn tất.", DataInspectorTone.Spawn))
            DrawWaves(waves, condition);
        serializedObject.ApplyModifiedProperties();
        DataInspectorGUI.Validate(() => ((WaveConfigSO)target).Validate());
    }

    #endregion

    #region Wave Cards & Editing

    private void DrawWaves(SerializedProperty waves, SerializedProperty condition)
    {
        for (int i = 0; i < waves.arraySize; i++)
        {
            var wave = waves.GetArrayElementAtIndex(i);
            using (new EditorGUILayout.VerticalScope("box"))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField($"WAVE {i + 1} · CẤP ENEMY {i + 1}", EditorStyles.boldLabel);
                    if (GUILayout.Button("Xóa", GUILayout.Width(50))) { waves.DeleteArrayElementAtIndex(i); break; }
                }
                EditorGUILayout.PropertyField(wave.FindPropertyRelative("spawnCount"));
                var mode = wave.FindPropertyRelative("spawnMode");
                EditorGUILayout.PropertyField(mode);
                if (mode.enumValueIndex == (int)WaveSpawnMode.Sequential)
                    EditorGUILayout.PropertyField(wave.FindPropertyRelative("spawnInterval"),
                        new GUIContent("Khoảng cách spawn (giây)", wave.FindPropertyRelative("spawnInterval").tooltip));
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
    }
    #endregion
}
