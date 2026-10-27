using UnityEditor;

// * Thêm mẫu SerializeReference trong Inspector, không phải chỉnh YAML bằng tay.
[CustomEditor(typeof(CharacterDataSO))]
public sealed class CharacterSkillEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.Space();
        if (UnityEngine.GUILayout.Button("Add Projectile Skill"))
        {
            serializedObject.Update();
            SerializedProperty skills = serializedObject.FindProperty("activeSkills");
            int index = skills.arraySize;
            skills.arraySize++;
            skills.GetArrayElementAtIndex(index).managedReferenceValue = new ProjectileSkill();
            serializedObject.ApplyModifiedProperties();
        }
        EditorGUILayout.HelpBox("Each template creates a separate skill runtime per spawn. Assign a projectile prefab before playing.", MessageType.Info);
    }
}
