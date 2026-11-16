using System;
using System.Collections.Generic;
using UnityEngine;

#region EnemySpawnValidation

/// <summary>Kiểm tra cấu hình quái và vùng sinh trước khi triển khai.</summary>
public interface IEnemySpawnValidation
{
    void ValidateCharacter(CharacterDataSO data);
    void ValidatePerimeter();
}

#endregion

#region EnemySpawn

/// <summary>Tạo quái thường/Elite và controller phù hợp.</summary>
public interface IEnemySpawn
{
    EnemyController CreateController();
    CharacterView Spawn(CharacterDataSO data, int level, Vector2 point, ICharacterController controller = null);
    CharacterView SpawnElite(CharacterDataSO data, int level, Vector2 point, float scale, float healthMultiplier);
}

#endregion

#region EnemyDespawn

/// <summary>Thu hồi quái mà không phát sự kiện chết hoặc rơi loot.</summary>
public interface IEnemyDespawn
{
    void Despawn(CharacterView character);
}

#endregion

#region EnemySpawnGeometry

/// <summary>Đọc vùng sinh, camera và lấy các vị trí spawn.</summary>
public interface IEnemySpawnGeometry
{
    Vector2 SpawnCenterPosition { get; }
    Camera FormationCamera { get; }
    Vector2 SpawnAreaSize { get; }
    float SpawnPlaneZ { get; }
    Vector2 ChoosePerimeterPoint();
    void GetSpawnPoints(List<Vector2> destination);
}

#endregion

#region EnemySpawnLateEvents

/// <summary>Thông báo bước cuối frame để cập nhật nhóm spawn.</summary>
public interface IEnemySpawnLateEvents
{
    event Action SpawnLateUpdate;
}

#endregion

#region System Contract

/// <summary>Gom hợp đồng của EnemySpawnManager; consumer nhận vai trò nhỏ cần dùng.</summary>
public interface IEnemySpawnService
{
    IEnemySpawnValidation GetEnemySpawnValidation();
    IEnemySpawn GetEnemySpawn();
    IEnemyDespawn GetEnemyDespawn();
    IEnemySpawnGeometry GetEnemySpawnGeometry();
    IEnemySpawnLateEvents GetEnemySpawnLateEvents();
}

#endregion
