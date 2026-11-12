using System;
using System.Collections.Generic;
using UnityEngine;

// Path chỉ có waypoint A...B. Không còn preset thẳng/chéo/zigzag hoặc dữ liệu Formation trộn chung.
public static class EnemyPathBuilder
{
    #region Local And World Geometry

    public static Vector2[] BuildLocal(EnemyPathSO path)
    {
        if (path == null)
        {
            throw new ArgumentNullException(nameof(path));
        }

        path.Validate();
        var knots = new Vector2[path.Waypoints.Count];

        for (int i = 0; i < knots.Length; i++)
        {
            knots[i] = path.Waypoints[i];
        }

        return path.Smooth ? BuildSmooth(knots, path.SamplesPerSegment) : knots;
    }

    // Chỉ dịch đường theo tâm camera + offset. Không chọn điểm spawn và không tự xoay đường.
    public static Vector2[] Build(EnemyPathSO path, Vector2 cameraPosition)
    {
        if (!IsFinite(cameraPosition))
        {
            throw new ArgumentException("Vị trí camera phải hữu hạn.");
        }

        Vector2[] points = BuildLocal(path);
        Vector2 center = path.GetCenter(cameraPosition);

        for (int i = 0; i < points.Length; i++)
        {
            points[i] += center;
        }

        return points;
    }

    private static Vector2[] BuildSmooth(Vector2[] knots, int samples)
    {
        var result = new Vector2[(knots.Length - 1) * samples + 1];

        for (int segment = 0; segment < knots.Length - 1; segment++)
        {
            Vector2 p0 = knots[Mathf.Max(0, segment - 1)];
            Vector2 p1 = knots[segment];
            Vector2 p2 = knots[segment + 1];
            Vector2 p3 = knots[Mathf.Min(knots.Length - 1, segment + 2)];

            for (int sample = 0; sample < samples; sample++)
            {
                float t = sample / (float)samples;
                float squared = t * t;
                float cubed = squared * t;

                result[segment * samples + sample] = 0.5f * ((2f * p1) + (-p0 + p2) * t +
                    (2f * p0 - 5f * p1 + 4f * p2 - p3) * squared +
                    (-p0 + 3f * p1 - 3f * p2 + p3) * cubed);
            }
        }

        result[result.Length - 1] = knots[knots.Length - 1];
        return result;
    }

    private static bool IsFinite(Vector2 point)
    {
        return !float.IsNaN(point.x) && !float.IsInfinity(point.x) &&
            !float.IsNaN(point.y) && !float.IsInfinity(point.y);
    }

    #endregion
}

// Bản sao đường của đúng một nhóm. Cache độ dài giúp các slot lấy vị trí mà không quét lại mọi đoạn.
public sealed class EnemyPathRoute
{
    #region Cached Geometry

    private readonly Transform cameraAnchor;
    private readonly Vector2 initialCameraPosition;
    public Vector2 AnchorOffset => cameraAnchor != null ? (Vector2)cameraAnchor.position - initialCameraPosition : Vector2.zero;

    private readonly Vector2[] points;
    private readonly float[] cumulativeDistances;

    public IReadOnlyList<Vector2> Points => points;
    public float Length => cumulativeDistances[cumulativeDistances.Length - 1];

    public EnemyPathRoute(Vector2[] source, Transform cameraAnchor = null)
    {
        if (source == null || source.Length < 2)
        {
            throw new ArgumentException("Đường runtime cần ít nhất hai điểm.");
        }

        this.cameraAnchor = cameraAnchor;
        initialCameraPosition = cameraAnchor != null ? (Vector2)cameraAnchor.position : Vector2.zero;
        points = (Vector2[])source.Clone();
        cumulativeDistances = new float[points.Length];

        for (int i = 0; i < points.Length; i++)
        {
            if (float.IsNaN(points[i].x) || float.IsInfinity(points[i].x) ||
                float.IsNaN(points[i].y) || float.IsInfinity(points[i].y))
            {
                throw new ArgumentException("Tọa độ đường runtime phải hữu hạn.");
            }

            if (i > 0)
            {
                cumulativeDistances[i] = cumulativeDistances[i - 1] + Vector2.Distance(points[i - 1], points[i]);
            }
        }

        if (Length <= 0.0001f || float.IsInfinity(Length))
        {
            throw new ArgumentException("Đường runtime phải có chiều dài dương, hữu hạn.");
        }
    }

    #endregion

    #region Distance Sampling

    public Vector2 Sample(float travelled, int passes)
    {
        return SampleAtInitialAnchor(travelled, passes) + AnchorOffset;
    }

    private Vector2 SampleAtInitialAnchor(float travelled, int passes)
    {
        if (travelled < 0f)
        {
            int next = 1;

            while (next < points.Length - 1 && cumulativeDistances[next] <= 0.0001f)
            {
                next++;
            }

            return points[0] + (points[next] - points[0]).normalized * travelled;
        }

        if (travelled >= Length * passes)
        {
            return passes % 2 == 0 ? points[0] : points[points.Length - 1];
        }

        int pass = Mathf.FloorToInt(travelled / Length);
        float along = travelled - pass * Length;
        return SampleForward(pass % 2 == 0 ? along : Length - along);
    }

    private Vector2 SampleForward(float distance)
    {
        if (distance <= 0f)
        {
            return points[0];
        }

        if (distance >= Length)
        {
            return points[points.Length - 1];
        }

        int index = Array.BinarySearch(cumulativeDistances, distance);

        if (index >= 0)
        {
            return points[index];
        }

        index = ~index;
        float span = cumulativeDistances[index] - cumulativeDistances[index - 1];
        float ratio = span <= 0f ? 0f : (distance - cumulativeDistances[index - 1]) / span;
        return Vector2.Lerp(points[index - 1], points[index], ratio);
    }

    // Runtime và tab Test dùng chung phép lấy vị trí: hình giữ offset, nối đuôi dùng độ trễ quãng đường.
    public Vector2 MemberPosition(EnemyFormationSettings settings, float travelled, int slot, Vector2 offset)
    {
        if (settings.movement == EnemyFormationMovement.FollowPath)
        {
            return Sample(travelled - slot * settings.followSpacing, settings.passes);
        }

        return Sample(travelled, settings.passes) + offset;
    }

    public float CompletionDistance(EnemyFormationSettings settings, int memberCount)
    {
        float trailingDistance = settings.movement == EnemyFormationMovement.FollowPath
            ? Mathf.Max(0, memberCount - 1) * settings.followSpacing
            : 0f;

        return Length * settings.passes + trailingDistance;
    }

    #endregion
}
