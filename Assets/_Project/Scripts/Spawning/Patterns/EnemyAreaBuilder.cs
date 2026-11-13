using System;
using UnityEngine;

/// <summary>Tạo các slot trên viền hình rỗng, không tạo điểm trong ruột.</summary>
public static class EnemyAreaBuilder
{
    #region Sinh viền hình rỗng

    /// <summary>Sinh đủ count điểm trên viền hình, rồi áp dụng kích thước và góc xoay.</summary>
    public static Vector2[] Build(EnemyFormationSO shape, int count)
    {
        if (shape == null || count < 3) throw new ArgumentException("Area cần hình kín và ít nhất 3 điểm.");
        shape.Validate();
        var result = new Vector2[count];
        Vector2[] vertices;
        switch (shape.Shape)
        {
            case FormationType.FilledCircle:
                for (int i = 0; i < count; i++)
                {
                    float angle = i * 2f * Mathf.PI / count;
                    result[i] = Transform(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 0.5f, shape);
                }
                return result;
            case FormationType.FilledSquare:
                vertices = new[] { new Vector2(-0.5f,-0.5f), new Vector2(0.5f,-0.5f), new Vector2(0.5f,0.5f), new Vector2(-0.5f,0.5f) }; break;
            case FormationType.Triangle:
                vertices = new[] { new Vector2(0f,0.5f), new Vector2(0.5f,-0.5f), new Vector2(-0.5f,-0.5f) }; break;
            case FormationType.Diamond:
                vertices = new[] { new Vector2(0f,0.5f), new Vector2(0.5f,0f), new Vector2(0f,-0.5f), new Vector2(-0.5f,0f) }; break;
            case FormationType.Hexagon:
                vertices = new Vector2[6];
                for (int i = 0; i < 6; i++) vertices[i] = new Vector2(Mathf.Cos(i * Mathf.PI / 3f), Mathf.Sin(i * Mathf.PI / 3f)) * 0.5f;
                break;
            default: throw new ArgumentException("Area không hỗ trợ hình hở hoặc Custom.");
        }
        float perimeter = 0f;
        for (int i = 0; i < vertices.Length; i++)
        {
            vertices[i] = Transform(vertices[i], shape);
        }
        for (int i = 0; i < vertices.Length; i++) perimeter += Vector2.Distance(vertices[i], vertices[(i + 1) % vertices.Length]);
        for (int i = 0; i < count; i++)
        {
            float distance = perimeter * i / count;
            for (int edge = 0; edge < vertices.Length; edge++)
            {
                Vector2 a = vertices[edge], b = vertices[(edge + 1) % vertices.Length];
                float length = Vector2.Distance(a, b);
                if (distance <= length || edge == vertices.Length - 1)
                {
                    result[i] = Vector2.Lerp(a, b, distance / length);
                    break;
                }
                distance -= length;
            }
        }
        return result;
    }

    #endregion

    #region Biến đổi tọa độ

    /// <summary>Đổi điểm chuẩn sang offset theo Size và Rotation của hình.</summary>
    private static Vector2 Transform(Vector2 point, EnemyFormationSO shape)
        => FormationBuilder.Rotate(Vector2.Scale(point, shape.Size), shape.Rotation);
    #endregion
}
