using System;
using UnityEngine;

[Serializable]
public sealed class StatBonusSkill : ISkill, ISkillData, IDisposable
{
    [SerializeField] private StatBonus bonus;
    [NonSerialized] private readonly CharacterSkills owner;
    [NonSerialized] private IDisposable lease;
    [NonSerialized] private bool disposed;

    public StatBonus Bonus => bonus;

    public StatBonusSkill() { }

    public StatBonusSkill(CharacterSkills owner, StatBonus bonus)
    {
        this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
        this.bonus = bonus;
    }

    public bool TryExecute()
    {
        if (disposed || lease != null || owner == null || !owner.CanRun) return false;

        float previousCapacity = owner.Identity.MaxShield;
        IDisposable modifier = owner.Identity.AddTemporaryBonus(bonus);
        try
        {
            // Rebuilding stats may synchronously despawn the owner and dispose this skill.
            if (!disposed && owner.CanRun)
            {
                // Grant only new shield capacity, never refill previously consumed shield.
                float addedShield = Mathf.Max(0f, owner.Identity.MaxShield - previousCapacity);
                owner.Owner.GrantShield(addedShield);
            }
        }
        catch
        {
            modifier.Dispose();
            throw;
        }

        if (disposed || !owner.CanRun)
        {
            modifier.Dispose();
            return false;
        }

        lease = modifier;
        return true;
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        IDisposable previous = lease;
        lease = null;
        previous?.Dispose();
    }
}
