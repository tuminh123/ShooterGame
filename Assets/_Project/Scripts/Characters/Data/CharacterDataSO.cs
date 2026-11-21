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

    [Tooltip("Skill vốn có của nhân vật, dùng asset nguồn Character/Equipment. Cấp ban đầu đọc từ từng SkillDataSO; không lưu cooldown ở đây.")]
    [SerializeField] private SkillDataSO[] skills = System.Array.Empty<SkillDataSO>();
    public System.Collections.Generic.IReadOnlyList<SkillDataSO> Skills => skills; // Bridge đọc skill của từng nhân vật từ cấu hình này.
}
