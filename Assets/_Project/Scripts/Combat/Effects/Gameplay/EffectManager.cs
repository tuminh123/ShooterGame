using System;
using System.Collections.Generic;
using UnityEngine;

// Nhận yêu cầu từ đạn/vùng skill; quản lý áp dụng, thời gian hiệu lực và thu hồi runtime.
public sealed class EffectManager : IDisposable
{
    private readonly EffectRuntimePool pool;
    private readonly ITimerManager timers;
    private readonly List<EffectRuntime> activeEffects = new List<EffectRuntime>(64); // Các effect còn hiệu lực; thêm khi Apply, gỡ khi kết thúc.
    private readonly Dictionary<(CharacterRuntimeData, IGameplayEffect), EffectRuntime> lookup =
        new Dictionary<(CharacterRuntimeData, IGameplayEffect), EffectRuntime>(); // Tìm effect áp dụng lại mà không quét toàn bộ runtime.
    private readonly Dictionary<CharacterRuntimeData, List<EffectRuntime>> targetEffects =
        new Dictionary<CharacterRuntimeData, List<EffectRuntime>>(); // Pause/clear chỉ duyệt các effect thuộc mục tiêu đó.
    private readonly Stack<List<EffectRuntime>> freeTargetLists = new Stack<List<EffectRuntime>>();
    private readonly List<EffectRuntime> pendingReturns = new List<EffectRuntime>(16); // Effect đã ngừng hoạt động, chờ callback kết thúc mới Remove/Reset/trả pool.
    private int operationDepth; // Hoãn trả pool khi Apply/Tick còn đang chạy qua callback damage hoặc stats.
    private bool disposed;

    public EffectManager(EffectRuntimePool pool, ITimerManager timers)
    { this.pool = pool; this.timers = timers; }

    // Đạn hoặc vùng skill gọi trực tiếp. Phe nguồn phục vụ lọc đồng đội và sát thương định kỳ.
    public bool Apply(IGameplayEffect configuration, CharacterTarget target,
        CharacterTeam sourceTeam, float effectHit = 0f)
    {
        if (disposed || !target.TryGetCharacter(out CharacterView character) ||
            character.Effects == null || character.Effects.IsDisposed || character.RuntimeData.Team == sourceTeam)
            return false;
        if (configuration == null) throw new ArgumentNullException(nameof(configuration));
        if (float.IsNaN(effectHit) || float.IsInfinity(effectHit))
            throw new ArgumentOutOfRangeException(nameof(effectHit));
        configuration.Validate();

        float chance = configuration.UseEffectStats
            ? ApplicationChance(configuration.ApplicationChance, effectHit, character.RuntimeData.FinalStats.effectResistance)
            : configuration.ApplicationChance;
        if (chance <= 0f || (chance < 100f && UnityEngine.Random.value * 100f >= chance)) return false;

        if (lookup.TryGetValue((character.RuntimeData, configuration), out EffectRuntime active))
        {
            // Kéo dài thời gian tồn tại nhưng giữ nhịp; bắn liên tục không trì hoãn độc/cháy mãi.
            timers.Restart(active.Timer, configuration.Duration);
            return true;
        }

        operationDepth++;
        EffectRuntime created = null;
        try
        {
            created = configuration.RentRuntime(pool);
            created.Initialize(configuration, character, sourceTeam);
            created.Manager = this;
            created.ActiveIndex = activeEffects.Count;
            activeEffects.Add(created);
            lookup.Add((created.Identity, configuration), created);
            if (!targetEffects.TryGetValue(created.Identity, out List<EffectRuntime> group))
            {
                group = freeTargetLists.Count > 0 ? freeTargetLists.Pop() : new List<EffectRuntime>(4);
                targetEffects.Add(created.Identity, group);
            }
            created.TargetIndex = group.Count;
            group.Add(created);
            created.Timer = timers.Register(configuration.Duration, created, configuration.TickInterval);
            created.Apply();
            // Apply có thể kích hoạt callback thu hồi mục tiêu hoặc kết thúc trận.
            if (disposed || !target.TryGetCharacter(out _)) Deactivate(created);
            return created.IsActive;
        }
        catch
        {
            if (created != null) Deactivate(created);
            throw;
        }
        finally { EndOperation(); }
    }

