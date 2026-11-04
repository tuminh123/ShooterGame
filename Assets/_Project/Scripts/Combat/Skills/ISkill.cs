// Serialized skill settings are separate from scheduled runtime actions.
public interface ISkillData { }

// The scheduler knows only this behavior. Dependencies belong to each concrete skill.
public interface ISkill
{
    bool TryExecute();
}

internal sealed class SkillAction : ISkill
{
    private readonly System.Func<bool> execute;

    public SkillAction(System.Func<bool> execute)
        => this.execute = execute ?? throw new System.ArgumentNullException(nameof(execute));

    public bool TryExecute() => execute();
}
