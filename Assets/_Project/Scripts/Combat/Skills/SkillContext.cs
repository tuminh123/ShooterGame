public sealed class SkillContext
{
    public CharacterView Owner { get; }
    public ShootingService Shooting { get; }
    public CharacterSkillHandler Handler { get; internal set; }
    public SkillContext(CharacterView owner, ShootingService shooting)
    { Owner = owner; Shooting = shooting; }
}
