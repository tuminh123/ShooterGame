// The scheduler knows only this behavior. Dependencies belong to each concrete skill.
public interface ISkill
{
    bool TryExecute();
}
