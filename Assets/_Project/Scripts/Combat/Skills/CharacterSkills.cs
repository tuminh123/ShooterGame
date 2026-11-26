using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Bộ skill vốn có (nguồn Character/Equipment) của một lượt spawn, và là context chung mà mọi skill
/// của nhân vật đó (kể cả skill gameplay) dùng để biết chủ nhân còn hợp lệ hay không.
/// </summary>
/// <remarks>
/// Khởi tạo khi spawn, dispose khi chết hoặc despawn. Skill gameplay nhận trong trận do <see cref="SkillSystem"/> giữ.
/// Dùng lại được (<see cref="IPoolable"/>, CharacterSkillSystem sở hữu pool): skill vốn có thuê từ <see cref="SkillFactory"/>
/// và trả lại khi Dispose; collection và danh sách tạm được giữ giữa các lượt nên spawn lại không cấp phát chúng.
/// </remarks>
public sealed class CharacterSkills : IDisposable, IPoolable
{
    #region Dependencies And State

    private readonly List<SkillRuntime> skills = new List<SkillRuntime>();
    private readonly HashSet<SkillDataSO> unique = new HashSet<SkillDataSO>(); // Kiểm tra trùng khi khởi tạo; dùng lại.
    // Bản duyệt ổn định; mỗi lần duyệt mượn một list riêng vì callback có thể gọi lồng Dispose/SetPaused.
    private readonly Stack<List<SkillRuntime>> spareSnapshots = new Stack<List<SkillRuntime>>();
    private CharacterHandle handle; // Đúng lượt spawn đã tạo bộ skill này.
    private SkillFactory factory; // Nơi thuê và trả skill vốn có.
    private bool disposed = true, ticking; // Chưa khởi tạo = đã kết thúc: IsValid false.
    internal CharacterView Owner { get; private set; }
    internal CharacterRuntimeData Identity { get; private set; } // Runtime của lượt spawn: hành vi đọc phe, chỉ số và thêm modifier qua đây.

    /// <summary>
    /// Bộ skill vẫn thuộc đúng lượt spawn đang sống. Mọi <see cref="SkillRuntime"/> của bộ này đều dựa vào
    /// điều kiện duy nhất này, nên không runtime nào phải tự so sánh generation nữa.
    /// </summary>
    internal bool IsValid => !disposed && handle.IsAlive;
    // Khóa hành động tạm thời không làm IsValid mất hiệu lực: skill phải còn để tiếp tục sau choáng.
    internal bool CanRun => IsValid && Owner.isActiveAndEnabled && Owner.CanUseSkills;

    /// <summary>Đang trong vòng Tick: pool phải hoãn việc nhận lại bộ skill này tới khi vòng tick kết thúc.</summary>
    internal bool IsTicking => ticking;

    #endregion

    #region Initialization

    /// <summary>Bộ rỗng cho pool; chưa thuộc lượt spawn nào cho tới <see cref="Initialize"/>.</summary>
    public CharacterSkills() { }

    /// <summary>Bộ skill dùng ngay (ngoài pool): tạo rồi khởi tạo.</summary>
    public CharacterSkills(CharacterView owner, IReadOnlyList<SkillGrant> grants, SkillFactory factory) => Initialize(owner, grants, factory);

