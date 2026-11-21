using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using Zenject;

/// <summary>
/// Controller của màn Character (Collection + Detail). Giữ trạng thái màn (trang, nhân vật đang xem, tab),
/// gọi service, dựng model rồi đẩy xuống View. View chỉ hiển thị và phát event.
/// </summary>
/// <remarks>
/// Mở/đóng từ thanh bar Home (<see cref="HomeNavigationBar"/>) qua <see cref="Open"/> / <see cref="Close"/>;
/// UIManager hiện màn này đè lên Home. Thanh bar nghe <see cref="NavigationChanged"/> để chọn nút nổi và ẩn ở Detail.
/// Dữ liệu được vẽ lại khi có event (đổi lựa chọn, đổi bonus, nâng cấp), không polling.
/// Chỉ số hiển thị dùng đúng <see cref="StatCalculator"/> của gameplay, không có công thức thứ hai.
/// </remarks>
public sealed class CharacterScreenController : UIPanelController
{
    #region References

    [SerializeField] private CharacterCollectionView collection;
    [SerializeField] private CharacterDetailView detail;
    [SerializeField] private CharacterScreenThemeSO theme;

    [Header("Upgrade Button Text")]
    [SerializeField] private string upgradeLabel = "UPGRADE";
    [SerializeField] private string maxLabel = "MAX";
    [SerializeField] private string lockedLabel = "LOCKED";

    /// <summary>Số ô skill trên màn Detail.</summary>
    private const int SkillSlots = 3;

    #endregion

    #region Dependencies And State

    private IPlayerSelection selection;
    private ICharacterUpgrades upgrades;
    private IPermanentBonuses bonuses;
    private IGameState game;
    private bool subscribed;
    private bool themeValid;

    private CharacterScreenPage page = CharacterScreenPage.Collection;
    private CharacterScreenTab tab = CharacterScreenTab.Level;
    private int viewingIndex; // Chỉ số trong IPlayerSelection.PlayableCharacters.

    #endregion

    #region Navigation State (HomeNavigationBar)

    /// <summary>Màn đang mở theo yêu cầu điều hướng (đổi ngay khi mở/đóng, không chờ hiệu ứng trượt xong).</summary>
    public bool IsShown => WantsVisible;

    /// <summary>Đang ở trang Detail (thanh bar Home ẩn để không che footer của Detail).</summary>
    public bool IsOnDetail => page == CharacterScreenPage.Detail;

    /// <summary>Phát khi màn mở/đóng hoặc đổi trang Collection ↔ Detail; thanh bar đọc lại hai thuộc tính trên.</summary>
    public event Action NavigationChanged;

    #endregion

    #region Lifecycle

    /// <summary>Zenject inject cả object đang tắt; đăng ký event khi object được bật.</summary>
    [Inject]
    public void Construct(IPlayerSelection playerSelection, ICharacterUpgrades characterUpgrades,
        IPermanentBonuses permanentBonuses, IGameState gameState)
    {
        Unsubscribe();
        selection = playerSelection;
        upgrades = characterUpgrades;
        bonuses = permanentBonuses;
        game = gameState;
        if (isActiveAndEnabled) Subscribe();
    }

    // Kiểm tra Theme một lần, lúc mở màn lần đầu (object có thể đang tắt từ đầu nên không dựa vào Awake).
    private bool EnsureThemeValid()
    {
        if (themeValid) return true;
        try
        {
            if (theme == null) throw new InvalidOperationException("Assign Character Screen Theme on CharacterScreenController.");
            theme.Validate();
            themeValid = true;
        }
        catch (Exception exception)
        {
            // Lỗi cấu hình: báo rõ và không mở màn, thay vì vẽ dở dang.
            Debug.LogException(exception, this);
        }
        return themeValid;
    }

    private void OnEnable() => Subscribe();
    private void OnDisable() => Unsubscribe();

