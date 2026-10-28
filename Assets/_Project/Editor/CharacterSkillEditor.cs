using System;
using UnityEditor;

// Select stateless strategy types without hand-editing SerializeReference YAML.
[CustomEditor(typeof(SkillDataSO), true)]
public sealed class CharacterSkillEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawPropertiesExcluding(serializedObject, "m_Script", "skill");
        if (target is ProjectileSkillDataSO)
            StrategySelector.Draw(serializedObject.FindProperty("skill"), typeof(ProjectileSkill));
        else if (target is StatBonusSkillDataSO)
            StrategySelector.Draw(serializedObject.FindProperty("skill"), typeof(StatBonusSkill));
        serializedObject.ApplyModifiedProperties();
    }
}

[CustomEditor(typeof(ShootingPatternDataSO))]
public sealed class ShootingPatternEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawPropertiesExcluding(serializedObject, "m_Script", "pattern");
        StrategySelector.Draw(serializedObject.FindProperty("pattern"),
            typeof(StraightShootingPattern), typeof(CircularShootingPattern), typeof(SpiralShootingPattern));
        serializedObject.ApplyModifiedProperties();
    }
}

[CustomEditor(typeof(ProjectileDataSO))]
public sealed class ProjectileDataEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawPropertiesExcluding(serializedObject, "m_Script", "flight", "hit");
        StrategySelector.Draw(serializedObject.FindProperty("flight"),
            typeof(StraightProjectileFlight), typeof(HomingProjectileFlight));
        StrategySelector.Draw(serializedObject.FindProperty("hit"), typeof(SingleTargetProjectileHit));
        serializedObject.ApplyModifiedProperties();
    }
}

internal static class StrategySelector
{
    public static void Draw(SerializedProperty property, params Type[] types)
    {
        var names = new string[types.Length + 1];
        names[0] = "(Select strategy)";
        int current = 0;
        Type actual = property.managedReferenceValue?.GetType();
        for (int i = 0; i < types.Length; i++)
        {
            names[i + 1] = types[i].Name;
            if (actual == types[i]) current = i + 1;
        }
        int selected = EditorGUILayout.Popup(property.displayName, current, names);
        if (selected != current)
            property.managedReferenceValue = selected == 0 ? null : Activator.CreateInstance(types[selected - 1]);
    }
}
