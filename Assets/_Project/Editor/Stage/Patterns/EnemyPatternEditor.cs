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
        using (DataInspectorGUI.Section("FORMATION GEOMETRY", "Chọn asset hình học chứa Đặc/Rỗng, Shape, Level và kích thước. Cả ba cách spawn đều sinh đúng số slot của asset này.", DataInspectorTone.Visual))
            Field("formation");
        using (DataInspectorGUI.Section("SPAWN MODE & START TIME", "Chỉ chọn cách xuất hiện/chuyển động. Delay là mốc giây từ đầu Stage; không quyết định hình Đặc/Rỗng.", DataInspectorTone.Spawn))
        { Field("type"); Field("delay"); }
        using (DataInspectorGUI.Section("ENEMY ROSTER & LEVEL", "Chọn ngẫu nhiên template trong danh sách; chỉ dùng Enemy thường có visual. Cấp này độc lập số thứ tự wave.", DataInspectorTone.Stats))
        { Field("enemies"); Field("enemyLevel"); }
        using (DataInspectorGUI.Section("CONTACT KNOCKBACK", "Chỉ thành viên chạm Player bị đẩy lùi. Với Path, thành viên tách ra và đi nốt hành trình.", DataInspectorTone.Advanced))
        { Field("knockbackDistance"); Field("knockbackDuration"); }
        var type = serializedObject.FindProperty("type");
        if (type.hasMultipleDifferentValues)
            EditorGUILayout.HelpBox("Chọn cùng Type để chỉnh thông số riêng.", MessageType.Info);
        else
        {
            var mode = (EnemyPatternType)type.enumValueIndex;
            if (mode != EnemyPatternType.Area)
                using (DataInspectorGUI.Section("PATH MOVEMENT", "Path bám camera. Speed dùng world unit/giây; Passes = 1 đi A → B, 2 đi A → B → A.", DataInspectorTone.Spawn))
                { Field("path"); Field("speed"); Field("passes"); }
            if (mode == EnemyPatternType.SequentialPath)
                using (DataInspectorGUI.Section("SEQUENTIAL DEPLOYMENT", "Sinh lần lượt từng slot tại đầu Path cộng offset hình học; mỗi thành viên đi Path riêng từ lúc được sinh.", DataInspectorTone.Spawn))
                    Field("spawnInterval");
            if (mode == EnemyPatternType.Area)
                using (DataInspectorGUI.Section("PLAYER AREA & SHRINK", "Đặt đúng hình đã chọn tại Player, Đặc hoặc Rỗng đều được. Thu liên tục/theo đợt chỉ điều khiển tiến trình thu, không đổi hình học.", DataInspectorTone.Spawn))
                {
                    Field("areaShrinkMode"); Field("shrinkDuration");
                    var shrinkMode = serializedObject.FindProperty("areaShrinkMode");
                    if (shrinkMode.hasMultipleDifferentValues || shrinkMode.enumValueIndex == (int)AreaShrinkMode.Burst)
                    { Field("shrinkBurstDuration"); Field("shrinkPauseDuration"); }
                }
        }
        serializedObject.ApplyModifiedProperties();
        foreach (var item in targets)
        {
            try
            {
                var data = (EnemyPatternSO)item;
                data.Validate();
                EditorGUILayout.HelpBox($"Hình {data.formation.FillMode}, Level {data.formation.Level}: {data.Count} slot/quái. Số lượng này dùng cho mọi cách spawn.", MessageType.Info);
            }
            catch (Exception e) { EditorGUILayout.HelpBox(e.Message, MessageType.Error); }
        }
        using (new EditorGUI.DisabledScope(targets.Length != 1))
            if (GUILayout.Button("Test Formation / Path / Area in Scene")) EnemyFormationWorkbench.Open((EnemyPatternSO)target);
    }
    /// <summary>Vẽ thuộc tính serialized cùng các trường con của nó.</summary>
    private void Field(string name) => DataInspectorGUI.Field(serializedObject, name);
    #endregion
}
