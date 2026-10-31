using System;

// Shared behavior only. Per-character state and returned lifetimes belong to SkillManager.
public interface ISkill
{
    (bool succeeded, IDisposable lifetime) Execute(
        CharacterView owner, CharacterTarget target, in SkillData data);
}
