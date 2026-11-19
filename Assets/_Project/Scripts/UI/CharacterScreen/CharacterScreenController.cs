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
/// Mở từ Home qua <see cref="Open"/>; UIManager hiện màn này đè lên Home và Back ở Collection trả về Home.
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
        collection.BackClicked += OnCollectionBack;
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
        collection.BackClicked -= OnCollectionBack;
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
    public override void SetVisible(bool visible)
    {
        gameObject.SetActive(visible);
        if (visible) Redraw();
    }

    #endregion

    #region View Events

    private void OnCardClicked(int index)
    {
        if (index < 0 || index >= selection.PlayableCharacters.Count) return;
        viewingIndex = index;
        page = CharacterScreenPage.Detail;
        Redraw();
    }

    private void OnCollectionBack() => RequestVisibility(false);

    private void OnDetailBack()
    {
        page = CharacterScreenPage.Collection;
        Redraw();
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
        if (upgrades.TryUpgrade(character.CharacterType, KindOf(tab), out bool saved) && !saved)
            Debug.LogWarning($"Upgrade of {character.CharacterType} was applied but not saved yet; it will be retried.", this);
        // Vẽ lại qua Upgraded.
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
        collection.gameObject.SetActive(!showDetail);
        detail.gameObject.SetActive(showDetail);
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
            rarity.Label, rarity.Text, rarity.TextColor, character.DisplayName, character.Portrait, star.Current,
            tab, level.Current, level.Max, BuildStats(character), BuildSkillIcons(character),
            type == selection.SelectedCharacterType,
            BuildUpgradeButton(tab == CharacterScreenTab.Level ? level : star)));
    }

    // Chỉ số cuối = chỉ số gốc + bonus vĩnh viễn (level + trang bị), cùng công thức với Player khi vào trận.
    private StatRowModel[] BuildStats(CharacterDataSO character)
    {
        StatBonus bonus = bonuses.GetBonus(character.CharacterType);
        BaseStats final = StatCalculator.Calculate(character.BaseStats, new[] { bonus.ToModifiers() });
        IReadOnlyList<CharacterScreenThemeSO.StatEntry> entries = theme.Stats;
        var rows = new StatRowModel[entries.Count];
        for (int i = 0; i < entries.Count; i++)
            rows[i] = new StatRowModel(entries[i].Icon,
                CharacterScreenThemeSO.FormatValue(final.Get(entries[i].Stat), entries[i].Format));
        return rows;
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
