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

    #region Innate Skills

    [Tooltip("Skill vốn có của nhân vật, dùng asset nguồn Character/Equipment. Cấp ban đầu đọc từ từng SkillDataSO; không lưu cooldown ở đây.")]
    [SerializeField] private SkillDataSO[] skills = System.Array.Empty<SkillDataSO>();
    public System.Collections.Generic.IReadOnlyList<SkillDataSO> Skills => skills; // Bridge đọc skill của từng nhân vật từ cấu hình này.

    #endregion

    #region Skill Progression (chỉ Player)

    [Tooltip("Các bước mở/nâng skill của Player theo level hoặc sao, ví dụ: Skill2 cấp 1 khi Character Level ≥ 20; HolyBolt cấp 3 khi Stars ≥ 2. Enemy bỏ qua.")]
    [SerializeField] private SkillProgressStep[] skillProgression = System.Array.Empty<SkillProgressStep>();

    /// <summary>Các bước tiến trình skill (chỉ đọc), để UI liệt kê điều kiện mở/nâng.</summary>
    public System.Collections.Generic.IReadOnlyList<SkillProgressStep> SkillProgression => skillProgression;

    /// <summary>
    /// Nơi DUY NHẤT tính skill của Player theo level và sao. Combat dùng lúc spawn và UI dùng để hiển thị,
    /// nên hai bên không bao giờ lệch nhau.
    /// </summary>
    /// <remarks>
    /// Skill có sẵn luôn có; skill chỉ nằm trong tiến trình thì có khi một bước mở khóa (Skill Level = 1) đã thỏa.
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
            result.Add(new SkillGrant(skill, GetProgressLevel(skill, characterLevel, stars)));

        for (int i = 0; i < skillProgression.Length; i++)
        {
            SkillDataSO skill = skillProgression[i].Skill;
            // Mỗi skill chỉ thêm một lần: bỏ qua skill có sẵn và skill đã gặp ở bước trước.
            if (IsInnate(skill) || AppearsBefore(skill, i))
                continue;
            if (!IsUnlocked(skill, characterLevel, stars))
                continue;
            result.Add(new SkillGrant(skill, GetProgressLevel(skill, characterLevel, stars)));
        }
    }

    // Skill chưa có sẵn chỉ mở khi một bước mở khóa (Skill Level = 1) của nó đã thỏa;
    // bước nâng cấp đã thỏa không tự mở skill còn khóa.
    private bool IsUnlocked(SkillDataSO skill, int characterLevel, int stars)
    {
        foreach (SkillProgressStep step in skillProgression)
        {
            if (step.Skill == skill && step.SkillLevel == 1 && step.IsMet(characterLevel, stars))
                return true;
        }
        return false;
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

    /// <summary>
    /// Mọi skill của nhân vật, mỗi skill một lần: skill có sẵn trước, rồi skill trong tiến trình theo thứ tự khai báo.
    /// Màn Character dùng để vẽ đủ ô skill, kể cả ô còn khóa.
    /// </summary>
    public void GetAllSkills(System.Collections.Generic.List<SkillDataSO> result)
    {
        if (result == null)
            throw new System.ArgumentNullException(nameof(result));
        result.Clear();
        result.AddRange(skills);
        for (int i = 0; i < skillProgression.Length; i++)
        {
            SkillDataSO skill = skillProgression[i].Skill;
            if (IsInnate(skill) || AppearsBefore(skill, i))
                continue;
            result.Add(skill);
        }
    }

    /// <summary>
    /// Bước mở khóa (Skill Level = 1) đầu tiên của <paramref name="skill"/>; false với skill có sẵn (không cần mở).
    /// Màn Character dùng để hiện điều kiện như "Lv 20" trên ô còn khóa.
    /// </summary>
    public bool TryGetUnlockStep(SkillDataSO skill, out SkillProgressStep unlock)
    {
        unlock = default;
        if (IsInnate(skill))
            return false;
        foreach (SkillProgressStep step in skillProgression)
        {
            if (step.Skill == skill && step.SkillLevel == 1)
            {
                unlock = step;
                return true;
            }
        }
        return false;
    }

    /// <summary>Các bước mở ở đúng sao <paramref name="star"/> (một sao có thể nâng nhiều skill), theo thứ tự khai báo.</summary>
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

    private bool IsInnate(SkillDataSO skill) => System.Array.IndexOf(skills, skill) >= 0;

    private bool AppearsBefore(SkillDataSO skill, int index)
    {
        for (int i = 0; i < index; i++)
        {
            if (skillProgression[i].Skill == skill)
                return true;
        }
        return false;
    }

    #endregion

    #region Configuration Validation

    /// <summary>
    /// Chặn cấu hình skill sai trước khi dùng: skill trống/trùng/nguồn Gameplay, cấp vượt số cấp của skill,
    /// điều kiện sao ngoài 1..5, hoặc skill chưa có sẵn mà thiếu bước mở khóa. Không tự sửa asset.
    /// </summary>
    public void ValidateSkillProgression()
    {
        var innate = new System.Collections.Generic.HashSet<SkillDataSO>();
        foreach (SkillDataSO skill in skills)
        {
            ValidateCharacterSkill(skill);
            if (!innate.Add(skill))
                throw new System.InvalidOperationException($"{name}: {skill.name} khai báo trùng trong Skills.");
        }

        foreach (SkillProgressStep step in skillProgression)
        {
            ValidateCharacterSkill(step.Skill);
            ValidateStep(step);
            if (!innate.Contains(step.Skill) && !HasUnlockStep(step.Skill))
                throw new System.InvalidOperationException($"{name}: {step.Skill.name} chưa có trong Skills nên cần một bước Skill Level = 1 để mở khóa.");
        }
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

    private bool HasUnlockStep(SkillDataSO skill)
    {
        foreach (SkillProgressStep step in skillProgression)
        {
            if (step.Skill == skill && step.SkillLevel == 1)
                return true;
        }
        return false;
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
