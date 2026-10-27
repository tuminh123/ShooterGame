using System;
using System.Collections.Generic;

// * Bộ skill của một lượt spawn; cả Player và Enemy dùng cùng vòng đời.
// * Chỉ giữ bản runtime; mẫu trên CharacterDataSO luôn được để nguyên.
public sealed class CharacterSkills : IDisposable
{
    private readonly List<ISkill> skills = new List<ISkill>();

    public CharacterSkills(CharacterDataSO template, SkillContext context)
    {
        try
        {
            AddTemplates(template.PassiveSkills, context);
            AddTemplates(template.ActiveSkills, context);
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    private void AddTemplates(IReadOnlyList<ISkill> templates, SkillContext context)
    {
        for (int i = 0; i < templates.Count; i++)
        {
            if (templates[i] == null)
                continue;
            ISkill runtime = templates[i].CreateRuntime(context);
            if (runtime == null || ReferenceEquals(runtime, templates[i]))
                throw new InvalidOperationException("A skill template must create a separate runtime instance.");
            skills.Add(runtime);
            runtime.Activate();
        }
    }

    public void Tick(float deltaTime)
    {
        for (int i = 0; i < skills.Count; i++)
            if (skills[i].IsActive)
                skills[i].Tick(deltaTime);
    }

    public void Dispose()
    {
        for (int i = skills.Count - 1; i >= 0; i--)
            skills[i].Deactivate();
        skills.Clear();
    }
}
