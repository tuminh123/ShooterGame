using System;
using System.Collections.Generic;
using UnityEngine;

// Unity serialize danh sách có field rõ tên; runtime mới dựng hai dictionary độc lập.
// Đây là cấu hình theo LEVEL TRẬN, không phải cấp vĩnh viễn của nhân vật.
[Serializable]
public sealed class ShootingLevelEntry
{
    [Min(1)] public int level = 1;
    [SerializeReference] public IShooter shooter = new StraightShooter();
    [SerializeReference] public IProjectileMove projectileMove = new StraightProjectileMove();
    [Min(1)] public int projectileCount = 1;
}

[Serializable]
public sealed class ShootingEffectLevelEntry
{
    [Min(1)] public int level = 1;
    [SerializeReference] public List<IGameplayEffect> effects = new List<IGameplayEffect>();
}

[Serializable]
public sealed class ShootingLevelTable
{
    [SerializeField] private List<ShootingLevelEntry> shootingLevels = new List<ShootingLevelEntry>();
    [SerializeField] private List<ShootingEffectLevelEntry> effectLevels = new List<ShootingEffectLevelEntry>();

    // Không serialize/cache dictionary trên asset dùng chung: mỗi skill runtime giữ một bản riêng.
    internal ShootingLevelLookup CreateLookup(int maxLevel)
    {
        Validate(maxLevel);
        return new ShootingLevelLookup(shootingLevels, effectLevels);
    }

    public void Validate(int maxLevel)
    {
        if (shootingLevels == null || effectLevels == null)
            throw new InvalidOperationException("Shooting level lists cannot be null.");
        var keys = new HashSet<int>();
        foreach (ShootingLevelEntry entry in shootingLevels)
        {
            if (entry == null || entry.level < 1 || entry.level > maxLevel || !keys.Add(entry.level) ||
                entry.shooter == null || entry.projectileMove == null ||
                entry.projectileCount < 1 || entry.projectileCount > 1024)
                throw new InvalidOperationException("Invalid or duplicate shooting level entry.");
            entry.shooter.Validate();
            ShootBehaviorFactory.ValidateSupported(entry.shooter);
            ProjectileMoveFactory.ValidateSupported(entry.projectileMove);
        }
        keys.Clear();
        foreach (ShootingEffectLevelEntry entry in effectLevels)
        {
            if (entry == null || entry.level < 1 || entry.level > maxLevel || !keys.Add(entry.level) || entry.effects == null)
                throw new InvalidOperationException("Invalid or duplicate effect level entry.");
            foreach (IGameplayEffect effect in entry.effects)
            {
                if (effect == null) throw new InvalidOperationException("Select a type for every level effect.");
                effect.Validate();
            }
        }
    }
}

internal sealed class ShootingLevelLookup
{
    // Shooter là CONFIG template. Không gọi OnShotCompleted trên những instance trong dictionary.
    private readonly Dictionary<int, (IShooter Shooter, IProjectileMove Movement, int Count)> shooting =
        new Dictionary<int, (IShooter, IProjectileMove, int)>();
    private readonly Dictionary<int, IReadOnlyList<IGameplayEffect>> effects =
        new Dictionary<int, IReadOnlyList<IGameplayEffect>>();
    private readonly List<int> effectKeys = new List<int>();

    internal ShootingLevelLookup(List<ShootingLevelEntry> shootingLevels, List<ShootingEffectLevelEntry> effectLevels)
    {
        foreach (ShootingLevelEntry entry in shootingLevels)
            shooting.Add(entry.level, (entry.shooter, entry.projectileMove, entry.projectileCount));
        foreach (ShootingEffectLevelEntry entry in effectLevels)
        {
            effects.Add(entry.level, entry.effects);
            effectKeys.Add(entry.level);
        }
        effectKeys.Sort(); // Inspector không bắt buộc phải nhập các mốc đúng thứ tự.
    }

    internal (IShooter Shooter, IProjectileMove Movement, int Count) ResolveShooting(int level,
        IShooter baseShooter, IProjectileMove baseMovement, int baseCount)
    {
        var selected = (Shooter: baseShooter, Movement: baseMovement, Count: baseCount);
        int best = 0;
        foreach (var entry in shooting)
            if (entry.Key <= level && entry.Key > best) { best = entry.Key; selected = entry.Value; }
        return selected; // Mốc gần nhất <= level; mỗi entry chứa trọn bộ cấu hình bắn.
    }

    internal IReadOnlyList<IGameplayEffect> ResolveEffects(int level, IReadOnlyList<IGameplayEffect> baseEffects)
    {
        // Tạo snapshot mới CHỈ khi đổi cấp. Đạn cũ vẫn giữ list cũ, không bị nâng cấp ngược khi đang bay.
        var resolved = new List<IGameplayEffect>(baseEffects);
        var unique = new HashSet<IGameplayEffect>(baseEffects);
        foreach (int key in effectKeys)
        {
            if (key > level) break;
            foreach (IGameplayEffect effect in effects[key])
                if (unique.Add(effect)) resolved.Add(effect);
        }
        return resolved; // Mở effect cộng dồn; cấu hình cùng reference chỉ thêm một lần.
    }
}
