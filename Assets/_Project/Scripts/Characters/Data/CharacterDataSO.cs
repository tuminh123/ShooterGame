using UnityEngine;
using UnityEngine.Serialization;

// * Template chỉ số, hình ảnh và danh sách skill vốn có, dùng chung cấu trúc cho player, enemy và boss.
// ! Không lưu máu hiện tại, cooldown hoặc thay đổi trong trận trên asset; các trạng thái đó thuộc runtime riêng.
[CreateAssetMenu(fileName = "CharacterData", menuName = "Shooter Game/Data/Character")]
public sealed class CharacterDataSO : ScriptableObject
{
    #region Character Identity

    [FormerlySerializedAs("characterId")]
    [Tooltip("Tên hiển thị trên UI; có thể đổi mà không thay khóa nhân vật trong save.")]
    [SerializeField] private string displayName;  // * Tên hiển thị trên giao diện, đổi tên không ảnh hưởng khóa enum hoặc save.
    [Tooltip("Khóa ghép Character Database, Level Config và save. Chọn khác None; không khai báo trùng trong database.")]
    [SerializeField] private CharacterType characterType;                    // * Khóa duy nhất để ghép config và lưu tiến trình; chọn một type khác None.
    [Tooltip("Phe dùng để chọn mục tiêu và xử lý sát thương. Boss cũng phải chọn Enemy.")]
    [SerializeField] private CharacterTeam team; // * Phe của loại nhân vật; boss cũng thuộc Enemy.
    [Tooltip("Bật cho template Boss để dùng luồng boss và animation tương ứng; Enemy thường và Elite để tắt.")]
    [SerializeField] private bool isBoss; // Đánh dấu boss để hiện HP/giáp; không thay đổi phe hoặc cách nhận sát thương.
    [Tooltip("Prefab ngoại hình, kích thước/offset hình ảnh, hitbox và bố cục thanh máu. Các phần được chỉnh độc lập.")]
    [SerializeField] private CharacterVisualSettings visualSettings = CharacterVisualSettings.Default;

    #region  Get
    
    public string DisplayName => displayName;  // * Nhãn hiển thị, không dùng làm khóa tra cứu.
    public CharacterType CharacterType => characterType;                       // * Khóa dùng chung cho database, level service và save.
    public CharacterTeam Team => team; // * Dùng phân biệt Player và Enemy khi tạo hành vi và xử lý tiếp xúc.
    public bool IsBoss => isBoss;
    public CharacterVisualSettings VisualSettings => visualSettings;
    public GameObject VisualPrefab => visualSettings.Prefab;
    public HitboxData Hitbox => visualSettings.Hitbox;
    
    #endregion

    #endregion

    #region Base Stats Template

    [Tooltip("Chỉ số gốc ở cấp 1, trước bonus cấp và trang bị. Các tỷ lệ dùng thang 100 = 100%.")]
    [SerializeField] private BaseStats baseStats = BaseStats.Default; // Percent stats: 100 = 100%; crit damage is additional percent.

    // * Trả về bản sao struct; sửa bản sao không làm thay đổi dữ liệu của asset.
    public BaseStats BaseStats => baseStats;

    public void ValidateStats() => baseStats.Validate();

    #endregion

    [Tooltip("Skill vốn có của nhân vật, dùng asset nguồn Character/Equipment. Cấp ban đầu đọc từ từng SkillDataSO; không lưu cooldown ở đây.")]
    [SerializeField] private SkillDataSO[] skills = System.Array.Empty<SkillDataSO>(); // * Danh sách cấu hình skill vốn có; không lưu cooldown hoặc trạng thái chạy trên asset.
    public System.Collections.Generic.IReadOnlyList<SkillDataSO> Skills => skills; // * Bridge đọc skill của từng nhân vật từ cấu hình này.
}
