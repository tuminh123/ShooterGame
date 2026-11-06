using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Shooter Game/Pooling/Prewarm Settings", fileName = "PoolPrewarmSettings")]
public sealed class PoolPrewarmSettingsSO : ScriptableObject
{
    [Serializable]
    public struct PrefabEntry
    {
        public GameObject prefab;
        [Min(0)] public int count;
    }

    [Header("Shared GameObject Shells")]
    [Min(0)] public int characterShells = 64;
    [Min(0)] public int bullets = 256;
    [Min(0)] public int effectShells = 16;
    [Min(0)] public int pickups = 128;
    [Header("Visuals Per Character Type")]
    [Min(0)] public int playerVisuals = 1;
    [Min(0)] public int enemyVisuals = 64;
    [Min(0)] public int bossVisuals = 2;
    [Header("Managed Runtime Objects")]
    [Min(0)] public int characterRuntimes = 64;
    [Min(0)] public int slowRuntimes = 32;
    [Min(0)] public int damageOverTimeRuntimes = 64;
    [Header("Job Buffer Capacity")]
    [Min(0)] public int timerCapacity = 1024;
    [Header("Additional Prefabs (future VFX, world objects...)")]
    public PrefabEntry[] additionalPrefabs = Array.Empty<PrefabEntry>();

    public void Validate()
    {
        if (characterShells < 0 || bullets < 0 || effectShells < 0 || pickups < 0 ||
            playerVisuals < 0 || enemyVisuals < 0 || bossVisuals < 0 || characterRuntimes < 0 ||
            slowRuntimes < 0 || damageOverTimeRuntimes < 0 || timerCapacity < 0 || additionalPrefabs == null)
            throw new InvalidOperationException("Prewarm counts must be nonnegative.");
        foreach (PrefabEntry entry in additionalPrefabs)
            if (entry.count < 0 || (entry.count > 0 && entry.prefab == null))
                throw new InvalidOperationException("Assign every additional prewarm prefab with a positive count.");
    }
}
