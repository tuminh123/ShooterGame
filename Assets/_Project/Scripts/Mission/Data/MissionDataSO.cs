using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Một mission con: việc cần làm và số cần đạt. Không có thưởng riêng.</summary>
[Serializable]
public sealed class MissionTask
{
    [Tooltip("ID ổn định trong mission, được lưu trong save. Đổi ID thì tiến độ đã lưu của mission con này về 0.")]
    [SerializeField] private string id;
    [Tooltip("Chữ hiện trên UI, ví dụ \"Defeat 100 enemies\".")]
    [SerializeField] private string description;
    [SerializeField] private MissionObjectiveType objective;
    [Tooltip("Chỉ dùng với Kill Enemy: nhóm quái được tính. Any = mọi nhóm, gồm cả boss.")]
    [SerializeField] private EnemyKind enemyKind;
    [Tooltip("Chỉ dùng với Kill Enemy: đích danh loại quái. None = loại nào cũng tính.")]
    [SerializeField] private CharacterType character;
    [SerializeField, Min(1)] private int target = 1;

    /// <summary>ID ổn định trong mission.</summary>
    public string Id => id;

    /// <summary>Chữ hiện trên UI.</summary>
    public string Description => description;

    /// <summary>Việc được tính tiến độ.</summary>
    public MissionObjectiveType Objective => objective;

    /// <summary>Nhóm quái cần giết; chỉ có nghĩa với KillEnemy.</summary>
    public EnemyKind EnemyKind => enemyKind;

    /// <summary>Loại quái cần giết; None là bất kỳ. Chỉ có nghĩa với KillEnemy.</summary>
    public CharacterType Character => character;

    /// <summary>Số cần đạt.</summary>
    public int Target => target;

    /// <summary>Việc vừa xảy ra có tính cho mission con này không.</summary>
    public bool Matches(MissionSignal signal)
    {
        if (signal.Objective != objective) return false;
        if (objective != MissionObjectiveType.KillEnemy) return true;
        return (enemyKind == EnemyKind.Any || enemyKind == signal.EnemyKind) &&
               (character == CharacterType.None || character == signal.Character);
    }
}

/// <summary>
/// Một mission chính, lắp từ nhiều mission con (<see cref="MissionTask"/>) và nhiều phần thưởng (<see cref="IReward"/>).
/// Xong mọi mission con thì nhận được cả gói thưởng.
/// </summary>
/// <remarks>Asset chỉ chứa cấu hình; tiến độ nằm ở <see cref="MissionRuntime"/>. Một mission dùng được ở nhiều database.</remarks>
[CreateAssetMenu(fileName = "Mission", menuName = "Shooter Game/Missions/Mission")]
public sealed class MissionDataSO : ScriptableObject
{
    #region Configuration

    [Tooltip("ID ổn định được lưu trong save; không trống, không trùng trong cùng database.")]
    [SerializeField] private string id;
    [SerializeField] private string displayName;
    [SerializeField, TextArea] private string description;
    [SerializeField] private Sprite icon;
    [Tooltip("Các mission con, chạy song song; xong hết mới nhận được thưởng.")]
    [SerializeField] private List<MissionTask> tasks = new List<MissionTask>();
    [SerializeReference] private List<IReward> rewards = new List<IReward>();

    /// <summary>ID ổn định.</summary>
    public string Id => id;

    /// <summary>Tên hiển thị.</summary>
    public string DisplayName => displayName;

    /// <summary>Mô tả ngắn.</summary>
    public string Description => description;

    /// <summary>Icon; có thể null.</summary>
    public Sprite Icon => icon;

    /// <summary>Các mission con.</summary>
    public IReadOnlyList<MissionTask> Tasks => tasks;

    /// <summary>Gói thưởng của mission.</summary>
    public IReadOnlyList<IReward> Rewards => rewards;

    #endregion

    #region Validation

    /// <summary>ID, mission con và thưởng hợp lệ. Việc thưởng khớp database nhân vật/trang bị do Progression kiểm tra.</summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(id)) throw new InvalidOperationException($"{name}: mission chưa có ID.");
        if (tasks.Count == 0) throw new InvalidOperationException($"{name}: cần ít nhất một mission con.");
        if (rewards.Count == 0) throw new InvalidOperationException($"{name}: cần ít nhất một phần thưởng.");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (MissionTask task in tasks)
        {
            if (task == null || string.IsNullOrWhiteSpace(task.Id) || !ids.Add(task.Id))
                throw new InvalidOperationException($"{name}: mission con trống, thiếu ID hoặc trùng ID.");
            if (!Enum.IsDefined(typeof(MissionObjectiveType), task.Objective) || task.Target < 1)
                throw new InvalidOperationException($"{name}/{task.Id}: mục tiêu không hợp lệ hoặc Target < 1.");
            if (task.Objective == MissionObjectiveType.KillBoss)
                throw new InvalidOperationException($"{name}/{task.Id}: Kill Boss không còn dùng; chọn Kill Enemy với Enemy Kind = Boss.");
            if (!Enum.IsDefined(typeof(EnemyKind), task.EnemyKind) || !Enum.IsDefined(typeof(CharacterType), task.Character))
                throw new InvalidOperationException($"{name}/{task.Id}: bộ lọc quái không hợp lệ.");
            if (task.Objective != MissionObjectiveType.KillEnemy &&
                (task.EnemyKind != EnemyKind.Any || task.Character != CharacterType.None))
                throw new InvalidOperationException($"{name}/{task.Id}: bộ lọc quái chỉ dùng với Kill Enemy.");
        }
        RewardBatch.From(rewards);
    }

    #endregion
}
