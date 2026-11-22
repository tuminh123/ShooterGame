using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Bộ skill vốn có (nguồn Character/Equipment) của một lượt spawn, và là context chung mà mọi skill
/// của nhân vật đó (kể cả skill gameplay) dùng để biết chủ nhân còn hợp lệ hay không.
/// </summary>
/// <remarks>Tạo khi spawn, dispose khi chết hoặc despawn. Skill gameplay nhận trong trận do <see cref="SkillSystem"/> giữ.</remarks>
public sealed class CharacterSkills : IDisposable
{

    #region Dependencies And State
    private readonly List<SkillRuntime> skills = new List<SkillRuntime>();
    private readonly CharacterHandle handle; // Đúng lượt spawn đã tạo bộ skill này.
    private bool disposed, ticking;
    internal CharacterView Owner { get; }
    internal CharacterRuntimeData Identity { get; } // Runtime của lượt spawn: hành vi đọc phe, chỉ số và thêm modifier qua đây.

    /// <summary>
    /// Bộ skill vẫn thuộc đúng lượt spawn đang sống. Mọi <see cref="SkillRuntime"/> của bộ này đều dựa vào
    /// điều kiện duy nhất này, nên không runtime nào phải tự so sánh generation nữa.
    /// </summary>
    internal bool IsValid => !disposed && handle.IsAlive;
    // Khóa hành động tạm thời không làm IsValid mất hiệu lực: skill phải còn để tiếp tục sau choáng.
    internal bool CanRun => IsValid && Owner.isActiveAndEnabled && Owner.CanUseSkills;

    #endregion

    #region Initialization

    // CharacterView tạo context khi vỏ còn tắt; mọi runtime ban đầu thuộc context này.
    // grants: skill + cấp bắt đầu, do CharacterSkillSystem tính sẵn (Player theo level/sao đã lưu, Enemy theo cấp mặc định).
    public CharacterSkills(CharacterView owner, IReadOnlyList<SkillGrant> grants, SkillFactory factory)
    {
        Owner = owner ?? throw new ArgumentNullException(nameof(owner));
        Identity = owner.RuntimeData ?? throw new ArgumentException("Initialize character data before skills.");
        handle = new CharacterHandle(owner);
        if (grants == null) throw new ArgumentNullException(nameof(grants));
        if (factory == null) throw new ArgumentNullException(nameof(factory));
        try
        {
            var unique = new HashSet<SkillDataSO>();
            foreach (SkillGrant grant in grants)
            {
                SkillDataSO definition = grant.Skill;
                if (definition == null || !unique.Add(definition)) throw new ArgumentException("Initial skills must be non-null and unique.");
                if (definition.Source == SkillSource.Gameplay) throw new ArgumentException("Initial character skills must use separate Character/Equipment definitions.");
                SkillRuntime runtime = factory.Create(definition, this, grant.Level);
                if (!IsValid || !runtime.IsActive)
                {
                    runtime.Dispose();
                    throw new InvalidOperationException("Character ended during skill initialization.");
                }
                skills.Add(runtime);
            }
            Identity.RefillSpawnVitals();
        }
        catch { Dispose(); throw; }
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
        foreach (SkillRuntime skill in skills.ToArray())
            if (skill.IsActive) skill.SetPaused(paused);
    }

    #endregion

    #region Cleanup

    // Death/despawn/destroy dọn cooldown, visual và modifier của toàn bộ skill nhân vật.
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        var failures = new List<Exception>();
        foreach (SkillRuntime skill in skills.ToArray())
            try { skill.Dispose(); } catch (Exception exception) { failures.Add(exception); }
        skills.Clear();
        if (failures.Count > 0) throw new AggregateException("Character skill cleanup failed.", failures);
    }

    #endregion
}
