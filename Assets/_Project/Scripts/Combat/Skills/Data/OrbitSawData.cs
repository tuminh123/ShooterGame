using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

/// <summary>Thông số của <see cref="OrbitSawSkill"/> tại một cấp: số lưỡi, bán kính quỹ đạo, tốc độ quay, nhịp đánh lại.</summary>
/// <remarks>Chỉ đọc, không chứa state runtime. Không đổi tên class hoặc field: Unity lưu chúng trong asset skill.</remarks>
[Serializable]
public sealed class OrbitSawData : SkillBehaviourData
{
    #region Configuration

    [SerializeField][Tooltip("Prefab hiển thị lưỡi cưa quay quanh chủ nhân.")] private SkillAreaVisual visualPrefab;
    [SerializeField][Tooltip("Số lưỡi cưa hoạt động ở cấp này.")][Min(1)] private int bladeCount = 1;
    [SerializeField][Tooltip("Khoảng cách từ chủ nhân đến quỹ đạo lưỡi cưa.")][Min(0.01f)] private float orbitRadius = 0.5f;
    [SerializeField][Tooltip("Bán kính va chạm của mỗi lưỡi cưa.")][Min(0.01f)] private float bladeRadius = 0.1f;
    [SerializeField][Tooltip("Tốc độ quay quanh chủ nhân, tính theo độ mỗi giây.")][Min(0.01f)] private float degreesPerSecond = 180f;
    [SerializeField][Tooltip("Thời gian tối thiểu trước khi cùng lưỡi đánh lại cùng mục tiêu.")][Min(0.01f)] private float hitInterval = 0.5f;
    [Tooltip("Các hiệu ứng áp dụng khi lưỡi cưa đánh trúng.")][SerializeReference] private List<IGameplayEffect> effects = new List<IGameplayEffect>();

    /// <summary>Prefab hiển thị lưỡi cưa quay quanh chủ nhân.</summary>
    public SkillAreaVisual VisualPrefab => visualPrefab;
    /// <summary>Số lưỡi cưa hoạt động ở cấp này.</summary>
    public int BladeCount => bladeCount;
    /// <summary>Khoảng cách từ chủ nhân đến quỹ đạo lưỡi cưa.</summary>
    public float OrbitRadius => orbitRadius;
    /// <summary>Bán kính va chạm của mỗi lưỡi cưa.</summary>
    public float BladeRadius => bladeRadius;
    /// <summary>Tốc độ quay quanh chủ nhân, tính theo độ mỗi giây.</summary>
    public float DegreesPerSecond => degreesPerSecond;
    /// <summary>Thời gian tối thiểu trước khi cùng lưỡi đánh lại cùng mục tiêu.</summary>
    public float HitInterval => hitInterval;
    /// <summary>Các hiệu ứng áp dụng khi lưỡi cưa đánh trúng.</summary>
    public IReadOnlyList<IGameplayEffect> Effects => effects;

    public override SkillBehaviourType Type => SkillBehaviourType.OrbitSaw;

    #endregion

    #region Validation

    // Kiểm tra kích thước, số lưỡi và effect trước khi thay visual/state.
    public override void Validate()
    {
        if (visualPrefab == null || bladeCount < 1 || bladeCount > 128 || !Positive(orbitRadius) ||
            !Positive(bladeRadius) || !Positive(degreesPerSecond) || !Positive(hitInterval))
            throw new InvalidOperationException("Invalid orbit saw data.");
        ValidateEffects(effects);
    }
    #endregion
}
