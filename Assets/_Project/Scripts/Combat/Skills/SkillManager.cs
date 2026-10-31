using System;
using System.Collections.Generic;
using UnityEngine;
using Zenject;

// One scene service owns all character skill state; assets/behaviors never hold runtime state.
public sealed class SkillManager : ITickable, IDisposable
{
    public readonly struct Handle
    {
        internal readonly SkillManager Manager;
        internal readonly long Id;
        internal Handle(SkillManager manager, long id) { Manager = manager; Id = id; }
    }

    private sealed class Slot
    {
        public SkillDataSO Definition;
        public SkillData Data;
        public float Cooldown;
        public bool Executing;
        public bool Applied;
        public IDisposable Lifetime;
    }

    private sealed class Registration
    {
        public Handle Handle;
        public CharacterView Owner;
        public CharacterRuntimeData Identity;
        public Slot[] Active;
        public Slot[] Passive;
    }

    private readonly Dictionary<long, Registration> registrations = new Dictionary<long, Registration>();
    private readonly Dictionary<CharacterView, Handle> owners = new Dictionary<CharacterView, Handle>();
    private readonly List<Handle> tickBuffer = new List<Handle>();
    private long nextId;
    private bool disposed;
    public int Count => registrations.Count;

    public Handle Register(CharacterView owner, CharacterDataSO definition)
    {
        if (disposed) throw new ObjectDisposedException(nameof(SkillManager));
        if (owner == null || owner.RuntimeData == null || definition == null)
            throw new ArgumentException("Register an initialized character and its skill definitions.");
        if (owners.TryGetValue(owner, out Handle previous))
        {
            if (TryGet(previous, out Registration existing) && ReferenceEquals(existing.Identity, owner.RuntimeData))
                return previous;
            Unregister(previous);
        }
        var registration = new Registration
        {
            Handle = new Handle(this, checked(++nextId)), Owner = owner, Identity = owner.RuntimeData,
            Active = CreateSlots(definition.ActiveSkills), Passive = CreateSlots(definition.PassiveSkills)
        };
        registrations.Add(registration.Handle.Id, registration);
        owners.Add(owner, registration.Handle);
        return registration.Handle;
    }

    private static Slot[] CreateSlots(SkillDataSO[] definitions)
    {
        if (definitions == null) throw new ArgumentException("Skill arrays cannot be null.");
        var slots = new Slot[definitions.Length];
        for (int i = 0; i < slots.Length; i++)
        {
            if (definitions[i] == null) throw new ArgumentException("A skill slot has no asset.");
            definitions[i].Validate();
            slots[i] = new Slot { Definition = definitions[i], Data = definitions[i].Data };
        }
        return slots;
    }

    public bool IsRegistered(Handle handle) => TryGet(handle, out _);

    // OnEnable runs this after initialization. Temporary disable preserves passive lifetimes.
    public void Activate(Handle handle)
    {
        if (!TryGet(handle, out Registration registration) || !CanExecute(registration)) return;
        foreach (Slot slot in registration.Passive)
        {
            if (!CanExecute(registration)) return;
            if (!slot.Applied && slot.Cooldown <= 0f)
                Execute(registration, slot, new CharacterTarget(registration.Owner), true);
        }
    }

    public bool TryExecute(Handle handle, int activeSkillIndex, CharacterTarget target)
    {
        if (!TryGetSlot(handle, SkillCategory.Active, activeSkillIndex, out Registration registration, out Slot slot) ||
            !CanExecute(registration) || slot.Cooldown > 0f || slot.Executing)
            return false;
        return Execute(registration, slot, target, false);
    }

    private bool Execute(Registration registration, Slot slot, CharacterTarget target, bool passive)
    {
        if (slot.Executing || !CanExecute(registration)) return false;
        slot.Executing = true;
        try
        {
            var result = slot.Definition.Behavior.Execute(registration.Owner, target, in slot.Data);
            // Damage/stat callbacks may kill, despawn or reuse a shell during execution.
            if (!result.succeeded || !CanExecute(registration))
            {
                result.lifetime?.Dispose();
                if (passive) slot.Cooldown = slot.Data.cooldown;
                return false;
            }
            IDisposable previous = slot.Lifetime;
            slot.Lifetime = null;
            previous?.Dispose();
            if (!CanExecute(registration)) { result.lifetime?.Dispose(); return false; }
            slot.Lifetime = result.lifetime;
            slot.Applied = passive;
            slot.Cooldown = passive ? 0f : slot.Data.cooldown;
            return true;
        }
        finally { slot.Executing = false; }
    }

