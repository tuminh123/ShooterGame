using System;
using System.Collections.Generic;
using UnityEngine;

// Hành vi runtime; nhận data theo cấp từ SkillRuntime.
[Serializable]
public sealed class FrostAuraSkill : Skill
{
    #region Runtime State

    [NonSerialized] private FrostAuraData data; // Cấu hình đang hoạt động.
    [NonSerialized] private IGameplayEffect[] effectSnapshot; // Danh sách effect của đúng cấp.
    [NonSerialized] private Dictionary<CharacterTarget, EffectHandle> slowed; // Handle runtime riêng của từng mục tiêu trong vùng.
    [NonSerialized] private SlowEffect slowEffect; // Cấu hình slow dùng chung; manager sở hữu runtime.
    [NonSerialized] private HashSet<CharacterTarget> inside; // Mục tiêu được query trong bước này.
    [NonSerialized] private List<CharacterTarget> leaving; // Buffer gỡ effect không còn trong vùng.
    [NonSerialized] private CharacterTarget[] targets; // Buffer query dùng lại.
    [NonSerialized] private SkillAreaVisual visual; // Visual đang thuê pool.
    [NonSerialized] private float radius; // Bán kính được dùng khi query.
    [NonSerialized] private bool paused; // Pause phải gỡ slow duy trì.
    [NonSerialized] private bool disposed; // Cleanup một lần.
    [NonSerialized] private int revision; // Chặn vòng Tick cũ sau callback thay data.
    public override SkillBehaviourType Type => SkillBehaviourType.SlowAura;

    #endregion

    #region Configuration

    // Cấp phát state độc lập cho đúng owner.
    protected override void InitializeRuntime()
    {
        data = null;
        effectSnapshot = Array.Empty<IGameplayEffect>();
        slowed = new Dictionary<CharacterTarget, EffectHandle>();
        slowEffect = null;
        inside = new HashSet<CharacterTarget>();
        leaving = new List<CharacterTarget>();
        targets = Array.Empty<CharacterTarget>();
        visual = null;
        radius = 0f;
        revision = 0;
        paused = disposed = false;
    }

    // Đổi data sẽ gỡ runtime cũ; bước Tick tiếp theo áp dụng đúng vùng và mức slow mới.
    public override void ApplyData(SkillBehaviourData configuration)
    {
        var next = configuration as FrostAuraData ?? throw new ArgumentException("FrostAuraSkill requires FrostAuraData.");
        next.Validate();
        revision++;
        ClearSlow();
        if (visual != null && data != null && data.visualPrefab != next.visualPrefab)
        {
            Services.Pools.Despawn(visual.gameObject);
            visual = null;
        }
        data = next;
        slowEffect = new SlowEffect(next.slowFraction);
        radius = next.radius;
        effectSnapshot = next.effects.ToArray();
        if (visual != null) visual.SetRadius(radius);
    }

    #endregion

    #region Continuous Execution And Cleanup
    // Cập nhật vùng tác động bằng truy vấn không gian chung, không dùng Physics2D.
    public override void Tick(float deltaTime)
    {
        if (disposed || paused || !Owner.CanRun || deltaTime <= 0f) return;
        if (visual == null)
        {
            GameObject clone = Services.Pools.Spawn(data.visualPrefab.gameObject, Owner.Owner.HitPosition, Quaternion.identity,
                beforeActivate: item => item.GetComponent<SkillAreaVisual>().Initialize(Owner.Owner, radius));
            if (clone == null) throw new InvalidOperationException("Could not spawn frost visual.");
            visual = clone.GetComponent<SkillAreaVisual>();
        }
        int found = Owner.Owner.Search.FindAll(Owner.Owner.HitPosition, radius,
            CharacterSearch.OpponentOf(Owner.Identity.Team), ref targets, Owner.Owner);
        inside.Clear();
        int currentRevision = revision;
        try
        {
            for (int i = 0; i < found && !disposed && !paused && Owner.CanRun && currentRevision == revision; i++)
            {
                CharacterTarget target = targets[i];
                if (!target.TryGetCharacter(out _)) continue;
                inside.Add(target);
                if (slowed.ContainsKey(target)) continue;
                EffectHandle handle = Services.Effects.ApplyMaintained(slowEffect, target, Owner.Identity.Team);
                // Stats callback có thể vừa thu hồi chính chủ aura/mục tiêu.
                if (disposed || paused || !Owner.CanRun || currentRevision != revision || !target.TryGetCharacter(out _))
                {
                    Services.Effects.Remove(handle);
                    continue;
                }
                slowed.Add(target, handle);
                // Chỉ áp dụng lúc vào vùng; không spam hoặc gia hạn duration mỗi frame.
                if (Services.Effects != null)
                    foreach (IGameplayEffect effect in effectSnapshot)
                    {
                        if (!Owner.CanRun || disposed || currentRevision != revision) break;
                        Services.Effects.Apply(effect, target, Owner.Identity.Team, Owner.Identity.FinalStats.effectHit);
                    }
            }
            leaving.Clear();
            foreach (CharacterTarget target in slowed.Keys)
                if (!inside.Contains(target) || !target.TryGetCharacter(out _)) leaving.Add(target);
            foreach (CharacterTarget target in leaving) RemoveSlow(target);
        }
        finally
        {
            Array.Clear(targets, 0, targets.Length);
            inside.Clear();
            leaving.Clear();
        }
    }

    private void RemoveSlow(CharacterTarget target)
    {
        if (!slowed.TryGetValue(target, out var entry)) return;
        slowed.Remove(target); // Gỡ ownership trước callback tính lại stats.
        Services.Effects.Remove(entry);
    }

    private void ClearSlow()
    {
        // Không giữ enumerator khi gỡ effect có thể phát callback. Mỗi handle thuộc đúng một nguồn aura.
        while (slowed.Count > 0)
        {
            CharacterTarget target;
            using (var iterator = slowed.Keys.GetEnumerator()) { iterator.MoveNext(); target = iterator.Current; }
            try { RemoveSlow(target); }
            catch (Exception exception) { Debug.LogException(exception); }
        }
    }

    // Đồng bộ visual và trạng thái liên tục với pause của Owner.
    public override void SetPaused(bool value)
    {
        if (paused == value) return;
        paused = value;
        if (visual != null) visual.SetVisible(!value);
        if (value) ClearSlow();
    }

    // Thu hồi visual và state của đúng instance; gọi nhiều lần vẫn an toàn.
    public override void Dispose()
    {
        if (disposed) return;
        disposed = true;
        try { ClearSlow(); }
        finally
        {
            if (visual != null) Services.Pools.Despawn(visual.gameObject);
            visual = null;
            Array.Clear(targets, 0, targets.Length);
        }
    }


    #endregion
}
