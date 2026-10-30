using System;
using System.Collections.Generic;
using UnityEngine;
// Owns passive lifetime, retry timers and cooldowns. Strategies have no independent Update.
public sealed class CharacterSkillHandler : IDisposable
{
    private readonly List<SkillRuntime> skills = new List<SkillRuntime>();
    private readonly SkillFactory factory;
    private readonly SkillContext context;
    private readonly IReadOnlyList<SkillRuntime> readOnlySkills;
    private bool disposed;
    private bool executing;
    public IReadOnlyList<SkillRuntime> Skills => readOnlySkills;
    public CharacterSkillHandler(SkillDataSO[] definitions, SkillFactory factory, SkillContext context)
    {
        this.factory = factory ?? throw new ArgumentNullException(nameof(factory));
        this.context = context ?? throw new ArgumentNullException(nameof(context));
        if (context.Handler != null) throw new InvalidOperationException("Each handler needs its own skill context.");
        context.Handler = this;
        readOnlySkills = skills.AsReadOnly();
        try
        {
            foreach (SkillDataSO data in definitions ?? Array.Empty<SkillDataSO>()) AddSkill(data);
        }
        catch { Dispose(); throw; }
    }
    public SkillRuntime AddSkill(SkillDataSO data)
    {
        EnsureMutable();
        SkillRuntime runtime = factory.Create(data, context);
        skills.Add(runtime);
        return runtime;
    }
    public void RemoveSkill(SkillRuntime runtime)
    {
        EnsureMutable();
        if (skills.Remove(runtime)) runtime.Dispose();
    }
    public void Tick(float deltaTime)
    {
        if (disposed || !IsOwnerAlive() || deltaTime <= 0f) return;
        // Passives precede active casts, independent of array ordering.
        foreach (SkillRuntime runtime in skills)
        {
            if (!runtime.IsAttached) continue;
            runtime.CooldownRemaining = Mathf.Max(0f, runtime.CooldownRemaining - deltaTime);
            runtime.RetryRemaining = Mathf.Max(0f, runtime.RetryRemaining - deltaTime);
            if (runtime.Data.Category == SkillCategory.Passive && !runtime.PassiveApplied) Execute(runtime);
        }
        foreach (SkillRuntime runtime in skills)
        {
            if (!IsOwnerAlive()) break;
            if (runtime.Data.Category == SkillCategory.Active && runtime.Data.AutoCast) TryUse(runtime);
        }
    }
    public bool TryUse(SkillRuntime runtime)
    {
        if (disposed || executing || !IsOwnerAlive() || runtime == null || !runtime.IsAttached ||
            !skills.Contains(runtime) || runtime.CooldownRemaining > 0f || runtime.RetryRemaining > 0f ||
            runtime.Data.Category != SkillCategory.Active) return false;
        return Execute(runtime);
    }
    private bool Execute(SkillRuntime runtime)
    {
        if (runtime.RetryRemaining > 0f || !runtime.IsAttached || !IsOwnerAlive()) return false;
        executing = true;
        try
        {
            SkillExecution result = runtime.Strategy.Execute(context, runtime);
            if (!result.Succeeded)
            {
                result.Lifetime?.Dispose();
                runtime.RetryRemaining = runtime.Data.RetryInterval;
                return false;
            }
            if (!runtime.IsAttached) { result.Lifetime?.Dispose(); return false; }
            runtime.Lifetime?.Dispose();
            runtime.Lifetime = result.Lifetime;
            if (runtime.Data.Category == SkillCategory.Passive) runtime.PassiveApplied = true;
            else runtime.CooldownRemaining = runtime.Data.Cooldown;
            return true;
        }
        finally { executing = false; }
    }
    // Event-driven passives register a subscription once, then request gated activations here.
    // Their initial SkillExecution.Lifetime owns the subscription and any persistent resources.
    public bool TryTriggerPassive(SkillRuntime runtime, Func<bool> effect)
    {
        if (effect == null) throw new ArgumentNullException(nameof(effect));
        if (disposed || executing || !IsOwnerAlive() || runtime == null || !runtime.IsAttached ||
            !runtime.PassiveApplied || runtime.Data.Category != SkillCategory.Passive ||
            !skills.Contains(runtime) || runtime.CooldownRemaining > 0f)
            return false;
        executing = true;
        try
        {
            if (!effect() || !runtime.IsAttached) return false;
            runtime.CooldownRemaining = runtime.Data.Cooldown;
            return true;
        }
        finally { executing = false; }
    }
    private bool IsOwnerAlive() => context.Owner != null && context.Owner.isActiveAndEnabled &&
        context.Owner.RuntimeData != null && context.Owner.RuntimeData.IsAlive;
    private void EnsureMutable()
    {
        if (disposed) throw new ObjectDisposedException(nameof(CharacterSkillHandler));
        if (executing) throw new InvalidOperationException("Cannot change skill membership during execution.");
    }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        for (int i = skills.Count - 1; i >= 0; i--) skills[i].Dispose();
        // Keep iteration stable if a callback disposes the owner while executing a skill.
    }
}
