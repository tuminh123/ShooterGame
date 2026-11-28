using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using UnityEngine;

/// <summary>Hành vi cưa xoay: các lưỡi cưa quay quanh chủ nhân, gây sát thương và effect lên quái chạm phải.</summary>
/// <remarks>Hành vi liên tục (chạy trong Tick). Mỗi lưỡi có nhịp đánh lại riêng cho từng mục tiêu. Data: <see cref="OrbitSawData"/>.</remarks>
public sealed class OrbitSawSkill : Skill
{
    #region Runtime State

    private OrbitSawData data; // Cấu hình cấp đang hoạt động.
    private IGameplayEffect[] effectSnapshot; // Effect riêng của cưa.
    private SkillAreaVisual[] blades; // Visual đang thuê từ pool.
    private Dictionary<CharacterHandle, float>[] nextHits; // Lịch hit riêng của mỗi lưỡi.
    private List<CharacterHandle> expired; // Buffer dọn mục tiêu hết cooldown/đã despawn.
    private CharacterHandle[] targets; // Buffer query, không cấp phát mỗi frame.
    private int count; // Số lưỡi đang chạy.
    private float angle; // Góc hiện tại, giữ khi nâng cấp.
    private float clock; // Thời gian phục vụ lịch hit.
    private Vector2 previousCenter; // Tâm ở bước trước để sweep chuyển động.
    private bool hasPrevious; // Không tạo sweep giả khi đổi số lưỡi.
    private bool paused; // Trạng thái visual và cập nhật.
    private bool disposed; // Cleanup một lần.
    private int revision; // Callback đổi data vô hiệu hóa vòng Tick cũ.
    public override SkillBehaviourType Type => SkillBehaviourType.OrbitSaw;

    #endregion

    #region Configuration

    // Cấp phát lại state mutable, không chia sẻ collection với mẫu hoặc owner khác.
    protected override void InitializeRuntime()
    {
        // Lần đầu tạo collection; các lượt sau (pool) chỉ xóa nội dung để dùng lại. Dispose đã trả mọi lưỡi về pool.
        data = null;
        if (effectSnapshot == null) effectSnapshot = Array.Empty<IGameplayEffect>();
        if (blades == null) blades = Array.Empty<SkillAreaVisual>();
        else Array.Clear(blades, 0, blades.Length);
        if (nextHits == null) nextHits = Array.Empty<Dictionary<CharacterHandle, float>>();
        else foreach (Dictionary<CharacterHandle, float> hits in nextHits) hits?.Clear();
        if (expired == null) expired = new List<CharacterHandle>();
        else expired.Clear();
        if (targets == null) targets = Array.Empty<CharacterHandle>();
        else Array.Clear(targets, 0, targets.Length);
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
        bool changedPrefab = data != null && data.VisualPrefab != next.VisualPrefab;
        for (int i = 0; i < blades.Length; i++)
        {
            if (i < next.BladeCount && !changedPrefab) continue;
            if (blades[i] != null) Services.Pools.Despawn(blades[i].gameObject);
            blades[i] = null;
            nextHits[i].Clear();
        }
        Array.Resize(ref blades, next.BladeCount);
        Array.Resize(ref nextHits, next.BladeCount);
        for (int i = 0; i < nextHits.Length; i++)
        {
            if (nextHits[i] == null) nextHits[i] = new Dictionary<CharacterHandle, float>();
            if (blades[i] != null) blades[i].SetRadius(next.BladeRadius);
        }
        data = next;
        effectSnapshot = SnapshotEffects(effectSnapshot, next.Effects);
        count = next.BladeCount;
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
        float turn = data.DegreesPerSecond * deltaTime;
        int tickRevision = revision; // Callback damage có thể đổi data (revision++) giữa chừng: khi đó dừng vòng này.
        clock += deltaTime;

        int found = FindTargetsAroundSweep(center);
        try
        {
            for (int blade = 0; blade < count && StillValid(tickRevision); blade++)
                SweepBlade(blade, center, turn, found, tickRevision);
        }
        finally { Array.Clear(targets, 0, targets.Length); }

        if (disposed || tickRevision != revision) return;
        angle = Mathf.Repeat(angle + turn, 360f);
        previousCenter = center;
        hasPrevious = true;
    }

