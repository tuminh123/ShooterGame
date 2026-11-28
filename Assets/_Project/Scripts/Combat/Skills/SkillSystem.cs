using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// System skill gameplay của miền Combat: các skill nhận được trong trận (qua bảng chọn khi lên cấp).
/// Skill vốn có của nhân vật không thuộc đây mà thuộc <see cref="CharacterSkills"/>.
/// </summary>
/// <remarks>Do <see cref="CombatManager"/> tạo và gọi <see cref="Tick()"/>; không bind vào Zenject.</remarks>
public sealed class SkillSystem : IDisposable
{
    #region Services And Runtime Collection

    /// <summary>Skill nhận trong màn luôn bắt đầu ở cấp 1, rồi lên cấp khi được chọn lại (TryUpgrade).</summary>
    public const int GameplayStartLevel = 1;

    private readonly List<SkillRuntime> skills = new List<SkillRuntime>(); // Runtime gameplay, mỗi phần tử có owner + definition riêng.
    // Bản duyệt ổn định khi callback đổi danh sách. Mượn list riêng mỗi lần vì dọn skill có thể gọi lồng
    // (gỡ buff → chủ chết → RemoveOwner lại); trả lại sau đó nên chạy ổn định không cấp phát.
    private readonly Stack<List<SkillRuntime>> spareSnapshots = new Stack<List<SkillRuntime>>();
    private readonly SkillFactory factory; // Tạo toàn bộ bộ hành vi trước khi đăng ký một runtime.
    private bool disposed, ticking, clearing, adding; // Chặn callback lồng nhau khi collection đang thay đổi.
    private int battleVersion; // Reset trong callback vô hiệu hóa thao tác grant đang dang dở.
    public int SkillCount
    {
        get
        {
            int count = 0;
            foreach (SkillRuntime skill in skills)
                if (skill.IsActive) count++;
            return count;
        }
    }

    #endregion

    #region Initialization And Lookup

    // Không nghe reset lượt chơi: miền Battle (nằm trên Combat) gọi ICombatLifecycle.ClearBattleSkills khi reset.
    public SkillSystem(SkillFactory skillFactory)
    {
        this.factory = skillFactory ?? throw new ArgumentNullException(nameof(skillFactory));
    }

    // UI chỉ đọc skill gameplay của đúng owner.
    public void GetOwnedSkills(CharacterView owner, List<SkillRuntime> result)
    {
        if (result == null) throw new ArgumentNullException(nameof(result));
        result.Clear();
        if (owner == null || owner.RuntimeData == null || !owner.RuntimeData.IsAlive) return;
        foreach (SkillRuntime skill in skills) if (skill.IsActive && skill.BelongsTo(owner)) result.Add(skill);
    }

    /// <summary>Số skill trong trận đang có của <paramref name="owner"/> (buff theo số skill). Không cấp phát.</summary>
    public int CountOwned(CharacterView owner)
    {
        if (owner == null) return 0;
        int count = 0;
        foreach (SkillRuntime skill in skills) if (skill.IsActive && skill.BelongsTo(owner)) count++;
        return count;
    }

    // Cùng asset trên hai owner vẫn là hai instance độc lập.
    public bool TryGetRuntime(SkillDataSO definition, CharacterView owner, out SkillRuntime runtime)
    {
        foreach (SkillRuntime skill in skills)
            if (skill.IsActive && skill.Definition == definition && skill.BelongsTo(owner)) { runtime = skill; return true; }
        runtime = null;
        return false;
    }

    #endregion

    #region Grant And Registration

    // Nhận skill từ UI hoặc gameplay: chỉ tạo mới nếu chưa sở hữu, không cộng lại bonus khi nâng cấp.
    // context là CharacterSkills của lượt spawn hiện tại (CharacterSkillSystem cung cấp); null nghĩa là nhân vật không còn hợp lệ.
    public bool GrantOrUpgrade(SkillDataSO definition, CharacterSkills context)
    {
        if (disposed || definition == null || definition.Source != SkillSource.Gameplay || context == null || !context.IsValid) return false;
        if (TryGetRuntime(definition, context.Owner, out SkillRuntime runtime)) return runtime.TryUpgrade();
        return AddGameplaySkill(definition, context);
    }

    // Tạo skill gameplay mới trong context của lượt spawn hiện tại.
    private bool AddGameplaySkill(SkillDataSO definition, CharacterSkills context)
    {
        if (disposed || definition == null || definition.Source != SkillSource.Gameplay || context == null || !context.IsValid) return false;
        if (clearing || adding) throw new InvalidOperationException("Cannot add skill during cleanup/stat callback.");
        if (TryGetRuntime(definition, context.Owner, out _)) return false;
        int version = battleVersion; // Không commit nếu bonus callback reset cả trận.
        adding = true;
        try
        {
            var runtime = factory.Create(definition, context, GameplayStartLevel);
            if (disposed || version != battleVersion || !context.IsValid || !runtime.IsActive) { runtime.Dispose(); return false; }
            skills.Add(runtime);
            return true;
        }
        finally { adding = false; }
    }

