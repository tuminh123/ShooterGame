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
    internal bool CanRun => !disposed && Owner != null &&
        ReferenceEquals(Owner.RuntimeData, Identity) && Owner.isActiveAndEnabled && Identity.IsAlive;

    public int InnateCount => innateSkills.Count;

    public CharacterSkills(CharacterView owner, IReadOnlyList<SkillDataSO> definitions,
        SkillFactory factory)
    {
        Owner = owner ?? throw new ArgumentNullException(nameof(owner));
        Identity = owner.RuntimeData ?? throw new ArgumentException("Initialize the character before its skills.", nameof(owner));
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
                innateSkills.Add(new SkillRuntime(definition, behavior));
            }
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    // Action chỉ chuẩn bị đầu vào; ShootingSkill.ExecuteSkill thực hiện lượt bắn.
    internal ISkill CreateShootingAction(ShootingSkill shootingData, CombatSpawner combatSpawner,
        ShootBehaviorFactory shootFactory, ProjectileMoveFactory moveFactory, float targetSearchRadius)
    {
        ShootingSkill runtimeSkill = shootingData.CreateRuntime();
        return new SkillAction(() =>
        {
            if (!CanRun) return false;

            CharacterTarget selectedTarget = default;
            Vector2 spawnPosition = (Vector2)Owner.HitPosition + shootingData.MuzzleOffset;
            Vector2 aimDirection = shootingData.DefaultDirection.normalized;
            if (shootingData.RequiresTarget)
            {
                selectedTarget = CharacterSearch.FindNearest(Owner.HitPosition, targetSearchRadius,
                    CharacterSearch.OpponentOf(Identity.Team), Owner);
                if (!selectedTarget.TryGetCharacter(out CharacterView targetCharacter)) return false;
                Vector2 directionToTarget = (Vector2)targetCharacter.HitPosition - spawnPosition;
                if (directionToTarget.sqrMagnitude > 0.000001f) aimDirection = directionToTarget.normalized;
            }

            return runtimeSkill.ExecuteSkill(combatSpawner, shootFactory, moveFactory,
                Identity.Team, spawnPosition, aimDirection, selectedTarget) > 0;
        });
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
                try { innateSkills[i].Tick(deltaTime); }
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
