using UnityEditor;
using UnityEngine;

/// <summary>Inspector spawner kèm lối mở Workbench cho asset Pattern.</summary>
[CustomEditor(typeof(EnemySpawnManager))]
public sealed class EnemySpawnManagerEditor : Editor
{
    #region Cấu hình xem trước và Inspector

    private EnemyPatternSO preview;
    /// <summary>Vẽ cấu hình component và cho mở Workbench khi đã chọn Pattern.</summary>
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.Space();
        preview = (EnemyPatternSO)EditorGUILayout.ObjectField("Pattern Preview", preview, typeof(EnemyPatternSO), false);
        using (new EditorGUI.DisabledScope(preview == null))
            if (GUILayout.Button("Open Pattern Workbench")) EnemyFormationWorkbench.Open(preview);
    }
    #endregion
}