    private void Subscribe()
    {
        if (subscribed || selection == null) return;
        subscribed = true;
        selection.SelectionChanged += OnSelectionChanged;
        bonuses.BonusesChanged += Redraw;
        upgrades.Upgraded += OnUpgraded;

        collection.CardClicked += OnCardClicked;
        detail.PrevClicked += OnPrev;
        detail.NextClicked += OnNext;
        detail.BackClicked += OnDetailBack;
        detail.SelectClicked += OnSelect;
        detail.UpgradeClicked += OnUpgrade;
        detail.TabChanged += OnTabChanged;
    }

    private void Unsubscribe()
    {
        if (!subscribed) return;
        subscribed = false;
        selection.SelectionChanged -= OnSelectionChanged;
        bonuses.BonusesChanged -= Redraw;
        upgrades.Upgraded -= OnUpgraded;

        collection.CardClicked -= OnCardClicked;
        detail.PrevClicked -= OnPrev;
        detail.NextClicked -= OnNext;
        detail.BackClicked -= OnDetailBack;
        detail.SelectClicked -= OnSelect;
        detail.UpgradeClicked -= OnUpgrade;
        detail.TabChanged -= OnTabChanged;
    }

    #endregion

    #region Open And Visibility

    /// <summary>Mở màn Character ở trang Collection. Chỉ mở được khi game đang ở Home.</summary>
    public void Open()
    {
        if (game == null || game.State != GameState.Home || !EnsureThemeValid()) return;
        page = CharacterScreenPage.Collection;
        RequestVisibility(true);
    }

    /// <inheritdoc/>
    // Cả màn trượt vào/ra (SlideTransition trên CharacterScreen). UIManager gọi Home.SetVisible cùng lúc,
    // nên Home trượt ra khi màn này trượt vào và ngược lại.
    public override void SetVisible(bool visible)
    {
        NavigationChanged?.Invoke();
        if (!visible)
        {
            PlayOut(this);
            return;
        }
        PlayIn(this);
        // Mở ở trang Collection thì Detail phải tắt (lần trước có thể bị ẩn giữa chừng khi đang ở Detail).
        if (page == CharacterScreenPage.Collection) detail.gameObject.SetActive(false);
        Redraw();
    }

    /// <summary>Đóng màn về Home (nút Battle của thanh bar); hiệu ứng trượt ra do <see cref="SetVisible"/> lo.</summary>
    public void Close() => RequestVisibility(false);

    #endregion

    #region View Events

    private void OnCardClicked(int index)
    {
        if (index < 0 || index >= selection.PlayableCharacters.Count) return;
        viewingIndex = index;
        page = CharacterScreenPage.Detail;
        // Detail tự bật và trượt vào đè lên Collection (Collection luôn bật làm nền nên không cần chờ trượt xong).
        PlayIn(detail);
        Redraw();
        NavigationChanged?.Invoke(); // Thanh bar trượt xuống cùng lúc Detail trượt vào.
    }

    // Detail trượt ra rồi tự tắt, lộ Collection đã vẽ lại bên dưới.
    private void OnDetailBack()
    {
        page = CharacterScreenPage.Collection;
        Redraw();
        PlayOut(detail);
        NavigationChanged?.Invoke(); // Thanh bar trượt lên lại cùng lúc.
    }

    // Bật kèm trượt vào. View chưa gắn SlideTransition thì bật ngay, để màn vẫn chạy khi thiếu component.
    // Bấm liên tục khi đang trượt chỉ làm tween mới thay tween cũ (giật nhẹ), không cần khóa thao tác.
    private static void PlayIn(MonoBehaviour view)
    {
        if (view.TryGetComponent(out SlideTransition slide)) slide.PlayIn();
        else view.gameObject.SetActive(true);
    }

    // Trượt ra rồi tắt; onComplete chạy sau khi đã tắt (dùng để báo UIManager đóng màn).
    private static void PlayOut(MonoBehaviour view, Action onComplete = null)
    {
        if (view.TryGetComponent(out SlideTransition slide)) slide.PlayOut(onComplete);
        else
        {
            view.gameObject.SetActive(false);
            onComplete?.Invoke();
        }
    }

    // ←/→ đi vòng tròn và giữ nguyên tab đang mở.
    private void OnPrev() => Step(-1);
    private void OnNext() => Step(1);

