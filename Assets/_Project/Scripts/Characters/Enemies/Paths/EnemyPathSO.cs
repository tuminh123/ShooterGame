using System;
using System.Collections.Generic;
using UnityEngine;

// Một đường duy nhất bắt đầu từ A và B. Người dùng thêm/kéo waypoint để tạo đường riêng.
[CreateAssetMenu(fileName = "EnemyPath", menuName = "Shooter Game/Enemy/Path")]
public sealed class EnemyPathSO : ScriptableObject
{
    #region Serialized Path

    [Tooltip("Tắt: tâm Path trùng tâm camera. Bật: dùng Center Offset tương đối với camera; tâm vẫn luôn bám camera.")]
    [SerializeField] private bool useCustomCenter;

    [Tooltip("Độ lệch tâm Path so với camera theo world X/Y. Không phải vị trí cố định ngoài world. Không dùng góc xoay hoặc zoom camera.")]
    [SerializeField] private Vector2 centerOffset;

    [Tooltip("Các điểm theo thứ tự: điểm đầu là A, điểm cuối là B. Kéo cả A/B hoặc chèn điểm giữa trong tab Path. Gốc (0,0) là tâm Path bám camera; nhóm spawn tại A. Đường giữ trục world X/Y, không xoay theo điểm spawn hoặc player.")]
    [SerializeField] private Vector2[] waypoints = { new Vector2(-5f, 0f), new Vector2(5f, 0f) };

    [Tooltip("Tắt: nối điểm bằng đoạn thẳng. Bật: đường cong mượt đi qua waypoint; gizmo vẽ đúng đường đã lấy mẫu.")]
    [SerializeField] private bool smooth;

    [Tooltip("Số đoạn nhỏ lấy mẫu giữa hai waypoint khi làm mượt. Mặc định 8; tăng để đường cong sát hơn, tối đa 32.")]
    [Range(2, 32)] [SerializeField] private int samplesPerSegment = 8;

    #endregion

    #region Read Access And Validation

    public bool UseCustomCenter => useCustomCenter;
    public Vector2 CenterOffset => useCustomCenter ? centerOffset : Vector2.zero;

    public Vector2 GetCenter(Vector2 cameraPosition)
    {
        return cameraPosition + CenterOffset;
    }

    public IReadOnlyList<Vector2> Waypoints => waypoints;
    public bool Smooth => smooth;
    public int SamplesPerSegment => samplesPerSegment;

    public void Validate()
    {
        if (useCustomCenter && (float.IsNaN(centerOffset.x) || float.IsInfinity(centerOffset.x) ||
            float.IsNaN(centerOffset.y) || float.IsInfinity(centerOffset.y)))
        {
            throw new InvalidOperationException("Offset tâm Path phải hữu hạn.");
        }

        if (waypoints == null || waypoints.Length < 2 || waypoints.Length > 128)
        {
            throw new InvalidOperationException("Path cần từ 2 đến 128 waypoint.");
        }

        for (int i = 0; i < waypoints.Length; i++)
        {
            Vector2 point = waypoints[i];

            if (float.IsNaN(point.x) || float.IsInfinity(point.x) ||
                float.IsNaN(point.y) || float.IsInfinity(point.y))
            {
                throw new InvalidOperationException("Tọa độ waypoint phải hữu hạn.");
            }

            if (i > 0 && Vector2.Distance(waypoints[i - 1], point) < 0.001f)
            {
                throw new InvalidOperationException("Hai waypoint liên tiếp không được trùng nhau.");
            }
        }

        if (smooth && (samplesPerSegment < 2 || samplesPerSegment > 32))
        {
            throw new InvalidOperationException("Số mẫu đường cong phải trong khoảng 2–32.");
        }
    }

    #endregion
}