    #endregion

    #region Scheduling And Owner Lifecycle

    // CombatManager gọi mỗi frame; skill vốn có của nhân vật do CharacterView tự tick qua CharacterSkills.
    public void Tick() => Tick(Time.deltaTime);
    // Dùng cho test/manual simulation, cùng logic scheduler production.
    // Callback có thể despawn/reset/spawn; chỉ duyệt số phần tử có sẵn ở đầu frame.
    public void Tick(float deltaTime)
    {
        SkillRuntime.ValidateDeltaTime(deltaTime);
        if (disposed || deltaTime == 0f) return;
        if (ticking || clearing) throw new InvalidOperationException("Cannot recursively tick skills.");
        ticking = true;
        int count = skills.Count;
        try
        {
            for (int i = 0; i < count; i++)
                try { skills[i].Tick(deltaTime); } catch (Exception exception) { Debug.LogException(exception); }
        }
        finally
        {
            ticking = false;
            RemoveInactive();
        }
    }

    // Owner tạm tắt: pause mọi hành vi và timer ngay, không chờ tới frame tick tiếp theo.
    // Chỉ duyệt khi owner thật sự có skill gameplay (thường chỉ Player): quái spawn/chết không tốn gì ở đây.
    public void SetOwnerPaused(CharacterView owner, bool paused)
    {
        if (!HasOwner(owner)) return;
        List<SkillRuntime> snapshot = RentSnapshot();
        try { foreach (SkillRuntime skill in snapshot) if (skill.IsActive && skill.BelongsTo(owner)) skill.SetPaused(paused); }
        finally { ReturnSnapshot(snapshot); }
    }

    // Thu hồi toàn bộ skill của một lượt spawn; không ảnh hưởng skill của owner khác.
    // Danh sách lỗi chỉ tạo khi thật sự có lỗi (quái chết mỗi lần trước đây đều cấp phát một list).
    public void RemoveOwner(CharacterView owner)
    {
        if (!HasOwner(owner)) return;
        List<Exception> failures = null; // Một hành vi cleanup lỗi không được giữ các skill còn lại của owner.
        List<SkillRuntime> snapshot = RentSnapshot();
        try
        {
            foreach (SkillRuntime skill in snapshot) if (skill.BelongsTo(owner))
                try { skill.Dispose(); } catch (Exception exception) { (failures ??= new List<Exception>()).Add(exception); }
        }
        finally { ReturnSnapshot(snapshot); }
        if (!ticking && !clearing) RemoveInactive();
        if (failures != null) throw new AggregateException("Owner skill cleanup failed.", failures);
    }

    private bool HasOwner(CharacterView owner)
    {
        foreach (SkillRuntime skill in skills) if (skill.BelongsTo(owner)) return true;
        return false;
    }

    private List<SkillRuntime> RentSnapshot()
    {
        List<SkillRuntime> snapshot = spareSnapshots.Count > 0 ? spareSnapshots.Pop() : new List<SkillRuntime>(8);
        snapshot.AddRange(skills);
        return snapshot;
    }

    private void ReturnSnapshot(List<SkillRuntime> snapshot)
    {
        snapshot.Clear();
        spareSnapshots.Push(snapshot);
    }

    #endregion

    #region Battle Reset And Disposal

    // Hết trận: dispose mọi timer/action/bonus trước khi bỏ reference; lỗi một runtime không chặn các runtime còn lại.
    public void ClearBattleSkills()
    {
        if (clearing) return;
        clearing = true;
        battleVersion++;
        List<Exception> failures = null;
        try
        {
            foreach (SkillRuntime skill in skills)
                try { skill.Dispose(); } catch (Exception exception) { (failures ??= new List<Exception>()).Add(exception); }
        }
        finally
        {
            clearing = false;
            if (!ticking) RemoveInactive();
        }
        if (failures != null) throw new AggregateException("Skill cleanup failed.", failures);
    }

    // Chỉ xóa entry đã Dispose để không làm sai chỉ số khi đang duyệt callback.
    private void RemoveInactive() { for (int i = skills.Count - 1; i >= 0; i--) if (!skills[i].IsActive) skills.RemoveAt(i); }

    // Dọn scene; runtime không giữ asset/context sang scene kế tiếp.
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        ClearBattleSkills();
    }

    #endregion
}
