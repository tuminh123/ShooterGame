using System;
using System.Collections.Generic;
using UnityEngine;
using Zenject;

// Sở hữu tất cả skill của Player/Enemy trong trận, không tách danh sách skill ban đầu và skill nhận thêm.
public sealed class SkillManager : ITickable, IDisposable
{
    #region Services And Runtime Collection

    private readonly List<SkillRuntime> skills = new List<SkillRuntime>(); // Nguồn runtime duy nhất, mỗi phần tử có owner + definition riêng.
    private readonly LevelManager levels; // Nhận reset trận; không tự quyết định luồng game.
    private readonly ITimeSource timeSource; // Dùng scaled time để tôn trọng pause.
    private readonly SkillFactory factory; // Tạo toàn bộ bộ hành vi trước khi đăng ký một runtime.
    private readonly CharacterManager characters; // Dựng lại skill ban đầu nếu reset khi nhân vật còn tồn tại.
    private bool disposed, ticking, clearing, adding, resetPending; // Guard callback lồng nhau và defer rebuild tới khi collection ổn định.
    private int runVersion; // Reset trong callback vô hiệu hóa thao tác grant đang dang dở.
    public int RunCount { get { int count = 0; foreach (SkillRuntime skill in skills) if (skill.IsActive) count++; return count; } }

    #endregion

    #region Initialization And Lookup

    // Singleton scene do Zenject tạo; không tạo runtime trước khi owner có dữ liệu spawn.
    public SkillManager(LevelManager levels, ITimeSource timeSource, SkillFactory factory, CharacterManager characters)
    {
        this.levels = levels; this.timeSource = timeSource; this.factory = factory; this.characters = characters;
        levels.RunReset += OnRunReset;
    }

    // UI đọc snapshot của đúng owner, không cần ghép hai nguồn skill.
    public void GetOwnedSkills(CharacterView owner, List<SkillRuntime> result)
    {
        if (result == null) throw new ArgumentNullException(nameof(result));
        result.Clear();
        if (owner == null || owner.RuntimeData == null || !owner.RuntimeData.IsAlive) return;
        foreach (SkillRuntime skill in skills) if (skill.IsActive && skill.BelongsTo(owner)) result.Add(skill);
    }

    // Cùng asset trên hai owner vẫn là hai instance độc lập.
    public bool TryGetRuntime(SkillDataSO definition, CharacterView owner, out SkillRuntime runtime)
    {
        foreach (SkillRuntime skill in skills)
            if (skill.IsActive && skill.Definition == definition && skill.BelongsTo(owner)) { runtime = skill; return true; }
        runtime = null; return false;
    }

    #endregion

    #region Grant And Registration

    // Nhận skill từ UI hoặc gameplay: chỉ tạo mới nếu chưa sở hữu, không cộng lại bonus khi nâng cấp.
    public bool GrantOrUpgrade(SkillDataSO definition, CharacterView owner)
    {
        if (disposed || definition == null || definition.Source != SkillSource.Gameplay || owner == null || owner.Skills == null || !owner.Skills.IsValid) return false;
        if (TryGetRuntime(definition, owner, out SkillRuntime runtime)) return runtime.TryUpgrade();
        return AddInitialSkill(definition, owner.Skills);
    }

    // Dùng cả lúc khởi tạo context, khi owner.Skills chưa được gán xong trên CharacterView.
    internal bool AddInitialSkill(SkillDataSO definition, CharacterSkills context)
    {
        if (disposed || context == null || !context.IsValid) return false;
        if (clearing || adding) throw new InvalidOperationException("Cannot add skill during cleanup/stat callback.");
        if (TryGetRuntime(definition, context.Owner, out _)) return false;
        int version = runVersion; // Không commit nếu bonus callback reset cả trận.
        adding = true;
        try
        {
            var runtime = factory.Create(definition, context);
            if (disposed || version != runVersion || !context.IsValid || !runtime.IsActive) { runtime.Dispose(); return false; }
            skills.Add(runtime);
            return true;
        }
        finally { adding = false; ProcessPendingReset(); }
    }

    #endregion

