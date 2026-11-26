using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Data của một cấp skill. Mỗi cấp chứa <b>đầy đủ</b> thông số của các hành vi được bật ở cấp đó,
/// không kế thừa từ cấp trước; hành vi không có entry ở cấp này thì chưa được mở.
/// </summary>
[Serializable]
public sealed class SkillLevelData
{
    [Tooltip("Mô tả hiển thị khi skill đạt cấp này; để trống sẽ dùng mô tả chung của skill.")]
    [SerializeField, TextArea] private string description; // Mô tả cấp này.
    [Tooltip("Thông số của các hành vi được bật ở cấp này. Bỏ một entry để hành vi chưa mở ở cấp này.")]
    [SerializeReference] private SkillBehaviourData[] behaviours = Array.Empty<SkillBehaviourData>(); // Các loại có mặt tại cấp này mới được mở.
    public string Description => description;
    // Trả thẳng mảng qua IReadOnlyList (không bọc ReadOnlyCollection): được đọc mỗi lần spawn khi skill áp cấp.
    // Giữ null để Validate của skill báo lỗi cấu hình thay vì giả là danh sách rỗng.
    public IReadOnlyList<SkillBehaviourData> Behaviours => behaviours;
}
