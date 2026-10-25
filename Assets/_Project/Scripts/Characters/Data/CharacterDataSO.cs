using System.Collections.Generic;
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

    public string DisplayName => displayName;  // * Nhãn hiển thị, không dùng làm khóa tra cứu.
    public CharacterType CharacterType => characterType;                       // * Khóa dùng chung cho database, level service và save.

    #endregion

    #region Base Stats Template

    [SerializeField] private BaseStats baseStats = global::BaseStats.Default;  // * Chỉ số gốc; critChance nhập 1.5 tương ứng 150%.

    // * Trả về bản sao struct; sửa bản sao không làm thay đổi dữ liệu của asset.
    public BaseStats BaseStats => baseStats;

    #endregion

    #region Starting Skill Templates

    // ! SerializeReference lưu class thuần có [System.Serializable] triển khai ISkill; không hỗ trợ MonoBehaviour hoặc ScriptableObject.
    // ! Inspector mặc định chưa có bộ chọn loại class; muốn chọn/thêm loại skill trong Inspector cần custom editor riêng.
    [SerializeReference] private List<ISkill> passiveSkills = new List<ISkill>();  // * Các skill bị động mẫu; mỗi phần tử là một đối tượng triển khai ISkill.
    [SerializeReference] private List<ISkill> activeSkills = new List<ISkill>();   // * Các skill chủ động mẫu; phân nhóm bằng danh sách chứa chúng.

    public IReadOnlyList<ISkill> PassiveSkills => passiveSkills;  // * Đọc các skill mẫu; runtime cần sao chép cả list và trạng thái skill bên trong.
    public IReadOnlyList<ISkill> ActiveSkills => activeSkills;    // * Không Activate/Deactivate skill mẫu trực tiếp trên asset khi chơi.

    #endregion
}