    // CharacterView tạo context khi vỏ còn tắt; mọi runtime ban đầu thuộc context này.
    // grants: skill + cấp bắt đầu, do CharacterSkillSystem tính sẵn (Player theo level/sao đã lưu, Enemy theo cấp mặc định).
    internal void Initialize(CharacterView owner, IReadOnlyList<SkillGrant> grants, SkillFactory factory)
    {
        if (!disposed) throw new InvalidOperationException("Character skills are already running.");
        Owner = owner ?? throw new ArgumentNullException(nameof(owner));
        Identity = owner.RuntimeData ?? throw new ArgumentException("Initialize character data before skills.");
        if (grants == null) throw new ArgumentNullException(nameof(grants));
        this.factory = factory ?? throw new ArgumentNullException(nameof(factory));
        handle = new CharacterHandle(owner);
        disposed = false;
        ticking = false;
        try
        {
            unique.Clear();
            for (int i = 0; i < grants.Count; i++) // Chỉ số, không foreach: tránh enumerator của IReadOnlyList mỗi lần spawn.
            {
                SkillGrant grant = grants[i];
                SkillDataSO definition = grant.Skill;
                if (definition == null || !unique.Add(definition)) throw new ArgumentException("Initial skills must be non-null and unique.");
                if (definition.Source == SkillSource.Gameplay) throw new ArgumentException("Initial character skills must use separate Character/Equipment definitions.");
                SkillRuntime runtime = factory.Rent(definition, this, grant.Level);
                skills.Add(runtime); // Nhận quyền sở hữu ngay để Dispose bên dưới trả nó về pool.
                if (!IsValid || !runtime.IsActive) throw new InvalidOperationException("Character ended during skill initialization.");
            }
            Identity.RefillSpawnVitals();
        }
        catch { Dispose(); throw; }
        finally { unique.Clear(); }
    }

    #endregion

    #region Runtime Operations

    public bool TryGetRuntime(SkillDataSO definition, out SkillRuntime runtime)
    {
        foreach (SkillRuntime skill in skills)
            if (skill.IsActive && skill.Definition == definition) { runtime = skill; return true; }
        runtime = null;
        return false;
    }

    // CharacterView gọi mỗi Update; không chạy khi game pause hoặc owner tạm tắt.
    public void Tick(float deltaTime)
    {
        SkillRuntime.ValidateDeltaTime(deltaTime);
        if (disposed || deltaTime == 0f || !CanRun) return;
        if (ticking) throw new InvalidOperationException("Cannot recursively tick character skills.");
        ticking = true;
        try
        {
            for (int i = 0; i < skills.Count && CanRun; i++)
                try { skills[i].Tick(deltaTime); } catch (Exception exception) { Debug.LogException(exception); }
        }
        finally { ticking = false; }
    }

    internal void SetPaused(bool paused)
    {
        List<SkillRuntime> snapshot = RentSnapshot();
        try
        {
            foreach (SkillRuntime skill in snapshot)
                if (skill.IsActive) skill.SetPaused(paused);
        }
        finally { ReturnSnapshot(snapshot); }
    }

    #endregion

    #region Cleanup

    // Death/despawn/destroy dọn cooldown, visual và modifier của toàn bộ skill nhân vật, rồi trả runtime về pool.
    // Danh sách lỗi chỉ tạo khi thật sự có lỗi.
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        List<Exception> failures = null;
        List<SkillRuntime> snapshot = RentSnapshot();
        skills.Clear();
        try
        {
            foreach (SkillRuntime skill in snapshot)
            {
                try { skill.Dispose(); }
                catch (Exception exception) { (failures ??= new List<Exception>()).Add(exception); }
                finally { factory.Release(skill); }
            }
        }
        finally { ReturnSnapshot(snapshot); }
        if (failures != null) throw new AggregateException("Character skill cleanup failed.", failures);
    }

    // Pool gọi khi nhận lại: kết thúc nếu còn chạy, rồi bỏ tham chiếu tới chủ của lượt cũ.
    void IPoolable.ResetForPool()
    {
        try { Dispose(); }
        finally
        {
            Owner = null;
            Identity = null;
            handle = default;
            factory = null;
            ticking = false;
        }
    }

    private List<SkillRuntime> RentSnapshot()
    {
        List<SkillRuntime> snapshot = spareSnapshots.Count > 0 ? spareSnapshots.Pop() : new List<SkillRuntime>(4);
        snapshot.AddRange(skills);
        return snapshot;
    }

    private void ReturnSnapshot(List<SkillRuntime> snapshot)
    {
        snapshot.Clear();
        spareSnapshots.Push(snapshot);
    }

    #endregion
}
