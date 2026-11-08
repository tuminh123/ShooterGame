using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

internal sealed class OrbitSawSkillRuntime : ISkill, IRunLevelSkill, IContinuousSkill, IDisposable
{
    private readonly OrbitSawSkill data;
    private readonly CharacterSkills owner;
    private readonly PoolManager pools;
    private readonly SkillAreaVisual[] blades = new SkillAreaVisual[5];
    private readonly Dictionary<CharacterTarget, float>[] nextHits = new Dictionary<CharacterTarget, float>[5];
    private readonly List<CharacterTarget> expired = new List<CharacterTarget>();
    private CharacterTarget[] targets = Array.Empty<CharacterTarget>();
    private int count = 1;
    private float angle;
    private float clock;
    private Vector2 previousCenter;
    private bool hasPrevious;
    private bool paused;
    private bool disposed;
    private int revision;

    internal OrbitSawSkillRuntime(OrbitSawSkill data, CharacterSkills owner, PoolManager pools)
    {
        this.data = data; this.owner = owner; this.pools = pools;
        for (int i = 0; i < nextHits.Length; i++) nextHits[i] = new Dictionary<CharacterTarget, float>();
    }

    public void SetRunLevel(int level)
    {
        revision++;
        count = data.BladeCountAt(level);
        if (level == 1)
        {
            angle = clock = 0f;
            foreach (var hits in nextHits) hits.Clear();
        }
        // Reset có thể giảm số lưỡi; trả đúng phần thừa và xóa lịch hit của chúng.
        for (int i = count; i < blades.Length; i++)
        {
            if (blades[i] != null) pools.Despawn(blades[i].gameObject);
            blades[i] = null;
            nextHits[i].Clear();
        }
        hasPrevious = false; // Đổi cách chia góc không tạo sweep giả xuyên qua tâm quỹ đạo.
    }

    public bool TryExecute() => false; // Skill duy trì liên tục, không cần cast lại theo cooldown.

    public void Tick(float deltaTime)
    {
        if (disposed || paused || !owner.CanRun || deltaTime <= 0f) return;
        Vector2 center = owner.Owner.HitPosition;
        if (!hasPrevious) previousCenter = center;
        float turn = data.DegreesPerSecond * deltaTime;
        int currentRevision = revision;
        clock += deltaTime;
        // Query rộng một lần, rồi dùng hình học sweep cho từng lưỡi. Không dùng Physics2D.
        Vector2 middle = (center + previousCenter) * 0.5f;
        int found = owner.Owner.Search.FindAll(middle,
            data.OrbitRadius + data.BladeRadius + Vector2.Distance(center, previousCenter) * 0.5f,
            CharacterSearch.OpponentOf(owner.Identity.Team), ref targets, owner.Owner);
        try
        {
            for (int blade = 0; blade < count && !disposed && owner.CanRun && currentRevision == revision; blade++)
            {
                EnsureBlade(blade);
                Prune(nextHits[blade]);
                float phase = 360f * blade / count;
                // Chia cung quay thành đoạn <=15 độ, tránh chỉ đo hai đầu khiến cưa xuyên quái ở frame chậm.
                int steps = Mathf.Clamp(Mathf.CeilToInt(turn / 15f), 1, 128);
                Vector2 start = previousCenter + Direction(angle + phase) * data.OrbitRadius;
                for (int step = 1; step <= steps && !disposed && owner.CanRun && currentRevision == revision; step++)
                {
                    float t = step / (float)steps;
                    Vector2 end = Vector2.Lerp(previousCenter, center, t) + Direction(angle + turn * t + phase) * data.OrbitRadius;
                    for (int i = 0; i < found && !disposed && owner.CanRun && currentRevision == revision; i++)
                    {
                        CharacterTarget target = targets[i];
                        if (nextHits[blade].ContainsKey(target) || !target.TryGetCharacter(out CharacterView victim)) continue;
                        if (!SpatialGeometry.SweepCircle(new float2(start.x, start.y), new float2(end.x, end.y),
                            data.BladeRadius, victim.GetSpatialEntity(), out _)) continue;
                        // Lịch riêng theo lưỡi + danh tính spawn. Ghi trước callback damage để chống gọi lồng nhau.
                        nextHits[blade][target] = clock + data.HitInterval;
                        victim.ReceiveDamage(owner.Identity.CreateDamage());
                    }
                    start = end;
                }
                if (!disposed && currentRevision == revision && blades[blade] != null)
                    blades[blade].SetPose(Direction(angle + turn + phase) * data.OrbitRadius, (angle + turn) * 3f);
            }
        }
        finally { Array.Clear(targets, 0, targets.Length); }
        if (disposed || currentRevision != revision) return;
        angle = Mathf.Repeat(angle + turn, 360f);
        previousCenter = center;
        hasPrevious = true;
    }

    private static Vector2 Direction(float degrees)
        => new Vector2(Mathf.Cos(degrees * Mathf.Deg2Rad), Mathf.Sin(degrees * Mathf.Deg2Rad));

    private void EnsureBlade(int index)
    {
        if (blades[index] != null) return;
        GameObject clone = pools.Spawn(data.VisualPrefab.gameObject, owner.Owner.HitPosition, Quaternion.identity,
            beforeActivate: item => item.GetComponent<SkillAreaVisual>().Initialize(owner.Owner, data.BladeRadius));
        if (clone == null) throw new InvalidOperationException("Could not spawn saw visual.");
        blades[index] = clone.GetComponent<SkillAreaVisual>();
    }

    private void Prune(Dictionary<CharacterTarget, float> hits)
    {
        expired.Clear();
        foreach (var hit in hits)
            if (hit.Value <= clock || !hit.Key.TryGetCharacter(out _)) expired.Add(hit.Key);
        foreach (CharacterTarget target in expired) hits.Remove(target);
        expired.Clear();
    }

    public void SetPaused(bool value)
    {
        if (paused == value) return;
        paused = value;
        hasPrevious = false;
        foreach (SkillAreaVisual blade in blades) if (blade != null) blade.SetVisible(!value);
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        for (int i = 0; i < blades.Length; i++)
        {
            if (blades[i] != null) pools.Despawn(blades[i].gameObject);
            blades[i] = null;
            nextHits[i].Clear();
        }
        Array.Clear(targets, 0, targets.Length);
        expired.Clear();
    }
}
