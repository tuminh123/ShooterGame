using UnityEditor;
using UnityEngine;

/// <summary>
/// Vẽ <see cref="ImageStyle"/> trên một dòng: [Tên lớp] [Color/Sprite ▼] [ô màu hoặc ô kéo sprite].
/// Chỉ hiện đúng ô của chế độ đang chọn, để người chỉnh không nhầm giữa màu và ảnh.
/// </summary>
[CustomPropertyDrawer(typeof(ImageStyle))]
public sealed class ImageStyleDrawer : PropertyDrawer
{
    private const float ModeWidth = 70f;
    private const float Spacing = 4f;

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);
        Rect content = EditorGUI.PrefixLabel(position, label);
        // Ô con không thụt lề thêm: đã nằm sau nhãn.
        int indent = EditorGUI.indentLevel;
        EditorGUI.indentLevel = 0;

        SerializedProperty mode = property.FindPropertyRelative("mode");
        var modeRect = new Rect(content.x, content.y, ModeWidth, content.height);
        var valueRect = new Rect(content.x + ModeWidth + Spacing, content.y, content.width - ModeWidth - Spacing, content.height);
        EditorGUI.PropertyField(modeRect, mode, GUIContent.none);

        bool useSprite = !mode.hasMultipleDifferentValues && mode.enumValueIndex == (int)ImageStyleMode.Sprite;
        SerializedProperty value = property.FindPropertyRelative(useSprite ? "sprite" : "color");
        EditorGUI.PropertyField(valueRect, value, GUIContent.none);

        EditorGUI.indentLevel = indent;
        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label) => EditorGUIUtility.singleLineHeight;
}
