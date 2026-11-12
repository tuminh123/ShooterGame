using System;
using UnityEditor;
using UnityEngine;

/// <summary>Wave sở hữu danh sách lượt; mỗi lượt có Formation/Path/thông số riêng.</summary>
[CustomEditor(typeof(WaveConfigSO))]
public sealed class WaveConfigEditor : Editor
{
    #region Wave Inspector

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"));
        }

        SerializedProperty condition = serializedObject.FindProperty("advanceCondition");
        EditorGUILayout.PropertyField(condition);
        EditorGUILayout.HelpBox("Mỗi phần tử trong danh sách đội hình là một lần spawn riêng. Danh sách rỗng: toàn Wave spawn thường. Boss cấu hình riêng trong StageDataSO.", MessageType.Info);
        SerializedProperty waves = serializedObject.FindProperty("waves");

        for (int i = 0; i < waves.arraySize; i++)
        {
            SerializedProperty wave = waves.GetArrayElementAtIndex(i);
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.BeginHorizontal();
            wave.isExpanded = EditorGUILayout.Foldout(wave.isExpanded, $"Wave {i + 1}", true);
            bool remove = GUILayout.Button(new GUIContent("Xóa", "Xóa Wave, có thể Undo."), GUILayout.Width(50));
            EditorGUILayout.EndHorizontal();

            if (remove)
            {
                waves.DeleteArrayElementAtIndex(i);
                EditorGUILayout.EndVertical();
                break;
            }

            if (wave.isExpanded)
            {
                if ((WaveAdvanceCondition)condition.enumValueIndex == WaveAdvanceCondition.AfterDuration)
                {
                    EditorGUILayout.PropertyField(wave.FindPropertyRelative("delayAfterSpawn"));
                }

                EditorGUILayout.PropertyField(wave.FindPropertyRelative("spawnCount"));
                EditorGUILayout.PropertyField(wave.FindPropertyRelative("spawnInterval"));
                EditorGUILayout.PropertyField(wave.FindPropertyRelative("enemies"), true);
                DrawEntries(wave, i);
                DrawSchedule(wave);
            }

            EditorGUILayout.EndVertical();
        }

        if (GUILayout.Button(new GUIContent("Thêm Wave", "Thêm Wave cuối danh sách; sao chép Wave cuối nếu đã có.")))
        {
            waves.arraySize++;
            waves.GetArrayElementAtIndex(waves.arraySize - 1).isExpanded = true;
        }

        serializedObject.ApplyModifiedProperties();

        try
        {
            ((WaveConfigSO)target).Validate();
        }
        catch (Exception exception)
        {
            EditorGUILayout.HelpBox(exception.Message, MessageType.Error);
        }
    }

    #endregion

    #region Ordered Formation Entries

    private void DrawEntries(SerializedProperty wave, int waveIndex)
    {
        SerializedProperty entries = wave.FindPropertyRelative("formationSpawns");
        EditorGUILayout.Space();
        EditorGUILayout.LabelField($"Lượt spawn đội hình ({entries.arraySize})", EditorStyles.boldLabel);

        for (int i = 0; i < entries.arraySize; i++)
        {
            SerializedProperty entry = entries.GetArrayElementAtIndex(i);
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.BeginHorizontal();
            entry.isExpanded = EditorGUILayout.Foldout(entry.isExpanded, $"Lượt {i + 1}", true);
            bool remove = GUILayout.Button("Xóa lượt", GUILayout.Width(75));
            EditorGUILayout.EndHorizontal();

            if (remove)
            {
                entries.DeleteArrayElementAtIndex(i);
                EditorGUILayout.EndVertical();
                break;
            }

            if (entry.isExpanded)
            {
                EditorGUILayout.PropertyField(entry.FindPropertyRelative("formation"));
                EditorGUILayout.PropertyField(entry.FindPropertyRelative("path"));
                EditorGUILayout.PropertyField(entry.FindPropertyRelative("settings"), true);

                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(i == 0))
                    {
                        if (GUILayout.Button(new GUIContent("Lên", "Đưa lượt này lên trước, giữ nguyên hình/đường/thông số.")))
                        {
                            entries.MoveArrayElement(i, i - 1);
                        }
                    }

                    using (new EditorGUI.DisabledScope(i == entries.arraySize - 1))
                    {
                        if (GUILayout.Button(new GUIContent("Xuống", "Đưa lượt này xuống sau.")))
                        {
                            entries.MoveArrayElement(i, i + 1);
                        }
                    }

                    if (GUILayout.Button(new GUIContent("Mở Workbench", "Chỉnh/xem riêng lượt này; gán từ tool chỉ thay lượt đã chọn.")))
                    {
                        serializedObject.ApplyModifiedProperties();
                        EnemyFormationWorkbench.Open((WaveConfigSO)target, waveIndex, i);
                    }
                }
            }

            EditorGUILayout.EndVertical();
        }

        if (GUILayout.Button(new GUIContent("Thêm lượt đội hình", "Thêm một lần spawn ở cuối; sao chép lượt cuối nếu có để chỉnh sang hình/đường khác.")))
        {
            bool first = entries.arraySize == 0;
            entries.arraySize++;
            SerializedProperty added = entries.GetArrayElementAtIndex(entries.arraySize - 1);
            added.isExpanded = true;

            if (first)
            {
                // Đặt rõ mặc định cho phần tử đầu; không phụ thuộc Unity khởi tạo class bằng zero hay initializer.
                var defaults = new EnemyFormationSettings();
                SerializedProperty settings = added.FindPropertyRelative("settings");
                settings.FindPropertyRelative("passes").intValue = defaults.passes;
                settings.FindPropertyRelative("followSpacing").floatValue = defaults.followSpacing;
                settings.FindPropertyRelative("knockbackDuration").floatValue = defaults.knockbackDuration;
                settings.FindPropertyRelative("stunDuration").floatValue = defaults.stunDuration;
            }
        }
    }

    #endregion

    #region Shared Budget Display

    private static void DrawSchedule(SerializedProperty wave)
    {
        SerializedProperty entries = wave.FindPropertyRelative("formationSpawns");

        if (entries.arraySize == 0)
        {
            return;
        }

        SerializedProperty after = wave.FindPropertyRelative("formationSpawnAfter");
        EditorGUILayout.PropertyField(after, new GUIContent("Spawn After", after.tooltip));

        try
        {
            // Cùng class tính ngân sách với runtime; không có công thức khác trong Editor.
            var schedule = new WaveConfigSO.Wave
            {
                spawnCount = wave.FindPropertyRelative("spawnCount").intValue,
                formationSpawns = new EnemyFormationSpawn[entries.arraySize]
            };

            for (int i = 0; i < entries.arraySize; i++)
            {
                SerializedProperty entry = entries.GetArrayElementAtIndex(i);
                schedule.formationSpawns[i] = new EnemyFormationSpawn
                {
                    formation = (EnemyFormationSO)entry.FindPropertyRelative("formation").objectReferenceValue,
                    path = (EnemyPathSO)entry.FindPropertyRelative("path").objectReferenceValue
                };
            }

            WaveConfigSO.FormationSpawnPlan plan = schedule.GetFormationSpawnPlan();
            EditorGUILayout.LabelField("Spawn After tối đa", plan.MaxSpawnAfter.ToString());

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Đầu Wave"))
                {
                    after.intValue = 0;
                }

                if (GUILayout.Button("Cuối Wave"))
                {
                    after.intValue = plan.MaxSpawnAfter;
                }
            }

            schedule.formationSpawnAfter = after.intValue;
            plan = schedule.GetFormationSpawnPlan();
            EditorGUILayout.HelpBox($"{plan.Before} con thường → {plan.GroupCount} lượt đội hình ({plan.FormationEnemyCount} con) → {plan.After} con thường.\nCác lượt chạy theo thứ tự danh sách, cách nhau theo Spawn Interval.", MessageType.Info);
        }
        catch (Exception exception)
        {
            EditorGUILayout.HelpBox(exception.Message, MessageType.Error);
        }
    }

    #endregion
}
