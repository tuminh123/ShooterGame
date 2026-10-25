// * Loại nhân vật cụ thể làm khóa duy nhất cho template, level config và save; không phải nhóm nghề Warrior/Mage.
// ! Thêm nhân vật mới bằng giá trị số mới, không đổi số đã dùng trong asset. Không dùng tên hiển thị làm khóa save.
public enum CharacterType
{
    #region Character Types

    None = 0,               // * Chưa chọn nhân vật; không dùng làm type cho template hoặc bản ghi tiến trình.
    BlessedGladiator = 1     // * Type riêng của nhân vật Blessed Gladiator.

    #endregion
}
