using UnityEngine;
using UnityEngine.Serialization;

// Template chỉ số, hình ảnh và danh sách skill vốn có, dùng chung cấu trúc cho player, enemy và boss.
// Lưu ý: Không lưu máu hiện tại, cooldown hoặc thay đổi trong trận trên asset; các trạng thái đó thuộc runtime riêng.
[CreateAssetMenu(fileName = "CharacterData", menuName = "Shooter Game/Data/Character")]
public sealed class CharacterDataSO : ScriptableObject
{
    #region Character Identity

    [FormerlySerializedAs("characterId")]
    [Tooltip("Tên hiển thị trên UI; có thể đổi mà không thay khóa nhân vật trong save.")]
    [SerializeField] private string displayName;
    [Tooltip("Khóa ghép Character Database, Level Config và save. Chọn khác None; không khai báo trùng trong database.")]
    [SerializeField] private CharacterType characterType;
    [Tooltip("Phe dùng để chọn mục tiêu và xử lý sát thương. Boss cũng phải chọn Enemy.")]
    [SerializeField] private CharacterTeam team;
    [Tooltip("Bật cho template Boss để dùng luồng boss và animation tương ứng; Enemy thường và Elite để tắt.")]
    [SerializeField] private bool isBoss; // Đánh dấu boss để hiện HP/giáp; không thay đổi phe hoặc cách nhận sát thương.
    [Tooltip("Prefab ngoại hình, kích thước/offset hình ảnh, hitbox và bố cục thanh máu. Các phần được chỉnh độc lập.")]
    [SerializeField] private CharacterVisualSettings visualSettings = CharacterVisualSettings.Default;
    [Tooltip("Ảnh chân dung trên màn Character (thẻ Collection). Xuất bằng Tools/Shooter Game/Character UI Exporter.")]
    [SerializeField] private Sprite portrait;
    [Tooltip("Icon mảnh của nhân vật (đầu nhân vật) trên giá nâng sao và thanh mảnh. Xuất bằng Tools/Shooter Game/Character UI Exporter; để trống thì dùng ảnh chân dung.")]
    [SerializeField] private Sprite shardIcon;
    [Tooltip("Nhân vật dạng UI (Image theo bộ phận, đứng Idle) cho màn Detail. Xuất bằng Tools/Shooter Game/Character UI Exporter.")]
    [SerializeField] private CharacterUIVisual uiVisualPrefab;
    [Tooltip("Độ hiếm hiển thị trên màn Character. Chỉ là thông tin hiển thị, không đổi chỉ số.")]
    [SerializeField] private CharacterRarity rarity = CharacterRarity.Common;

    #region  Get
    
    public string DisplayName => displayName; // Nhãn hiển thị, không dùng làm khóa tra cứu.
    public CharacterType CharacterType => characterType; // Khóa dùng chung cho database, level service và save.
    public CharacterTeam Team => team; // Dùng phân biệt Player và Enemy khi tạo hành vi và xử lý tiếp xúc.
    public bool IsBoss => isBoss;
    public CharacterVisualSettings VisualSettings => visualSettings;
    public GameObject VisualPrefab => visualSettings.Prefab;
    public HitboxData Hitbox => visualSettings.Hitbox;
    /// <summary>Ảnh chân dung cho UI; null thì UI giữ ảnh mặc định của prefab.</summary>
    public Sprite Portrait => portrait;
    /// <summary>Icon mảnh của nhân vật; chưa xuất thì dùng ảnh chân dung (có thể null nếu cả hai chưa có).</summary>
    public Sprite ShardIcon => shardIcon != null ? shardIcon : portrait;
    /// <summary>Prefab nhân vật dạng UI cho màn Detail; null khi chưa xuất.</summary>
    public CharacterUIVisual UIVisualPrefab => uiVisualPrefab;
    /// <summary>Độ hiếm chỉ để hiển thị.</summary>
    public CharacterRarity Rarity => rarity;

    #endregion

    #endregion

    #region Base Stats Template

