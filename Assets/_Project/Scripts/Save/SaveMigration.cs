using System;
using System.Collections.Generic;
using System.IO;

/// <summary>
/// Chuyển hồ sơ cũ (schema 2/3, một key <c>player_data</c>) sang schema 4 tách theo section,
/// hoặc ghi các section mặc định cho hồ sơ mới. Chỉ lớp này biết bố cục save cũ.
/// </summary>
/// <remarks>Do <see cref="SaveManager"/> tạo và chạy ngay sau khi mở file; không bind vào Zenject.</remarks>
public sealed class SaveMigration
{
    #region Fields

    private readonly SaveStorage storage;

    #endregion

    #region Constructor

    public SaveMigration(SaveStorage storage)
    {
        this.storage = storage ?? throw new ArgumentNullException(nameof(storage));
    }

    #endregion

    #region Migration

    /// <summary>Chạy migration nếu cần. Lỗi thì khóa ghi (file cũ giữ nguyên để thử lại lần sau).</summary>
    public void Run()
    {
        if (!storage.IsLoaded) return;
        try
        {
            if (storage.HasKey(GameConstants.SaveKeys.Format))
            {
                storage.Load(GameConstants.SaveKeys.Format, new SaveFormatData(), ValidateFormat);
                return;
            }

            var selection = new PlayerSelectionSaveData();
            var levels = new CharacterLevelsSaveData();
            var equipment = new EquipmentSaveData();
            var currencies = new CurrencySaveData();
            if (storage.HasKey(GameConstants.SaveKeys.PlayerData))
            {
                PlayerSaveData legacy = storage.Load<PlayerSaveData>(GameConstants.SaveKeys.PlayerData, null, ValidateLegacy);
                selection.selectedCharacterType = legacy.selectedCharacterType;
                foreach (PlayerSaveData.CharacterProgress entry in legacy.characters)
                    levels.characters.Add(new CharacterLevelsSaveData.Entry { characterType = entry.characterType, permanentLevel = entry.permanentLevel });
                equipment.equippedItemIds = new List<string>(legacy.equippedItemIds ?? new List<string>());
                if (legacy.version == 3)
                {
                    currencies.currencies.Clear();
                    foreach (PlayerSaveData.CurrencyBalance entry in legacy.currencies)
                        currencies.currencies.Add(new CurrencySaveData.Balance { type = entry.type, balance = entry.balance });
                }
            }

            // Cùng quy tắc kiểm tra mà system sở hữu dữ liệu dùng khi đọc (Validate nằm trên chính lớp section).
            PlayerSelectionSaveData.Validate(selection);
            CharacterLevelsSaveData.Validate(levels);
            EquipmentSaveData.Validate(equipment);
            CurrencySaveData.Validate(currencies);

            Write(GameConstants.SaveKeys.PlayerSelection, selection);
            Write(GameConstants.SaveKeys.CharacterLevels, levels);
            Write(GameConstants.SaveKeys.Equipment, equipment);
            Write(GameConstants.SaveKeys.Currencies, currencies);
            // Marker ghi CUỐI: lần chuyển đổi lỗi có thể chạy lại từ dữ liệu cũ nguyên vẹn.
            Write(GameConstants.SaveKeys.Format, new SaveFormatData());
            // Giữ player_data cũ trong cùng file để phục hồi; khi đã có marker schema 4 thì không đọc lại.
        }
        catch (Exception exception) { storage.BlockLoading(exception); }
    }

    #endregion

    #region Private Helpers

    private void Write<T>(string key, T value)
    {
        if (!storage.Save(key, value))
            throw new IOException("Could not finish save migration for key: " + key, storage.LastError);
    }

    private static void ValidateFormat(SaveFormatData data)
    {
        if (data == null || data.version != SaveFormatData.CurrentVersion)
            throw new InvalidOperationException("Unsupported save format; expected version " + SaveFormatData.CurrentVersion + ".");
    }

    private static void ValidateLegacy(PlayerSaveData data)
    {
        if (data == null || (data.version != 2 && data.version != 3) || data.characters == null)
            throw new InvalidOperationException("Only legacy save versions 2 and 3 can migrate to the section format.");
        foreach (PlayerSaveData.CharacterProgress entry in data.characters)
            if (entry == null) throw new InvalidOperationException("Null legacy character progress.");
        if (data.version == 3)
        {
            if (data.currencies == null) throw new InvalidOperationException("Missing legacy currency balances.");
            foreach (PlayerSaveData.CurrencyBalance entry in data.currencies)
                if (entry == null) throw new InvalidOperationException("Null legacy currency balance.");
        }
    }

    #endregion
}
