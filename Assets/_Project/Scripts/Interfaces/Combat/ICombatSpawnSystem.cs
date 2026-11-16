using UnityEngine;

#region ProjectileSpawner

/// <summary>Tạo đạn từ snapshot sát thương, quỹ đạo và hình ảnh.</summary>
public interface IProjectileSpawner
{
    bool SpawnBullet(ProjectileSpawnData projectileData, Vector2 spawnPosition, Vector2 shootingDirection, CharacterTarget target = default);
}

#endregion

#region EffectVisualSpawner

/// <summary>Phát VFX theo ID; không áp dụng gameplay effect.</summary>
public interface IEffectVisualSpawner
{
    void SpawnEffect(EffectType effectType, Vector3 spawnPosition);
}

#endregion

#region CombatObjectRelease

/// <summary>Thu hồi vỏ và visual của đối tượng combat.</summary>
public interface ICombatObjectRelease
{
    void Release(GameObject instance);
}

#endregion

#region EffectVisualCleanup

/// <summary>Dọn các hiệu ứng hình ảnh đang được spawner sở hữu.</summary>
public interface IEffectVisualCleanup
{
    void ClearEffects();
}

#endregion

#region System Contract

/// <summary>Gom hợp đồng của CombatSpawner; consumer nhận vai trò nhỏ cần dùng.</summary>
public interface ICombatSpawnSystem
{
    IProjectileSpawner GetProjectileSpawner();
    IEffectVisualSpawner GetEffectVisualSpawner();
    ICombatObjectRelease GetCombatObjectRelease();
    IEffectVisualCleanup GetEffectVisualCleanup();
}

#endregion