    #region Scheduling And Owner Lifecycle

    // Zenject chỉ gọi một vòng tick chung cho cả skill ban đầu và skill mới.
    public void Tick() => Tick(timeSource.DeltaTime);
    // Dùng cho test/manual simulation, cùng logic scheduler production.
    public void Tick(float deltaTime) => TickSelected(deltaTime, null);
    // Context cung cấp manual simulation cho đúng owner, không có một collection riêng.
    internal void TickOwner(CharacterView owner, float deltaTime) => TickSelected(deltaTime, owner);

    // Callback có thể despawn/reset/spawn; chỉ duyệt số phần tử có sẵn ở đầu frame.
    private void TickSelected(float deltaTime, CharacterView owner)
    {
        SkillRuntime.ValidateDeltaTime(deltaTime);
        if (disposed || deltaTime == 0f) return;
        if (ticking || clearing) throw new InvalidOperationException("Cannot recursively tick skills.");
        ticking = true;
        int count = skills.Count;
        try
        {
            for (int i = 0; i < count; i++)
                if (owner == null || skills[i].BelongsTo(owner))
                    try { skills[i].Tick(); } catch (Exception exception) { Debug.LogException(exception); }
        }
        finally { ticking = false; RemoveInactive(); ProcessPendingReset(); }
    }

    // Owner tạm tắt: pause mọi hành vi và timer ngay, không chờ tới frame tick tiếp theo.
    internal void SetOwnerPaused(CharacterView owner, bool paused)
    {
        foreach (SkillRuntime skill in skills.ToArray()) if (skill.IsActive && skill.BelongsTo(owner)) skill.SetPaused(paused);
    }

    // Thu hồi toàn bộ skill của một lượt spawn; không ảnh hưởng skill của owner khác.
    internal void RemoveOwner(CharacterView owner)
    {
        var failures = new List<Exception>(); // Một hành vi cleanup lỗi không được giữ các skill còn lại của owner.
        foreach (SkillRuntime skill in skills.ToArray()) if (skill.BelongsTo(owner))
            try { skill.Dispose(); } catch (Exception exception) { failures.Add(exception); }
        if (!ticking && !clearing) RemoveInactive();
        if (failures.Count > 0) throw new AggregateException("Owner skill cleanup failed.", failures);
    }

    #endregion

    #region Run Reset And Disposal

    // Hết trận: dispose mọi timer/action/bonus trước khi bỏ reference; lỗi một runtime không chặn các runtime còn lại.
    public void ClearRunSkills()
    {
        if (clearing) return;
        clearing = true; runVersion++;
        var failures = new List<Exception>();
        try { foreach (SkillRuntime skill in skills) try { skill.Dispose(); } catch (Exception exception) { failures.Add(exception); } }
        finally { clearing = false; if (!ticking) RemoveInactive(); }
        if (failures.Count > 0) throw new AggregateException("Skill cleanup failed.", failures);
    }

    // Reset đang callback phải hủy runtime cũ ngay; rebuild initial definitions đợi collection ổn định.
    private void OnRunReset()
    {
        ClearRunSkills(); resetPending = true; ProcessPendingReset();
    }

    // Khi GameManager reset sau khi dọn world, registry rỗng nên không tự sinh skill/Player mới.
    private void ProcessPendingReset()
    {
        if (!resetPending || ticking || adding || clearing || disposed) return;
        resetPending = false;
        foreach (CharacterView character in new List<CharacterView>(characters.Characters.Keys))
            if (character != null && character.Skills != null && character.Skills.IsValid) character.Skills.RebuildInitialSkills();
    }

    // Chỉ xóa entry đã Dispose để không làm sai chỉ số khi đang duyệt callback.
    private void RemoveInactive() { for (int i = skills.Count - 1; i >= 0; i--) if (!skills[i].IsActive) skills.RemoveAt(i); }

    // Dọn scene; runtime không giữ asset/context sang scene kế tiếp.
    public void Dispose()
    {
        if (disposed) return;
        disposed = true; levels.RunReset -= OnRunReset; ClearRunSkills();
    }

    #endregion
}
