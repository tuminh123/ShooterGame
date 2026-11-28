using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Lên lịch nhiều Elite và tối đa một Boss từ dữ liệu Stage. Do StageManager tạo và tick; không bind vào Zenject.</summary>
internal sealed class BossSpawnSystem
{

    #region Dependencies And State
    public event Action<StageSpawnWarning, float> SpawnScheduled;
    // Stage cung cấp mốc hoàn tất wave nếu xác định được; không thay thế điều kiện summon thực tế.
    public float? ScheduledWaveCompletion { private get; set; }

    #endregion

    #region Supporting Types
    private sealed class ActiveElite
    {
        public BossSpawnDataSO.Entry entry;
        public bool spawned;
    }

    #endregion

    #region Dependencies And State
    private readonly EnemySpawner spawner; // Tạo/thu hồi quái, vùng sinh; do StageManager tạo và dùng chung cho mọi system.
    private readonly StageSpawnRoster roster;
    private readonly BossArena summon; // Đấu trường của Boss: dọn quái thường, khóa vùng, sinh Boss.
    private readonly IAudio audio; // Đổi nhạc + rung khi Boss xuất hiện: Stage sở hữu thời điểm này nên tự phát.
    private AudioId bossMusic; // Theo BossSpawnDataSO của Stage đang chạy.
    private readonly List<ActiveElite> elites = new List<ActiveElite>();
    private BossSpawnDataSO.Entry boss;
    private BossAppearanceCondition condition;
    private Vector2 arenaSize;
    private bool allWavesSpawned, finalWaveCleared, bossSpawned;
    private bool bossSchedulePublished;

    public bool DeploymentComplete
    {
        get
        {
            foreach (ActiveElite elite in elites) if (!elite.spawned) return false;
            return boss == null || bossSpawned;
        }
    }
    public int AliveCount => roster.AliveCount;

    #endregion

    #region Initialization

    /// <summary>Nhận spawner, đấu trường Boss do StageManager tạo và Audio để đổi nhạc khi Boss xuất hiện.</summary>
    public BossSpawnSystem(EnemySpawner spawner, BossArena arena, IAudio audio)
    {
        this.audio = audio ?? throw new ArgumentNullException(nameof(audio));
        this.spawner = spawner ?? throw new ArgumentNullException(nameof(spawner));
        this.summon = arena ?? throw new ArgumentNullException(nameof(arena));
        roster = new StageSpawnRoster(spawner);
    }

    #endregion

    #region Validation

    /// <summary>Kiểm tra entry Elite/Boss và nhân vật tương ứng.</summary>
    public void Validate(BossSpawnDataSO config)
    {
        config.Validate();
        foreach (BossSpawnDataSO.Entry entry in config.Entries) spawner.ValidateCharacter(entry.Character);
    }

    #endregion

    #region Runtime Operations

    /// <summary>Đăng ký các Elite theo StageTime và ghi nhận cấu hình điều kiện Boss.</summary>
    public void Execute(BossSpawnDataSO config)
    {
        condition = config.BossAppearanceCondition;
        bossMusic = config.BossMusic;
        foreach (BossSpawnDataSO.Entry entry in config.Entries)
        {
            if (entry.Type == SpecialEnemyType.Elite)
            {
                elites.Add(new ActiveElite { entry = entry });
                SpawnScheduled?.Invoke(StageSpawnWarning.Elite, entry.EliteStageTime);
            }
            else
            {
                boss = entry;
                arenaSize = config.ArenaSize;
                if (condition == BossAppearanceCondition.AfterAllWavesSpawned && ScheduledWaveCompletion.HasValue)
                {
                    bossSchedulePublished = true;
                    SpawnScheduled?.Invoke(StageSpawnWarning.Boss, ScheduledWaveCompletion.Value);
                }
            }
        }
    }

    /// <summary>Nhận event Wave để kích hoạt điều kiện xuất hiện Boss.</summary>
    public void NotifyAllWavesSpawned() => allWavesSpawned = true;
    public void NotifyFinalWaveCleared() => finalWaveCleared = true;

    /// <summary>Spawn Elite tại mốc StageTime rồi summon Boss khi event Wave phù hợp đã tới.</summary>
    public void Tick(float stageTime, float deltaTime)
    {
        if (deltaTime <= 0f) return;
        foreach (ActiveElite elite in elites)
        {
            if (elite.spawned || stageTime < elite.entry.EliteStageTime) continue;
            var view = spawner.SpawnElite(elite.entry.Character, elite.entry.EnemyLevel,
                spawner.ChoosePerimeterPoint(), elite.entry.EliteScale, elite.entry.EliteHealthMultiplier);
            roster.Add(view);
            elite.spawned = true;
        }
        if (boss == null || bossSpawned) return;
        bool ready = condition == BossAppearanceCondition.AfterAllWavesSpawned ? allWavesSpawned : finalWaveCleared;
        if (!ready) return;
        if (!bossSchedulePublished)
        {
            // Điều kiện dọn quái không có mốc dự đoán chính xác: báo ngay khi điều kiện đạt.
            bossSchedulePublished = true;
            SpawnScheduled?.Invoke(StageSpawnWarning.Boss, stageTime);
        }
        roster.Add(summon.Summon(boss.Character, boss.EnemyLevel, arenaSize));
        bossSpawned = true;
        // Hết trận (thắng/thua) thì Game đổi nhạc theo state; vào trận mới thì Game đặt lại nhạc trận.
        audio.Play(bossMusic);
        audio.Play(AudioId.HapticHeavy);
    }

    #endregion

    #region Cleanup

    /// <summary>Dọn lượt đặc biệt và gỡ ranh giới đấu trường.</summary>
    public void Stop()
    {
        try { roster.Clear(); }
        finally
        {
            summon.Clear();
            elites.Clear();
            boss = null;
            allWavesSpawned = finalWaveCleared = bossSpawned = false;
            bossSchedulePublished = false;
            ScheduledWaveCompletion = null;
        }
    }

    #endregion
}
