using System;
using System.Collections.Generic;
using UnityEngine;

internal sealed class FrostAuraSkillRuntime : ISkill, IRunLevelSkill, IContinuousSkill, IDisposable
{
    private readonly FrostAuraSkill data;
    private readonly CharacterSkills owner;
    private readonly PoolManager pools;
    private readonly Dictionary<CharacterTarget, (CharacterEffectHandler Handler, long Token)> slowed =
        new Dictionary<CharacterTarget, (CharacterEffectHandler, long)>();
    private readonly HashSet<CharacterTarget> inside = new HashSet<CharacterTarget>();
    private readonly List<CharacterTarget> leaving = new List<CharacterTarget>();
    private CharacterTarget[] targets = Array.Empty<CharacterTarget>();
    private SkillAreaVisual visual;
    private float radius;
    private bool paused;
    private bool disposed;
    private int revision;

    internal FrostAuraSkillRuntime(FrostAuraSkill data, CharacterSkills owner, PoolManager pools)
    {
        this.data = data; this.owner = owner; this.pools = pools;
        radius = data.RadiusAt(1);
    }

    public void SetRunLevel(int level)
    {
        radius = data.RadiusAt(level);
        revision++;
        if (visual != null) visual.SetRadius(radius);
        // Reset về cấp 1 phải gỡ ngay slow cũ, không để mục tiêu ngoài bán kính mới còn chịu aura.
        ClearSlow();
    }

    public bool TryExecute() => false;

    public void Tick(float deltaTime)
    {
        if (disposed || paused || !owner.CanRun || deltaTime <= 0f) return;
        if (visual == null)
        {
            GameObject clone = pools.Spawn(data.VisualPrefab.gameObject, owner.Owner.HitPosition, Quaternion.identity,
                beforeActivate: item => item.GetComponent<SkillAreaVisual>().Initialize(owner.Owner, radius));
            if (clone == null) throw new InvalidOperationException("Could not spawn frost visual.");
            visual = clone.GetComponent<SkillAreaVisual>();
        }
        int found = owner.Owner.Search.FindAll(owner.Owner.HitPosition, radius,
            CharacterSearch.OpponentOf(owner.Identity.Team), ref targets, owner.Owner);
        inside.Clear();
        int currentRevision = revision;
        try
        {
            for (int i = 0; i < found && !disposed && !paused && owner.CanRun && currentRevision == revision; i++)
            {
                CharacterTarget target = targets[i];
                if (!target.TryGetCharacter(out CharacterView victim) || victim.Effects == null || victim.Effects.IsDisposed) continue;
                inside.Add(target);
                if (slowed.ContainsKey(target)) continue;
                CharacterEffectHandler handler = victim.Effects;
                long token = handler.AddSpeedMultiplier(1f - data.SlowFraction);
                // Stats callback có thể vừa thu hồi chính chủ aura/mục tiêu.
                if (disposed || paused || !owner.CanRun || currentRevision != revision || !target.TryGetCharacter(out _))
                {
                    handler.RemoveSpeedMultiplier(token);
                    continue;
                }
                slowed.Add(target, (handler, token));
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
        entry.Handler.RemoveSpeedMultiplier(entry.Token);
    }

    private void ClearSlow()
    {
        // Không giữ enumerator khi gỡ token có thể phát callback. Mỗi token thuộc đúng một nguồn aura.
        while (slowed.Count > 0)
        {
            CharacterTarget target;
            using (var iterator = slowed.Keys.GetEnumerator()) { iterator.MoveNext(); target = iterator.Current; }
            try { RemoveSlow(target); }
            catch (Exception exception) { Debug.LogException(exception); }
        }
    }

    public void SetPaused(bool value)
    {
        if (paused == value) return;
        paused = value;
        if (visual != null) visual.SetVisible(!value);
        if (value) ClearSlow();
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        try { ClearSlow(); }
        finally
        {
            if (visual != null) pools.Despawn(visual.gameObject);
            visual = null;
            Array.Clear(targets, 0, targets.Length);
        }
    }
}