    private void Step(int delta)
    {
        int count = selection.PlayableCharacters.Count;
        if (count == 0) return;
        viewingIndex = ((viewingIndex + delta) % count + count) % count;
        Redraw();
    }

    private void OnTabChanged(CharacterScreenTab next)
    {
        if (tab == next) return;
        tab = next;
        Redraw();
    }

    private void OnSelect()
    {
        if (!TryGetViewing(out CharacterDataSO character)) return;
        if (!selection.SelectCharacter(character.CharacterType))
            Debug.LogWarning($"Could not save character selection {character.CharacterType}; it will be retried.", this);
        // Vẽ lại qua SelectionChanged.
    }

    private void OnUpgrade()
    {
        if (!TryGetViewing(out CharacterDataSO character)) return;
        CharacterUpgradeKind kind = KindOf(tab);
        // Một lần bấm Level chỉ nạp điểm; chỉ ăn mừng khi cấp (hoặc sao) thật sự tăng.
        int before = upgrades.GetQuote(character.CharacterType, kind).Current;
        bool upgraded = upgrades.TryUpgrade(character.CharacterType, kind, out bool saved);
        if (upgraded && !saved)
            Debug.LogWarning($"Upgrade of {character.CharacterType} was applied but not saved yet; it will be retried.", this);
        // Vẽ lại qua Upgraded (đã chạy xong bên trong TryUpgrade).
        int after = upgrades.GetQuote(character.CharacterType, kind).Current;
        if (!upgraded || after <= before) return;
        detail.Celebrate();
        // Sao mới mở perk cùng số: dòng perk đó nảy lên để người chơi thấy vừa nhận gì.
        if (kind == CharacterUpgradeKind.Star) detail.PlayPerkUnlocked(after);
    }

    #endregion

    #region Service Events

    private void OnSelectionChanged(CharacterType type) => Redraw();
    private void OnUpgraded(CharacterType type, CharacterUpgradeKind kind) => Redraw();

    #endregion

    #region Drawing

    // Chỉ vẽ khi màn đang hiện; dữ liệu đọc mới hoàn toàn mỗi lần để không giữ bản sao lệch với service.
    private void Redraw()
    {
        if (!themeValid || selection == null || !isActiveAndEnabled) return;
        bool showDetail = page == CharacterScreenPage.Detail && TryGetViewing(out _);
        // Collection luôn bật làm nền khi màn mở; Detail đè lên và tự bật/tắt bằng hiệu ứng trượt (PlayIn/PlayOut).
        // Không giữ cờ "đang trượt" nên không thể kẹt thao tác.
        collection.gameObject.SetActive(true);
        if (showDetail) DrawDetail();
        else DrawCollection();
    }

    private void DrawCollection()
    {
        IReadOnlyList<CharacterDataSO> characters = selection.PlayableCharacters;
        CharacterType selected = selection.SelectedCharacterType;
        var cards = new CharacterCardModel[characters.Count];
        for (int i = 0; i < characters.Count; i++)
        {
            CharacterDataSO character = characters[i];
            CharacterType type = character.CharacterType;
            cards[i] = new CharacterCardModel(
                character.Portrait,
                character.DisplayName,
                upgrades.GetQuote(type, CharacterUpgradeKind.Level).Current,
                upgrades.GetQuote(type, CharacterUpgradeKind.Star).Current,
                type == selected ? CharacterCardState.Selected : CharacterCardState.Normal);
        }
        // Chưa có mở khóa: mọi nhân vật Player đều đã có.
        collection.Bind(cards, characters.Count, characters.Count);
    }

    private void DrawDetail()
    {
        TryGetViewing(out CharacterDataSO character);
        CharacterType type = character.CharacterType;
        UpgradeQuote level = upgrades.GetQuote(type, CharacterUpgradeKind.Level);
        UpgradeQuote star = upgrades.GetQuote(type, CharacterUpgradeKind.Star);
        CharacterScreenThemeSO.RarityEntry rarity = theme.GetRarity(character.Rarity);

        detail.SetNavigationVisible(selection.PlayableCharacters.Count > 1);
        detail.Bind(new CharacterDetailModel(
            type, rarity.Label, rarity.Text, rarity.TextColor, character.DisplayName, character.Portrait,
            character.UIVisualPrefab, star.Current,
            tab, level.Current, level.Max, level.Progress, level.ProgressRequired,
            BuildStats(character), BuildSkillIcons(character), BuildStarPerks(type, star.Current),
            type == selection.SelectedCharacterType,
            BuildUpgradeButton(tab == CharacterScreenTab.Level ? level : star)));
    }

