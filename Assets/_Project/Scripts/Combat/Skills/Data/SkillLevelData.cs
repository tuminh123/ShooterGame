using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class SkillLevelData
{
    [SerializeField, TextArea] private string description; // Mô tả cấp này.
    [SerializeReference] private SkillBehaviourData[] behaviours = Array.Empty<SkillBehaviourData>(); // Các loại có mặt tại cấp này mới được mở.
    public string Description => description;
    public IReadOnlyList<SkillBehaviourData> Behaviours => behaviours == null ? null : Array.AsReadOnly(behaviours);
}
