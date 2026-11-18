using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Dựng đường local/world dùng chung cho runtime và Workbench.</summary>
public static class EnemyPathBuilder
{

    #region Dựng hình học local và world

    /// <summary>Sao chép waypoint local; làm mượt nếu asset bật Smooth.</summary>
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

    /// <summary>Dịch đường local theo tâm camera và offset; không xoay theo camera.</summary>
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

    #endregion

    #region Làm mượt và kiểm tra tọa độ

    /// <summary>Lấy mẫu Catmull-Rom qua waypoint và giữ nguyên hai đầu đường.</summary>
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

    /// <summary>Kiểm tra cả hai thành phần của tọa độ đều hữu hạn.</summary>
    private static bool IsFinite(Vector2 point)
    {
        return !float.IsNaN(point.x) && !float.IsInfinity(point.x) &&
            !float.IsNaN(point.y) && !float.IsInfinity(point.y);
    }

    #endregion
}

/// <summary>Bản sao đường của một nhóm, có cache chiều dài và độ dịch camera.</summary>
public sealed class EnemyPathRoute
{

    #region Hình học cache và tâm camera

    private readonly Transform cameraAnchor;
    private readonly Vector2 initialCameraPosition;
    /// <summary>Độ dịch XY của camera so với lúc tạo route.</summary>
    public Vector2 AnchorOffset => cameraAnchor != null ? (Vector2)cameraAnchor.position - initialCameraPosition : Vector2.zero;

    private readonly Vector2[] points;
    private readonly float[] cumulativeDistances;

    public IReadOnlyList<Vector2> Points => points;
    public float Length => cumulativeDistances[cumulativeDistances.Length - 1];

    #endregion

    #region Khởi tạo route

    /// <summary>Sao chép điểm world và tính chiều dài tích lũy; từ chối đường suy biến.</summary>
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

    #region Lấy mẫu theo quãng đường

    /// <summary>Lấy vị trí theo quãng đường, số lượt đi và độ dịch camera hiện tại.</summary>
    public Vector2 Sample(float travelled, int passes)
    {
        return SampleAtInitialAnchor(travelled, passes) + AnchorOffset;
    }

    /// <summary>Lấy mẫu trước khi cộng offset camera; các lượt luân phiên đi A đến B rồi quay lại.</summary>
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

    /// <summary>Tìm góc lấy mẫu tiếp theo theo chiều đang đi, kể cả lượt quay về.</summary>
    public float NextCornerDistance(float travelled, int passes)
    {
        float end = Length * passes;
        if (travelled >= end) return end;
        int pass = Mathf.Min(passes - 1, Mathf.FloorToInt(travelled / Length));
        float along = travelled - pass * Length;
        if (pass % 2 == 0)
        {
            for (int i = 1; i < cumulativeDistances.Length; i++)
                if (cumulativeDistances[i] > along + 0.00001f) return pass * Length + cumulativeDistances[i];
        }
        else
        {
            for (int i = cumulativeDistances.Length - 2; i >= 0; i--)
            {
                float reverseDistance = Length - cumulativeDistances[i];
                if (reverseDistance > along + 0.00001f) return pass * Length + reverseDistance;
            }
        }
        return Mathf.Min(end, (pass + 1) * Length);
    }

    /// <summary>Tìm đoạn bằng binary search rồi nội suy vị trí theo quãng đường từ A.</summary>
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

    #endregion
}
