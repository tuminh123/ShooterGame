using UnityEditor;
using UnityEngine;

/// <summary>
/// Vẽ một bước <see cref="SkillProgressStep"/> trong Inspector của CharacterDataSO: skill được <b>chọn từ dropdown</b>
/// liệt kê danh sách Skills của chính asset đó, thay vì kéo thả lại asset skill.
/// </summary>
/// <remarks>
/// Giá trị lưu vẫn là tham chiếu tới asset skill (không lưu số thứ tự), nên đổi thứ tự hay xóa bớt Skills
/// không làm bước trỏ sang skill khác. Skill đang chọn mà không còn trong Skills thì hiện cảnh báo trong dropdown.
/// </remarks>
[CustomPropertyDrawer(typeof(SkillProgressStep))]
public sealed class SkillProgressStepDrawer : PropertyDrawer
{
    #region Layout

    private const int Rows = 4;

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float line = EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
        return property.isExpanded ? line * (Rows + 1) : line;
    }

    #endregion

    #region Drawing

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);
        float line = EditorGUIUtility.singleLineHeight;
        float step = line + EditorGUIUtility.standardVerticalSpacing;
        var row = new Rect(position.x, position.y, position.width, line);

        SerializedProperty skill = property.FindPropertyRelative("skill");
        string header = skill.objectReferenceValue != null ? skill.objectReferenceValue.name : "(chưa chọn skill)";
        property.isExpanded = EditorGUI.Foldout(row, property.isExpanded, label.text + "  ·  " + header, true);
        if (!property.isExpanded)
        {
            EditorGUI.EndProperty();
            return;
        }

        EditorGUI.indentLevel++;
        row.y += step;
        DrawSkillPopup(row, property.serializedObject.FindProperty("skills"), skill);
        row.y += step;
        EditorGUI.PropertyField(row, property.FindPropertyRelative("skillLevel"));
        row.y += step;
        EditorGUI.PropertyField(row, property.FindPropertyRelative("condition"));
        row.y += step;
        EditorGUI.PropertyField(row, property.FindPropertyRelative("required"));
        EditorGUI.indentLevel--;
        EditorGUI.EndProperty();
    }

    // Dropdown gồm "(chưa chọn)" + các skill trong Skills; skill hiện tại không có trong Skills thì thêm một mục cảnh báo ở cuối.
    private static void DrawSkillPopup(Rect row, SerializedProperty skills, SerializedProperty skill)
    {
        if (skills == null || !skills.isArray)
        {
            EditorGUI.PropertyField(row, skill);
            return;
        }

        Object current = skill.objectReferenceValue;
        int count = skills.arraySize;
        bool currentMissing = current != null;
        var options = new GUIContent[count + 1 + (current != null ? 1 : 0)];
        var values = new Object[options.Length];
        options[0] = new GUIContent("(chưa chọn)");
        int selected = 0;
        for (int i = 0; i < count; i++)
        {
            Object option = skills.GetArrayElementAtIndex(i).objectReferenceValue;
            values[i + 1] = option;
            options[i + 1] = new GUIContent(option != null ? (i + 1) + ". " + option.name : (i + 1) + ". (trống)");
            if (option != null && option == current)
            {
                selected = i + 1;
                currentMissing = false;
            }
        }
        if (currentMissing)
        {
            options[count + 1] = new GUIContent("⚠ " + current.name + " (không có trong Skills)");
            values[count + 1] = current;
            selected = count + 1;
        }
        else if (current != null)
        {
            // Không cần mục cảnh báo: rút gọn mảng về đúng số mục.
            System.Array.Resize(ref options, count + 1);
            System.Array.Resize(ref values, count + 1);
        }

        EditorGUI.BeginChangeCheck();
        int picked = EditorGUI.Popup(row, new GUIContent("Skill", "Chọn trong danh sách Skills ở trên."), selected, options);
        if (EditorGUI.EndChangeCheck())
            skill.objectReferenceValue = values[picked];
    }

    #endregion
}
