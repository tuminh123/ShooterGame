using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Danh sách <see cref="SkillAreaVisual"/> đang hiển thị (aura băng, cưa xoay). <see cref="CombatManager"/> tạo
/// và gọi <see cref="LateTick"/> để mọi visual bám theo chủ sau khi Simulation đã áp di chuyển.
/// </summary>
/// <remarks>
/// Visual tự thêm/gỡ mình ở OnEnable/OnDisable, nên mọi đường thu hồi (đổi data, dừng skill, dọn trận,
/// Destroy) đều tự rời danh sách mà skill không phải nhớ gỡ.
/// </remarks>
public sealed class SkillAreaVisuals
{
    #region State

    private readonly HashSet<SkillAreaVisual> active = new HashSet<SkillAreaVisual>();
    private readonly List<SkillAreaVisual> snapshot = new List<SkillAreaVisual>(16); // Dùng lại mỗi frame.

    #endregion

    #region Registration

    /// <summary>Visual vừa bật. Gọi lặp lại vẫn an toàn.</summary>
    internal void Add(SkillAreaVisual visual) => active.Add(visual);

    /// <summary>Visual vừa tắt hoặc bị hủy.</summary>
    internal void Remove(SkillAreaVisual visual) => active.Remove(visual);

    /// <summary>Bỏ mọi tham chiếu khi CombatManager dispose.</summary>
    internal void Clear() => active.Clear();

    #endregion

    #region Frame

    /// <summary>Áp vị trí/góc của mọi visual theo chủ. Duyệt bản chụp vì một visual có thể bị tắt giữa vòng lặp.</summary>
    internal void LateTick()
    {
        if (active.Count == 0) return;
        snapshot.Clear();
        snapshot.AddRange(active);
        try
        {
            foreach (SkillAreaVisual visual in snapshot)
            {
                if (visual == null) continue;
                try { visual.LateTick(); }
                catch (Exception exception) { Debug.LogException(exception, visual); }
            }
        }
        finally { snapshot.Clear(); }
    }

    #endregion
}