    // Đầu vào/đầu ra đều là phần trăm; kháng hiệu ứng giữ giới hạn 80% như hệ thống cũ.
    public static float ApplicationChance(float baseChance, float effectHit, float resistance)
        => Mathf.Clamp(baseChance * (1f + Mathf.Max(0f, effectHit) / 100f) *
            (1f - Mathf.Clamp(resistance, 0f, 80f) / 100f), 0f, 100f);

    internal int CountFor(CharacterView owner, CharacterRuntimeData identity)
    {
        return targetEffects.TryGetValue(identity, out List<EffectRuntime> group) ? group.Count : 0;
    }

    internal void ClearTarget(CharacterView owner, CharacterRuntimeData identity)
    {
        operationDepth++;
        try
        {
            if (targetEffects.TryGetValue(identity, out List<EffectRuntime> group))
                while (group.Count > 0) Deactivate(group[group.Count - 1]);
        }
        finally { EndOperation(); }
    }

    internal void SetPaused(CharacterView owner, CharacterRuntimeData identity, bool paused)
    {
        if (targetEffects.TryGetValue(identity, out List<EffectRuntime> group))
            foreach (EffectRuntime runtime in group) timers.SetPaused(runtime.Timer, paused);
    }

    // ITimerManager gọi trên main thread. Không cần vòng quét effect mỗi frame nữa.
    internal void HandleTimer(EffectRuntime runtime, TimerResult result)
    {
        operationDepth++;
        try
        {
            if (!runtime.IsActive) return;
            if (runtime.Owner == null || !ReferenceEquals(runtime.Owner.RuntimeData, runtime.Identity) || !runtime.Identity.IsAlive)
            { Deactivate(runtime); return; }
            runtime.Tick(result.DueTickCount);
            // Callback damage có thể vừa làm mới effect; không gỡ theo Completed của lịch cũ.
            if (runtime.IsActive && result.Completed && timers.GetRemaining(runtime.Timer) <= 0f) Deactivate(runtime);
        }
        catch
        {
            Deactivate(runtime);
            throw;
        }
        finally { EndOperation(); }
    }

    private void Deactivate(EffectRuntime runtime)
    {
        if (!runtime.IsActive) return;
        runtime.IsActive = false;
        timers.Cancel(runtime.Timer);
        lookup.Remove((runtime.Identity, runtime.Configuration));
        List<EffectRuntime> group = targetEffects[runtime.Identity];
        int targetLast = group.Count - 1;
        if (runtime.TargetIndex != targetLast)
        {
            EffectRuntime movedTarget = group[targetLast];
            group[runtime.TargetIndex] = movedTarget;
            movedTarget.TargetIndex = runtime.TargetIndex;
        }
        group.RemoveAt(targetLast);
        if (group.Count == 0)
        {
            targetEffects.Remove(runtime.Identity);
            freeTargetLists.Push(group); // Giữ dung lượng, không cấp phát list mới cho mỗi lượt apply/despawn.
        }
        int last = activeEffects.Count - 1;
        int index = runtime.ActiveIndex;
        if (index != last)
        {
            EffectRuntime moved = activeEffects[last];
            activeEffects[index] = moved;
            moved.ActiveIndex = index;
        }
        activeEffects.RemoveAt(last);
        pendingReturns.Add(runtime);
    }

    private void EndOperation()
    {
        if (--operationDepth != 0) return;
        operationDepth++;
        try
        {
            while (pendingReturns.Count > 0)
            {
                int last = pendingReturns.Count - 1;
                EffectRuntime runtime = pendingReturns[last];
                pendingReturns.RemoveAt(last);
                try { runtime.Remove(); }
                catch (Exception exception) { Debug.LogException(exception); }
                finally { pool.Return(runtime); }
            }
        }
        finally { operationDepth--; }
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        operationDepth++;
        try
        {
            while (activeEffects.Count > 0) Deactivate(activeEffects[activeEffects.Count - 1]);
        }
        finally { EndOperation(); }
    }
}
