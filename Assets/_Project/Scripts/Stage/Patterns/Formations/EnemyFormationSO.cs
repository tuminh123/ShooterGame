using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Cách phân bố slot hình học, độc lập với cách spawn theo Path hoặc Area.</summary>
public enum FormationFillMode
{
    [InspectorName("Đặc")] Solid = 0,
    [InspectorName("Rỗng")] Hollow = 1
}

/// <summary>Các cách nhân đối xứng điểm Custom quanh trục hoặc tâm.</summary>
public enum FormationSymmetry
{
    [InspectorName("Tự do")] None,
    [InspectorName("Trái / phải")] MirrorHorizontal,
    [InspectorName("Trên / dưới")] MirrorVertical,
    [InspectorName("Cả hai trục")] BothAxes,
    [InspectorName("Qua tâm")] AroundCenter
}

/// <summary>Dữ liệu hình học đội hình; không giữ nhân vật hoặc tiến trình runtime.</summary>
[CreateAssetMenu(fileName = "EnemyFormation", menuName = "Shooter Game/Enemy/Formation")]
public sealed class EnemyFormationSO : ScriptableObject
{

    #region Cấu hình hình học

    [Tooltip("Hình chuẩn luôn đối xứng; Custom cho phép tự đặt điểm và chọn đối xứng tự động.")]
    [SerializeField] private FormationType shape = FormationType.FilledSquare;

    [Tooltip("Đặc dùng toàn bộ hình. Rỗng chỉ đặt quái trên một viền kín; không phụ thuộc cách spawn.")]
    [SerializeField] private FormationFillMode fillMode;

    [Tooltip("Đặc: tăng hàng/lớp như cũ. Rỗng: Level 1 tự lấy số điểm tối thiểu của hình, mỗi cấp sau gấp đôi mật độ trên cùng một viền. Size không đổi.")]
    [Range(1, 16)] [SerializeField] private int level = 1;

    [Tooltip("Chiều rộng/cao world unit của hình chuẩn. Tăng Size chỉ giãn điểm, không tăng số enemy. Custom dùng Custom Scale.")]
    [SerializeField] private Vector2 size = new Vector2(8f, 8f);

    [Tooltip("Góc xoay cả hình quanh tâm, theo độ; không xoay sprite từng enemy.")]
    [SerializeField] private float rotation;

    [Tooltip("Điểm gốc Custom theo world unit quanh tâm (0,0), trước Scale/Rotation. Thêm/xóa ở đây hoặc kéo trong tab Formation.")]
    [SerializeField] private Vector2[] customPoints = { new Vector2(-1f, 0f), new Vector2(1f, 0f) };

    [Tooltip("Tạo thêm điểm đối xứng từ các điểm gốc. Điểm trùng trên trục/tâm chỉ tạo một slot.")]
    [SerializeField] private FormationSymmetry symmetry;

    [Tooltip("Phóng to/thu nhỏ Custom quanh tâm, giữ nguyên số điểm. Tọa độ điểm gốc không bị thay đổi.")]
    [Min(0.01f)] [SerializeField] private float customScale = 1f;

    #endregion

    #region Dữ liệu chỉ đọc

    public FormationType Shape => shape;
    public FormationFillMode FillMode => fillMode;
    public int HollowSlotCount => checked(MinimumHollowSlots(shape) * (1 << (level - 1)));
    public static bool SupportsHollow(FormationType value)
        => value == FormationType.FilledSquare || value == FormationType.FilledCircle || value == FormationType.Triangle ||
            value == FormationType.Diamond || value == FormationType.Hexagon;

    // Hình tự quyết định số điểm tối thiểu, không có quota gốc phải nhập bằng tay.
    private static int MinimumHollowSlots(FormationType value)
    {
        switch (value)
        {
            case FormationType.FilledSquare:
            case FormationType.Diamond: return 4;
            case FormationType.FilledCircle:
            case FormationType.Triangle: return 3;
            case FormationType.Hexagon: return 6;
            default: throw new InvalidOperationException("Hình này không hỗ trợ viền Rỗng.");
        }
    }
    public int Level => level;
    public Vector2 Size => size;
    public float Rotation => rotation;
    public IReadOnlyList<Vector2> CustomPoints => customPoints;
    public FormationSymmetry Symmetry => symmetry;
    public float CustomScale => customScale;
    /// <summary>Tính số thành viên từ hình và Level, hoặc các điểm Custom sau đối xứng.</summary>
    public int SlotCount => FormationBuilder.GetSlotCount(this);

    #endregion

    #region Kiểm tra cấu hình

    /// <summary>Kiểm tra hình, Level, kích thước và tọa độ Custom hữu hạn.</summary>
    public void Validate()
    {
        if (!Enum.IsDefined(typeof(FormationType), shape) || !Enum.IsDefined(typeof(FormationFillMode), fillMode) || level < 1 || level > 16)
        {
            throw new InvalidOperationException("Formation cần hình hợp lệ và cấp từ 1 đến 16.");
        }

        if (fillMode == FormationFillMode.Hollow)
        {
            if (!SupportsHollow(shape))
                throw new InvalidOperationException("Mode Rỗng chỉ nhận hình khép kín: tròn, vuông, tam giác, thoi hoặc lục giác.");
            if (HollowSlotCount > 4096)
                throw new InvalidOperationException("Số slot Rỗng vượt 4096; giảm Level của hình.");
        }

        if (!Finite(size.x) || !Finite(size.y) || size.x <= 0f || size.y <= 0f || !Finite(rotation))
        {
            throw new InvalidOperationException("Size phải dương, hữu hạn; góc xoay phải hữu hạn.");
        }

        if (shape != FormationType.Custom)
        {
            return;
        }

        if (!Enum.IsDefined(typeof(FormationSymmetry), symmetry) || !Finite(customScale) || customScale <= 0f ||
            customPoints == null || customPoints.Length < 1 || customPoints.Length > 1024)
        {
            throw new InvalidOperationException("Custom cần 1–1024 điểm gốc, Scale dương và chế độ đối xứng hợp lệ.");
        }

        foreach (Vector2 point in customPoints)
        {
            if (!Finite(point.x) || !Finite(point.y))
            {
                throw new InvalidOperationException("Điểm Custom phải có tọa độ hữu hạn.");
            }
        }
    }

    /// <summary>Kiểm tra số thực không NaN hoặc vô cực.</summary>
    private static bool Finite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    #endregion
}
