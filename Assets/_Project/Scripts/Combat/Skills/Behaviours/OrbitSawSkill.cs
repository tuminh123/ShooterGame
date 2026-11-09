using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

// Hành vi runtime; nhận data theo cấp từ SkillRuntime.
[Serializable]
public sealed class OrbitSawSkill : Skill
{
    #region Runtime State

    [NonSerialized] private OrbitSawData data; // Cấu hình cấp đang hoạt động.
    [NonSerialized] private IGameplayEffect[] effectSnapshot; // Effect riêng của cưa.
    [NonSerialized] private SkillAreaVisual[] blades; // Visual đang thuê từ pool.
    [NonSerialized] private Dictionary<CharacterTarget, float>[] nextHits; // Lịch hit riêng của mỗi lưỡi.
    [NonSerialized] private List<CharacterTarget> expired; // Buffer dọn mục tiêu hết cooldown/đã despawn.
    [NonSerialized] private CharacterTarget[] targets; // Buffer query, không cấp phát mỗi frame.
    [NonSerialized] private int count; // Số lưỡi đang chạy.
    [NonSerialized] private float angle; // Góc hiện tại, giữ khi nâng cấp.
    [NonSerialized] private float clock; // Thời gian phục vụ lịch hit.
    [NonSerialized] private Vector2 previousCenter; // Tâm ở bước trước để sweep chuyển động.
    [NonSerialized] private bool hasPrevious; // Không tạo sweep giả khi đổi số lưỡi.
    [NonSerialized] private bool paused; // Trạng thái visual và cập nhật.
    [NonSerialized] private bool disposed; // Cleanup một lần.
    [NonSerialized] private int revision; // Callback đổi data vô hiệu hóa vòng Tick cũ.
    public override SkillBehaviourType Type => SkillBehaviourType.OrbitSaw;

    #endregion

    #region Configuration

    // Cấp phát lại state mutable, không chia sẻ collection với mẫu hoặc owner khác.
    protected override void InitializeRuntime()
    {
        data = null;
        effectSnapshot = Array.Empty<IGameplayEffect>();
        blades = Array.Empty<SkillAreaVisual>();
        nextHits = Array.Empty<Dictionary<CharacterTarget, float>>();
        expired = new List<CharacterTarget>();
        targets = Array.Empty<CharacterTarget>();
        count = revision = 0;
        angle = clock = 0f;
        hasPrevious = paused = disposed = false;
    }

    // Tăng số lưỡi bằng data; giữ góc quay và lịch hit của những lưỡi còn tồn tại.
    public override void ApplyData(SkillBehaviourData configuration)
    {
        var next = configuration as OrbitSawData ?? throw new ArgumentException("OrbitSawSkill requires OrbitSawData.");
        next.Validate();
        revision++;
        bool changedPrefab = data != null && data.visualPrefab != next.visualPrefab;
        for (int i = 0; i < blades.Length; i++)
        {
            if (i < next.bladeCount && !changedPrefab) continue;
            if (blades[i] != null) Services.Pools.Despawn(blades[i].gameObject);
            blades[i] = null;
            nextHits[i].Clear();
        }
        Array.Resize(ref blades, next.bladeCount);
        Array.Resize(ref nextHits, next.bladeCount);
        for (int i = 0; i < nextHits.Length; i++)
        {
            if (nextHits[i] == null) nextHits[i] = new Dictionary<CharacterTarget, float>();
            if (blades[i] != null) blades[i].SetRadius(next.bladeRadius);
        }
        data = next;
        effectSnapshot = next.effects.ToArray();
        count = next.bladeCount;
        hasPrevious = false;
    }

    #endregion

