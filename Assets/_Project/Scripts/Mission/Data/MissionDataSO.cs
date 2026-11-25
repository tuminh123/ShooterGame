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
    [Tooltip("Nhóm việc được tính tiến độ.")]
    [SerializeField] private MissionActivity activity;
    [Tooltip("Giá trị của enum chi tiết thuộc nhóm (xem DetailType). 0 = Any: loại nào cũng tính.")]
    [SerializeField] private int detail;
    [Tooltip("Đối tượng đích danh (loại quái với Kill, nhân vật với Upgrade). 0 = bất kỳ.")]
    [SerializeField] private int subject;
    [SerializeField, Min(1)] private int target = 1;

    /// <summary>ID ổn định trong mission.</summary>
    public string Id => id;

    /// <summary>Chữ hiện trên UI.</summary>
    public string Description => description;

    /// <summary>Nhóm việc được tính tiến độ.</summary>
    public MissionActivity Activity => activity;

    /// <summary>Giá trị của enum chi tiết thuộc nhóm; 0 = Any.</summary>
    public int Detail => detail;

    /// <summary>Đối tượng đích danh (CharacterType); 0 = bất kỳ.</summary>
    public int Subject => subject;

    /// <summary>Số cần đạt.</summary>
    public int Target => target;

    /// <summary>
    /// Enum chi tiết của một nhóm. Một chỗ duy nhất biết nhóm nào dùng enum nào: Validate và Inspector cùng đọc ở đây.
    /// Thêm nhóm mới thì thêm một dòng.
    /// </summary>
    public static Type DetailType(MissionActivity activity)
    {
        switch (activity)
        {
            case MissionActivity.Kill: return typeof(EnemyKind);
            case MissionActivity.Battle: return typeof(BattleDetail);
            case MissionActivity.Collect: return typeof(CollectDetail);
            case MissionActivity.Upgrade: return typeof(UpgradeDetail);
            case MissionActivity.Mission: return typeof(MissionDetail);
            default: return null;
        }
    }

    /// <summary>Nhóm này có lọc theo đối tượng đích danh (Subject) không.</summary>
    public static bool UsesSubject(MissionActivity activity)
        => activity == MissionActivity.Kill || activity == MissionActivity.Upgrade;

    /// <summary>Màn người chơi nên tới để làm mission con này. Một chỗ duy nhất map nhóm việc sang màn, để UI không phải biết luật.</summary>
    public MissionDestination Destination
    {
        get
        {
            switch (activity)
            {
                case MissionActivity.Kill:
                case MissionActivity.Battle:
                case MissionActivity.Collect:
                    return MissionDestination.Battle;
                case MissionActivity.Upgrade:
                    return detail == (int)UpgradeDetail.EquipmentLevel ? MissionDestination.Equipment : MissionDestination.Heroes;
                default: // Mission (Final Mission): không có nơi nào để "đi tới".
                    return MissionDestination.None;
            }
        }
    }

    /// <summary>Việc vừa xảy ra có tính cho mission con này không. 0 ở task nghĩa là bất kỳ.</summary>
    public bool Matches(MissionSignal signal)
        => signal.Activity == activity &&
           (detail == 0 || detail == signal.Detail) &&
           (subject == 0 || subject == signal.Subject);
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
            if (!Enum.IsDefined(typeof(MissionActivity), task.Activity) || task.Target < 1)
                throw new InvalidOperationException($"{name}/{task.Id}: nhóm việc không hợp lệ hoặc Target < 1.");
            if (!Enum.IsDefined(MissionTask.DetailType(task.Activity), task.Detail))
                throw new InvalidOperationException($"{name}/{task.Id}: chi tiết {task.Detail} không thuộc nhóm {task.Activity}.");
            if (task.Subject != 0 && (!MissionTask.UsesSubject(task.Activity) || !Enum.IsDefined(typeof(CharacterType), task.Subject)))
                throw new InvalidOperationException($"{name}/{task.Id}: Subject chỉ dùng với Kill/Upgrade và phải là CharacterType hợp lệ.");
        }
        RewardBatch.From(rewards);
    }

    #endregion
}
