using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(WaveConfigSO))]
public sealed class WaveConfigEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        using (new EditorGUI.DisabledScope(true))
            EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"));
        SerializedProperty condition = serializedObject.FindProperty("advanceCondition");
        EditorGUILayout.PropertyField(condition);
        bool timed = (WaveAdvanceCondition)condition.enumValueIndex == WaveAdvanceCondition.AfterDuration;
        EditorGUILayout.HelpBox("Each wave spawns Enemy Count normal enemies, one per Spawn Interval. Stage spawns its configured bosses afterwards. Timed mode waits Delay After Spawn before the next wave; clear mode waits until enemies and bosses die. The final wave always requires every enemy and boss to be cleared. Bosses and rewards are configured on StageDataSO.", MessageType.Info);
        SerializedProperty waves = serializedObject.FindProperty("waves");
        waves.isExpanded = EditorGUILayout.Foldout(waves.isExpanded, $"Waves ({waves.arraySize})", true);
        if (waves.isExpanded)
        {
            for (int i = 0; i < waves.arraySize; i++)
            {
                SerializedProperty wave = waves.GetArrayElementAtIndex(i);
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.BeginHorizontal();
                wave.isExpanded = EditorGUILayout.Foldout(wave.isExpanded, $"Wave {i + 1}", true);
                bool remove = GUILayout.Button("Remove", GUILayout.Width(65));
                EditorGUILayout.EndHorizontal();
                if (remove)
                {
                    waves.DeleteArrayElementAtIndex(i);
                    EditorGUILayout.EndVertical();
                    break;
                }
                if (wave.isExpanded)
                {
                    if (timed) EditorGUILayout.PropertyField(wave.FindPropertyRelative("delayAfterSpawn"));
                    EditorGUILayout.PropertyField(wave.FindPropertyRelative("spawnCount"), new GUIContent("Enemy Count", "Total enemies to spawn in this wave."));
                    EditorGUILayout.PropertyField(wave.FindPropertyRelative("spawnInterval"));
                    EditorGUILayout.PropertyField(wave.FindPropertyRelative("enemies"), true);
                }
                EditorGUILayout.EndVertical();
            }
            if (GUILayout.Button("Add Wave"))
            {
                waves.arraySize++;
                SerializedProperty added = waves.GetArrayElementAtIndex(waves.arraySize - 1);
                added.isExpanded = true;
                if (waves.arraySize == 1)
                {
                    added.FindPropertyRelative("delayAfterSpawn").floatValue = 20f;
                    added.FindPropertyRelative("spawnCount").intValue = 20;
                    added.FindPropertyRelative("spawnInterval").floatValue = 0.5f;
                }
            }
        }
        serializedObject.ApplyModifiedProperties();
        try { ((WaveConfigSO)target).Validate(); }
        catch (System.Exception exception) { EditorGUILayout.HelpBox(exception.Message, MessageType.Error); }
    }
}
