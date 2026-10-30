using System;
public interface ISkill
{
    SkillExecution Execute(SkillContext context, SkillRuntime runtime);
}
public readonly struct SkillExecution
{
    public bool Succeeded { get; }
    public IDisposable Lifetime { get; }
    public SkillExecution(bool succeeded, IDisposable lifetime = null)
    { Succeeded = succeeded; Lifetime = lifetime; }
}
