using System;
using System.Collections.Generic;
using UnityEngine;
using Zenject;

// Owns run skills and their stat leases for explicitly supplied Player/Enemy owners.
public sealed class SkillManager : ITickable, IDisposable
{
    private readonly List<SkillRuntime> runSkills = new List<SkillRuntime>();
    private readonly LevelManager levels;
    private readonly ITimerManager timers;
    private readonly ITimeSource timeSource;
    private bool ticking;
    private bool clearing;
    private bool disposed;
    private bool adding;
    private int runVersion;
    private readonly SkillFactory factory;
    private readonly CharacterManager characters;

    public int RunCount
    {
        get
        {
            int count = 0;
            foreach (SkillRuntime skill in runSkills)
                if (skill.IsActive) count++;
            return count;
        }
    }

    public SkillManager(LevelManager levels, ITimerManager timers, ITimeSource timeSource,
        SkillFactory factory, CharacterManager characters)
    {
        this.levels = levels ?? throw new ArgumentNullException(nameof(levels));
        this.timers = timers;
        this.timeSource = timeSource;
        this.factory = factory;
        this.characters = characters;
        levels.RunReset += OnRunReset;
    }

    // Một cửa nhận phần thưởng: skill đã có (innate hoặc run) chỉ nâng cùng runtime, không cộng bonus hai lần.
    public bool GrantOrUpgrade(SkillDataSO definition, CharacterView owner)
    {
        if (disposed || owner == null || owner.RuntimeData == null || !owner.RuntimeData.IsAlive || owner.Skills == null)
            return false;
        if (TryGetRuntime(definition, owner, out SkillRuntime existing)) return existing.TryUpgrade();
        ISkill behavior = factory.Create(definition, owner.Skills);
        bool added = false;
        try { added = AddSkill(definition, behavior, owner); return added; }
        finally { if (!added && behavior is IDisposable disposable) disposable.Dispose(); }
    }

    public bool TryGetRuntime(SkillDataSO definition, CharacterView owner, out SkillRuntime runtime)
    {
        if (owner != null && owner.Skills != null && owner.Skills.TryGetRuntime(definition, out runtime)) return true;
        foreach (SkillRuntime skill in runSkills)
            if (skill.IsActive && skill.Definition == definition && skill.BelongsTo(owner)) { runtime = skill; return true; }
        runtime = null;
        return false;
    }

    // The future card/reward system supplies configuration and a separately created behavior.
    public bool AddSkill(SkillDataSO definition, ISkill behavior, CharacterView owner)
    {
        if (disposed) return false;
        if (clearing) throw new InvalidOperationException("Cannot add run skills during cleanup.");
        if (adding) throw new InvalidOperationException("Cannot add run skills from a stat-change callback.");
        if (definition == null) throw new ArgumentNullException(nameof(definition));
        if (behavior == null) throw new ArgumentNullException(nameof(behavior));
        if (owner == null || owner.RuntimeData == null || !owner.RuntimeData.IsAlive)
            throw new ArgumentException("Run skills require a living character owner.", nameof(owner));
        definition.Validate();
        if (owner.Skills != null && owner.Skills.TryGetRuntime(definition, out _)) return false;
        if (ReferenceEquals(definition.Data, behavior))
            throw new ArgumentException("Create a runtime instance instead of registering the shared template.", nameof(behavior));

        foreach (SkillRuntime skill in runSkills)
        {
            if (!skill.IsActive) continue;
            if (skill.Definition == definition && skill.BelongsTo(owner)) return false;
            if (skill.UsesBehavior(behavior))
                throw new ArgumentException("Create a separate behavior instance for each skill.", nameof(behavior));
        }

        int version = runVersion;
        adding = true;
        try
        {
            var skill = new SkillRuntime(definition, behavior, timers, !owner.isActiveAndEnabled, owner);
            // Applying stats can synchronously reset the run, dispose the manager or release the owner.
            if (disposed || runVersion != version || !skill.IsActive)
            {
                skill.Dispose();
                return false;
            }
            runSkills.Add(skill);
            return true;
        }
        finally { adding = false; }
    }

    public void Tick()
    {
        Tick(timeSource.DeltaTime);
    }

    public void Tick(float deltaTime)
    {
        // Chỉ quyết định lúc thử Execute; cooldown đã được ITimerManager cập nhật trước lượt này.
        SkillRuntime.ValidateDeltaTime(deltaTime);
        if (disposed || deltaTime == 0f) return;
        if (ticking || clearing) throw new InvalidOperationException("Cannot tick run skills recursively or during cleanup.");

        int count = runSkills.Count;
        ticking = true;
        try
        {
            // Skills added during execution wait until the next manager tick.
            for (int i = 0; i < count; i++)
            {
                try { runSkills[i].Tick(); }
                catch (Exception exception) { Debug.LogException(exception); }
            }
        }
        finally
        {
            ticking = false;
            RemoveInactive();
        }
    }

    public void ClearRunSkills()
    {
        if (clearing) return;
        runVersion++;
        clearing = true;
        List<Exception> failures = null;
        try
        {
            foreach (SkillRuntime skill in runSkills)
            {
                try { skill.Dispose(); }
                catch (Exception exception)
                {
                    if (failures == null) failures = new List<Exception>();
                    failures.Add(exception);
                }
            }
        }
        finally
        {
            clearing = false;
            if (!ticking) RemoveInactive();
        }
        if (failures != null) throw new AggregateException("Run skill cleanup failed.", failures);
    }

    private void RemoveInactive()
    {
        for (int i = runSkills.Count - 1; i >= 0; i--)
            if (!runSkills[i].IsActive) runSkills.RemoveAt(i);
    }

    private void OnRunReset()
    {
        try { ClearRunSkills(); }
        catch (Exception exception) { Debug.LogException(exception); }
        // Innate skill không bị gỡ; chỉ hạ cấp TRẬN về 1, giữ bonus riêng và Definition.Level.
        foreach (CharacterView character in new List<CharacterView>(characters.Characters.Keys))
        {
            try { if (character != null) character.Skills?.ResetRunLevels(); }
            catch (Exception exception) { Debug.LogException(exception, character); }
        }
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        levels.RunReset -= OnRunReset;
        ClearRunSkills();
    }
}
