using System;
using System.Collections.Generic;
using UnityEngine;

// Composition data lives outside both independent systems.
[CreateAssetMenu(menuName = "Shooter Game/Integration/Character Skill Loadouts")]
public sealed class CharacterSkillLoadoutSO : ScriptableObject
{
    [Serializable]
    public sealed class Loadout
    {
        public CharacterType characterType;
        public SkillDataSO[] skills = Array.Empty<SkillDataSO>();
    }

    [SerializeField] private Loadout[] loadouts = Array.Empty<Loadout>();

    public Dictionary<CharacterType, SkillDataSO[]> CreateLookup()
    {
        var lookup = new Dictionary<CharacterType, SkillDataSO[]>();
        foreach (Loadout loadout in loadouts)
        {
            if (loadout == null || loadout.characterType == CharacterType.None ||
                !Enum.IsDefined(typeof(CharacterType), loadout.characterType) || loadout.skills == null ||
                lookup.ContainsKey(loadout.characterType))
                throw new InvalidOperationException("Invalid or duplicate character skill loadout.");
            var unique = new HashSet<SkillDataSO>();
            foreach (SkillDataSO skill in loadout.skills)
            {
                if (skill == null || !unique.Add(skill))
                    throw new InvalidOperationException("Loadouts require non-null, unique skills.");
                skill.Validate();
            }
            lookup.Add(loadout.characterType, (SkillDataSO[])loadout.skills.Clone());
        }
        return lookup;
    }
}