    [Tooltip("Chỉ số gốc ở cấp 1, trước bonus cấp và trang bị. Các tỷ lệ dùng thang 100 = 100%.")]
    [SerializeField] private BaseStats baseStats = BaseStats.Default; // Crit damage là phần trăm CỘNG THÊM vào đòn thường, không phải tổng.

    // Trả về bản sao struct; sửa bản sao không làm thay đổi dữ liệu của asset.
    public BaseStats BaseStats => baseStats;

    public void ValidateStats() => baseStats.Validate();

    #endregion

    #region Skills (mọi skill của nhân vật)

    [Tooltip("MỌI skill của nhân vật (nguồn Character/Equipment), mỗi skill một lần. Skill không có bước mở khóa trong Skill Progression là skill có sẵn; cấp ban đầu đọc từ từng SkillDataSO. Enemy/Boss: mọi skill đều có sẵn.")]
    [SerializeField] private SkillDataSO[] skills = System.Array.Empty<SkillDataSO>();

    /// <summary>Mọi skill của nhân vật, theo thứ tự khai báo (cũng là thứ tự ô skill trên màn Character).</summary>
    public System.Collections.Generic.IReadOnlyList<SkillDataSO> Skills => skills;

    #endregion

    #region Skill Progression (chỉ Player)

    [Tooltip("Các bước mở/nâng skill của Player, chọn skill trong danh sách Skills ở trên. Ví dụ: EmergencyShield cấp 1 khi Character Level ≥ 15 (mở khóa); HolyBolt cấp 3 khi Stars ≥ 2 (nâng cấp). Enemy bỏ qua.")]
    [SerializeField] private SkillProgressStep[] skillProgression = System.Array.Empty<SkillProgressStep>();

    /// <summary>Các bước tiến trình skill (chỉ đọc), để UI liệt kê điều kiện mở/nâng.</summary>
    public System.Collections.Generic.IReadOnlyList<SkillProgressStep> SkillProgression => skillProgression;

    /// <summary>
    /// Nơi DUY NHẤT tính skill của Player theo level và sao. Combat dùng lúc spawn và UI dùng để hiển thị,
    /// nên hai bên không bao giờ lệch nhau.
    /// </summary>
    /// <remarks>
    /// Skill không có bước mở khóa thì luôn có; skill có bước mở khóa (Skill Level = 1) chỉ có khi một bước đó đã thỏa.
    /// Cấp mỗi skill = cấp cao nhất trong các bước đã thỏa, không thấp hơn cấp mặc định của skill.
    /// Ghi vào <paramref name="result"/> do caller sở hữu (xóa trước khi ghi) để không cấp phát mỗi lần gọi.
    /// </remarks>
    public void GetSkillGrants(int characterLevel, int stars, System.Collections.Generic.List<SkillGrant> result)
    {
        if (result == null)
            throw new System.ArgumentNullException(nameof(result));
        if (characterLevel < 1)
            throw new System.ArgumentOutOfRangeException(nameof(characterLevel));
        ValidateSkillProgression();
        result.Clear();
        foreach (SkillDataSO skill in skills)
        {
            if (IsUnlocked(skill, characterLevel, stars))
                result.Add(new SkillGrant(skill, GetProgressLevel(skill, characterLevel, stars)));
        }
    }

    /// <summary>
    /// Mọi skill của nhân vật (chính là danh sách Skills). Màn Character dùng để vẽ đủ ô skill, kể cả ô còn khóa;
    /// danh sách rỗng thì không có ô nào.
    /// </summary>
    public void GetAllSkills(System.Collections.Generic.List<SkillDataSO> result)
    {
        if (result == null)
            throw new System.ArgumentNullException(nameof(result));
        result.Clear();
        result.AddRange(skills);
    }

