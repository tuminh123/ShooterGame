using System;
using System.Collections.Generic;

/// <summary>DTO legacy schema 2/3, giữ để đọc save cũ trong SaveMigration. Dữ liệu mới dùng SaveSections.</summary>
[Serializable]
public sealed class PlayerSaveData
{
    #region Persistent Player Fields

    /// <summary>Phiên bản legacy; schema hiện tại ở SaveFormatData.CurrentVersion.</summary>
    public const int CurrentVersion = 3;

    /// <summary>Nhận diện phiên bản dữ liệu, không phải level người chơi.</summary>
    public int version = CurrentVersion;
    /// <summary>Type nhân vật đang chọn; None khi chưa chọn nhân vật.</summary>
    public CharacterType selectedCharacterType = CharacterType.None;
    /// <summary>Tiến trình vĩnh viễn riêng của từng nhân vật, không phải danh mục mọi nhân vật trong game.</summary>
    public List<CharacterProgress> characters = new List<CharacterProgress>();
    /// <summary>Field bổ sung của schema 2; save cũ thiếu field được xem là chưa mặc trang bị.</summary>
    public List<string> equippedItemIds = new List<string>();
    /// <summary>Số dư từng loại tiền; khởi tạo đủ mọi CurrencyType với số dư 0.</summary>
    public List<CurrencyBalance> currencies = CreateCurrencyBalances();

    #endregion

    #region Persistent Currency Balances

    /// <summary>Số dư của một loại tiền trong save legacy.</summary>
    [Serializable]
    public sealed class CurrencyBalance
    {
        /// <summary>Loại tiền.</summary>
        public CurrencyType type;
        /// <summary>Số dư; dùng long để không tràn số khi tích lũy lâu.</summary>
        public long balance;
    }

    /// <summary>Tạo danh sách số dư 0 cho mọi CurrencyType, để save mới không thiếu loại tiền nào.</summary>
    internal static List<CurrencyBalance> CreateCurrencyBalances()
    {
        var balances = new List<CurrencyBalance>();
        foreach (CurrencyType type in Enum.GetValues(typeof(CurrencyType)))
            balances.Add(new CurrencyBalance { type = type, balance = 0 });
        return balances;
    }

    #endregion

    #region Per-Character Progress

    /// <summary>Một bản ghi tiến trình của nhân vật; Type đối chiếu với CharacterDataSO.CharacterType.</summary>
    [Serializable]
    public sealed class CharacterProgress
    {
        /// <summary>Type cố định, độc lập với tên hiển thị và tên asset.</summary>
        public CharacterType characterType;
        /// <summary>Chỉ lưu level đã đạt; bonus được tính lại từ config khi vào game.</summary>
        public int permanentLevel = 1;
    }

    #endregion

    #region Independent Snapshot

    /// <summary>Sao chép cả danh sách và bản ghi để người đọc không sửa ngầm dữ liệu đang do SaveManager quản lý.</summary>
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
