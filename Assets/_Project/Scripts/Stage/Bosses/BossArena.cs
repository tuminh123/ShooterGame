using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Đấu trường Boss: dọn quái thường, khóa vùng di chuyển, vẽ viền, kéo Player và Elite vào trong rồi sinh Boss.
/// Do <see cref="BossSpawnSystem"/> sở hữu (StageManager tạo và truyền vào); không bind vào Zenject.
/// </summary>
internal sealed class BossArena
{

    #region Dependencies And State
    private readonly ICharacterRegistry characters;
    private readonly IPlayerReader players;
    private readonly EnemySpawner spawner; // Tạo/thu hồi quái, vùng sinh; do StageManager tạo và dùng chung cho mọi system.
    private readonly IMovementArena movement;
    private GameObject arenaVisual;
    private Material arenaMaterial;

    #endregion

    #region Initialization

    public BossArena(
        ICharacterRegistry characterRegistry,
        IPlayerReader playerReader,
        EnemySpawner spawner,
        IMovementArena movementArena)
    {
        this.spawner = spawner ?? throw new System.ArgumentNullException(nameof(spawner));
        this.characters = characterRegistry;
        this.players = playerReader;
        this.movement = movementArena; // SimulationManager (SimulationContracts.cs).
    }

    #endregion

    #region Runtime Operations

    /// <summary>Dọn enemy thường, giữ Elite, mở ranh giới và tạo Boss trong vùng.</summary>
    public CharacterView Summon(CharacterDataSO data, int level, Vector2 arenaSize)
    {
        CharacterView player = players.CurrentPlayer;
        if (player == null || data == null || arenaSize.x <= 0f || arenaSize.y <= 0f)
            throw new System.InvalidOperationException("Boss cần player, dữ liệu hợp lệ và đấu trường dương.");

        Vector2 center = player.transform.position;
        Bounds bounds = new Bounds(center, new Vector3(arenaSize.x, arenaSize.y, 100f));
        movement.SetArenaBounds(bounds);
        CreateArenaOutline(center, arenaSize);
        var currentCharacters = new List<CharacterView>(characters.Characters.Keys);
        foreach (CharacterView enemy in currentCharacters)
        {
            if (enemy == null || enemy.RuntimeData == null || enemy.RuntimeData.Team != CharacterTeam.Enemy || enemy.IsElite)
                continue;
            spawner.Despawn(enemy);
        }
        ClampIntoArena(player.transform, bounds);
        foreach (CharacterView enemy in currentCharacters)
            if (enemy != null && enemy.IsElite && enemy.RuntimeData != null) ClampIntoArena(enemy.transform, bounds);

        Vector2 bossPosition = center + Vector2.up * (arenaSize.y * 0.3f);
        return spawner.Spawn(data, level, bossPosition);
    }

    #endregion

    #region Cleanup

    /// <summary>Gỡ ranh giới giữa lượt chơi.</summary>
    public void Clear()
    {
        movement.SetArenaBounds(null);
        if (arenaVisual != null) Object.Destroy(arenaVisual);
        if (arenaMaterial != null) Object.Destroy(arenaMaterial);
        arenaVisual = null;
        arenaMaterial = null;
    }

    #endregion

    #region Creation

    /// <summary>Vẽ viền đấu trường quanh vị trí player lúc Boss xuất hiện.</summary>
    private void CreateArenaOutline(Vector2 center, Vector2 size)
    {
        Clear();
        movement.SetArenaBounds(new Bounds(center, new Vector3(size.x, size.y, 100f)));
        arenaVisual = new GameObject("Boss Arena");
        var line = arenaVisual.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.loop = false;
        line.positionCount = 5;
        line.startWidth = line.endWidth = 0.08f;
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) throw new System.InvalidOperationException("Không tìm thấy shader để vẽ viền đấu trường.");
        arenaMaterial = new Material(shader);
        line.sharedMaterial = arenaMaterial;
        line.startColor = line.endColor = new Color(1f, 0.25f, 0.12f, 0.9f);
        Vector2 half = size * 0.5f;
        line.SetPosition(0, center + new Vector2(-half.x, -half.y));
        line.SetPosition(1, center + new Vector2(-half.x, half.y));
        line.SetPosition(2, center + new Vector2(half.x, half.y));
        line.SetPosition(3, center + new Vector2(half.x, -half.y));
        line.SetPosition(4, center + new Vector2(-half.x, -half.y));
    }

    #endregion

    #region Runtime Operations

    /// <summary>Kẹp vị trí nhân vật vào bên trong đấu trường.</summary>
    private static void ClampIntoArena(Transform target, Bounds bounds)
    {
        Vector3 point = target.position;
        point.x = Mathf.Clamp(point.x, bounds.min.x, bounds.max.x);
        point.y = Mathf.Clamp(point.y, bounds.min.y, bounds.max.y);
        target.position = point;
    }

    #endregion

}