    #region Continuous Execution And Cleanup
    // Cập nhật vùng tác động bằng truy vấn không gian chung, không dùng Physics2D.
    public override void Tick(float deltaTime)
    {
        if (disposed || paused || !Owner.CanRun || deltaTime <= 0f) return;
        Vector2 center = Owner.Owner.HitPosition;
        if (!hasPrevious) previousCenter = center;
        float turn = data.degreesPerSecond * deltaTime;
        int currentRevision = revision;
        clock += deltaTime;
        // Query rộng một lần, rồi dùng hình học sweep cho từng lưỡi. Không dùng Physics2D.
        Vector2 middle = (center + previousCenter) * 0.5f;
        int found = Owner.Owner.Search.FindAll(middle,
            data.orbitRadius + data.bladeRadius + Vector2.Distance(center, previousCenter) * 0.5f,
            CharacterSearch.OpponentOf(Owner.Identity.Team), ref targets, Owner.Owner);
        try
        {
            for (int blade = 0; blade < count && !disposed && Owner.CanRun && currentRevision == revision; blade++)
            {
                EnsureBlade(blade);
                Prune(nextHits[blade]);
                float phase = 360f * blade / count;
                // Chia cung quay thành đoạn <=15 độ, tránh chỉ đo hai đầu khiến cưa xuyên quái ở frame chậm.
                int steps = Mathf.Clamp(Mathf.CeilToInt(turn / 15f), 1, 128);
                Vector2 start = previousCenter + Direction(angle + phase) * data.orbitRadius;
                for (int step = 1; step <= steps && !disposed && Owner.CanRun && currentRevision == revision; step++)
                {
                    float t = step / (float)steps;
                    Vector2 end = Vector2.Lerp(previousCenter, center, t) + Direction(angle + turn * t + phase) * data.orbitRadius;
                    for (int i = 0; i < found && !disposed && Owner.CanRun && currentRevision == revision; i++)
                    {
                        CharacterTarget target = targets[i];
                        if (nextHits[blade].ContainsKey(target) || !target.TryGetCharacter(out CharacterView victim)) continue;
                        if (!SpatialGeometry.SweepCircle(new float2(start.x, start.y), new float2(end.x, end.y),
                            data.bladeRadius, victim.GetSpatialEntity(), out _)) continue;
                        // Lịch riêng theo lưỡi + danh tính spawn. Ghi trước callback damage để chống gọi lồng nhau.
                        nextHits[blade][target] = clock + data.hitInterval;
                        DamageResult hit = victim.ReceiveDamage(Owner.Identity.CreateDamage());
                        if (hit.Accepted && Services.Effects != null && Owner.CanRun)
                            foreach (IGameplayEffect effect in effectSnapshot)
                            {
                                if (!Owner.CanRun || disposed || currentRevision != revision) break;
                                Services.Effects.Apply(effect, target, Owner.Identity.Team, Owner.Identity.FinalStats.effectHit);
                            }
                    }
                    start = end;
                }
                if (!disposed && currentRevision == revision && blades[blade] != null)
                    blades[blade].SetPose(Direction(angle + turn + phase) * data.orbitRadius, (angle + turn) * 3f);
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
        GameObject clone = Services.Pools.Spawn(data.visualPrefab.gameObject, Owner.Owner.HitPosition, Quaternion.identity,
            beforeActivate: item => item.GetComponent<SkillAreaVisual>().Initialize(Owner.Owner, data.bladeRadius));
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

    // Đồng bộ visual và trạng thái liên tục với pause của Owner.
    public override void SetPaused(bool value)
    {
        if (paused == value) return;
        paused = value;
        hasPrevious = false;
        foreach (SkillAreaVisual blade in blades) if (blade != null) blade.SetVisible(!value);
    }

    // Thu hồi visual và state của đúng instance; gọi nhiều lần vẫn an toàn.
    public override void Dispose()
    {
        if (disposed) return;
        disposed = true;
        for (int i = 0; i < blades.Length; i++)
        {
            if (blades[i] != null) Services.Pools.Despawn(blades[i].gameObject);
            blades[i] = null;
            nextHits[i].Clear();
        }
        Array.Clear(targets, 0, targets.Length);
        expired.Clear();
    }


    #endregion
}