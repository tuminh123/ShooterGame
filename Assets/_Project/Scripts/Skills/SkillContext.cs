// * Dependency của skill runtime, không serialize tham chiếu scene lên mẫu skill.
public sealed class SkillContext
{
    public CharacterView Owner { get; }
    public ProjectileService Projectiles { get; }

    public SkillContext(CharacterView owner, ProjectileService projectiles)
    {
        Owner = owner;
        Projectiles = projectiles;
    }
}
