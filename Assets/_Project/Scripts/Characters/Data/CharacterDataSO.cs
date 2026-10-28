using UnityEngine;
using UnityEngine.Serialization;

// * Template chỉ số và skill khởi đầu, dùng chung cấu trúc cho player, enemy và boss.
// ! Không lưu máu hiện tại, cooldown hoặc thay đổi trong trận trên asset; các trạng thái đó thuộc runtime riêng.
[CreateAssetMenu(fileName = "CharacterData", menuName = "Shooter Game/Data/Character")]
public sealed class CharacterDataSO : ScriptableObject
{
    #region Character Identity

    [FormerlySerializedAs("characterId")]
    [SerializeField] private string displayName;  // * Tên hiển thị trên giao diện, đổi tên không ảnh hưởng khóa enum hoặc save.
    [SerializeField] private CharacterType characterType;                    // * Khóa duy nhất để ghép config và lưu tiến trình; chọn một type khác None.
    [SerializeField] private CharacterTeam team; // * Phe của loại nhân vật; boss cũng thuộc Enemy.
    [SerializeField] private GameObject visualPrefab; // * Chỉ chứa hình ảnh/Animator, được gắn làm con của vỏ chung khi spawn.
    [SerializeField] private Vector2 colliderSize = new Vector2(0.16f, 0.16f); // * Kích thước hitbox theo loại, không cần tạo prefab gameplay riêng.
    [SerializeField] private Vector2 colliderOffset = new Vector2(0f, 0.08f); // * Độ lệch hitbox so với gốc vỏ để khớp pivot của sprite.

    public string DisplayName => displayName;  // * Nhãn hiển thị, không dùng làm khóa tra cứu.
    public CharacterType CharacterType => characterType;                       // * Khóa dùng chung cho database, level service và save.
    public CharacterTeam Team => team; // * Dùng phân biệt Player và Enemy khi tạo hành vi và xử lý tiếp xúc.
    public GameObject VisualPrefab => visualPrefab; // * Mẫu visual dùng chung theo loại; không lưu instance đang chơi lên SO.
    public Vector2 ColliderSize => colliderSize; // * Factory áp dụng lại mỗi lần dùng vỏ để không giữ hitbox của loại trước.
    public Vector2 ColliderOffset => colliderOffset; // * Offset được sao chép sang BoxCollider2D của vỏ.

    [Header("Enemy Growth")]
    [SerializeField, Min(0.01f)] private float growthInterval = 50f; // * Số giây game mỗi bậc tăng sức mạnh, tính từ lần đầu loại quái spawn thành công.
    [SerializeField] private StatBonus growthPerStep = new StatBonus { maxHealth = 5f }; // * Bonus mỗi bậc; chỉ áp dụng cho cá thể được spawn sau mốc đó.

    public float GrowthInterval => Mathf.Max(0.01f, growthInterval); // * Chặn chia cho 0 kể cả khi asset được sửa ngoài Inspector.
    public StatBonus GrowthPerStep => growthPerStep; // * Giá trị struct sao chép, không thay đổi SO khi tính bonus.

    #endregion

    #region Base Stats Template

    [SerializeField] private BaseStats baseStats = global::BaseStats.Default;  // * Chỉ số gốc; critChance nhập 1.5 tương ứng 150%.

    // * Trả về bản sao struct; sửa bản sao không làm thay đổi dữ liệu của asset.
    public BaseStats BaseStats => baseStats;

    #endregion

    #region Starting Skill Templates

    // Category is authored on each skill asset; cooldown and passive state belong to its runtime.
    [SerializeField] private SkillDataSO[] skills = System.Array.Empty<SkillDataSO>();
    public SkillDataSO[] Skills => skills;

    #endregion
}
