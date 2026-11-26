using System;
using System.Collections.Generic;
using UnityEngine;
using Zenject;

/// <summary>
/// Manager của miền Mission: nhận tiến độ từ tracker, reset theo kỳ, trao thưởng và lưu.
/// Đây là class <b>duy nhất</b> của Mission được bind vào Zenject.
/// </summary>
/// <remarks>
/// <code>
/// MissionManager
/// ├── IMissionTracker[]    nghe event gameplay → báo tiến độ (MissionTrackers.cs)
/// └── MissionBoard[]       mỗi database một nhóm: kỳ hiện tại + tiến độ từng mission (key missions)
/// </code>
/// Giờ của mỗi nhóm lấy qua <see cref="IGameClock"/> theo <see cref="MissionDatabaseSO.Clock"/> (giờ máy hoặc giờ server);
/// nguồn chưa có giờ thì nhóm bị đóng. Miền này không tự đọc DateTime.UtcNow.
/// Chiều phụ thuộc: Mission → Game / Battle / Character / Progression / Clock / Save. Không miền gameplay nào biết Mission.
/// Không ghi file mỗi lần giết quái: tiến độ trong trận nằm ở RAM, ghi khi hết trận, về Home, nâng cấp, nhận thưởng hoặc app ẩn/thoát.
/// </remarks>
public sealed class MissionManager : DomainManager, IMissions, IInitializable, IDisposable
{
    #region Settings

    /// <summary>Các database mission, installer gán từ Inspector.</summary>
    [Serializable]
    public sealed class Settings
    {
        [Tooltip("Daily, Weekly và từng event; thứ tự này là thứ tự tab trên UI.")]
        [SerializeField] private List<MissionDatabaseSO> databases = new List<MissionDatabaseSO>();

        public Settings() { }

        public Settings(List<MissionDatabaseSO> databases) => this.databases = databases;

        /// <summary>Các database theo thứ tự tab.</summary>
        public IReadOnlyList<MissionDatabaseSO> Databases => databases;