    // Skill vẫn chạy đúng lượt data của frame này: chưa dispose, chủ còn hoạt động, data chưa bị thay.
    private bool StillValid(int tickRevision) => !disposed && Owner.CanRun && tickRevision == revision;

    // Query rộng một lần quanh đoạn chủ vừa đi, rồi dùng hình học sweep cho từng lưỡi. Không dùng Physics2D.
    private int FindTargetsAroundSweep(Vector2 center)
    {
        Vector2 middle = (center + previousCenter) * 0.5f;
        float radius = data.OrbitAt(clock) + data.BladeRadius + Vector2.Distance(center, previousCenter) * 0.5f;
        return Owner.Owner.Search.FindAll(middle, radius,
            CharacterSearch.OpponentOf(Owner.Identity.Team), ref targets, Owner.Owner);
    }

    // Quét một lưỡi theo cung quay của frame, chia đoạn <= 15 độ để frame chậm không làm cưa xuyên qua quái.
    private void SweepBlade(int blade, Vector2 center, float turn, int found, int tickRevision)
    {
        EnsureBlade(blade);
        Prune(nextHits[blade]);
        float phase = 360f * blade / count;
        int steps = Mathf.Clamp(Mathf.CeilToInt(turn / 15f), 1, 128);
        Vector2 start = previousCenter + Direction(angle + phase) * data.OrbitAt(clock);

        for (int step = 1; step <= steps && StillValid(tickRevision); step++)
        {
            float t = step / (float)steps;
            Vector2 end = Vector2.Lerp(previousCenter, center, t) + Direction(angle + turn * t + phase) * data.OrbitAt(clock);
            for (int i = 0; i < found && StillValid(tickRevision); i++)
                TryHitTarget(blade, start, end, targets[i]);
            start = end;
        }

        if (!disposed && tickRevision == revision && blades[blade] != null)
            blades[blade].SetPose(Direction(angle + turn + phase) * data.OrbitAt(clock), (angle + turn) * 3f);
    }

    // Đoạn quét của lưỡi chạm mục tiêu và mục tiêu đã hết nhịp đánh lại: gây damage rồi áp effect của cấp.
    private void TryHitTarget(int blade, Vector2 start, Vector2 end, CharacterHandle target)
    {
        if (nextHits[blade].ContainsKey(target) || !target.TryGetCharacter(out CharacterView victim)) return;
        if (!SpatialGeometry.SweepCircle(new float2(start.x, start.y), new float2(end.x, end.y),
            data.BladeRadius, victim.GetSpatialEntity(), out _)) return;

        // Lịch riêng theo lưỡi + danh tính spawn. Ghi trước callback damage để chống gọi lồng nhau.
        nextHits[blade][target] = clock + data.HitInterval;
        // Chỉ số chụp mỗi lần chạm, nên buff vừa nhận có tác dụng ngay với nhát kế tiếp.
        var hit = new HitPayload(Owner.Handle, Owner.Identity.FinalStats, Owner.Identity.FinalBonuses, Owner.Identity.Team,
            data.DamageMultiplier, effectSnapshot);
        Services.Hits.Apply(target, in hit, victim.HitPosition);
    }

    private static Vector2 Direction(float degrees)
        => new Vector2(Mathf.Cos(degrees * Mathf.Deg2Rad), Mathf.Sin(degrees * Mathf.Deg2Rad));

    private void EnsureBlade(int index)
    {
        if (blades[index] != null) return;
        GameObject clone = Services.Pools.Spawn(data.VisualPrefab.gameObject, Owner.Owner.HitPosition, Quaternion.identity,
            beforeActivate: item => item.GetComponent<SkillAreaVisual>().Initialize(Owner.Owner, data.BladeRadius, Services.AreaVisuals));
        if (clone == null) throw new InvalidOperationException("Could not spawn saw visual.");
        blades[index] = clone.GetComponent<SkillAreaVisual>();
    }

    private void Prune(Dictionary<CharacterHandle, float> hits)
    {
        expired.Clear();
        foreach (var hit in hits)
            if (hit.Value <= clock || !hit.Key.TryGetCharacter(out _)) expired.Add(hit.Key);
        foreach (CharacterHandle target in expired) hits.Remove(target);
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
