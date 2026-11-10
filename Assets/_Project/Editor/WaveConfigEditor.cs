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
        EditorGUILayout.HelpBox("After the last wave, repeat that last wave. Enemy level increases after every completed duration or cleared batch.", MessageType.Info);
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
                    EditorGUILayout.PropertyField(wave.FindPropertyRelative(timed ? "duration" : "spawnCount"));
                    EditorGUILayout.PropertyField(wave.FindPropertyRelative("spawnInterval"));
                    EditorGUILayout.PropertyField(wave.FindPropertyRelative("maxAliveEnemies"));
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
                    added.FindPropertyRelative("duration").floatValue = 30f;
                    added.FindPropertyRelative("spawnCount").intValue = 20;
                    added.FindPropertyRelative("spawnInterval").floatValue = 0.5f;
                    added.FindPropertyRelative("maxAliveEnemies").intValue = 20;
                }
            }
        }
        serializedObject.ApplyModifiedProperties();
        try { ((WaveConfigSO)target).Validate(); }
        catch (System.Exception exception) { EditorGUILayout.HelpBox(exception.Message, MessageType.Error); }
    }
}
