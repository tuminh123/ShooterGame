using System;

// Nơi DUY NHẤT đổi event gameplay thành tín hiệu nhiệm vụ. Mỗi tracker nghe một nhóm event, do MissionManager tạo.
// Tracker chỉ nói "vừa xảy ra việc gì"; việc đó tính cho nhiệm vụ nào do MissionTask.Matches quyết định.
// Thêm nguồn mới: một class ở đây + một dòng trong MissionManager.CreateTrackers.

#region Contract

/// <summary>
/// Một việc vừa xảy ra trong game: nhóm, chi tiết cụ thể, đối tượng và số lượng. Struct bất biến, không cấp phát.
/// Chỉ tạo qua factory để không lẫn enum chi tiết giữa các nhóm; task so khớp bằng <see cref="MissionTask.Matches"/>.
/// </summary>
/// <remarks>Chi tiết và đối tượng của tín hiệu luôn cụ thể, không bao giờ là Any (0): Any chỉ có nghĩa ở task.</remarks>
public readonly struct MissionSignal
{
    /// <summary>Nhóm việc.</summary>
    public readonly MissionActivity Activity;
    /// <summary>Giá trị của enum chi tiết thuộc nhóm (xem <see cref="MissionTask.DetailType"/>).</summary>
    public readonly int Detail;
    /// <summary>Đối tượng đích danh (CharacterType); 0 = không có.</summary>
    public readonly int Subject;
    /// <summary>Số lượng cộng vào tiến độ.</summary>
    public readonly int Amount;

    private MissionSignal(MissionActivity activity, int detail, int subject, int amount)
    {
        Activity = activity;
        Detail = detail;
        Subject = subject;
        Amount = amount;
    }

    /// <summary>Một con quái vừa chết.</summary>
    public static MissionSignal Kill(EnemyKind kind, CharacterType enemy)
        => new MissionSignal(MissionActivity.Kill, (int)kind, (int)enemy, 1);

    /// <summary>Một trận vừa kết thúc, thắng hoặc thua.</summary>
    public static MissionSignal Battle(BattleDetail result)
        => new MissionSignal(MissionActivity.Battle, (int)result, 0, 1);

    /// <summary>Nhặt được một lượng thứ gì đó.</summary>
    public static MissionSignal Collect(CollectDetail what, int amount)
        => new MissionSignal(MissionActivity.Collect, (int)what, 0, amount);

    /// <summary>Vừa nâng cấp; <paramref name="character"/> là None với trang bị.</summary>
    public static MissionSignal Upgrade(UpgradeDetail what, int amount, CharacterType character = CharacterType.None)
        => new MissionSignal(MissionActivity.Upgrade, (int)what, (int)character, amount);
}

/// <summary>Nghe event gameplay rồi báo tín hiệu. Gameplay không biết tracker; tracker không biết nhiệm vụ nào đang mở.</summary>
internal interface IMissionTracker : IDisposable
{
    /// <summary>Vừa xảy ra một việc; chỉ cộng tiến độ, chưa báo UI.</summary>
    event Action<MissionSignal> Progressed;

    /// <summary>Đã báo hết tín hiệu của việc này: báo UI một lần. true = ghi file (quái chết thì không ghi).</summary>
    event Action<bool> Finished;
}

#endregion

#region Kill

/// <summary>Quái, elite và boss bị hạ. Không ghi file: tiến độ ở RAM tới khi hết trận.</summary>
internal sealed class KillTracker : IMissionTracker
{
    private readonly ICharacterDeathEvents deaths;
    private readonly ICharacterRegistry characters; // Tra template của quái vừa chết.

    public event Action<MissionSignal> Progressed;
    public event Action<bool> Finished;

    internal KillTracker(ICharacterDeathEvents deaths, ICharacterRegistry characters)
    {
        this.deaths = deaths ?? throw new ArgumentNullException(nameof(deaths));
        this.characters = characters ?? throw new ArgumentNullException(nameof(characters));
        deaths.CharacterDied += OnCharacterDied;
    }

    public void Dispose() => deaths.CharacterDied -= OnCharacterDied;

