using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(SkillDataSO), true)]
public sealed class CharacterSkillEditor : Editor
{
    private static GUIStyle sectionHeaderStyle;

    #region Inspector

    // Chia dữ liệu skill thành các vùng rõ ràng trong Inspector.
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawSection("SKILL INFO", "Thông tin nhận diện và nguồn cấp skill.", () =>
        {
            DrawProperty("source");
            DrawProperty("level");
            DrawProperty("displayName");
            DrawProperty("description");
            DrawProperty("icon");
        });
        DrawSection("BOSS VISUAL", "Trạng thái animation được yêu cầu khi boss thực hiện hành vi bắn.",
            () => DrawProperty("shootingVisualState"));
        DrawSection($"LEVEL DATA     MAX LEVEL: {serializedObject.FindProperty("levels").arraySize}",
            "Mỗi phần tử là cấu hình của một cấp. Chỉ các Behaviour Data có trong cấp mới được kích hoạt ở cấp đó.",
            () => DrawLevels(serializedObject.FindProperty("levels")));
        DrawSection("BEHAVIOUR TEMPLATES", "Mẫu quyết định cách từng hành vi chạy. Thông số riêng theo cấp nằm trong Level Data.",
            () => DrawReferences(serializedObject.FindProperty("behaviours"), "Template", StrategySelector.GetTypes<ISkill>()));
        serializedObject.ApplyModifiedProperties();
        try { ((SkillDataSO)target).Validate(); }
        catch (Exception exception) { EditorGUILayout.HelpBox(exception.Message, MessageType.Error); }
    }

    // Vẽ một thuộc tính theo tên field đã serialize.
    private void DrawProperty(string name)
        => EditorGUILayout.PropertyField(serializedObject.FindProperty(name), true);

    // Khung riêng có tiêu đề lớn, mô tả ngắn và nội dung.
    private static void DrawSection(string title, string description, Action drawContent)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField(title, SectionHeaderStyle, GUILayout.Height(24f));
        EditorGUILayout.LabelField(description, EditorStyles.wordWrappedMiniLabel);
        EditorGUILayout.Space(4f);
        drawContent();
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(5f);
    }

    // Kiểu tiêu đề dùng chung cho các vùng trong Inspector.
    private static GUIStyle SectionHeaderStyle
    {
        get
        {
            if (sectionHeaderStyle == null)
            {
                sectionHeaderStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = 14,
                    fontStyle = FontStyle.Bold
                };
            }
            return sectionHeaderStyle;
        }
    }

    // Mỗi phần tử là cấu hình đầy đủ một cấp; số phần tử chính là MaxLevel.
    private static void DrawLevels(SerializedProperty levels)
    {
        for (int i = 0; i < levels.arraySize; i++)
        {
            SerializedProperty level = levels.GetArrayElementAtIndex(i);
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.BeginHorizontal();
            level.isExpanded = EditorGUILayout.Foldout(level.isExpanded, $"LEVEL {i + 1}", true, EditorStyles.foldoutHeader);
            bool remove = GUILayout.Button("Remove", GUILayout.Width(65));
            EditorGUILayout.EndHorizontal();
            if (remove)
            {
                levels.DeleteArrayElementAtIndex(i);
                EditorGUILayout.EndVertical();
                break;
            }
            if (level.isExpanded)
            {
                EditorGUILayout.PropertyField(level.FindPropertyRelative("description"));
                EditorGUILayout.LabelField("BEHAVIOUR DATA", EditorStyles.boldLabel);
                DrawReferences(level.FindPropertyRelative("behaviours"), "Behaviour Data", StrategySelector.GetTypes<SkillBehaviourData>());
            }
            EditorGUILayout.EndVertical();
        }
        if (GUILayout.Button("Add level"))
        {
            int index = levels.arraySize;
            levels.arraySize++;
            SerializedProperty level = levels.GetArrayElementAtIndex(index);
            level.FindPropertyRelative("description").stringValue = "";
            level.FindPropertyRelative("behaviours").arraySize = 0;
            level.isExpanded = true;
        }
    }

    // Dùng chung selector cho data và hành vi, không cần nhập enum hoặc string ID.
    private static void DrawReferences(SerializedProperty list, string itemLabel, Type[] types)
    {
        for (int i = 0; i < list.arraySize; i++)
        {
            SerializedProperty entry = list.GetArrayElementAtIndex(i);
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"{itemLabel.ToUpperInvariant()} {i + 1}", EditorStyles.boldLabel);
            bool up = GUILayout.Button("Up", GUILayout.Width(40));
            bool down = GUILayout.Button("Down", GUILayout.Width(50));
            bool remove = GUILayout.Button("Remove", GUILayout.Width(65));
            EditorGUILayout.EndHorizontal();
            if (remove)
            {
                list.DeleteArrayElementAtIndex(i);
                EditorGUILayout.EndVertical();
                break;
            }
            StrategySelector.Draw(entry, types, "Type");
            EditorGUILayout.EndVertical();
            if (up && i > 0) { list.MoveArrayElement(i, i - 1); break; }
            if (down && i + 1 < list.arraySize) { list.MoveArrayElement(i, i + 1); break; }
        }
        if (GUILayout.Button("Add item"))
        {
            int index = list.arraySize;
            list.arraySize++;
            list.GetArrayElementAtIndex(index).managedReferenceValue = null;
        }
    }

    #endregion
}

// Chọn lớp cụ thể cho các interface được lưu bằng SerializeReference, bao gồm từng effect trong danh sách.
internal static class StrategySelector
{
    public static Type[] GetTypes<T>()
        => TypeCache.GetTypesDerivedFrom<T>()
            .Where(type => !type.IsAbstract && !type.IsInterface && type.IsSerializable &&
                           type.GetConstructor(Type.EmptyTypes) != null)
            .OrderBy(type => type.Name).ToArray();

    public static void Draw(SerializedProperty property, Type[] types, string label = null)
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
        int selected = EditorGUILayout.Popup(label ?? property.displayName, current, names);
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
