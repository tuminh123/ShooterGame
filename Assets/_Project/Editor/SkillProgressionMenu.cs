using UnityEditor;
using UnityEngine;
using Zenject;

// Công cụ kiểm tra skill và bảng chọn khi lên cấp trong Editor. Không thêm debug UI vào game.
public static class SkillProgressionMenu
{
    [MenuItem("Shooter Game/Skills/Grant One Run Level")]
    private static void GrantRunLevel()
    {
        var scene = Object.FindObjectOfType<SceneContext>();
        if (scene == null) return;
        LevelManager levels = scene.Container.Resolve<LevelManager>();
        if (!levels.IsRunMaxLevel)
            levels.AddRunExperience(levels.RunExperienceToNextLevel - levels.RunExperience);
    }

    [MenuItem("Shooter Game/Skills/Grant One Run Level", true)]
    private static bool CanGrantRunLevel() => Application.isPlaying;

    [MenuItem("Shooter Game/Skills/Grant Or Upgrade Selected Skill")]
    private static void GrantOrUpgradeSelected()
    {
        var scene = Object.FindObjectOfType<SceneContext>();
        if (scene == null) { Debug.LogWarning("Open the gameplay scene and enter Play Mode first."); return; }
        var definition = (SkillDataSO)Selection.activeObject;
        CharacterView owner = scene.Container.Resolve<PlayerManager>().CurrentPlayer;
        SkillManager manager = scene.Container.Resolve<SkillManager>();
        bool changed = manager.GrantOrUpgrade(definition, owner);
        if (manager.TryGetRuntime(definition, owner, out SkillRuntime skill))
            Debug.Log($"{definition.name}: run level {skill.RunLevel}/{definition.MaxLevel}, changed={changed}.");
    }

    [MenuItem("Shooter Game/Skills/Grant Or Upgrade Selected Skill", true)]
    private static bool CanGrant() => Application.isPlaying && Selection.activeObject is SkillDataSO;
}
