using System;
using System.Collections.Generic;
using UnityEngine;

// Giữ nguyên ID đã lưu trong asset; 1/3 là hình rỗng cũ đã ngừng sử dụng.
public enum FormationType
{
    [InspectorName("Chữ nhật / Vuông đặc")] FilledSquare = 0,
    [InspectorName("Tròn / Elip đặc")] FilledCircle = 2,
    [InspectorName("Hàng ngang")] HorizontalLine = 4,
    [InspectorName("Hàng dọc")] VerticalLine = 5,
    [InspectorName("Tam giác đặc")] Triangle = 6,
    [InspectorName("Hình thoi đặc")] Diamond = 7,
    [InspectorName("Lục giác đặc")] Hexagon = 8,
    [InspectorName("Custom / Tự tạo")] Custom = 9
}

// Sinh các slot xác định, đối xứng theo hình. Không rải ngẫu nhiên hoặc cắt hàng cuối.
public static class FormationBuilder
{
    #region Public Geometry API

    public static int GetSlotCount(EnemyFormationSO formation)
    {
        formation.Validate();
        int level = formation.Level;

        switch (formation.Shape)
        {
            case FormationType.HorizontalLine:
            case FormationType.VerticalLine:
                return level + 1;
            case FormationType.FilledSquare:
                return (level + 1) * (level + 1);
            case FormationType.Triangle:
                return (level + 1) * (level + 2) / 2;
            case FormationType.Diamond:
                return level == 1 ? 4 : 1 + 2 * level * (level + 1);
            case FormationType.FilledCircle:
                return level == 1 ? 3 : 1 + 3 * (level - 1) * level;
            case FormationType.Hexagon:
                return level == 1 ? 6 : 1 + 3 * level * (level + 1);
            case FormationType.Custom:
                return BuildCustom(formation).Count;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    public static Vector2[] Build(EnemyFormationSO formation)
    {
        formation.Validate();
        int level = formation.Level;
        var points = new List<Vector2>(GetSlotCount(formation));

        switch (formation.Shape)
        {
            case FormationType.HorizontalLine:
            case FormationType.VerticalLine:
                for (int i = 0; i <= level; i++)
                {
                    float position = i / (float)level - 0.5f;
                    points.Add(formation.Shape == FormationType.HorizontalLine ? new Vector2(position, 0f) : new Vector2(0f, position));
                }
                break;
            case FormationType.FilledSquare:
                BuildSquare(points, level);
                break;
            case FormationType.Triangle:
                BuildTriangle(points, level);
                break;
            case FormationType.Diamond:
                BuildDiamond(points, level);
                break;
            case FormationType.FilledCircle:
                BuildCircle(points, level);
                break;
            case FormationType.Hexagon:
                BuildHexagon(points, level);
                break;
            case FormationType.Custom:
                points = BuildCustom(formation);
                break;
        }

        var result = new Vector2[points.Count];

        for (int i = 0; i < result.Length; i++)
        {
            Vector2 point = formation.Shape == FormationType.Custom
                ? points[i] * formation.CustomScale
                : Vector2.Scale(points[i], formation.Size);
            result[i] = Rotate(point, formation.Rotation);
        }

        return result;
    }

    public static Vector2 Rotate(Vector2 point, float degrees)
    {
        float angle = degrees * Mathf.Deg2Rad;
        return new Vector2(point.x * Mathf.Cos(angle) - point.y * Mathf.Sin(angle),
            point.x * Mathf.Sin(angle) + point.y * Mathf.Cos(angle));
    }

    #endregion

    #region Symmetric Filled Layouts

    private static void BuildSquare(List<Vector2> points, int level)
    {
        for (int row = 0; row <= level; row++)
        {
            for (int column = 0; column <= level; column++)
            {
                points.Add(new Vector2(column / (float)level - 0.5f, row / (float)level - 0.5f));
            }
        }
    }

    private static void BuildTriangle(List<Vector2> points, int level)
    {
        // Các hàng 1,2,... nằm trên lưới tam giác, đối xứng trái/phải.
        // Gốc là trọng tâm tam giác để quay hình không bị lệch tâm khối.
        for (int row = 0; row <= level; row++)
        {
            for (int column = 0; column <= row; column++)
            {
                points.Add(new Vector2((column - row * 0.5f) / level, 2f / 3f - row / (float)level));
            }
        }
    }

    private static void BuildDiamond(List<Vector2> points, int level)
    {
        for (int y = -level; y <= level; y++)
        {
            int width = level - Mathf.Abs(y);

            for (int x = -width; x <= width; x++)
            {
                if (level == 1 && x == 0 && y == 0)
                {
                    continue;
                }

                points.Add(new Vector2(x, y) / (2f * level));
            }
        }
    }

    private static void BuildCircle(List<Vector2> points, int level)
    {
        if (level == 1)
        {
            AddRing(points, 3, 0.5f);
            return;
        }

        points.Add(Vector2.zero);

        for (int ring = 1; ring < level; ring++)
        {
            AddRing(points, 6 * ring, 0.5f * ring / (level - 1));
        }
    }

    private static void AddRing(List<Vector2> points, int count, float radius)
    {
        // Từng vòng chia góc đều, cùng một trục đối xứng qua đỉnh trên.
        for (int i = 0; i < count; i++)
        {
            float angle = Mathf.PI * 0.5f + i * 2f * Mathf.PI / count;
            points.Add(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
        }
    }

    private static void BuildHexagon(List<Vector2> points, int level)
    {
        // Lưới axial lấp đầy lục giác, không dùng phép thử điểm ngẫu nhiên trong biên.
        for (int q = -level; q <= level; q++)
        {
            int from = Mathf.Max(-level, -q - level);
            int to = Mathf.Min(level, -q + level);

            for (int r = from; r <= to; r++)
            {
                if (level == 1 && q == 0 && r == 0)
                {
                    continue;
                }

                points.Add(new Vector2((q + r * 0.5f) / (2f * level), r / (2f * level)));
            }
        }
    }

    #endregion

    #region Custom Points And Symmetry

    private static List<Vector2> BuildCustom(EnemyFormationSO formation)
    {
        var points = new List<Vector2>();

        foreach (Vector2 point in formation.CustomPoints)
        {
            AddUnique(points, point);

            if (formation.Symmetry == FormationSymmetry.MirrorHorizontal || formation.Symmetry == FormationSymmetry.BothAxes)
            {
                AddUnique(points, new Vector2(-point.x, point.y));
            }

            if (formation.Symmetry == FormationSymmetry.MirrorVertical || formation.Symmetry == FormationSymmetry.BothAxes)
            {
                AddUnique(points, new Vector2(point.x, -point.y));
            }

            if (formation.Symmetry == FormationSymmetry.AroundCenter || formation.Symmetry == FormationSymmetry.BothAxes)
            {
                AddUnique(points, -point);
            }
        }

        return points;
    }

    private static void AddUnique(List<Vector2> points, Vector2 point)
    {
        foreach (Vector2 existing in points)
        {
            if ((existing - point).sqrMagnitude < 0.000001f)
            {
                return;
            }
        }

        points.Add(point);
    }

    #endregion
}
