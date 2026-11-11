using System;
using System.Collections.Generic;
using System.IO;
using Zenject;

// Chỉ lớp migration biết bố cục save cũ và các section mới; SaveManager chỉ biết đọc/ghi theo key.
public sealed class SaveMigration : IInitializable
{
    private readonly SaveManager saves;
    public SaveMigration(SaveManager saves) => this.saves = saves;

    public void Initialize()
    {
        if (!saves.IsLoaded) return;
        try
        {
            if (saves.HasKey(GameConstants.SaveKeys.Format))
            {
                saves.Load(GameConstants.SaveKeys.Format, new SaveFormatData(), ValidateFormat);
                return;
            }

            var selection = new PlayerSelectionSaveData();
            var levels = new CharacterLevelsSaveData();
            var equipment = new EquipmentSaveData();
            var currencies = new CurrencySaveData();
            if (saves.HasKey(GameConstants.SaveKeys.PlayerData))
            {
                PlayerSaveData legacy = saves.Load<PlayerSaveData>(GameConstants.SaveKeys.PlayerData, null, ValidateLegacy);
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

            PlayerManager.ValidateSelectionData(selection);
            CharacterLevelService.ValidateData(levels);
            PlayerEquipmentService.ValidateData(equipment);
            CurrencyService.ValidateData(currencies);

            Write(GameConstants.SaveKeys.PlayerSelection, selection);
            Write(GameConstants.SaveKeys.CharacterLevels, levels);
            Write(GameConstants.SaveKeys.Equipment, equipment);
            Write(GameConstants.SaveKeys.Currencies, currencies);
            // Commit marker ghi CUỐI: lần chuyển đổi lỗi có thể chạy lại từ legacy nguyên vẹn.
            Write(GameConstants.SaveKeys.Format, new SaveFormatData());
            // Giữ player_data cũ trong cùng file để phục hồi; khi có marker schema 4 thì không đọc lại.
        }
        catch (Exception exception) { saves.BlockLoading(exception); }
    }

    private void Write<T>(string key, T value)
    {
        if (!saves.Save(key, value))
            throw new IOException("Could not finish save migration for key: " + key, saves.LastError);
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
}