    /// <summary>
    /// Bước mở khóa (Skill Level = 1) đầu tiên của <paramref name="skill"/>; false với skill có sẵn (không cần mở).
    /// Màn Character dùng để hiện điều kiện như "Lv 15" trên ô còn khóa.
    /// </summary>
    public bool TryGetUnlockStep(SkillDataSO skill, out SkillProgressStep unlock)
    {
        foreach (SkillProgressStep step in skillProgression)
        {
            if (step.Skill == skill && step.SkillLevel == 1)
            {
                unlock = step;
                return true;
            }
        }
        unlock = default;
        return false;
    }

    /// <summary>Các bước skill ở đúng sao <paramref name="star"/> (một sao có thể nâng nhiều skill), theo thứ tự khai báo.</summary>
    public void GetStarSteps(int star, System.Collections.Generic.List<SkillProgressStep> result)
    {
        if (result == null)
            throw new System.ArgumentNullException(nameof(result));
        result.Clear();
        foreach (SkillProgressStep step in skillProgression)
        {
            if (step.Condition == SkillConditionType.Stars && step.Required == star)
                result.Add(step);
        }
    }

    // Skill không có bước mở khóa là skill có sẵn; có bước mở khóa thì chỉ mở khi một bước đó đã thỏa.
    // Bước nâng cấp đã thỏa không tự mở skill còn khóa.
    private bool IsUnlocked(SkillDataSO skill, int characterLevel, int stars)
    {
        bool hasUnlockStep = false;
        foreach (SkillProgressStep step in skillProgression)
        {
            if (step.Skill != skill || step.SkillLevel != 1)
                continue;
            hasUnlockStep = true;
            if (step.IsMet(characterLevel, stars))
                return true;
        }
        return !hasUnlockStep;
    }

    // Cấp cao nhất trong các bước đã thỏa; không có bước nào thì dùng cấp mặc định ghi trong asset của skill.
    private int GetProgressLevel(SkillDataSO skill, int characterLevel, int stars)
    {
        int level = skill.Level;
        foreach (SkillProgressStep step in skillProgression)
        {
            if (step.Skill == skill && step.IsMet(characterLevel, stars))
                level = Mathf.Max(level, step.SkillLevel);
        }
        return level;
    }

    #endregion

    #region Star Stats (thưởng dự phòng của sao, chỉ Player)

    [Tooltip("Chỉ số dự phòng của sao 1..5 (phần tử 0 = sao 1). Chỉ có tác dụng khi sao đó không có bước skill nào trong Skill Progression. Value = 0 là không có.")]
    [SerializeField] private StarStatReward[] starStats = new StarStatReward[CharacterLevelConfigSO.MaxStars];

    /// <summary>Sao <paramref name="star"/> có ít nhất một bước skill (mở hoặc nâng) hay không.</summary>
    public bool HasStarSkillReward(int star)
    {
        foreach (SkillProgressStep step in skillProgression)
        {
            if (step.Condition == SkillConditionType.Stars && step.Required == star)
                return true;
        }
        return false;
    }

    /// <summary>
    /// Nơi DUY NHẤT quyết định sao <paramref name="star"/> có cộng chỉ số dự phòng không:
    /// chỉ khi sao không có bước skill nào và đã nhập chỉ số. Progression (bonus) và màn Character (tab Stars) cùng gọi.
    /// </summary>
    public bool TryGetStarStat(int star, out StarStatReward reward)
    {
        reward = default;
        if (star < 1 || star > starStats.Length || HasStarSkillReward(star))
            return false;
        reward = starStats[star - 1];
        return reward.HasValue;
    }

    /// <summary>Tổng chỉ số dự phòng của các sao đã mở (1..<paramref name="stars"/>); cộng thẳng vào bonus vĩnh viễn.</summary>
    public StatBonus GetStarBonus(int stars)
    {
        StatBonus total = default;
        int opened = Mathf.Min(stars, CharacterLevelConfigSO.MaxStars);
        for (int star = 1; star <= opened; star++)
        {
            if (TryGetStarStat(star, out StarStatReward reward))
                total += StatBonus.Of(reward.Stat, reward.Value);
        }
        return total;
    }

    #endregion

    #region Configuration Validation