    public bool TryGetData(Handle handle, SkillCategory category, int index, out SkillData data)
    {
        data = default;
        if (!TryGetSlot(handle, category, index, out _, out Slot slot)) return false;
        data = slot.Data;
        return true;
    }

    public float GetCooldown(Handle handle, int activeSkillIndex)
        => TryGetSlot(handle, SkillCategory.Active, activeSkillIndex, out _, out Slot slot) ? slot.Cooldown : 0f;

    public bool TryLevelUp(Handle handle, SkillCategory category, int index)
    {
        if (!TryGetSlot(handle, category, index, out Registration registration, out Slot slot) ||
            !CanExecute(registration) || slot.Executing || slot.Data.level >= slot.Data.maxLevel)
            return false;
        if (category == SkillCategory.Active) { slot.Data.level++; return true; }

        slot.Executing = true;
        try
        {
            IDisposable previous = slot.Lifetime;
            slot.Lifetime = null;
            slot.Applied = false;
            previous?.Dispose();
        }
        finally { slot.Executing = false; }
        if (!CanExecute(registration)) return false;
        slot.Data.level++;
        if (Execute(registration, slot, new CharacterTarget(registration.Owner), true)) return true;
        slot.Data.level--;
        if (CanExecute(registration)) Execute(registration, slot, new CharacterTarget(registration.Owner), true);
        return false;
    }

    public void Tick() => Simulate(Time.deltaTime);

    public void Simulate(float deltaTime)
    {
        if (disposed) return;
        if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime < 0f)
            throw new ArgumentOutOfRangeException(nameof(deltaTime));
        // Snapshot handles: synchronous callbacks may register/unregister characters.
        tickBuffer.Clear();
        foreach (Registration registration in registrations.Values) tickBuffer.Add(registration.Handle);
        for (int i = 0; i < tickBuffer.Count; i++)
        {
            Handle handle = tickBuffer[i];
            if (!TryGet(handle, out Registration registration)) continue;
            if (registration.Owner == null || !ReferenceEquals(registration.Owner.RuntimeData, registration.Identity) ||
                !registration.Identity.IsAlive)
            { Unregister(handle); continue; }
            if (!registration.Owner.isActiveAndEnabled || deltaTime == 0f) continue;
            foreach (Slot slot in registration.Active) slot.Cooldown = Mathf.Max(0f, slot.Cooldown - deltaTime);
            foreach (Slot slot in registration.Passive) slot.Cooldown = Mathf.Max(0f, slot.Cooldown - deltaTime);
            Activate(handle);
        }
    }

    public bool Unregister(Handle handle)
    {
        if (!TryGet(handle, out Registration registration)) return false;
        // Remove ownership before invoking cleanup callbacks.
        registrations.Remove(handle.Id);
        owners.Remove(registration.Owner);
        DisposeSlots(registration.Active);
        DisposeSlots(registration.Passive);
        return true;
    }

    private static void DisposeSlots(Slot[] slots)
    {
        foreach (Slot slot in slots)
        {
            IDisposable lifetime = slot.Lifetime;
            slot.Lifetime = null;
            lifetime?.Dispose();
        }
    }

    private bool CanExecute(Registration registration)
        => TryGet(registration.Handle, out Registration current) && ReferenceEquals(current, registration) &&
           registration.Owner != null && registration.Owner.isActiveAndEnabled &&
           ReferenceEquals(registration.Owner.RuntimeData, registration.Identity) && registration.Identity.IsAlive;

    private bool TryGet(Handle handle, out Registration registration)
    {
        registration = null;
        return !disposed && ReferenceEquals(handle.Manager, this) && registrations.TryGetValue(handle.Id, out registration);
    }

    private bool TryGetSlot(Handle handle, SkillCategory category, int index,
        out Registration registration, out Slot slot)
    {
        slot = null;
        if (!TryGet(handle, out registration) || (category != SkillCategory.Active && category != SkillCategory.Passive))
            return false;
        Slot[] slots = category == SkillCategory.Active ? registration.Active : registration.Passive;
        if (index < 0 || index >= slots.Length) return false;
        slot = slots[index];
        return true;
    }

    public void Dispose()
    {
        if (disposed) return;
        var handles = new List<Handle>(owners.Values);
        foreach (Handle handle in handles) Unregister(handle);
        disposed = true;
        tickBuffer.Clear();
    }
}
