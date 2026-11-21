using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Cấu hình một Elite hoặc Boss bằng dữ liệu nhân vật có sẵn.</summary>
[CreateAssetMenu(fileName = "BossSpawn", menuName = "Shooter Game/Enemy/Special Spawn")]
public sealed class BossSpawnDataSO : ScriptableObject, IStageSpawnData
{
    [Serializable]
    public sealed class Entry
    {
        [Tooltip("Elite sinh theo Elite Stage Time; Boss sinh theo điều kiện wave. Toàn Stage chỉ được có một entry Boss.")]
        [SerializeField] private SpecialEnemyType type;
        [Tooltip("Template phe Enemy có visual. Boss cần Is Boss bật; Elite dùng Enemy thường và tối đa một skill.")]
        [SerializeField] private CharacterDataSO character;
        [Tooltip("Cấp nhân vật khi spawn, từ 1 trở lên; bonus được lấy từ Character Level Config.")]
        [SerializeField][Min(1)] private int enemyLevel = 1;
        [Tooltip("Mốc sinh Elite tính từ đầu Stage, bằng giây gameplay. 0 = sinh ngay; chỉ dùng khi Type là Elite.")]
        [SerializeField][Min(0f)] private float eliteStageTime;
        [Tooltip("Hệ số kích thước Elite so với Enemy thường; 1 = giữ nguyên, 1.5 = lớn hơn 50%. Chỉ dùng cho Elite.")]
        [SerializeField][Min(1f)] private float eliteScale = 1.5f;
        [Tooltip("Hệ số máu tối đa của Elite sau khi áp dụng cấp. 2 = gấp đôi HP; chỉ dùng cho Elite.")]
        [SerializeField][Min(1f)] private float eliteHealthMultiplier = 2f;

        /// <summary>Elite sinh theo Elite Stage Time; Boss sinh theo điều kiện wave. Toàn Stage chỉ được có một entry Boss.</summary>
        public SpecialEnemyType Type => type;
        /// <summary>Template phe Enemy có visual. Boss cần Is Boss bật; Elite dùng Enemy thường và tối đa một skill.</summary>
        public CharacterDataSO Character => character;
        /// <summary>Cấp nhân vật khi spawn, từ 1 trở lên; bonus được lấy từ Character Level Config.</summary>
        public int EnemyLevel => enemyLevel;
        /// <summary>Mốc sinh Elite tính từ đầu Stage, bằng giây gameplay. 0 = sinh ngay; chỉ dùng khi Type là Elite.</summary>
        public float EliteStageTime => eliteStageTime;
        /// <summary>Hệ số kích thước Elite so với Enemy thường; 1 = giữ nguyên, 1.5 = lớn hơn 50%. Chỉ dùng cho Elite.</summary>
        public float EliteScale => eliteScale;
        /// <summary>Hệ số máu tối đa của Elite sau khi áp dụng cấp. 2 = gấp đôi HP; chỉ dùng cho Elite.</summary>
        public float EliteHealthMultiplier => eliteHealthMultiplier;
    }

    [Tooltip("Nhiều Elite được phép; tối đa một entry Boss trong một Stage.")]
    [SerializeField] private List<Entry> entries = new List<Entry>();
    [Tooltip("AfterAllWavesSpawned: khi mọi chuỗi wave sinh xong. AfterFinalWaveCleared: chờ dọn sạch quái wave; mọi bộ Boss trong Stage phải cùng điều kiện.")]
    [SerializeField] private BossAppearanceCondition bossAppearanceCondition;
    [Tooltip("Kích thước vùng chiến đấu quanh player khi Boss xuất hiện.")]
    [SerializeField] private Vector2 arenaSize = new Vector2(12f, 8f);

    /// <summary>Nhiều Elite được phép; tối đa một entry Boss trong một Stage.</summary>
    public IReadOnlyList<Entry> Entries => entries;
    /// <summary>AfterAllWavesSpawned: khi mọi chuỗi wave sinh xong. AfterFinalWaveCleared: chờ dọn sạch quái wave; mọi bộ Boss trong Stage phải cùng điều kiện.</summary>
    public BossAppearanceCondition BossAppearanceCondition => bossAppearanceCondition;
    /// <summary>Kích thước vùng chiến đấu quanh player khi Boss xuất hiện.</summary>
    public Vector2 ArenaSize => arenaSize;

    /// <summary>Kiểm tra vai trò, điều kiện Boss và giới hạn skill của Elite.</summary>
    public void Validate()
    {
        if (entries == null || entries.Count == 0 || !Enum.IsDefined(typeof(BossAppearanceCondition), bossAppearanceCondition))
            throw new InvalidOperationException("Special Spawn cần entry và điều kiện Boss hợp lệ.");
        bool hasBoss = false;
        foreach (Entry entry in entries)
        {
            if (entry == null || !Enum.IsDefined(typeof(SpecialEnemyType), entry.Type) || entry.Character == null ||
                entry.Character.Team != CharacterTeam.Enemy || entry.Character.VisualPrefab == null || entry.EnemyLevel < 1 ||
                float.IsNaN(entry.EliteStageTime) || float.IsInfinity(entry.EliteStageTime) || entry.EliteStageTime < 0f)
                throw new InvalidOperationException("Mỗi entry cần loại, CharacterDataSO enemy và cấp hợp lệ.");
            if (entry.Type == SpecialEnemyType.Elite && (entry.Character.IsBoss || entry.EliteScale < 1f ||
                entry.EliteHealthMultiplier < 1f || entry.Character.Skills.Count > 1))
                throw new InvalidOperationException("Elite cần nhân vật thường, scale/HP từ 1 trở lên và tối đa một skill.");
            if (entry.Type == SpecialEnemyType.Boss)
            {
                if (!entry.Character.IsBoss || hasBoss || arenaSize.x <= 0f || arenaSize.y <= 0f)
                    throw new InvalidOperationException("Stage chỉ nhận một Boss; CharacterDataSO và đấu trường phải hợp lệ.");
                hasBoss = true;
            }
        }
    }
}
