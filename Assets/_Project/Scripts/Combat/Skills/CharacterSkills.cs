using System;
using System.Collections.Generic;
using UnityEngine;

// Owns only the skills equipped in CharacterDataSO for this spawn.
public sealed class CharacterSkills : IDisposable
{
    private readonly List<SkillRuntime> innateSkills = new List<SkillRuntime>();
    private bool ticking;
    private bool disposed;

    internal CharacterView Owner { get; }
    internal CharacterRuntimeData Identity { get; }
    private readonly ulong generation;
    internal bool CanRun => !disposed && Owner != null &&
        ReferenceEquals(Owner.RuntimeData, Identity) && Identity.Generation == generation && Owner.isActiveAndEnabled && Identity.IsAlive;

    public int InnateCount => innateSkills.Count;

    public bool TryGetRuntime(SkillDataSO definition, out SkillRuntime runtime)
    {
        foreach (SkillRuntime skill in innateSkills)
            if (skill.IsActive && skill.Definition == definition) { runtime = skill; return true; }
        runtime = null;
        return false;
    }

    internal void ResetRunLevels()
    {
        for (int i = 0; i < innateSkills.Count && !disposed; i++) innateSkills[i].ResetRunLevel();
    }

    public CharacterSkills(CharacterView owner, IReadOnlyList<SkillDataSO> definitions,
        SkillFactory factory)
    {
        Owner = owner ?? throw new ArgumentNullException(nameof(owner));
        Identity = owner.RuntimeData ?? throw new ArgumentException("Initialize the character before its skills.", nameof(owner));
        generation = Identity.Generation;
        if (factory == null) throw new ArgumentNullException(nameof(factory));

        try
        {
            if (definitions == null) throw new ArgumentNullException(nameof(definitions));
            var unique = new HashSet<SkillDataSO>();
            foreach (SkillDataSO definition in definitions)
            {
                if (definition == null || !unique.Add(definition))
                    throw new ArgumentException("Character skills must be non-null and unique.", nameof(definitions));

                ISkill behavior = factory.Create(definition, this);
                try { innateSkills.Add(new SkillRuntime(definition, behavior, Owner.Timers, !Owner.isActiveAndEnabled)); }
                catch
                {
                    if (behavior is IDisposable disposable) disposable.Dispose();
                    throw;
                }
            }
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    public void Tick(float deltaTime)
    {
        SkillRuntime.ValidateDeltaTime(deltaTime);
        if (!CanRun || deltaTime == 0f) return;
        if (ticking) throw new InvalidOperationException("Cannot tick character skills recursively.");

        ticking = true;
        try
        {
            // Disposal can clear the collection synchronously during skill execution.
            for (int i = 0; i < innateSkills.Count; i++)
            {
                if (!CanRun) break;
                try { innateSkills[i].Tick(); }
                catch (Exception exception) { Debug.LogException(exception, Owner); }
            }
        }
        finally
        {
            ticking = false;
            for (int i = innateSkills.Count - 1; i >= 0; i--)
                if (!innateSkills[i].IsActive) innateSkills.RemoveAt(i);
        }
    }

    // Timer vẫn được cập nhật tập trung; tắt tạm vỏ chỉ pause các cooldown của đúng cá thể này.
    internal void SetPaused(bool paused)
    {
        for (int i = 0; i < innateSkills.Count && !disposed; i++) innateSkills[i].SetPaused(paused);
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        List<Exception> failures = null;

        foreach (SkillRuntime skill in innateSkills)
        {
            try { skill.Dispose(); }
            catch (Exception exception)
            {
                if (failures == null) failures = new List<Exception>();
                failures.Add(exception);
            }
        }
        innateSkills.Clear();

        if (failures != null) throw new AggregateException("Character skill cleanup failed.", failures);
    }
}
