using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Select stateless strategy types without hand-editing SerializeReference YAML.
[CustomEditor(typeof(SkillDataSO), true)]
public sealed class CharacterSkillEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawPropertiesExcluding(serializedObject, "m_Script", "behavior");
        Type[] behaviors = TypeCache.GetTypesDerivedFrom<ISkill>()
            .Where(type => !type.IsAbstract && !type.IsInterface && type.IsSerializable &&
                           type.GetConstructor(Type.EmptyTypes) != null)
            .OrderBy(type => type.Name).ToArray();
        StrategySelector.Draw(serializedObject.FindProperty("behavior"), behaviors);
        serializedObject.ApplyModifiedProperties();
        try { ((SkillDataSO)target).Validate(); }
        catch (Exception exception) { EditorGUILayout.HelpBox(exception.Message, MessageType.Error); }
    }
}

[CustomPropertyDrawer(typeof(ShooterTypeAttribute))]
public sealed class ShooterTypeDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        Type[] types = TypeCache.GetTypesDerivedFrom<IShooter>()
            .Where(type => !type.IsAbstract && !type.IsInterface)
            .OrderBy(type => type.Name).ToArray();
        string[] names = new[] { "(Select shooter)" }.Concat(types.Select(type => type.Name)).ToArray();
        int current = Array.FindIndex(types, type => type.FullName == property.stringValue) + 1;
        EditorGUI.BeginProperty(position, label, property);
        int selected = EditorGUI.Popup(position, label.text, current, names);
        if (selected != current)
            property.stringValue = selected == 0 ? string.Empty : types[selected - 1].FullName;
        EditorGUI.EndProperty();
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
        if (property.managedReferenceValue != null)
        {
            SerializedProperty child = property.Copy();
            SerializedProperty end = property.GetEndProperty();
            bool enterChildren = true;
            while (child.NextVisible(enterChildren) && !SerializedProperty.EqualContents(child, end))
            {
                EditorGUILayout.PropertyField(child, true);
                enterChildren = false;
            }
        }
    }
}
