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

// Chọn lớp cụ thể cho các interface được lưu bằng SerializeReference, bao gồm từng effect trong danh sách.
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
            DrawField(child);
            enterChildren = false;
        }
    }

    private static void DrawField(SerializedProperty property)
    {
        if (property.isArray && property.name == "effects") { DrawEffects(property); return; }
        if (property.propertyType == SerializedPropertyType.ManagedReference &&
            (property.name == "shooter" || property.name == "projectileMove"))
        {
            Draw(property, property.name == "shooter" ? GetTypes<IShooter>() : GetTypes<IProjectileMove>());
            return;
        }
        if (property.propertyType != SerializedPropertyType.Generic)
        { EditorGUILayout.PropertyField(property, true); return; }

        // Đi vào từng entry nâng cấp để selector shooter/movement/effect vẫn hoạt động khi lồng trong list.
        property.isExpanded = EditorGUILayout.Foldout(property.isExpanded, property.displayName, true);
        if (!property.isExpanded) return;
        EditorGUI.indentLevel++;
        if (property.isArray)
        {
            int size = Mathf.Clamp(EditorGUILayout.IntField("Size", property.arraySize), 0, 256);
            if (size != property.arraySize) property.arraySize = size;
            for (int i = 0; i < property.arraySize; i++) DrawField(property.GetArrayElementAtIndex(i));
        }
        else
        {
            SerializedProperty child = property.Copy();
            SerializedProperty end = property.GetEndProperty();
            bool enter = true;
            while (child.NextVisible(enter) && !SerializedProperty.EqualContents(child, end))
            { DrawField(child); enter = false; }
        }
        EditorGUI.indentLevel--;
    }

    private static void DrawEffects(SerializedProperty effects)
    {
        effects.isExpanded = EditorGUILayout.Foldout(effects.isExpanded, effects.displayName, true);
        if (!effects.isExpanded) return;
        Type[] types = GetTypes<IGameplayEffect>();
        EditorGUI.indentLevel++;
        for (int index = 0; index < effects.arraySize; index++)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Effect {index + 1}");
            bool remove = GUILayout.Button("Remove", GUILayout.Width(70));
            EditorGUILayout.EndHorizontal();
            if (remove)
            {
                effects.DeleteArrayElementAtIndex(index);
                break;
            }
            Draw(effects.GetArrayElementAtIndex(index), types);
        }
        if (GUILayout.Button("Add effect"))
        {
            int index = effects.arraySize;
            effects.arraySize++;
            // Unity có thể nhân bản phần tử cuối khi tăng size; luôn tạo một ô chọn mới.
            effects.GetArrayElementAtIndex(index).managedReferenceValue = null;
        }
        EditorGUI.indentLevel--;
    }
}
