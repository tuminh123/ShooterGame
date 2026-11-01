using System;
using System.Collections.Generic;
using UnityEngine;
using Zenject;

// Only this composition service knows both Character and Skill.
public sealed class CharacterSkillBridge : IInitializable, ITickable, IDisposable
{
    private sealed class Binding
    {
        public CharacterView Owner;
        public CharacterRuntimeData Identity;
        public bool Active = true;
        public readonly List<SkillManager.Registration> Innate = new List<SkillManager.Registration>();
        public readonly List<SkillManager.Registration> Run = new List<SkillManager.Registration>();
        public readonly HashSet<SkillDataSO> RunDefinitions = new HashSet<SkillDataSO>();
        public bool IsCurrent => Active && Owner != null && ReferenceEquals(Owner.RuntimeData, Identity);
        public bool CanRun => IsCurrent && Owner.isActiveAndEnabled && Identity.IsAlive;
    }

    private readonly CharacterManager characters;
    private readonly SkillManager skills;
    private readonly ShootingService shooting;
    private readonly LevelManager levels;
    private readonly Dictionary<CharacterType, SkillDataSO[]> loadouts;
    private readonly Dictionary<CharacterView, Binding> bindings = new Dictionary<CharacterView, Binding>();
    private bool initialized;
    private bool disposed;

    public CharacterSkillBridge(CharacterManager characters, SkillManager skills, ShootingService shooting,
        LevelManager levels, CharacterSkillLoadoutSO loadouts)
    {
        this.characters = characters; this.skills = skills; this.shooting = shooting; this.levels = levels;
        this.loadouts = loadouts.CreateLookup();
    }

    public void Initialize()
    {
        if (initialized || disposed) return;
        initialized = true;
        characters.CharacterAdded += OnAdded;
        characters.CharacterRemoved += OnRemoved;
        characters.CharacterDied += OnRemoved;
        levels.RunReset += ClearRunSkills;
        foreach (var pair in new List<KeyValuePair<CharacterView, CharacterDataSO>>(characters.Characters))
            OnAdded(pair.Key, pair.Value);
    }

    private void OnAdded(CharacterView owner, CharacterDataSO definition)
    {
        if (bindings.ContainsKey(owner)) OnRemoved(owner);
        var binding = new Binding { Owner = owner, Identity = owner.RuntimeData };
        bindings.Add(owner, binding);
        try
        {
            if (!loadouts.TryGetValue(definition.CharacterType, out SkillDataSO[] definitions)) return;
            foreach (SkillDataSO skill in definitions)
                binding.Innate.Add(Register(binding, skill, false));
        }
        catch { OnRemoved(owner); throw; }
    }

    // UI passes the selected asset here after LevelManager awards a choice.
    // Duplicate selections are rejected until an explicit upgrade policy is configured.
    public bool AddRunSkill(CharacterView owner, SkillDataSO definition)
    {
        if (disposed || definition == null || !bindings.TryGetValue(owner, out Binding binding) ||
            !binding.CanRun || !binding.RunDefinitions.Add(definition)) return false;
        try { binding.Run.Add(Register(binding, definition, true)); return true; }
        catch { binding.RunDefinitions.Remove(definition); throw; }
    }

    private SkillManager.Registration Register(Binding binding, SkillDataSO definition, bool fromRun)
    {
        definition.Validate();
        ISkill instance = CreateBehavior(binding, definition.Behavior);
        try { return skills.Add(instance, definition.Data.cooldown, fromRun, () => binding.CanRun); }
        catch { (instance as IDisposable)?.Dispose(); throw; }
    }

