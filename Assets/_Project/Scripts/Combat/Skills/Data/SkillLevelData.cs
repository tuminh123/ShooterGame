using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class SkillLevelData
{
    [Tooltip("Mô tả hiển thị khi skill đạt cấp này; để trống sẽ dùng mô tả chung của skill.")]
    [SerializeField, TextArea] private string description; // Mô tả cấp này.
    [Tooltip("Thông số của các hành vi được bật ở cấp này. Bỏ một entry để hành vi chưa mở ở cấp này.")]
    [SerializeReference] private SkillBehaviourData[] behaviours = Array.Empty<SkillBehaviourData>(); // Các loại có mặt tại cấp này mới được mở.
    public string Description => description;
    public IReadOnlyList<SkillBehaviourData> Behaviours => behaviours == null ? null : Array.AsReadOnly(behaviours);
}
