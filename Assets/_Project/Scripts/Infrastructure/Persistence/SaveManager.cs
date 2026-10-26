using System;
using System.Collections.Generic;
using UnityEngine;
using Zenject;

// * Dịch vụ save class thuần; Zenject tạo và load trước PlayerManager, Easy Save 3 phụ trách đọc/ghi dữ liệu.
// ! Gọi trên main thread; level trận, cooldown, HP hiện tại và template SO không thuộc file save này.
public sealed class SaveManager : IInitializable, IDisposable
{
    #region Storage Configuration

    // * Cấu hình truyền từ installer; file dùng riêng cho thông tin người chơi.
    public sealed class Settings
    {
        public readonly string FileName;  // * Tên file tương đối trong persistentDataPath; không dùng đường dẫn asset.

        // * Chỉ nhận tên file để giữ dữ liệu người chơi trong thư mục save của ứng dụng.
        public Settings(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName) || System.IO.Path.GetFileName(fileName) != fileName)
                throw new ArgumentException("Provide a save file name without directories.", nameof(fileName));
            FileName = fileName;
        }
    }

    private readonly ES3Settings storage;  // * Chọn rõ File và PersistentDataPath, không phụ thuộc chế độ cache mặc định của plugin.

    #endregion

    #region Managed Save State

    private PlayerSaveData data;  // * Bản dữ liệu nội bộ duy nhất; thay đổi qua API để theo dõi dirty.
    private bool disposed;        // * Chặn sử dụng service sau khi scene container giải phóng.
    private Dictionary<CharacterType, PlayerSaveData.CharacterProgress> characterLookup = new Dictionary<CharacterType, PlayerSaveData.CharacterProgress>();  // * Cache tra cứu theo type, tham chiếu cùng bản ghi với danh sách save; không lưu riêng xuống file.

    public bool IsLoaded { get; private set; }      // * False nếu chưa load hoặc đọc file thất bại; khi đó không cho ghi đè file.
    public bool IsDirty { get; private set; }       // * Có thay đổi chưa được ghi thành công xuống bộ nhớ lưu trữ.
    public Exception LastError { get; private set; }  // * Lỗi đọc/ghi gần nhất để bên gọi hiển thị hoặc xử lý.

    // * Trả snapshot độc lập để UI hoặc hệ thống khác đọc; sửa snapshot không tác động dữ liệu save nội bộ.
    public PlayerSaveData GetSnapshot()
    {
        EnsureLoaded();
        return data.Clone();
    }

    #endregion

    #region Initialization & Loading

    // * Chuẩn bị cấu hình IO, chưa đọc/ghi file trong constructor.
    public SaveManager(Settings settings)
    {
        if (settings == null)
            throw new ArgumentNullException(nameof(settings));
        storage = new ES3Settings(settings.FileName, ES3.Location.File, ES3.Directory.PersistentDataPath);
    }

    // * Zenject gọi trước khi PlayerManager tạo nhân vật; Load tự ghi nhận lỗi và khóa thao tác ghi nếu đọc thất bại.
    public void Initialize()
    {
        Load();
    }

    // * Load hồ sơ hoặc tạo dữ liệu mặc định trong RAM nếu chưa có key; không tự ghi file mới.
    // ! Không reload khi còn thay đổi chưa lưu; lỗi load không tự reset hoặc ghi đè dữ liệu cũ.
    public bool Load()
    {
        EnsureNotDisposed();
        if (IsDirty)
            throw new InvalidOperationException("Save pending changes before reloading player data.");

        try
        {
            PlayerSaveData loaded = ES3.KeyExists(GameConstants.SaveKeys.PlayerData, storage)
                ? ES3.Load<PlayerSaveData>(GameConstants.SaveKeys.PlayerData, storage)
                : new PlayerSaveData();
            // * Save schema 2 trước khi có trang bị vẫn đọc được với loadout rỗng.
            if (loaded != null && loaded.equippedItemIds == null)
                loaded.equippedItemIds = new List<string>();
            Validate(loaded);
            Dictionary<CharacterType, PlayerSaveData.CharacterProgress> loadedLookup = BuildCharacterLookup(loaded);
            data = loaded;
            characterLookup = loadedLookup;
            IsLoaded = true;
            LastError = null;
            return true;
        }
        catch (Exception exception)
        {
            data = null;
            characterLookup.Clear();
            IsLoaded = false;
            LastError = exception;
            Debug.LogError($"Player save load failed; writing is blocked: {exception.Message}");
            return false;
        }
    }

    #endregion

    #region Persistent Character Progress

    // * Nhân vật chưa có bản ghi được xem là level 1; thao tác đọc không tạo bản ghi hoặc ghi file.
    public int GetPermanentLevel(CharacterType characterType)
    {
        EnsureLoaded();
        ValidateCharacterType(characterType);
        PlayerSaveData.CharacterProgress progress = FindCharacter(characterType);
        return progress != null ? progress.permanentLevel : 1;
    }

    // * Lưu level mới vào RAM; bên gọi kiểm tra điều kiện lên cấp và MaxLevel theo config trước khi gọi.
    // ! Không lưu bonus tính sẵn, EXP vĩnh viễn hoặc EXP trong trận. Gọi Save sau thay đổi quan trọng.
    public void SetPermanentLevel(CharacterType characterType, int level)
    {
        EnsureLoaded();
        ValidateCharacterType(characterType);
        if (level < 1)
            throw new ArgumentOutOfRangeException(nameof(level));

        PlayerSaveData.CharacterProgress progress = FindCharacter(characterType);
        if (progress == null)
        {
            progress = new PlayerSaveData.CharacterProgress { characterType = characterType, permanentLevel = level };
            data.characters.Add(progress);
            characterLookup.Add(characterType, progress);
            IsDirty = true;
        }
        else if (progress.permanentLevel != level)
        {
            progress.permanentLevel = level;
            IsDirty = true;
        }
    }

    // * Ghi nhận nhân vật đang chọn; chỉ lưu Type, không quyết định điều kiện mở khóa nhân vật.
    public void SelectCharacter(CharacterType characterType)
    {
        EnsureLoaded();
        ValidateCharacterType(characterType);
        if (data.selectedCharacterType == characterType)
            return;
        data.selectedCharacterType = characterType;
        IsDirty = true;
    }

    #endregion

    #region Equipped Items

    // * Trả bản sao; không cho sửa loadout ngầm mà bỏ qua dirty và thông báo của service.
    public string[] GetEquippedItemIds()
    {
        EnsureLoaded();
        return data.equippedItemIds.ToArray();
    }

    internal bool SetEquippedItemIds(IReadOnlyList<string> itemIds)
    {
        EnsureLoaded();
        if (itemIds == null)
            throw new ArgumentNullException(nameof(itemIds));
        var copy = new List<string>(itemIds.Count);
        var unique = new HashSet<string>(StringComparer.Ordinal);
        foreach (string id in itemIds)
        {
            if (string.IsNullOrWhiteSpace(id) || !unique.Add(id))
                throw new ArgumentException("Equipment IDs must be non-empty and unique.", nameof(itemIds));
            copy.Add(id);
        }
        if (copy.Count == data.equippedItemIds.Count && unique.SetEquals(data.equippedItemIds))
            return false;
        data.equippedItemIds = copy;
        IsDirty = true;
        return true;
    }

    #endregion

    #region Writing & Service Disposal

    // * Ghi toàn bộ hồ sơ người chơi bằng Easy Save; trả false nếu IO thất bại và giữ dirty để có thể thử lại.
    public bool Save()
    {
        EnsureLoaded();
        try
        {
            Validate(data);
            ES3.Save(GameConstants.SaveKeys.PlayerData, data, storage);
            IsDirty = false;
            LastError = null;
            return true;
        }
        catch (Exception exception)
        {
            IsDirty = true;
            LastError = exception;
            Debug.LogError($"Player save write failed: {exception.Message}");
            return false;
        }
    }

    public bool SaveIfDirty()
    {
        // Application callbacks may arrive while the scene container is being disposed.
        return !disposed && IsLoaded && (!IsDirty || Save());
    }

    // Explicit reset also works when loading failed. Delete only the configured player file.
    // Commit RAM after deletion succeeds so Dispose cannot write the previous profile back.
    public bool ResetData()
    {
        EnsureNotDisposed();
        var defaults = new PlayerSaveData();
        try
        {
            ES3.DeleteFile(storage);
        }
        catch (Exception exception)
        {
            LastError = exception;
            Debug.LogError($"Player save reset failed: {exception.Message}");
            return false;
        }

        data = defaults;
        characterLookup.Clear();
        IsLoaded = true;
        IsDirty = false;
        LastError = null;
        return true;
    }

    // * Lưu thay đổi còn lại khi Zenject hủy scene; không thay thế việc Save ngay tại các mốc tiến trình quan trọng.
    public void Dispose()
    {
        if (disposed)
            return;
        if (IsLoaded && IsDirty)
            Save();
        characterLookup.Clear();
        disposed = true;
    }

    #endregion

    #region Lookup & Data Validation

    // * Tra theo enum, thời gian trung bình O(1); không duyệt toàn bộ danh sách nhân vật.
    private PlayerSaveData.CharacterProgress FindCharacter(CharacterType characterType)
    {
        characterLookup.TryGetValue(characterType, out PlayerSaveData.CharacterProgress progress);
        return progress;
    }

    // * Dựng cache mới sau khi dữ liệu đã được kiểm tra; chỉ thay cache đang dùng khi toàn bộ lần load thành công.
    private static Dictionary<CharacterType, PlayerSaveData.CharacterProgress> BuildCharacterLookup(PlayerSaveData source)
    {
        var lookup = new Dictionary<CharacterType, PlayerSaveData.CharacterProgress>(source.characters.Count);
        foreach (PlayerSaveData.CharacterProgress progress in source.characters)
            lookup.Add(progress.characterType, progress);
        return lookup;
    }

    // * Kiểm tra schema và tiến trình trước khi sử dụng; chưa có migration nên không đoán cách đọc phiên bản khác.
    private static void Validate(PlayerSaveData candidate)
    {
        if (candidate == null || candidate.characters == null)
            throw new InvalidOperationException("Invalid player save data.");
        if (candidate.version != PlayerSaveData.CurrentVersion)
            throw new InvalidOperationException($"Player save version {candidate.version} requires migration to version {PlayerSaveData.CurrentVersion}; the file will not be overwritten.");
        if (candidate.equippedItemIds == null)
            throw new InvalidOperationException("Missing equipped item list in player save.");
        var equipmentIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (string id in candidate.equippedItemIds)
            if (string.IsNullOrWhiteSpace(id) || !equipmentIds.Add(id))
                throw new InvalidOperationException("Equipped item IDs must be non-empty and unique.");
        if (!Enum.IsDefined(typeof(CharacterType), candidate.selectedCharacterType))
            throw new InvalidOperationException("Selected character type is not defined.");

        var types = new HashSet<CharacterType>();
        foreach (PlayerSaveData.CharacterProgress progress in candidate.characters)
        {
            if (progress == null || progress.characterType == CharacterType.None || !Enum.IsDefined(typeof(CharacterType), progress.characterType) || progress.permanentLevel < 1)
                throw new InvalidOperationException("Invalid character progress in player save.");
            if (!types.Add(progress.characterType))
                throw new InvalidOperationException("Duplicate character type in player save.");
        }
    }

    // * Yêu cầu type đã khai báo trong enum và khác None khi thao tác tiến trình.
    private static void ValidateCharacterType(CharacterType characterType)
    {
        if (characterType == CharacterType.None || !Enum.IsDefined(typeof(CharacterType), characterType))
            throw new ArgumentOutOfRangeException(nameof(characterType), "Choose a defined character type other than None.");
    }

    // * Không cho dùng dữ liệu mặc định để vô tình ghi đè file đang lỗi hoặc chưa được load.
    private void EnsureLoaded()
    {
        EnsureNotDisposed();
        if (!IsLoaded)
            throw new InvalidOperationException("Player save has not loaded successfully.");
    }

    // * Bảo vệ vòng đời service do Zenject quản lý.
    private void EnsureNotDisposed()
    {
        if (disposed)
            throw new ObjectDisposedException(nameof(SaveManager));
    }

    #endregion
}
