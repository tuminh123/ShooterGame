using System;
using UnityEditor;
using UnityEngine;

internal enum DataInspectorTone { Info, Stats, Visual, Spawn, Rewards, Advanced, Legacy }

// Các DataSO dùng chung bố cục với Skill: tiêu đề màu, mô tả ngắn và thuộc tính có tooltip.
internal static class DataInspectorGUI
{
    #region Section Layout & Colors

    public static IDisposable Section(string title, string description, DataInspectorTone tone = DataInspectorTone.Info)
        => new SectionScope(title, description, tone);

    public static void DrawSection(string title, string description, Action draw, DataInspectorTone tone = DataInspectorTone.Info)
    {
        using (Section(title, description, tone)) draw();
    }

    private sealed class SectionScope : IDisposable
    {
        public SectionScope(string title, string description, DataInspectorTone tone)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            Color accent = Accent(tone);
            Color background = EditorGUIUtility.isProSkin ? new Color(0.18f, 0.18f, 0.18f) : new Color(0.94f, 0.94f, 0.94f);
            Rect header = GUILayoutUtility.GetRect(0f, 28f, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(header, Color.Lerp(background, accent, EditorGUIUtility.isProSkin ? 0.28f : 0.2f));
            EditorGUI.DrawRect(new Rect(header.x, header.y, 4f, header.height), accent);
            var style = new GUIStyle(EditorStyles.boldLabel) { fontSize = 13, clipping = TextClipping.Clip };
            style.normal.textColor = EditorGUIUtility.isProSkin ? Color.white : new Color(0.1f, 0.1f, 0.1f);
            GUI.Label(new Rect(header.x + 10f, header.y + 4f, header.width - 14f, header.height - 4f),
                new GUIContent(title, description), style);
            if (!string.IsNullOrWhiteSpace(description))
                EditorGUILayout.LabelField(description, EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(4f);
        }

        public void Dispose()
        {
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(5f);
        }
    }

    private static Color Accent(DataInspectorTone tone)
    {
        switch (tone)
        {
            case DataInspectorTone.Stats: return new Color(0.25f, 0.72f, 0.43f);
            case DataInspectorTone.Visual: return new Color(0.63f, 0.47f, 0.88f);
            case DataInspectorTone.Spawn: return new Color(0.94f, 0.58f, 0.22f);
            case DataInspectorTone.Rewards: return new Color(0.85f, 0.7f, 0.21f);
            case DataInspectorTone.Advanced: return new Color(0.23f, 0.73f, 0.74f);
            case DataInspectorTone.Legacy: return new Color(0.68f, 0.57f, 0.46f);
            default: return new Color(0.3f, 0.62f, 0.9f);
        }
    }

    #endregion

    #region Serialized Fields & Validation

    // Đổi nhãn vẫn giữ tooltip của field; mọi sửa đổi đi qua SerializedProperty để hỗ trợ Undo.
    public static void Field(SerializedObject data, string path, string label = null)
    {
        var property = data.FindProperty(path);
        if (property == null)
        {
            EditorGUILayout.HelpBox("Không tìm thấy thuộc tính: " + path, MessageType.Error);
            return;
        }
        if (label == null) EditorGUILayout.PropertyField(property, true);
        else EditorGUILayout.PropertyField(property, new GUIContent(label, property.tooltip), true);
    }

    public static void Validate(Action validate)
    {
        try { validate(); }
        catch (Exception exception) { EditorGUILayout.HelpBox(exception.Message, MessageType.Error); }
    }

    #endregion

    #region Formation Shape Selection

    // Dùng chung cho Inspector và Workbench: mode Rỗng không liệt kê hình hở hoặc Custom.
    public static FormationType[] AllowedFormationShapes(FormationFillMode mode)
        => mode == FormationFillMode.Hollow
            ? new[] { FormationType.FilledSquare, FormationType.FilledCircle, FormationType.Triangle, FormationType.Diamond, FormationType.Hexagon }
            : new[] { FormationType.FilledSquare, FormationType.FilledCircle, FormationType.HorizontalLine, FormationType.VerticalLine,
                FormationType.Triangle, FormationType.Diamond, FormationType.Hexagon, FormationType.Custom };

    public static void FormationShapeFields(SerializedObject data)
    {
        var fill = data.FindProperty("fillMode");
        var shape = data.FindProperty("shape");
        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(fill);
        bool modeChanged = EditorGUI.EndChangeCheck();
        if (fill.hasMultipleDifferentValues) { EditorGUILayout.PropertyField(shape); return; }
        var choices = AllowedFormationShapes((FormationFillMode)fill.intValue);
        var labels = new string[choices.Length];
        for (int i = 0; i < labels.Length; i++)
        {
            switch (choices[i])
            {
                case FormationType.FilledSquare: labels[i] = "Vuông / Chữ nhật"; break;
                case FormationType.FilledCircle: labels[i] = "Tròn / Elip"; break;
                case FormationType.HorizontalLine: labels[i] = "Hàng ngang"; break;
                case FormationType.VerticalLine: labels[i] = "Hàng dọc"; break;
                case FormationType.Triangle: labels[i] = "Tam giác"; break;
                case FormationType.Diamond: labels[i] = "Hình thoi"; break;
                case FormationType.Hexagon: labels[i] = "Lục giác"; break;
                default: labels[i] = "Custom / Tự tạo"; break;
            }
        }
        int selected = Array.IndexOf(choices, (FormationType)shape.intValue);
        if (modeChanged && selected < 0) { selected = 0; shape.intValue = (int)choices[0]; }
        Rect rect = EditorGUILayout.GetControlRect();
        EditorGUI.BeginProperty(rect, GUIContent.none, shape);
        EditorGUI.showMixedValue = shape.hasMultipleDifferentValues;
        EditorGUI.BeginChangeCheck();
        int next = EditorGUI.Popup(rect, new GUIContent("Shape", shape.tooltip), selected, Array.ConvertAll(labels, label => new GUIContent(label)));
        if (EditorGUI.EndChangeCheck() && next >= 0) shape.intValue = (int)choices[next];
        EditorGUI.showMixedValue = false;
        EditorGUI.EndProperty();
    }

    #endregion
}