    private ISkill CreateBehavior(Binding binding, ISkill template)
    {
        switch (template)
        {
            case NormalAttackSkill _:
                return new NormalAttackSkill(() => ContactAttack(binding));
            case ShootingSkill config:
            {
                ShootingDataSO shot = config.Shooting;
                var state = new ShootingRuntime();
                return new ShootingSkill(() => Fire(binding, shot, state));
            }
            case StatBonusSkill config:
                return new StatBonusSkill(config.Bonus, bonus =>
                {
                    if (!binding.CanRun) return null;
                    float shieldCapacity = binding.Identity.MaxShield;
                    IDisposable lease = binding.Identity.AddTemporaryBonus(bonus);
                    try
                    {
                        // New defense grants its new shield points once; it does not refill spent base shield.
                        if (binding.CanRun)
                            binding.Owner.GrantShield(Mathf.Max(0f, binding.Identity.MaxShield - shieldCapacity));
                        return lease;
                    }
                    catch { lease.Dispose(); throw; }
                });
            case RegenerationSkill config:
                return new RegenerationSkill(config.HealAmount,
                    () => binding.CanRun && binding.Identity.CurrentHealth < binding.Identity.FinalStats.maxHealth,
                    amount => { if (binding.CanRun) binding.Owner.ChangeHealth(amount); });
            case EmergencyShieldSkill config:
                return new EmergencyShieldSkill(config.HealthThreshold, config.ShieldAmount,
                    () => binding.CanRun
                        ? binding.Identity.CurrentHealth / binding.Identity.FinalStats.maxHealth * 100f
                        : float.PositiveInfinity,
                    amount => { if (binding.CanRun) binding.Owner.GrantShield(amount); });
            default:
                throw new InvalidOperationException($"Add dependency wiring for {template.GetType().Name} in CharacterSkillBridge.");
        }
    }

    private static bool ContactAttack(Binding binding)
    {
        if (!binding.CanRun) return false;
        CharacterTarget target = CharacterSearch.FindNearest(binding.Owner.HitPosition,
            binding.Identity.FinalStats.range, CharacterSearch.OpponentOf(binding.Identity.Team), binding.Owner);
        if (!target.TryGetCharacter(out CharacterView victim) || !CharacterSearch.Overlaps(binding.Owner, victim))
            return false;
        return binding.Owner.DealDamage(target).Accepted;
    }

    private bool Fire(Binding binding, ShootingDataSO shot, ShootingRuntime state)
    {
        if (!binding.CanRun) return false;
        CharacterTarget target = default;
        Vector2 position = (Vector2)binding.Owner.HitPosition + shot.MuzzleOffset;
        Vector2 direction = shot.DefaultDirection.normalized;
        if (shot.RequiresTarget)
        {
            target = CharacterSearch.FindNearest(binding.Owner.HitPosition, binding.Identity.FinalStats.range,
                CharacterSearch.OpponentOf(binding.Identity.Team), binding.Owner);
            if (!target.TryGetCharacter(out CharacterView victim)) return false;
            Vector2 toward = (Vector2)victim.HitPosition - position;
            if (toward.sqrMagnitude > 0.000001f) direction = toward.normalized;
        }
        return shooting.Fire(shot, state, binding.Identity, position, direction, target) > 0;
    }

    public void Tick()
    {
        if (disposed) return;
        try { skills.Tick(Time.deltaTime); }
        catch (Exception exception) { Debug.LogException(exception); }
    }

    private void OnRemoved(CharacterView owner)
    {
        if (!bindings.TryGetValue(owner, out Binding binding)) return;
        bindings.Remove(owner);
        binding.Active = false;
        var registrations = new List<SkillManager.Registration>(binding.Innate);
        registrations.AddRange(binding.Run);
        binding.Innate.Clear(); binding.Run.Clear(); binding.RunDefinitions.Clear();
        DisposeRegistrations(registrations);
    }

    public void ClearRunSkills()
    {
        try { skills.ClearRunSkills(); }
        finally
        {
            foreach (Binding binding in bindings.Values)
            { binding.Run.Clear(); binding.RunDefinitions.Clear(); }
        }
    }

    private static void DisposeRegistrations(List<SkillManager.Registration> registrations)
    {
        List<Exception> failures = null;
        foreach (SkillManager.Registration registration in registrations)
        {
            try { registration.Dispose(); }
            catch (Exception exception)
            {
                if (failures == null) failures = new List<Exception>();
                failures.Add(exception);
            }
        }
        if (failures != null) throw new AggregateException("Character skill cleanup failed.", failures);
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        characters.CharacterAdded -= OnAdded;
        characters.CharacterRemoved -= OnRemoved;
        characters.CharacterDied -= OnRemoved;
        levels.RunReset -= ClearRunSkills;
        List<Exception> failures = null;
        foreach (CharacterView owner in new List<CharacterView>(bindings.Keys))
        {
            try { OnRemoved(owner); }
            catch (Exception exception)
            {
                if (failures == null) failures = new List<Exception>();
                failures.Add(exception);
            }
        }
        if (failures != null) throw new AggregateException("Skill bridge cleanup failed.", failures);
    }
}
