using System;

// * Đầu mối gameplay cho cả level nhân vật vĩnh viễn và level trong trận; không giữ thêm bản sao tiến trình.
public sealed class LevelManager :
    ILevelService,
    ICharacterLevelCatalog,
    IPermanentLevelReader,
    IPermanentLevelCommands,
    IRunProgress,
    IRunProgressCommands
{
    #region Level Services
    private readonly ICharacterCatalog characterCatalog;  // * Tính bonus, nâng cấp vĩnh viễn và gọi SaveManager.
    private readonly ICharacterLevelReader characterLevelReader;
    private readonly ICharacterLevelCommands characterLevelCommands;
    private readonly IRunLevelReader runLevelReader;               // * Nhận EXP, tăng cấp và reset tiến trình trong trận.
    private readonly IRunLevelCommands runLevelCommands;

    // * Zenject inject hai service class thuần đã được cấu hình từ database.
    public LevelManager(ICharacterLevelService characterLevelService, IRunLevelService runLevelService)
    {
        this.characterCatalog = characterLevelService.GetCharacterCatalog() ?? throw new ArgumentNullException(nameof(characterLevelService));
        this.characterLevelReader = characterLevelService.GetCharacterLevelReader() ?? throw new ArgumentNullException(nameof(characterLevelService));
        this.characterLevelCommands = characterLevelService.GetCharacterLevelCommands() ?? throw new ArgumentNullException(nameof(characterLevelService));
        this.runLevelReader = runLevelService.GetRunLevelReader() ?? throw new ArgumentNullException(nameof(runLevelService));
        this.runLevelCommands = runLevelService.GetRunLevelCommands() ?? throw new ArgumentNullException(nameof(runLevelService));
    }

    #endregion

    #region Gameplay Notifications

    // * Chuyển tiếp Type, level cũ, level mới của nhân vật; bên nghe đăng ký qua manager.
    public event Action<CharacterType, int, int> CharacterLevelChanged
    {
        add => characterLevelReader.LevelChanged += value;
        remove => characterLevelReader.LevelChanged -= value;
    }

    // * Báo level đã tăng trong RAM nhưng chưa ghi file được; không gọi tăng cấp lại khi nhận thông báo này.
    public event Action<CharacterType, int, Exception> CharacterLevelSaveFailed
    {
        add => characterLevelReader.SaveFailed += value;
        remove => characterLevelReader.SaveFailed -= value;
    }

    // * Chuyển tiếp level cũ/mới của trận; chênh lệch là số lượt thưởng khi lên nhiều cấp cùng lúc.
    public event Action<int, int> RunLevelChanged
    {
        add => runLevelReader.LevelChanged += value;
        remove => runLevelReader.LevelChanged -= value;
    }

    // * Chuyển tiếp EXP dư và yêu cầu cấp tiếp để UI cập nhật thanh EXP.
    public event Action<int, int> RunExperienceChanged
    {
        add => runLevelReader.ExperienceChanged += value;
        remove => runLevelReader.ExperienceChanged -= value;
    }

    // * Báo tiến trình trận được reset về level 1 và EXP 0.
    public event Action RunReset
    {
        add => runLevelReader.RunReset += value;
        remove => runLevelReader.RunReset -= value;
    }

    #endregion

    #region Permanent Character Level API

    // * Tra template bằng type; tên hiển thị không tham gia việc đối chiếu save.
    public CharacterDataSO GetCharacter(CharacterType characterType) => characterCatalog.GetCharacter(characterType);

    // * Đọc level vĩnh viễn hiện tại của nhân vật từ hồ sơ đã load.
    public int GetPermanentLevel(CharacterType characterType) => characterLevelReader.GetLevel(characterType);

    // * Đọc config tăng trưởng riêng để hiển thị trần cấp hoặc bonus mỗi cấp.
    public CharacterLevelConfigSO GetPermanentConfig(CharacterType characterType) => characterCatalog.GetConfig(characterType);

    // * Tính bonus vĩnh viễn, chưa bao gồm level trong trận hoặc trang bị.
    public StatBonus GetPermanentBonus(CharacterType characterType) => characterLevelReader.GetTotalBonus(characterType);

    // * Tính chi phí lần nâng tiếp theo, false khi đã đạt trần.
    public bool TryGetUpgradeCost(CharacterType characterType, out long cost) => characterLevelReader.TryGetUpgradeCost(characterType, out cost);

    // * Sau khi bên gọi đã kiểm tra điều kiện/chi phí, tăng một cấp và thử save; saved phản ánh kết quả IO riêng.
    public bool TryIncreasePermanentLevel(CharacterType characterType, out bool saved) => characterLevelCommands.TryIncreaseLevel(characterType, out saved);

    // * Thử ghi lại hồ sơ sau lỗi IO, không tăng thêm level.
    public bool SaveCharacterProgress() => characterLevelCommands.SaveProgress();

    #endregion

    #region Run Level API

    public int RunLevel => runLevelReader.Level;                       // * Level hiện tại của người chơi trong lượt này.
    public int RunExperience => runLevelReader.Experience;             // * EXP còn dư ở cấp hiện tại.
    public int RunExperienceToNextLevel => runLevelReader.ExperienceToNextLevel;  // * EXP yêu cầu cho lần lên cấp, 0 khi đạt trần.
    public bool IsRunMaxLevel => runLevelReader.IsMaxLevel;            // * Lượt chơi đã đạt cấp tối đa.
    public StatBonus RunBonus => runLevelReader.TotalBonus;            // * Tổng bonus cố định từ level trong trận.

    // * Bắt đầu lượt mới; bên điều phối trận gọi sau khi đã đăng ký các sự kiện.
    public void StartNewRun() => runLevelCommands.StartNewRun();

    // * Gọi khi người chơi nhặt EXP hoặc nhận thưởng; trả số cấp tăng và tự phát sự kiện qua service.
    public int AddRunExperience(int amount) => runLevelCommands.AddExperience(amount);

    #endregion

    #region Facade Access

    // Các getter trả về vai trò của cùng instance; không tạo thêm service.
    public ICharacterLevelCatalog GetCharacterLevelCatalog() => this;

    public IPermanentLevelReader GetPermanentLevelReader() => this;

    public IPermanentLevelCommands GetPermanentLevelCommands() => this;

    public IRunProgress GetRunProgress() => this;

    public IRunProgressCommands GetRunProgressCommands() => this;

    #endregion
}
