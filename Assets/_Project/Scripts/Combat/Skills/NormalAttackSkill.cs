using System;

[Serializable]
public sealed class NormalAttackSkill : ISkill
{
    [NonSerialized] private readonly Func<bool> attack;
    public NormalAttackSkill() { }
    public NormalAttackSkill(Func<bool> attack)
        => this.attack = attack ?? throw new ArgumentNullException(nameof(attack));
    public bool TryExecute() => attack != null && attack();
}