        /// <summary>Mọi database hợp lệ và không trùng ID.</summary>
        public void Validate()
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (MissionDatabaseSO database in databases)
            {
                if (database == null) throw new InvalidOperationException("Mission settings has an empty database.");
                database.Validate();
                if (!ids.Add(database.Id)) throw new InvalidOperationException("Duplicate mission database ID: " + database.Id);
            }
        }
    }

    #endregion

    #region Fields

    private readonly ISaveStore saves;
    private readonly IRewardGrant rewards;
    private readonly IGameClock clock; // Giờ của mọi nhóm, theo nguồn từng database chọn.
    private readonly List<MissionBoard> boards = new List<MissionBoard>();
    private readonly List<IMissionTracker> trackers = new List<IMissionTracker>();
    private bool loaded;
    private bool dirty; // Tiến độ trong RAM chưa xếp để ghi.
    private bool progressed; // Có tiến độ đổi trong việc đang báo, chờ Finished mới báo UI.
    private bool claiming; // Chặn nhận lồng nhau từ callback của chính lần nhận.
    private bool disposed;

    #endregion

    #region Constructor

    /// <summary>Dựng các nhóm, tracker và đăng ký event; chưa đọc save.</summary>
    public MissionManager(
        Settings settings,
        ISaveStore saves,
        IGameClock gameClock,
        IRewardGrant rewardGrant,
        ICharacterRegistry characterRegistry,
        ICharacterDeathEvents characterDeaths,
        IGameState gameState,
        IBattleProgress battleProgress,
        ICharacterUpgrades characterUpgrades,
        IEquipment equipmentService)
    {
        if (settings == null) throw new ArgumentNullException(nameof(settings));
        this.saves = saves ?? throw new ArgumentNullException(nameof(saves));
        rewards = rewardGrant ?? throw new ArgumentNullException(nameof(rewardGrant));
        clock = gameClock ?? throw new ArgumentNullException(nameof(gameClock));

        CreateBoards(settings);
        CreateTrackers(characterRegistry, characterDeaths, gameState, battleProgress, characterUpgrades, equipmentService);
        Subscribe();
    }

    #endregion

    #region Setup

    private void CreateBoards(Settings settings)
    {
        foreach (MissionDatabaseSO database in settings.Databases)
            boards.Add(new MissionBoard(database));
    }

    // Nguồn tiến độ mới: thêm một dòng ở đây.
    private void CreateTrackers(
        ICharacterRegistry characters,
        ICharacterDeathEvents deaths,
        IGameState game,
        IBattleProgress battle,
        ICharacterUpgrades upgrades,
        IEquipment equipment)
    {
        trackers.Add(new KillTracker(deaths, characters));
        trackers.Add(new BattleResultTracker(game, battle));
        trackers.Add(new UpgradeTracker(upgrades, equipment));
    }

    private void Subscribe()
    {
        foreach (IMissionTracker tracker in trackers)
        {
            tracker.Progressed += OnProgressed;
            tracker.Finished += OnFinished;
        }
        saves.Saving += Flush; // App ẩn/thoát giữa trận: giữ tiến độ đang ở RAM.
        clock.Changed += Refresh; // Server vừa trả giờ hoặc vừa mất giờ: nhóm dùng giờ server mở/đóng ngay.
    }

    // Tracker tự gỡ khỏi event gameplay khi Dispose.
    private void Unsubscribe()
    {
        saves.Saving -= Flush;
        clock.Changed -= Refresh;
        foreach (IMissionTracker tracker in trackers)
        {
            tracker.Progressed -= OnProgressed;
            tracker.Finished -= OnFinished;
            tracker.Dispose();
        }
    }

    #endregion

    #region Lifecycle

    /// <summary>Đọc tiến độ sau Save và Progression; thưởng cấu hình sai chỉ báo lỗi, không chặn game.</summary>
    public override void Initialize()
    {
        if (disposed || loaded || !saves.IsLoaded) return;
        MissionsSaveData data = saves.Load(GameConstants.SaveKeys.Missions, new MissionsSaveData(), MissionsSaveData.Validate);
        foreach (MissionsSaveData.Board saved in data.boards)
            foreach (MissionBoard board in boards)
                if (string.Equals(saved.databaseId, board.Database.Id, StringComparison.Ordinal)) board.Restore(saved);
        loaded = true;
        RefreshBoards();
        foreach (MissionBoard board in boards)
            foreach (MissionRuntime mission in board.Missions)
            {
                try { rewards.ValidateRewards(mission.Data.Rewards); }
                catch (Exception exception) { Debug.LogException(exception, mission.Data); }
            }
    }

    /// <summary>
    /// Gỡ listener; chạy đầu tiên trong DisposeOrder nên mọi miền dưới còn sống. Cố ý KHÔNG ghi ở đây: nút Reset Data xóa file
    /// rồi reload scene, ghi lúc này sẽ đưa tiến độ cũ trở lại. Thoát app đã được ghi qua <see cref="ISaveStore.Saving"/>.
    /// </summary>
    public override void Dispose()
    {
        if (disposed) return;
        Unsubscribe();
        disposed = true;
        Changed = null;
    }

    #endregion

    #region IMissions

    /// <inheritdoc/>
    public IReadOnlyList<MissionBoard> Boards => boards;

    /// <inheritdoc/>
    public event Action Changed;

    /// <inheritdoc/>
    public bool HasClaimable
    {
        get
        {
            foreach (MissionBoard board in boards)
            {
                if (!board.IsOpen) continue;
                foreach (MissionRuntime mission in board.Missions)
                    if (mission.State == MissionState.Claimable) return true;
            }
            return false;
        }
    }

    /// <inheritdoc/>
    // Giờ máy, qua Clock để cả game chỉ có một nơi đọc giờ. Giờ máy luôn có nên bỏ qua kết quả bool.
    public DateTime UtcNow
    {
        get
        {
            clock.TryGetUtcNow(ClockSource.Device, out DateTime now);
            return now;
        }
    }

    /// <inheritdoc/>
    public void Refresh()
    {
        if (loaded && !disposed && RefreshBoards()) NotifyChanged();
    }

    /// <inheritdoc/>
    public bool TryClaim(MissionRuntime mission, out RewardResult result, out bool saved)
    {
        result = null;
        saved = false;
        if (mission == null) throw new ArgumentNullException(nameof(mission));
        if (!loaded || disposed || claiming) return false;
        Refresh(); // Có thể vừa qua kỳ mới trong lúc UI còn hiện nút Claim.
        MissionBoard board = FindBoard(mission);
        // Nhóm mà nguồn giờ chưa có giờ (ví dụ Server khi mất mạng) đã bị Refresh đóng, nên IsOpen chặn luôn trường hợp đó.
        if (board == null || !board.IsOpen || mission.State != MissionState.Claimable) return false;

        claiming = true;
        try
        {
            // Kiểm tra gói TRƯỚC khi đánh dấu: cấu hình sai thì ném lỗi mà không mất lượt nhận.
            rewards.ValidateRewards(mission.Data.Rewards);
            board.MarkClaimed(mission); // Trước snapshot: tiến độ Final Mission nằm trong cùng lần ghi.
            result = rewards.GrantRewards(mission.Data.Rewards,
                SaveEntry.Of(GameConstants.SaveKeys.Missions, CreateSaveData()), out saved);
            dirty = false; // Snapshot vừa ghi (hoặc đang chờ ghi lại) đã gồm mọi tiến độ.
        }
        finally { claiming = false; }
        NotifyChanged();
        return true;
    }

    #endregion

    #region Tracker Events

    // Chỉ cộng; một việc có thể gồm nhiều tín hiệu nên chưa báo UI.
    private void OnProgressed(MissionSignal signal) => progressed |= Add(signal);

    // Xong một việc: ghi trước, rồi báo UI đúng một lần. Mốc ghi cũng là lúc kiểm tra sang kỳ mới (về Home).
    private void OnFinished(bool save)
    {
        if (save)
        {
            if (loaded && !disposed) progressed |= RefreshBoards();
            Flush();
        }
        if (!progressed) return;
        progressed = false;
        NotifyChanged();
    }

    #endregion

    #region Private Helpers

    // Kiểm tra kỳ trước khi cộng, để tiến độ sau nửa đêm luôn vào kỳ mới. Trả về có đổi hay không.
    private bool Add(MissionSignal signal)
    {
        if (!loaded || disposed || signal.Amount <= 0) return false;
        bool changed = RefreshBoards();
        foreach (MissionBoard board in boards) changed |= board.Add(signal);
        dirty |= changed;
        return changed;
    }

    // Mỗi nhóm đọc giờ từ nguồn database của nó chọn; Mission không biết nguồn nào là máy hay server.
    private bool RefreshBoards()
    {
        bool changed = false;
        foreach (MissionBoard board in boards)
        {
            bool hasTime = clock.TryGetUtcNow(board.Database.Clock, out DateTime now);
            changed |= board.Refresh(now, hasTime);
        }
        dirty |= changed;
        return changed;
    }

    private MissionBoard FindBoard(MissionRuntime mission)
    {
        foreach (MissionBoard board in boards)
            foreach (MissionRuntime candidate in board.Missions)
                if (ReferenceEquals(candidate, mission)) return board;
        return null;
    }

    // IO lỗi thì SaveManager giữ snapshot để thử lại, không cần giữ cờ dirty.
    private void Flush()
    {
        if (!loaded || !dirty || !saves.IsLoaded) return;
        dirty = false;
        saves.Save(GameConstants.SaveKeys.Missions, CreateSaveData());
    }

    private MissionsSaveData CreateSaveData()
    {
        var data = new MissionsSaveData();
        foreach (MissionBoard board in boards) data.boards.Add(board.ToSaveData());
        MissionsSaveData.Validate(data);
        return data;
    }

    // Lỗi trình bày UI không được chặn gameplay (event quái chết) hay làm tưởng chưa nhận thưởng.
    private void NotifyChanged()
    {
        if (Changed == null) return;
        foreach (Action listener in Changed.GetInvocationList())
        {
            try { listener(); }
            catch (Exception exception) { Debug.LogException(exception); }
        }
    }

    #endregion
}
