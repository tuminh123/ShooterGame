using System;
using System.Collections.Generic;
using UnityEngine;

// Sở hữu skill Character/Equipment của một lượt spawn, độc lập với skill gameplay.
public sealed class CharacterSkills : IDisposable
{
    private readonly List<SkillRuntime> skills = new List<SkillRuntime>();
    private readonly ulong generation;
    private bool disposed, ticking;
    internal CharacterView Owner { get; }
    internal CharacterRuntimeData Identity { get; }
    internal bool IsValid => !disposed && Owner != null && ReferenceEquals(Owner.RuntimeData, Identity) && Identity.Generation == generation && Identity.IsAlive;
    internal bool CanRun => IsValid && Owner.isActiveAndEnabled;

    // CharacterView tạo context khi vỏ còn tắt; mọi runtime ban đầu thuộc context này.
    public CharacterSkills(CharacterView owner, IReadOnlyList<SkillDataSO> definitions, SkillFactory factory)
    {
        Owner = owner ?? throw new ArgumentNullException(nameof(owner));
        Identity = owner.RuntimeData ?? throw new ArgumentException("Initialize character data before skills.");
        generation = Identity.Generation;
        if (definitions == null) throw new ArgumentNullException(nameof(definitions));
        if (factory == null) throw new ArgumentNullException(nameof(factory));
        try
        {
            var unique = new HashSet<SkillDataSO>();
            foreach (SkillDataSO definition in definitions)
            {
                if (definition == null || !unique.Add(definition)) throw new ArgumentException("Initial skills must be non-null and unique.");
                if (definition.Source == SkillSource.Gameplay) throw new ArgumentException("Initial character skills must use separate Character/Equipment definitions.");
                SkillRuntime runtime = factory.Create(definition, this);
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
                try { skills[i].Tick(); } catch (Exception exception) { Debug.LogException(exception); }
        }
        finally { ticking = false; }
    }

    internal void SetPaused(bool paused)
    {
        foreach (SkillRuntime skill in skills.ToArray())
            if (skill.IsActive) skill.SetPaused(paused);
    }

    // Death/despawn/destroy dọn timer, visual và modifier của toàn bộ skill nhân vật.
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
}
