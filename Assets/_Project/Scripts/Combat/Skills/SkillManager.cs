using System;
using System.Collections.Generic;

// Pure scheduling: no Unity, character, targeting or progression dependencies.
public sealed class SkillManager : IDisposable
{
    public sealed class Registration : IDisposable
    {
        internal readonly SkillManager Manager;
        internal readonly ISkill Behavior;
        internal readonly Func<bool> CanRun;
        internal readonly List<Registration> Collection;
        internal readonly float Cooldown;
        public bool IsActive { get; internal set; } = true;
        public float CooldownRemaining { get; internal set; }

        internal Registration(SkillManager manager, ISkill behavior, float cooldown,
            Func<bool> canRun, List<Registration> collection)
        {
            Manager = manager; Behavior = behavior; Cooldown = cooldown;
            CanRun = canRun; Collection = collection;
        }

        public void Dispose() => Manager.Remove(this);
    }

    private readonly List<Registration> innateSkills = new List<Registration>();
    private readonly List<Registration> runSkills = new List<Registration>();
    private readonly List<Registration> updateBuffer = new List<Registration>();
    private bool ticking;
    private bool clearing;
    private bool disposed;
    public int InnateCount => innateSkills.Count;
    public int RunCount => runSkills.Count;

    // Every registration owns a distinct behavior instance, never the template from an asset.
    public Registration Add(ISkill behavior, float cooldown, bool fromRun = false, Func<bool> canRun = null)
    {
        if (disposed) throw new ObjectDisposedException(nameof(SkillManager));
        if (clearing) throw new InvalidOperationException("Cannot register skills during cleanup.");
        if (behavior == null) throw new ArgumentNullException(nameof(behavior));
        ValidateTime(cooldown);
        foreach (Registration existing in innateSkills)
            if (ReferenceEquals(existing.Behavior, behavior)) throw new ArgumentException("Create a separate behavior instance.");
        foreach (Registration existing in runSkills)
            if (ReferenceEquals(existing.Behavior, behavior)) throw new ArgumentException("Create a separate behavior instance.");
        List<Registration> collection = fromRun ? runSkills : innateSkills;
        var registration = new Registration(this, behavior, cooldown, canRun, collection);
        collection.Add(registration);
        return registration;
    }

    public void Tick(float deltaTime)
    {
        ValidateTime(deltaTime);
        if (disposed || deltaTime == 0f) return;
        if (ticking) throw new InvalidOperationException("SkillManager.Tick cannot be called recursively.");
        ticking = true;
        List<Exception> failures = null;
        updateBuffer.Clear();
        updateBuffer.AddRange(innateSkills);
        updateBuffer.AddRange(runSkills);
        try
        {
            foreach (Registration entry in updateBuffer)
            {
                if (!entry.IsActive) continue;
                try
                {
                    // A generic predicate pauses the clock; it exposes no owner type.
                    if (entry.CanRun != null && !entry.CanRun()) continue;
                    if (!entry.IsActive) continue;
                    entry.CooldownRemaining = Math.Max(0f, entry.CooldownRemaining - deltaTime);
                    if (entry.CooldownRemaining > 0f) continue;
                    bool succeeded = entry.Behavior.TryExecute();
                    if (entry.IsActive && succeeded) entry.CooldownRemaining = entry.Cooldown;
                }
                catch (Exception exception)
                {
                    if (failures == null) failures = new List<Exception>();
                    failures.Add(exception);
                    // Isolate a broken behavior so it cannot block all other skills or fail every frame.
                    try { entry.Dispose(); }
                    catch (Exception cleanup) { failures.Add(cleanup); }
                }
            }
        }
        finally { updateBuffer.Clear(); ticking = false; }
        if (failures != null) throw new AggregateException("Skill execution failed.", failures);
    }

    private void Remove(Registration registration)
    {
        if (!registration.IsActive) return;
        registration.IsActive = false;
        registration.Collection.Remove(registration);
        (registration.Behavior as IDisposable)?.Dispose();
    }

    public void ClearRunSkills() => Clear(false);

    private void Clear(bool all)
    {
        if (clearing) return;
        clearing = true;
        List<Exception> failures = null;
        try
        {
            var snapshot = new List<Registration>(runSkills);
            if (all) snapshot.AddRange(innateSkills);
            foreach (Registration entry in snapshot)
            {
                try { entry.Dispose(); }
                catch (Exception exception)
                {
                    if (failures == null) failures = new List<Exception>();
                    failures.Add(exception);
                }
            }
        }
        finally { clearing = false; }
        if (failures != null) throw new AggregateException("Skill cleanup failed.", failures);
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        Clear(true);
    }

    private static void ValidateTime(float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f)
            throw new ArgumentOutOfRangeException(nameof(value));
    }
}
