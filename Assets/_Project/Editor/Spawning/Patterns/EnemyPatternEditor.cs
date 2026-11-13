using System;
using UnityEditor;
using UnityEngine;

/// <summary>Inspector Pattern chỉ hiện thông số phù hợp với kiểu đã chọn.</summary>
[CustomEditor(typeof(EnemyPatternSO))]
[CanEditMultipleObjects]
public sealed class EnemyPatternEditor : Editor
{
    #region Inspector Pattern

    /// <summary>Vẽ cấu hình chung/riêng, kiểm tra asset và mở Workbench cho lựa chọn đơn.</summary>
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        Field("type"); Field("delay"); Field("enemies"); Field("enemyLevel");
        Field("knockbackDistance"); Field("knockbackDuration");
        var type = serializedObject.FindProperty("type");
        if (type.hasMultipleDifferentValues)
            EditorGUILayout.HelpBox("Chọn cùng Type để chỉnh thông số riêng.", MessageType.Info);
        else
        {
            var mode = (EnemyPatternType)type.enumValueIndex;
            if (mode != EnemyPatternType.SequentialPath) Field("formation");
            if (mode != EnemyPatternType.Area) { Field("path"); Field("speed"); Field("passes"); }
            if (mode == EnemyPatternType.SequentialPath) { Field("enemyCount"); Field("spawnInterval"); }
            if (mode == EnemyPatternType.Area) { Field("enemyCount"); Field("shrinkDuration"); }
        }
        serializedObject.ApplyModifiedProperties();
        foreach (var item in targets)
        {
            try { ((EnemyPatternSO)item).Validate(); }
            catch (Exception e) { EditorGUILayout.HelpBox(e.Message, MessageType.Error); }
        }
        using (new EditorGUI.DisabledScope(targets.Length != 1))
            if (GUILayout.Button("Open Formation / Path / Area Workbench")) EnemyFormationWorkbench.Open((EnemyPatternSO)target);
    }
    /// <summary>Vẽ thuộc tính serialized cùng các trường con của nó.</summary>
    private void Field(string name) => EditorGUILayout.PropertyField(serializedObject.FindProperty(name), true);
    #endregion
}
