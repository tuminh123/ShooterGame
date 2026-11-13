using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

// Cấu hình của một loại hành vi tại một cấp; không chứa state runtime.
[Serializable]
public sealed class OrbitSawData : SkillBehaviourData
{
    #region Configuration

    [Tooltip("Prefab hiển thị lưỡi cưa quay quanh chủ nhân.")] public SkillAreaVisual visualPrefab;
    [Tooltip("Số lưỡi cưa hoạt động ở cấp này.")][Min(1)] public int bladeCount = 1;
    [Tooltip("Khoảng cách từ chủ nhân đến quỹ đạo lưỡi cưa.")][Min(0.01f)] public float orbitRadius = 0.5f;
    [Tooltip("Bán kính va chạm của mỗi lưỡi cưa.")][Min(0.01f)] public float bladeRadius = 0.1f;
    [Tooltip("Tốc độ quay quanh chủ nhân, tính theo độ mỗi giây.")][Min(0.01f)] public float degreesPerSecond = 180f;
    [Tooltip("Thời gian tối thiểu trước khi cùng lưỡi đánh lại cùng mục tiêu.")][Min(0.01f)] public float hitInterval = 0.5f;
    [Tooltip("Các hiệu ứng áp dụng khi lưỡi cưa đánh trúng.")][SerializeReference] public List<IGameplayEffect> effects = new List<IGameplayEffect>();
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
