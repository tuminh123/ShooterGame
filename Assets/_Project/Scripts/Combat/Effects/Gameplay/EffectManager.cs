using System;
using System.Collections.Generic;
using UnityEngine;

// Nhận yêu cầu từ đạn/vùng skill; quản lý áp dụng, thời gian hiệu lực và thu hồi runtime.
public sealed class EffectManager : IDisposable
{
    #region Dependencies And Runtime State

    private readonly EffectRuntimePool pool; // Thuê/trả class runtime; không quản lý prefab VFX.
    private readonly ITimerManager timers; // Sở hữu thời gian còn lại và nhịp tick, callback chạy trên main thread.
    private readonly List<EffectRuntime> activeEffects = new List<EffectRuntime>(64); // Các effect còn hiệu lực; thêm khi Apply, gỡ khi kết thúc.
    private readonly Dictionary<(CharacterRuntimeData, IGameplayEffect), EffectRuntime> lookup =
        new Dictionary<(CharacterRuntimeData, IGameplayEffect), EffectRuntime>(); // Chỉ tra effect có thời hạn theo cùng instance cấu hình và mục tiêu.
    private readonly Dictionary<CharacterRuntimeData, List<EffectRuntime>> targetEffects =
        new Dictionary<CharacterRuntimeData, List<EffectRuntime>>(); // Pause/clear chỉ duyệt các effect thuộc mục tiêu đó.
    private readonly Stack<List<EffectRuntime>> freeTargetLists = new Stack<List<EffectRuntime>>(); // Tái sử dụng list sau khi mục tiêu hết effect.
    private readonly List<EffectRuntime> pendingReturns = new List<EffectRuntime>(16); // Effect đã ngừng hoạt động, chờ callback kết thúc mới Remove/Reset/trả pool.
    private int operationDepth; // Hoãn trả pool khi Apply/Tick còn đang chạy qua callback damage hoặc stats.
    private bool disposed; // Chặn yêu cầu mới ngay khi bắt đầu dọn manager.

    #endregion

    #region Initialization

    // Zenject cung cấp pool runtime và đồng hồ chung của scene.
    public EffectManager(EffectRuntimePool pool, ITimerManager timers)
    { this.pool = pool; this.timers = timers; }

    #endregion

    #region Public Effect API

    // Effect có thời hạn: cùng instance cấu hình trên cùng mục tiêu sẽ gia hạn, không thêm stack mới.
    // Trả false nếu mục tiêu không hợp lệ, cùng phe, roll thất bại hoặc effect bị dọn ngay trong Apply.
    public bool Apply(IGameplayEffect configuration, CharacterTarget target,
        CharacterTeam sourceTeam, float effectHit = 0f)
        => ApplyInternal(configuration, target, sourceTeam, effectHit, false, out _);

    // Skill giữ handle và gỡ khi ra vùng. Mỗi lần gọi tạo runtime riêng, không tự hết hạn theo Duration.
    // Vẫn kiểm tra chance/kháng theo cấu hình; nếu không áp dụng được thì trả handle mặc định.
    public EffectHandle ApplyMaintained(IGameplayEffect configuration, CharacterTarget target,
        CharacterTeam sourceTeam, float effectHit = 0f)
    {
        ApplyInternal(configuration, target, sourceTeam, effectHit, true, out EffectHandle handle);
        return handle;
    }

    // Kiểm tra cả manager sở hữu và phiên bản thuê; handle cũ không trỏ sang lượt dùng mới của pool.
    public bool IsActive(EffectHandle handle)
        => handle.Runtime != null && ReferenceEquals(handle.Runtime.Manager, this) &&
            handle.Runtime.Version == handle.Version && handle.Runtime.IsActive;

    // Gỡ đúng một effect, gọi lại hoặc truyền handle hết hiệu lực đều không gây tác động.
    public void Remove(EffectHandle handle)
    {
        if (!IsActive(handle)) return;
        operationDepth++;
        try { Deactivate(handle.Runtime); }
        finally { EndOperation(); }
    }

    #endregion

    #region Validation And Runtime Creation

