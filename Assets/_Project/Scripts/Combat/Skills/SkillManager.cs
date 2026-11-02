using System;
using System.Collections.Generic;
using UnityEngine;
using Zenject;

// Owns run skills supplied by an external system. Has no character or position source.
public sealed class SkillManager : ITickable, IDisposable
{
    private readonly List<SkillRuntime> runSkills = new List<SkillRuntime>();
    private readonly LevelManager levels;
    private bool ticking;
    private bool clearing;
    private bool disposed;

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

    public SkillManager(LevelManager levels)
    {
        this.levels = levels ?? throw new ArgumentNullException(nameof(levels));
        levels.RunReset += OnRunReset;
    }

    // The future card/reward system supplies configuration and a separately created behavior.
    public bool AddSkill(SkillDataSO definition, ISkill behavior)
    {
        if (disposed) return false;
        if (clearing) throw new InvalidOperationException("Cannot add run skills during cleanup.");
        if (definition == null) throw new ArgumentNullException(nameof(definition));
        if (behavior == null) throw new ArgumentNullException(nameof(behavior));
        definition.Validate();
        if (ReferenceEquals(definition.Behavior, behavior))
            throw new ArgumentException("Create a runtime instance instead of registering the shared template.", nameof(behavior));

        foreach (SkillRuntime skill in runSkills)
        {
            if (!skill.IsActive) continue;
            if (skill.Definition == definition) return false;
            if (skill.UsesBehavior(behavior))
                throw new ArgumentException("Create a separate behavior instance for each skill.", nameof(behavior));
        }

        runSkills.Add(new SkillRuntime(definition, behavior));
        return true;
    }

    public void Tick()
    {
        Tick(Time.deltaTime);
    }

    public void Tick(float deltaTime)
    {
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
                try { runSkills[i].Tick(deltaTime); }
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
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        levels.RunReset -= OnRunReset;
        ClearRunSkills();
    }
}
