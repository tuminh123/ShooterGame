using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

// Cấu hình của một loại hành vi tại một cấp; không chứa state runtime.
[Serializable]
public sealed class OrbitSawData : SkillBehaviourData
{
    #region Configuration

    public SkillAreaVisual visualPrefab; // Prefab visual của lưỡi cưa.
    [Min(1)] public int bladeCount = 1; // Số lưỡi ở cấp này.
    [Min(0.01f)] public float orbitRadius = 0.5f; // Bán kính quỹ đạo.
    [Min(0.01f)] public float bladeRadius = 0.1f; // Bán kính gây hit của một lưỡi.
    [Min(0.01f)] public float degreesPerSecond = 180f; // Tốc độ quay.
    [Min(0.01f)] public float hitInterval = 0.5f; // Nhịp hit riêng theo lưỡi và mục tiêu.
    [SerializeReference] public List<IGameplayEffect> effects = new List<IGameplayEffect>(); // Áp dụng khi cưa đánh trúng.
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
