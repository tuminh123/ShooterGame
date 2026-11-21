using System;
using UnityEngine;

/// <summary>Dựng viền rỗng cho hình học Formation; không quyết định cách spawn hoặc vị trí Player.</summary>
public static class EnemyAreaBuilder
{
    #region Sinh viền hình rỗng

    /// <summary>Sinh đủ count điểm trên viền hình, rồi áp dụng kích thước và góc xoay.</summary>
    public static Vector2[] Build(EnemyFormationSO shape, int count)
    {
        if (shape == null || count < 3) throw new ArgumentException("Area cần hình kín và ít nhất 3 điểm.");
        shape.Validate();

        if (shape.Shape == FormationType.FilledCircle) return BuildCircle(shape, count);

        Vector2[] vertices = GetUnitVertices(shape.Shape);
        for (int i = 0; i < vertices.Length; i++) vertices[i] = Transform(vertices[i], shape);

        // Count hình học luôn là số cạnh × 2^(Level-1): chia đều mỗi cạnh, giữ đủ các đỉnh.
        return count % vertices.Length == 0
            ? DistributePerEdge(vertices, count)
            : DistributeAlongPerimeter(vertices, count);
    }

    // Điểm cách đều nhau trên đường tròn đường kính 1, rồi theo Size/Rotation.
    private static Vector2[] BuildCircle(EnemyFormationSO shape, int count)
    {
        var result = new Vector2[count];
        for (int i = 0; i < count; i++)
        {
            float angle = i * 2f * Mathf.PI / count;
            result[i] = Transform(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 0.5f, shape);
        }
        return result;
    }

    // Đỉnh của đa giác chuẩn nằm trong ô vuông 1×1 quanh tâm, theo thứ tự đi vòng quanh viền.
    private static Vector2[] GetUnitVertices(FormationType type)
    {
        switch (type)
        {
            case FormationType.FilledSquare:
                return new[] { new Vector2(-0.5f, -0.5f), new Vector2(0.5f, -0.5f), new Vector2(0.5f, 0.5f), new Vector2(-0.5f, 0.5f) };
            case FormationType.Triangle:
                return new[] { new Vector2(0f, 0.5f), new Vector2(0.5f, -0.5f), new Vector2(-0.5f, -0.5f) };
            case FormationType.Diamond:
                return new[] { new Vector2(0f, 0.5f), new Vector2(0.5f, 0f), new Vector2(0f, -0.5f), new Vector2(-0.5f, 0f) };
            case FormationType.Hexagon:
                var hexagon = new Vector2[6];
                for (int i = 0; i < 6; i++) hexagon[i] = new Vector2(Mathf.Cos(i * Mathf.PI / 3f), Mathf.Sin(i * Mathf.PI / 3f)) * 0.5f;
                return hexagon;
            default:
                throw new ArgumentException("Area không hỗ trợ hình hở hoặc Custom.");
        }
    }

    // Count chia hết cho số cạnh: mỗi cạnh nhận cùng số điểm, bắt đầu từ đỉnh của cạnh đó.
    private static Vector2[] DistributePerEdge(Vector2[] vertices, int count)
    {
        var result = new Vector2[count];
        int perEdge = count / vertices.Length;
        for (int edge = 0; edge < vertices.Length; edge++)
        {
            Vector2 from = vertices[edge], to = vertices[(edge + 1) % vertices.Length];
            for (int i = 0; i < perEdge; i++)
                result[edge * perEdge + i] = Vector2.Lerp(from, to, i / (float)perEdge);
        }
        return result;
    }

    // Count lẻ so với số cạnh: đặt điểm cách đều theo chiều dài chu vi, đi lần lượt qua từng cạnh.
    private static Vector2[] DistributeAlongPerimeter(Vector2[] vertices, int count)
    {
        float perimeter = 0f;
        for (int i = 0; i < vertices.Length; i++) perimeter += Vector2.Distance(vertices[i], vertices[(i + 1) % vertices.Length]);

        var result = new Vector2[count];
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
