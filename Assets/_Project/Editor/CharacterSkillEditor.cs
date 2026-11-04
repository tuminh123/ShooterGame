using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(SkillDataSO), true)]
public sealed class CharacterSkillEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawPropertiesExcluding(serializedObject, "m_Script", "behavior");
        StrategySelector.Draw(serializedObject.FindProperty("behavior"), StrategySelector.GetTypes<ISkillData>());
        serializedObject.ApplyModifiedProperties();
        try { ((SkillDataSO)target).Validate(); }
        catch (Exception exception) { EditorGUILayout.HelpBox(exception.Message, MessageType.Error); }
    }
}

// Dùng cùng một bộ chọn cho skill, kiểu bắn và kiểu di chuyển; không cần custom attribute.
internal static class StrategySelector
{
    public static Type[] GetTypes<T>()
        => TypeCache.GetTypesDerivedFrom<T>()
            .Where(type => !type.IsAbstract && !type.IsInterface && type.IsSerializable &&
                           type.GetConstructor(Type.EmptyTypes) != null)
            .OrderBy(type => type.Name).ToArray();

    public static void Draw(SerializedProperty property, params Type[] types)
    {
        var names = new string[types.Length + 1];
        names[0] = "(Select strategy)";
        int current = 0;
        Type actual = property.managedReferenceValue?.GetType();
        for (int index = 0; index < types.Length; index++)
        {
            names[index + 1] = types[index].Name;
            if (actual == types[index]) current = index + 1;
        }
        int selected = EditorGUILayout.Popup(property.displayName, current, names);
        if (selected != current)
            property.managedReferenceValue = selected == 0 ? null : Activator.CreateInstance(types[selected - 1]);
        if (property.managedReferenceValue == null) return;

        SerializedProperty child = property.Copy();
        SerializedProperty end = property.GetEndProperty();
        bool enterChildren = true;
        while (child.NextVisible(enterChildren) && !SerializedProperty.EqualContents(child, end))
        {
            // Các interface lồng bên trong cũng cần chọn lớp cụ thể để Unity lưu lại.
            if (child.propertyType == SerializedPropertyType.ManagedReference &&
                (child.name == "shooter" || child.name == "projectileMove"))
            {
                Type[] nestedTypes = child.name == "shooter" ? GetTypes<IShooter>() : GetTypes<IProjectileMove>();
                EditorGUI.indentLevel++;
                Draw(child, nestedTypes);
                EditorGUI.indentLevel--;
            }
            else
                EditorGUILayout.PropertyField(child, true);
            enterChildren = false;
        }
    }
}
