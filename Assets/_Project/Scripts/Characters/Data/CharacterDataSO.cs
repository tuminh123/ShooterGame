using UnityEngine;
using UnityEngine.Serialization;

// * Template chỉ số, hình ảnh và danh sách skill vốn có, dùng chung cấu trúc cho player, enemy và boss.
// ! Không lưu máu hiện tại, cooldown hoặc thay đổi trong trận trên asset; các trạng thái đó thuộc runtime riêng.
[CreateAssetMenu(fileName = "CharacterData", menuName = "Shooter Game/Data/Character")]
public sealed class CharacterDataSO : ScriptableObject
{
    #region Character Identity

    [FormerlySerializedAs("characterId")]
    [SerializeField] private string displayName;  // * Tên hiển thị trên giao diện, đổi tên không ảnh hưởng khóa enum hoặc save.
    [SerializeField] private CharacterType characterType;                    // * Khóa duy nhất để ghép config và lưu tiến trình; chọn một type khác None.
    [SerializeField] private CharacterTeam team; // * Phe của loại nhân vật; boss cũng thuộc Enemy.
    [SerializeField] private bool isBoss; // Đánh dấu boss để hiện HP/giáp; không thay đổi phe hoặc cách nhận sát thương.
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

    [SerializeField] private BaseStats baseStats = BaseStats.Default; // Percent stats: 100 = 100%; crit damage is additional percent.

    // * Trả về bản sao struct; sửa bản sao không làm thay đổi dữ liệu của asset.
    public BaseStats BaseStats => baseStats;

    public void ValidateStats() => baseStats.Validate();

    #endregion

    [Header("Skills")]
    [SerializeField] private SkillDataSO[] skills = System.Array.Empty<SkillDataSO>(); // * Danh sách cấu hình skill vốn có; không lưu cooldown hoặc trạng thái chạy trên asset.
    public System.Collections.Generic.IReadOnlyList<SkillDataSO> Skills => skills; // * Bridge đọc skill của từng nhân vật từ cấu hình này.
}
