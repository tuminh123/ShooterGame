using System;

// Nơi DUY NHẤT đổi event gameplay thành tín hiệu nhiệm vụ. Mỗi tracker nghe một nhóm event, do MissionManager tạo.
// Tracker chỉ nói "vừa xảy ra việc gì"; việc đó tính cho nhiệm vụ nào do MissionTask.Matches quyết định.
// Thêm nguồn mới: một class ở đây + một dòng trong MissionManager.CreateTrackers.

#region Contract

/// <summary>Một việc vừa xảy ra trong game, kèm ID để nhiệm vụ tự xác minh có phải việc mình cần.</summary>
public readonly struct MissionSignal
{
    /// <summary>Loại việc.</summary>
    public readonly MissionObjectiveType Objective;
    /// <summary>Số lượng.</summary>
    public readonly int Amount;
    /// <summary>Nhóm quái; chỉ có nghĩa với KillEnemy và không bao giờ là Any.</summary>
    public readonly EnemyKind EnemyKind;
    /// <summary>ID quái; chỉ có nghĩa với KillEnemy.</summary>
    public readonly CharacterType Character;

    /// <summary>Việc không có đối tượng riêng (hết trận, nâng cấp).</summary>
    public MissionSignal(MissionObjectiveType objective, int amount) : this(objective, amount, EnemyKind.Any, CharacterType.None) { }

    private MissionSignal(MissionObjectiveType objective, int amount, EnemyKind enemyKind, CharacterType character)
    {
        Objective = objective;
        Amount = amount;
        EnemyKind = enemyKind;
        Character = character;
    }

    /// <summary>Một con quái vừa chết.</summary>
    public static MissionSignal Kill(EnemyKind kind, CharacterType character)
        => new MissionSignal(MissionObjectiveType.KillEnemy, 1, kind, character);
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
                Progressed?.Invoke(new MissionSignal(MissionObjectiveType.FinishBattle, 1));
                if (current == GameState.EndingWin) Progressed?.Invoke(new MissionSignal(MissionObjectiveType.WinBattle, 1));
                Progressed?.Invoke(new MissionSignal(MissionObjectiveType.CollectBattleGold, battle.Gold));
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

    private void OnCharacterRaised(CharacterType type, CharacterUpgradeKind kind, int amount)
        => Report(kind == CharacterUpgradeKind.Star
            ? MissionObjectiveType.RaiseCharacterStar
            : MissionObjectiveType.RaiseCharacterLevel, amount);

    private void OnEquipmentLeveledUp(EquipmentItem item) => Report(MissionObjectiveType.RaiseEquipmentLevel, 1);

    private void Report(MissionObjectiveType objective, int amount)
    {
        Progressed?.Invoke(new MissionSignal(objective, amount));
        Finished?.Invoke(true);
    }
}

#endregion
