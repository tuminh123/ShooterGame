using System;

// * Đầu mối gameplay cho cả level nhân vật vĩnh viễn và level trong trận; không giữ thêm bản sao tiến trình.
public sealed class LevelManager
{
    #region Level Services

    private readonly CharacterLevelService characterLevels;  // * Tính bonus, nâng cấp vĩnh viễn và gọi SaveManager.
    private readonly RunLevelService runLevels;               // * Nhận EXP, tăng cấp và reset tiến trình trong trận.

    // * Zenject inject hai service class thuần đã được cấu hình từ database.
    public LevelManager(CharacterLevelService characterLevels, RunLevelService runLevels)
    {
        this.characterLevels = characterLevels ?? throw new ArgumentNullException(nameof(characterLevels));
        this.runLevels = runLevels ?? throw new ArgumentNullException(nameof(runLevels));
    }

    #endregion

    #region Gameplay Notifications

    // * Chuyển tiếp Type, level cũ, level mới của nhân vật; bên nghe đăng ký qua manager.
    public event Action<CharacterType, int, int> CharacterLevelChanged
    {
        add => characterLevels.LevelChanged += value;
        remove => characterLevels.LevelChanged -= value;
    }

    // * Báo level đã tăng trong RAM nhưng chưa ghi file được; không gọi tăng cấp lại khi nhận thông báo này.
    public event Action<CharacterType, int, Exception> CharacterLevelSaveFailed
    {
        add => characterLevels.SaveFailed += value;
        remove => characterLevels.SaveFailed -= value;
    }

    // * Chuyển tiếp level cũ/mới của trận; chênh lệch là số lượt thưởng khi lên nhiều cấp cùng lúc.
    public event Action<int, int> RunLevelChanged
    {
        add => runLevels.LevelChanged += value;
        remove => runLevels.LevelChanged -= value;
    }

    // * Chuyển tiếp EXP dư và yêu cầu cấp tiếp để UI cập nhật thanh EXP.
    public event Action<int, int> RunExperienceChanged
    {
        add => runLevels.ExperienceChanged += value;
        remove => runLevels.ExperienceChanged -= value;
    }

    // * Báo tiến trình trận được reset về level 1 và EXP 0.
    public event Action RunReset
    {
        add => runLevels.RunReset += value;
        remove => runLevels.RunReset -= value;
    }

    #endregion

    #region Permanent Character Level API

    // * Tra template bằng type; tên hiển thị không tham gia việc đối chiếu save.
    public CharacterDataSO GetCharacter(CharacterType characterType) => characterLevels.GetCharacter(characterType);

    // * Đọc level vĩnh viễn hiện tại của nhân vật từ hồ sơ đã load.
    public int GetPermanentLevel(CharacterType characterType) => characterLevels.GetLevel(characterType);

    // * Đọc config tăng trưởng riêng để hiển thị trần cấp hoặc bonus mỗi cấp.
    public PermanentLevelConfigSO GetPermanentConfig(CharacterType characterType) => characterLevels.GetConfig(characterType);

    // * Tính bonus vĩnh viễn, chưa bao gồm level trong trận hoặc trang bị.
    public StatBonus GetPermanentBonus(CharacterType characterType) => characterLevels.GetTotalBonus(characterType);

    // * Tính chi phí lần nâng tiếp theo, false khi đã đạt trần.
    public bool TryGetUpgradeCost(CharacterType characterType, out long cost) => characterLevels.TryGetUpgradeCost(characterType, out cost);

    // * Sau khi bên gọi đã kiểm tra điều kiện/chi phí, tăng một cấp và thử save; saved phản ánh kết quả IO riêng.
    public bool TryIncreasePermanentLevel(CharacterType characterType, out bool saved) => characterLevels.TryIncreaseLevel(characterType, out saved);

    // * Thử ghi lại hồ sơ sau lỗi IO, không tăng thêm level.
    public bool SaveCharacterProgress() => characterLevels.SaveProgress();

    #endregion

    #region Run Level API

    public int RunLevel => runLevels.Level;                       // * Level hiện tại của người chơi trong lượt này.
    public int RunExperience => runLevels.Experience;             // * EXP còn dư ở cấp hiện tại.
    public int RunExperienceToNextLevel => runLevels.ExperienceToNextLevel;  // * EXP yêu cầu cho lần lên cấp, 0 khi đạt trần.
    public bool IsRunMaxLevel => runLevels.IsMaxLevel;            // * Lượt chơi đã đạt cấp tối đa.
    public StatBonus RunBonus => runLevels.TotalBonus;            // * Tổng bonus cố định từ level trong trận.

    // * Bắt đầu lượt mới; bên điều phối trận gọi sau khi đã đăng ký các sự kiện.
    public void StartNewRun() => runLevels.StartNewRun();

    // * Gọi khi người chơi nhặt EXP hoặc nhận thưởng; trả số cấp tăng và tự phát sự kiện qua service.
    public int AddRunExperience(int amount) => runLevels.AddExperience(amount);

    #endregion
}