    // Chỉ số cuối = chỉ số gốc + bonus vĩnh viễn (level + perk sao + trang bị), cùng công thức với Player khi vào trận.
    private StatRowModel[] BuildStats(CharacterDataSO character)
    {
        StatBonus bonus = bonuses.GetBonus(character.CharacterType);
        BaseStats final = StatCalculator.Calculate(character.BaseStats, new[] { bonus.ToModifiers() });
        IReadOnlyList<CharacterScreenThemeSO.StatEntry> entries = theme.Stats;
        var rows = new StatRowModel[entries.Count];
        for (int i = 0; i < entries.Count; i++)
        {
            float amount = final.Get(entries[i].Stat);
            rows[i] = new StatRowModel(entries[i].Icon, CharacterScreenThemeSO.FormatValue(amount, entries[i].Format), amount);
        }
        return rows;
    }

    // Perk i mở ở sao i+1: đã có đủ sao thì Unlocked, sao kế tiếp là Next, còn lại Locked.
    private StarPerkModel[] BuildStarPerks(CharacterType type, int stars)
    {
        IReadOnlyList<StarPerk> perks = upgrades.GetStarPerks(type);
        var models = new StarPerkModel[perks.Count];
        for (int i = 0; i < perks.Count; i++)
        {
            int star = i + 1;
            StarPerkState state = star <= stars ? StarPerkState.Unlocked
                : star == stars + 1 ? StarPerkState.Next
                : StarPerkState.Locked;
            StarPerk perk = perks[i];
            models[i] = new StarPerkModel(star, perk?.Icon, perk != null ? perk.Description : string.Empty, state);
        }
        return models;
    }

    private static Sprite[] BuildSkillIcons(CharacterDataSO character)
    {
        var icons = new Sprite[SkillSlots];
        IReadOnlyList<SkillDataSO> skills = character.Skills;
        for (int i = 0; i < icons.Length && i < skills.Count; i++)
            icons[i] = skills[i] != null ? skills[i].Icon : null;
        return icons;
    }

    // Chữ và giá của nút UPGRADE theo lý do bị chặn của tab đang mở.
    private UpgradeButtonModel BuildUpgradeButton(UpgradeQuote quote)
    {
        var costs = new CostModel[quote.Costs.Count];
        for (int i = 0; i < costs.Length; i++)
            costs[i] = new CostModel(theme.GetCurrencyIcon(quote.Costs[i].Currency),
                quote.Costs[i].Amount.ToString("N0", CultureInfo.InvariantCulture));

        switch (quote.Block)
        {
            case UpgradeBlock.None: return new UpgradeButtonModel(upgradeLabel, true, costs);
            case UpgradeBlock.NotEnoughCurrency: return new UpgradeButtonModel(upgradeLabel, false, costs);
            case UpgradeBlock.MaxReached: return new UpgradeButtonModel(maxLabel, false, null);
            case UpgradeBlock.NotAvailable: return new UpgradeButtonModel(lockedLabel, false, null);
            default: throw new ArgumentOutOfRangeException(nameof(quote));
        }
    }

    #endregion

    #region Helpers

    private bool TryGetViewing(out CharacterDataSO character)
    {
        IReadOnlyList<CharacterDataSO> characters = selection.PlayableCharacters;
        bool valid = viewingIndex >= 0 && viewingIndex < characters.Count;
        character = valid ? characters[viewingIndex] : null;
        return valid;
    }

    private static CharacterUpgradeKind KindOf(CharacterScreenTab screenTab)
        => screenTab == CharacterScreenTab.Level ? CharacterUpgradeKind.Level : CharacterUpgradeKind.Star;

    #endregion
}
