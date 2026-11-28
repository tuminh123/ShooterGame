using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Đặc tính thêm của đạn, nằm trong <see cref="ShootingData"/>: xuyên, kích thước, tác động khi trúng / khi hết đường / theo nhịp.
/// Chỉ đọc; mọi viên đạn của cùng cấp skill dùng chung instance.
/// </summary>
/// <remarks>Không đổi tên class hoặc field: Unity lưu chúng trong asset skill.</remarks>
[Serializable]
public sealed class ProjectileTraits
{
    #region Configuration

    [Tooltip("Số quái xuyên qua sau lần trúng đầu. 0 = trúng là mất; -1 = xuyên tất cả.")]
    [SerializeField, Min(-1)] private int pierce;
    [Tooltip("Bật: đạn va chạm quái. Tắt: đạn bay xuyên không gây đòn (bình ném tới điểm, cầu phép chỉ gây đòn theo nhịp).")]
    [SerializeField] private bool hitsEnemies = true;
    [Tooltip("Hệ số kích thước: nhân cả hình và hitbox của BulletType.")]
    [SerializeField, Min(0.05f)] private float size = 1f;
    [Tooltip("Tốc độ xoay hình (độ/giây), cho rìu/khiên ném. 0 = không xoay.")]
    [SerializeField] private float spinDegreesPerSecond;
    [Tooltip("Đẩy mục tiêu theo HƯỚNG BAY thay vì ra xa người bắn (lao ghim kéo quái theo).")]
    [SerializeField] private bool pushAlongFlight;
    [Tooltip("Tác động mỗi lần trúng (nổ, tách đạn, vũng…), tại điểm trúng.")]
    [SerializeReference] private List<ISkillImpact> onHit = new List<ISkillImpact>();
    [Tooltip("Tác động khi đạn kết thúc tự nhiên: hết thời gian, hết quãng, về tới tay (nổ cuối đường, vỡ mảnh, bình vỡ…).")]
    [SerializeReference] private List<ISkillImpact> onExpire = new List<ISkillImpact>();
    [Tooltip("Nhịp (giây) chạy tác động khi đang bay; 0 = không có.")]
    [SerializeField, Min(0f)] private float pulseInterval;
    [Tooltip("Tác động theo nhịp khi đang bay (cầu phép gây sát thương quanh nó, tường lửa để lại vệt lửa).")]
    [SerializeReference] private List<ISkillImpact> onPulse = new List<ISkillImpact>();

    #endregion

    #region Access

    public int Pierce => pierce;
    public bool HitsEnemies => hitsEnemies;
    public float Size => size;
    public float SpinDegreesPerSecond => spinDegreesPerSecond;
    public bool PushAlongFlight => pushAlongFlight;
    public IReadOnlyList<ISkillImpact> OnHit => onHit;
    public IReadOnlyList<ISkillImpact> OnExpire => onExpire;
    public float PulseInterval => pulseInterval;
    public IReadOnlyList<ISkillImpact> OnPulse => onPulse;

    /// <summary>Đặc tính mặc định của đạn thường (trúng là mất, không tác động phụ).</summary>
    public static readonly ProjectileTraits Default = new ProjectileTraits();

    #endregion

    #region Validation

    public void Validate()
    {
        if (pierce < -1 || !(size > 0f) || float.IsInfinity(size) || float.IsNaN(spinDegreesPerSecond) ||
            !(pulseInterval >= 0f) || float.IsInfinity(pulseInterval))
            throw new InvalidOperationException("Invalid projectile traits.");
        SkillImpacts.Validate(onHit);
        SkillImpacts.Validate(onExpire);
        SkillImpacts.Validate(onPulse);
        if (pulseInterval > 0f && onPulse.Count == 0) throw new InvalidOperationException("Pulse interval needs pulse impacts.");
    }

    #endregion
}
