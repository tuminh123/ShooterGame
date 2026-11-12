using System;
using System.Collections.Generic;
using UnityEngine;

public enum FormationSymmetry
{
    [InspectorName("Tự do")] None,
    [InspectorName("Trái / phải")] MirrorHorizontal,
    [InspectorName("Trên / dưới")] MirrorVertical,
    [InspectorName("Cả hai trục")] BothAxes,
    [InspectorName("Qua tâm")] AroundCenter
}

// Hình tự quyết định số slot. Wave không cắt bớt điểm để ép hình theo số enemy bất kỳ.
[CreateAssetMenu(fileName = "EnemyFormation", menuName = "Shooter Game/Enemy/Formation")]
public sealed class EnemyFormationSO : ScriptableObject
{
    #region Geometry Configuration

    [Tooltip("Hình chuẩn luôn đối xứng; Custom cho phép tự đặt điểm và chọn đối xứng tự động.")]
    [SerializeField] private FormationType shape = FormationType.FilledSquare;

    [Tooltip("Cấp 1 là hình nhỏ nhất. Tăng cấp thêm hàng/lớp và số enemy; không tăng kích thước ngoài. Dùng Size để giãn hình.")]
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

    #region Formation Movement

    [Tooltip("Tốc độ cả đội hình trên Path theo world unit/giây. Dùng chung cho mọi lượt chọn asset này. Nhập 3 là 3 unit/giây, không nhân tốc độ enemy. Camera dịch là chuyển động cộng thêm; khi tan nhóm enemy dùng tốc độ riêng.")]
    [Min(0.01f)] [SerializeField] private float speed = 3f;

    #endregion

    #region Read Access And Validation

    public float Speed => speed;
    public FormationType Shape => shape;
    public int Level => level;
    public Vector2 Size => size;
    public float Rotation => rotation;
    public IReadOnlyList<Vector2> CustomPoints => customPoints;
    public FormationSymmetry Symmetry => symmetry;
    public float CustomScale => customScale;
    public int SlotCount => FormationBuilder.GetSlotCount(this);

    public void Validate()
    {
        if (!Finite(speed) || speed <= 0f)
        {
            throw new InvalidOperationException("Speed của Formation phải dương và hữu hạn.");
        }

        if (!Enum.IsDefined(typeof(FormationType), shape) || level < 1 || level > 16)
        {
            throw new InvalidOperationException("Formation cần hình hợp lệ và cấp từ 1 đến 16.");
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

    private static bool Finite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    #endregion
}

public enum EnemyFormationMovement
{
    [InspectorName("Giữ nguyên cả khối")]
    KeepShape,

    [InspectorName("Nối đuôi trên đường")]
    FollowPath
}

// Thông số của một lượt spawn: thuộc Wave, không thuộc asset hình dạng hoặc asset đường đi.
[Serializable]
public sealed class EnemyFormationSettings
{
    #region Group Movement

    [Tooltip("Giữ nguyên cả khối: mọi thành viên nhận chuyển động chung. Nối đuôi: từng slot đi qua cùng đường với khoảng cách trễ.")]
    public EnemyFormationMovement movement;

    [Tooltip("1 lượt = A → B. 2 lượt = A → B → A. Nối đuôi chờ slot cuối đi hết lượt cuối mới giải tán.")]
    [Range(1, 100)] public int passes = 1;

    [Tooltip("Khoảng cách dọc đường giữa hai slot nối đuôi, theo world unit. Chỉ dùng với chế độ nối đuôi.")]
    [Min(0.05f)] public float followSpacing = 0.8f;

    #endregion

    #region Formation Break Reaction

    [Tooltip("Thời gian thực hiện knockback theo giây gameplay. Dùng riêng cho phá đội hình, không đổi recoil của đòn đánh thường.")]
    [Min(0.01f)] public float knockbackDuration = 0.2f;

    [Tooltip("Thời gian choáng cả nhóm sau khi kết thúc knockback. Hết choáng, từng enemy đuổi player bằng tốc độ riêng. Hết đường không gây choáng.")]
    [Min(0f)] public float stunDuration = 0.5f;

    #endregion

    #region Copy And Validation

    // Các trường đều là kiểu giá trị; bản sao không giữ tham chiếu tới dữ liệu đang chỉnh trong Inspector.
    public EnemyFormationSettings Copy()
    {
        return (EnemyFormationSettings)MemberwiseClone();
    }

    public void Validate()
    {
        if (!Enum.IsDefined(typeof(EnemyFormationMovement), movement) ||
            passes < 1 || passes > 100)
        {
            throw new InvalidOperationException("Kiểm tra kiểu di chuyển và số lượt.");
        }

        if (movement == EnemyFormationMovement.FollowPath && (!IsFinite(followSpacing) || followSpacing < 0.05f))
        {
            throw new InvalidOperationException("Khoảng cách nối đuôi phải ít nhất 0.05 world unit.");
        }

        if (!IsFinite(knockbackDuration) || knockbackDuration <= 0f ||
            !IsFinite(stunDuration) || stunDuration < 0f)
        {
            throw new InvalidOperationException("Kiểm tra khoảng văng, thời gian knockback và thời gian choáng.");
        }
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    #endregion
}