    private void OnCharacterDied(CharacterView character)
    {
        // Lúc này quái còn trong registry; chết xong hiệu ứng mới bị gỡ.
        if (character == null || !characters.Characters.TryGetValue(character, out CharacterDataSO data) ||
            data.Team != CharacterTeam.Enemy) return;
        Progressed?.Invoke(MissionSignal.Kill(GetKind(character, data), data.CharacterType));
        Finished?.Invoke(false);
    }

    // Boss là thuộc tính của template; Elite là quái thường được nâng lúc spawn nên nằm ở cá thể.
    private static EnemyKind GetKind(CharacterView character, CharacterDataSO data)
    {
        if (data.IsBoss) return EnemyKind.Boss;
        return character.IsElite ? EnemyKind.Elite : EnemyKind.Normal;
    }
}

#endregion

#region Battle

/// <summary>Kết quả trận. Ghi khi hết trận và khi về Home.</summary>
internal sealed class BattleResultTracker : IMissionTracker
{
    private readonly IGameState game;
    private readonly IBattleProgress battle;

    public event Action<MissionSignal> Progressed;
    public event Action<bool> Finished;

    internal BattleResultTracker(IGameState game, IBattleProgress battle)
    {
        this.game = game ?? throw new ArgumentNullException(nameof(game));
        this.battle = battle ?? throw new ArgumentNullException(nameof(battle));
        game.StateChanged += OnGameStateChanged;
    }

    public void Dispose() => game.StateChanged -= OnGameStateChanged;

    // EndingWin/EndingLose: GameManager đã chấp nhận kết quả và thống kê của lượt vẫn còn (reset sau khi rời gameplay).
    private void OnGameStateChanged(GameState previous, GameState current)
    {
        switch (current)
        {
            case GameState.EndingWin:
            case GameState.EndingLose:
                // Một tín hiệu mỗi trận: task "chơi hết trận" để Any, task "thắng" để Win.
                Progressed?.Invoke(MissionSignal.Battle(current == GameState.EndingWin ? BattleDetail.Win : BattleDetail.Lose));
                Progressed?.Invoke(MissionSignal.Collect(CollectDetail.Gold, battle.Gold));
                Finished?.Invoke(true);
                break;
            case GameState.Home: // Bỏ trận giữa chừng: vẫn giữ số quái đã hạ.
                Finished?.Invoke(true);
                break;
        }
    }
}

#endregion

#region Upgrade

/// <summary>Nâng level/sao nhân vật và level trang bị. Ghi ngay sau mỗi lần nâng.</summary>
internal sealed class UpgradeTracker : IMissionTracker
{
    private readonly ICharacterUpgrades upgrades;
    private readonly IEquipment equipment;

    public event Action<MissionSignal> Progressed;
    public event Action<bool> Finished;

    internal UpgradeTracker(ICharacterUpgrades upgrades, IEquipment equipment)
    {
        this.upgrades = upgrades ?? throw new ArgumentNullException(nameof(upgrades));
        this.equipment = equipment ?? throw new ArgumentNullException(nameof(equipment));
        upgrades.CharacterRaised += OnCharacterRaised;
        equipment.EquipmentLeveledUp += OnEquipmentLeveledUp;
    }

    public void Dispose()
    {
        upgrades.CharacterRaised -= OnCharacterRaised;
        equipment.EquipmentLeveledUp -= OnEquipmentLeveledUp;
    }

    // Mang theo loại nhân vật để task có thể chỉ tính một nhân vật đích danh.
    private void OnCharacterRaised(CharacterType type, CharacterUpgradeKind kind, int amount)
        => Report(kind == CharacterUpgradeKind.Star ? UpgradeDetail.CharacterStar : UpgradeDetail.CharacterLevel, amount, type);

    private void OnEquipmentLeveledUp(EquipmentItem item) => Report(UpgradeDetail.EquipmentLevel, 1, CharacterType.None);

    private void Report(UpgradeDetail what, int amount, CharacterType character)
    {
        Progressed?.Invoke(MissionSignal.Upgrade(what, amount, character));
        Finished?.Invoke(true);
    }
}

#endregion