    // Hai API dùng chung kiểm tra và đăng ký runtime; maintained chỉ thay luật gia hạn/thời hạn.
    private bool ApplyInternal(IGameplayEffect configuration, CharacterTarget target,
        CharacterTeam sourceTeam, float effectHit, bool maintained, out EffectHandle handle)
    {
        handle = default;
        if (disposed || !target.TryGetCharacter(out CharacterView character) ||
            character.RuntimeData.Team == sourceTeam)
            return false;
        if (configuration == null) throw new ArgumentNullException(nameof(configuration));
        if (float.IsNaN(effectHit) || float.IsInfinity(effectHit))
            throw new ArgumentOutOfRangeException(nameof(effectHit));
        configuration.Validate();

        // Tắt UseEffectStats chỉ bỏ effectHit/kháng; ApplicationChance vẫn được dùng để roll.
        float chance = configuration.UseEffectStats
            ? ApplicationChance(configuration.ApplicationChance, effectHit, character.RuntimeData.FinalStats.effectResistance)
            : configuration.ApplicationChance;
        if (chance <= 0f || (chance < 100f && UnityEngine.Random.value * 100f >= chance)) return false;

        if (!maintained && lookup.TryGetValue((character.RuntimeData, configuration), out EffectRuntime active))
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
            created.IsMaintained = maintained;
            // Đăng ký đầy đủ trước Apply: callback stat/damage có thể yêu cầu dọn ngay runtime này.
            created.ActiveIndex = activeEffects.Count;
            activeEffects.Add(created);
            if (!maintained) lookup.Add((created.Identity, configuration), created);
            if (!targetEffects.TryGetValue(created.Identity, out List<EffectRuntime> group))
            {
                group = freeTargetLists.Count > 0 ? freeTargetLists.Pop() : new List<EffectRuntime>(4);
                targetEffects.Add(created.Identity, group);
            }
            created.TargetIndex = group.Count;
            group.Add(created);
            // Slow duy trì không cần timer. Effect duy trì có tick dùng lịch vô hạn đến khi skill gỡ.
            if (!maintained || configuration.TickInterval > 0f)
                created.Timer = timers.Register(maintained ? float.PositiveInfinity : configuration.Duration,
                    created, configuration.TickInterval);
            created.Apply();
            // Apply có thể kích hoạt callback thu hồi mục tiêu hoặc kết thúc trận.
            if (disposed || !target.TryGetCharacter(out _)) Deactivate(created);
            if (created.IsActive) handle = new EffectHandle(created);
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

    #endregion

    #region Target Lifecycle

    // CharacterView gọi khi chết/thu hồi. Chỉ duyệt nhóm của mục tiêu, không quét mọi effect trong scene.
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

    // Tắt/bật tạm thời chỉ dừng/tiếp tục timer, giữ nguyên thời gian còn lại và nhịp tick.
    // Slow duy trì không có timer; skill vùng chịu trách nhiệm gỡ slow khi chính skill bị pause.
    internal void SetPaused(CharacterView owner, CharacterRuntimeData identity, bool paused)
    {
        if (targetEffects.TryGetValue(identity, out List<EffectRuntime> group))
            foreach (EffectRuntime runtime in group) timers.SetPaused(runtime.Timer, paused);
    }

    #endregion

    #region Timer Dispatch

    // ITimerManager gọi trên main thread. Kiểm tra đúng lượt spawn trước khi gây tác động định kỳ.
    internal void HandleTimer(EffectRuntime runtime, TimerResult result)
    {
        operationDepth++;
        try
        {
            if (!runtime.IsActive) return;
            if (runtime.Owner == null || !ReferenceEquals(runtime.Owner.RuntimeData, runtime.Identity) ||
                runtime.Identity.Generation != runtime.Generation || !runtime.Identity.IsAlive)
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

    #endregion

    #region Deactivation And Deferred Pool Returns

    // Loại khỏi timer và các bảng tra ngay; hành vi Remove/Reset được hoãn đến EndOperation.
    private void Deactivate(EffectRuntime runtime)
    {
        if (!runtime.IsActive) return;
        runtime.IsActive = false;
        timers.Cancel(runtime.Timer);
        if (!runtime.IsMaintained) lookup.Remove((runtime.Identity, runtime.Configuration));
        // Đổi chỗ phần tử cuối để gỡ O(1); cập nhật index của runtime được chuyển vào chỗ trống.
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

    // Chỉ thao tác ngoài cùng được trả pool, tránh callback lồng nhau tái sử dụng runtime còn đang chạy.
    private void EndOperation()
    {
        if (--operationDepth != 0) return;
        operationDepth++; // Remove cũng có thể phát callback stat và tạo thêm yêu cầu cleanup.
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

    #endregion

    #region Disposal

    // Kết thúc scene/trận: ngừng nhận effect, dọn mọi runtime rồi trả chúng về pool.
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

    #endregion
}
