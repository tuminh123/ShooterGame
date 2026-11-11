using System;
using System.Collections.Generic;

// DTO legacy schema 2/3, giữ để đọc save cũ trong SaveMigration. Dữ liệu mới dùng SaveSections.
[Serializable]
public sealed class PlayerSaveData
{
    #region Persistent Player Fields

    public const int CurrentVersion = 3;  // Phiên bản legacy; schema hiện tại ở SaveFormatData.CurrentVersion.

    public int version = CurrentVersion;                                  // * Nhận diện phiên bản dữ liệu, không phải level người chơi.
    public CharacterType selectedCharacterType = CharacterType.None;                      // * Type nhân vật đang chọn; None khi chưa chọn nhân vật.
    public List<CharacterProgress> characters = new List<CharacterProgress>();  // * Tiến trình vĩnh viễn riêng của từng nhân vật, không phải danh mục mọi nhân vật trong game.
    // * Field bổ sung của schema 2; save cũ thiếu field được xem là chưa mặc trang bị.
    public List<string> equippedItemIds = new List<string>();
    public List<CurrencyBalance> currencies = CreateCurrencyBalances();

    #endregion

    #region Persistent Currency Balances

    [Serializable]
    public sealed class CurrencyBalance
    {
        public CurrencyType type;
        public long balance;
    }

    internal static List<CurrencyBalance> CreateCurrencyBalances()
    {
        var balances = new List<CurrencyBalance>();
        foreach (CurrencyType type in Enum.GetValues(typeof(CurrencyType)))
            balances.Add(new CurrencyBalance { type = type, balance = 0 });
        return balances;
    }

    #endregion

    #region Per-Character Progress

    // * Một bản ghi tiến trình của nhân vật; Type đối chiếu với CharacterDataSO.CharacterType.
    [Serializable]
    public sealed class CharacterProgress
    {
        public CharacterType characterType;       // * Type cố định, độc lập với tên hiển thị và tên asset.
        public int permanentLevel = 1;   // * Chỉ lưu level đã đạt; bonus được tính lại từ config khi vào game.
    }

    #endregion

    #region Independent Snapshot

    // * Sao chép cả danh sách và bản ghi để người đọc không sửa ngầm dữ liệu đang do SaveManager quản lý.
    public PlayerSaveData Clone()
    {
        var clone = new PlayerSaveData
        {
            version = version,
            selectedCharacterType = selectedCharacterType,
            equippedItemIds = new List<string>(equippedItemIds),
            currencies = new List<CurrencyBalance>(currencies.Count),
            characters = new List<CharacterProgress>(characters.Count)
        };
        foreach (CurrencyBalance currency in currencies)
            clone.currencies.Add(new CurrencyBalance { type = currency.type, balance = currency.balance });
        foreach (CharacterProgress character in characters)
        {
            clone.characters.Add(new CharacterProgress
            {
                characterType = character.characterType,
                permanentLevel = character.permanentLevel
            });
        }
        return clone;
    }

    #endregion
}