    /// <summary>
    /// Chặn cấu hình skill sai trước khi dùng: skill trống/trùng/nguồn Gameplay, bước chọn skill không có trong Skills,
    /// cấp vượt số cấp của skill, điều kiện sao ngoài 1..5, chỉ số sao sai. Không tự sửa asset.
    /// </summary>
    public void ValidateSkillProgression()
    {
        var known = new System.Collections.Generic.HashSet<SkillDataSO>();
        foreach (SkillDataSO skill in skills)
        {
            ValidateCharacterSkill(skill);
            if (!known.Add(skill))
                throw new System.InvalidOperationException($"{name}: {skill.name} khai báo trùng trong Skills.");
        }

        foreach (SkillProgressStep step in skillProgression)
        {
            if (step.Skill == null)
                throw new System.InvalidOperationException($"{name}: một bước Skill Progression chưa chọn skill.");
            if (!known.Contains(step.Skill))
                throw new System.InvalidOperationException($"{name}: {step.Skill.name} không có trong Skills; kéo skill vào Skills rồi chọn lại.");
            ValidateStep(step);
        }

        ValidateStarStats();
    }

    // Một bước hợp lệ: cấp nằm trong số cấp của skill, điều kiện thuộc enum, sao trong 1..MaxStars.
    private void ValidateStep(SkillProgressStep step)
    {
        if (step.SkillLevel < 1 || step.SkillLevel > step.Skill.MaxLevel)
            throw new System.InvalidOperationException($"{name}: cấp của {step.Skill.name} phải trong 1..{step.Skill.MaxLevel}.");
        if (step.Required < 1)
            throw new System.InvalidOperationException($"{name}: điều kiện của {step.Skill.name} phải >= 1.");
        if (!System.Enum.IsDefined(typeof(SkillConditionType), step.Condition))
            throw new System.InvalidOperationException($"{name}: loại điều kiện của {step.Skill.name} không hợp lệ.");
        // Thiết kế hiện tại: level chỉ mở khóa skill, nâng cấp skill chỉ bằng sao. Bỏ dòng này nếu sau này cho level nâng skill.
        if (step.Condition == SkillConditionType.CharacterLevel && step.SkillLevel != 1)
            throw new System.InvalidOperationException($"{name}: bước theo Character Level chỉ dùng để mở khóa (Skill Level = 1); nâng cấp {step.Skill.name} bằng điều kiện Stars.");
        if (step.Condition == SkillConditionType.Stars && step.Required > CharacterLevelConfigSO.MaxStars)
            throw new System.InvalidOperationException($"{name}: điều kiện sao của {step.Skill.name} phải trong 1..{CharacterLevelConfigSO.MaxStars}.");
    }

    // Tối đa một mục mỗi sao; chỉ số thuộc enum, giá trị hữu hạn và không âm.
    private void ValidateStarStats()
    {
        if (starStats.Length > CharacterLevelConfigSO.MaxStars)
            throw new System.InvalidOperationException($"{name}: Star Stats tối đa {CharacterLevelConfigSO.MaxStars} mục (mỗi sao một mục).");
        foreach (StarStatReward reward in starStats)
        {
            if (!System.Enum.IsDefined(typeof(StatType), reward.Stat) || float.IsNaN(reward.Value) ||
                float.IsInfinity(reward.Value) || reward.Value < 0f)
                throw new System.InvalidOperationException($"{name}: Star Stats có chỉ số không hợp lệ hoặc giá trị âm.");
        }
    }

    // Skill của nhân vật phải có và là definition riêng (nguồn Character/Equipment), không dùng chung asset Gameplay.
    private void ValidateCharacterSkill(SkillDataSO skill)
    {
        if (skill == null)
            throw new System.InvalidOperationException($"{name}: skill không được để trống.");
        if (skill.Source == SkillSource.Gameplay)
            throw new System.InvalidOperationException($"{name}: {skill.name} là skill Gameplay; skill nhân vật phải dùng definition nguồn Character/Equipment.");
    }

    #endregion
}
