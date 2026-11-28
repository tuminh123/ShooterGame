using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Buff chỉ số có thời hạn do skill cấp (khiên về tay +DEF 3s, mỗi quái giết +1% Damage 5s…).
/// Giữ lease của từng buff và gỡ khi hết giờ; buff cùng <c>key</c> trên cùng nhân vật thì gia hạn và cập nhật giá trị thay vì chồng.
/// </summary>
/// <remarks>
/// Không dùng EffectSystem vì effect chỉ áp lên phe đối địch và có luật xác suất/kháng. Lease của lượt spawn cũ tự vô hiệu
/// (StatSheet có version), nên nhân vật chết/về pool thì chỉ cần bỏ entry.
/// Do <see cref="CombatManager"/> tạo và tick; không bind vào Zenject.
/// </remarks>
public sealed class TimedBuffSystem
{
    #region Types

    private sealed class Entry
    {
        public CharacterHandle Owner;
        public object Key; // Nguồn buff (thường là skill data); cùng key thì gia hạn.
        public StatModifierLease Lease;
        public float Remaining;
    }

    #endregion

    #region State

    private readonly List<Entry> active = new List<Entry>(16);
    private readonly Stack<Entry> spare = new Stack<Entry>();

    /// <summary>Số buff đang chạy (kiểm tra).</summary>
    public int ActiveCount => active.Count;

    #endregion

    #region Apply

    /// <summary>
    /// Cấp hoặc gia hạn buff <paramref name="modifier"/> trong <paramref name="seconds"/> giây cho <paramref name="owner"/>.
    /// Cùng owner + key đang chạy: thay giá trị (Update) và đặt lại thời gian.
    /// </summary>
    public void Apply(CharacterHandle owner, object key, StatModifier modifier, float seconds)
    {
        if (key == null) throw new ArgumentNullException(nameof(key));
        if (!(seconds > 0f) || float.IsInfinity(seconds)) throw new ArgumentOutOfRangeException(nameof(seconds));
        modifier.Validate();
        if (!owner.TryGetCharacter(out CharacterView view) || !view.RuntimeData.IsAlive) return;
        for (int index = 0; index < active.Count; index++)
        {
            Entry existing = active[index];
            if (!ReferenceEquals(existing.Key, key) || !existing.Owner.Equals(owner)) continue;
            existing.Remaining = seconds;
            existing.Lease.Update(modifier);
            return;
        }
        StatModifierLease lease = view.RuntimeData.Stats.AddSource(modifier);
        Entry entry = spare.Count > 0 ? spare.Pop() : new Entry();
        entry.Owner = owner;
        entry.Key = key;
        entry.Lease = lease;
        entry.Remaining = seconds;
        active.Add(entry);
    }

    #endregion

    #region Frame

    /// <summary>Trừ thời gian; hết giờ hoặc chủ không còn thì gỡ.</summary>
    public void Tick(float deltaTime)
    {
        for (int index = active.Count - 1; index >= 0; index--)
        {
            Entry entry = active[index];
            entry.Remaining -= deltaTime;
            if (entry.Remaining > 0f && entry.Owner.IsAlive) continue;
            RemoveAt(index);
        }
    }

    /// <summary>Gỡ mọi buff (kết thúc trận).</summary>
    public void Clear()
    {
        for (int index = active.Count - 1; index >= 0; index--) RemoveAt(index);
    }

    private void RemoveAt(int index)
    {
        Entry entry = active[index];
        int last = active.Count - 1;
        active[index] = active[last];
        active.RemoveAt(last);
        StatModifierLease lease = entry.Lease;
        entry.Lease = null;
        entry.Key = null;
        entry.Owner = default;
        spare.Push(entry);
        try { lease?.Dispose(); } // Lease của lượt spawn cũ tự bỏ qua (version).
        catch (Exception exception) { Debug.LogException(exception); }
    }

    #endregion
}
