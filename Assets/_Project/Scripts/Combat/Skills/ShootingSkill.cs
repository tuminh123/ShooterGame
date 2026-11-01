using System;
using UnityEngine;

[Serializable]
public sealed class ShootingSkill : ISkill
{
    [SerializeField] private ShootingDataSO shooting;
    [NonSerialized] private readonly Func<bool> fire;
    public ShootingDataSO Shooting => shooting;
    public ShootingSkill() { }
    public ShootingSkill(Func<bool> fire)
        => this.fire = fire ?? throw new ArgumentNullException(nameof(fire));
    public bool TryExecute() => fire != null && fire();
}
