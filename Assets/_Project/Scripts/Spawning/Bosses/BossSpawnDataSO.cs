using System;
using System.Collections.Generic;
using UnityEngine;

public enum SpecialEnemyType { Elite, Boss }
public enum BossAppearanceCondition { AfterAllWavesSpawned, AfterFinalWaveCleared }

/// <summary>Cấu hình một Elite hoặc Boss bằng dữ liệu nhân vật có sẵn.</summary>
[CreateAssetMenu(fileName = "BossSpawn", menuName = "Shooter Game/Enemy/Special Spawn")]
public sealed class BossSpawnDataSO : ScriptableObject, IStageSpawnData
{
    [Serializable]
    public sealed class Entry
    {
        [Tooltip("Elite sinh theo Elite Stage Time; Boss sinh theo điều kiện wave. Toàn Stage chỉ được có một entry Boss.")]
        public SpecialEnemyType type;
        [Tooltip("Template phe Enemy có visual. Boss cần Is Boss bật; Elite dùng Enemy thường và tối đa một skill.")]
        public CharacterDataSO character;
        [Tooltip("Cấp nhân vật khi spawn, từ 1 trở lên; bonus được lấy từ Character Level Config.")]
        [Min(1)] public int enemyLevel = 1;
        [Tooltip("Mốc sinh Elite tính từ đầu Stage, bằng giây gameplay. 0 = sinh ngay; chỉ dùng khi Type là Elite.")]
        [Min(0f)] public float eliteStageTime;
        [Tooltip("Hệ số kích thước Elite so với Enemy thường; 1 = giữ nguyên, 1.5 = lớn hơn 50%. Chỉ dùng cho Elite.")]
        [Min(1f)] public float eliteScale = 1.5f;
        [Tooltip("Hệ số máu tối đa của Elite sau khi áp dụng cấp. 2 = gấp đôi HP; chỉ dùng cho Elite.")]
        [Min(1f)] public float eliteHealthMultiplier = 2f;
    }

    [Tooltip("Nhiều Elite được phép; tối đa một entry Boss trong một Stage.")]
    public List<Entry> entries = new List<Entry>();
    [Tooltip("AfterAllWavesSpawned: khi mọi chuỗi wave sinh xong. AfterFinalWaveCleared: chờ dọn sạch quái wave; mọi bộ Boss trong Stage phải cùng điều kiện.")]
    public BossAppearanceCondition bossAppearanceCondition;
    [Tooltip("Kích thước vùng chiến đấu quanh player khi Boss xuất hiện.")]
    public Vector2 arenaSize = new Vector2(12f, 8f);
    public Type SystemType => typeof(BossSpawnSystem);
    public IStageSpawnSystem CreateSystem(Zenject.DiContainer container) => container.Instantiate<BossSpawnSystem>();

    /// <summary>Kiểm tra vai trò, điều kiện Boss và giới hạn skill của Elite.</summary>
    public void Validate()
    {
        if (entries == null || entries.Count == 0 || !Enum.IsDefined(typeof(BossAppearanceCondition), bossAppearanceCondition))
            throw new InvalidOperationException("Special Spawn cần entry và điều kiện Boss hợp lệ.");
        bool hasBoss = false;
        foreach (Entry entry in entries)
        {
            if (entry == null || !Enum.IsDefined(typeof(SpecialEnemyType), entry.type) || entry.character == null ||
                entry.character.Team != CharacterTeam.Enemy || entry.character.VisualPrefab == null || entry.enemyLevel < 1 ||
                float.IsNaN(entry.eliteStageTime) || float.IsInfinity(entry.eliteStageTime) || entry.eliteStageTime < 0f)
                throw new InvalidOperationException("Mỗi entry cần loại, CharacterDataSO enemy và cấp hợp lệ.");
            if (entry.type == SpecialEnemyType.Elite && (entry.character.IsBoss || entry.eliteScale < 1f ||
                entry.eliteHealthMultiplier < 1f || entry.character.Skills.Count > 1))
                throw new InvalidOperationException("Elite cần nhân vật thường, scale/HP từ 1 trở lên và tối đa một skill.");
            if (entry.type == SpecialEnemyType.Boss)
            {
                if (!entry.character.IsBoss || hasBoss || arenaSize.x <= 0f || arenaSize.y <= 0f)
                    throw new InvalidOperationException("Stage chỉ nhận một Boss; CharacterDataSO và đấu trường phải hợp lệ.");
                hasBoss = true;
            }
        }
    }
}
