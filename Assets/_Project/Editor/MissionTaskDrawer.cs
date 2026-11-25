using System;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Vẽ một mission con trong Inspector: chọn nhóm việc rồi chi tiết là dropdown của đúng enum thuộc nhóm đó.
/// Dữ liệu lưu dạng số (detail, subject) để thêm nhóm mới không đổi định dạng asset; drawer chỉ lo hiển thị cho dễ chọn.
/// </summary>
[CustomPropertyDrawer(typeof(MissionTask))]
public sealed class MissionTaskDrawer : PropertyDrawer
{
    private static readonly string[] FieldOrder = { "id", "description", "activity", "detail", "subject", "target" };

    /// <inheritdoc/>
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        int lines = FieldOrder.Length - (UsesSubject(property) ? 0 : 1);
        return lines * (EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing) +
               EditorGUIUtility.standardVerticalSpacing * 2f;
    }

    /// <inheritdoc/>
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);
        var activity = property.FindPropertyRelative("activity");
        var detail = property.FindPropertyRelative("detail");
        var subject = property.FindPropertyRelative("subject");
        Rect line = new Rect(position.x, position.y + EditorGUIUtility.standardVerticalSpacing, position.width, EditorGUIUtility.singleLineHeight);
        float step = EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

        EditorGUI.PropertyField(line, property.FindPropertyRelative("id")); line.y += step;
        EditorGUI.PropertyField(line, property.FindPropertyRelative("description")); line.y += step;

        // Đổi nhóm thì chi tiết và đối tượng cũ không còn nghĩa nên về Any.
        EditorGUI.BeginChangeCheck();
        EditorGUI.PropertyField(line, activity);
        if (EditorGUI.EndChangeCheck())
        {
            detail.intValue = 0;
            subject.intValue = 0;
        }
        line.y += step;

        Type detailType = MissionTask.DetailType((MissionActivity)activity.intValue);
        if (detailType != null)
        {
            var current = (Enum)Enum.ToObject(detailType, detail.intValue);
            detail.intValue = Convert.ToInt32(EditorGUI.EnumPopup(line, new GUIContent("Detail"), current));
        }
        line.y += step;

        if (UsesSubject(property))
        {
            var current = (Enum)Enum.ToObject(typeof(CharacterType), subject.intValue);
            subject.intValue = Convert.ToInt32(EditorGUI.EnumPopup(line, new GUIContent("Subject"), current));
            line.y += step;
        }

        EditorGUI.PropertyField(line, property.FindPropertyRelative("target"));
        EditorGUI.EndProperty();
    }

    private static bool UsesSubject(SerializedProperty property)
        => MissionTask.UsesSubject((MissionActivity)property.FindPropertyRelative("activity").intValue);
}
