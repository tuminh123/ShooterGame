using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using Zenject;

/// <summary>
/// Controller của màn Equipment: giữ trạng thái màn (tab đang lọc, món đang xem trong popup), gọi <see cref="IEquipment"/>,
/// dựng model rồi đẩy xuống View. View chỉ hiển thị và phát event.
/// </summary>
/// <remarks>
/// Mở/đóng từ thanh bar Home (<see cref="HomeNavigationBar"/>) qua <see cref="Open"/> / <see cref="Close"/>; UIManager hiện màn này
/// đè lên Home. Vẽ lại khi có event (đồ đổi, bonus đổi, đổi nhân vật), không polling.
/// Luật mốc dòng phụ và skill đọc từ chính data (<see cref="EquipmentRulesSO"/>, <see cref="EquipmentDataSO.TryGetSkill"/>,
/// <see cref="EquipmentSetData.IsActive"/>) nên popup luôn khớp với gameplay. Chỉ số dùng đúng <see cref="StatCalculator"/>.
/// </remarks>
public sealed class EquipmentScreenController : UIPanelController
{
    #region References

    [SerializeField] private EquipmentLoadoutView loadout;
    [SerializeField] private EquipmentInventoryView inventory;
    [SerializeField] private EquipmentInfoPopupView infoPopup;
    [SerializeField] private EquipmentStatsPopupView statsPopup;
    [SerializeField] private EquipmentThemeSO theme;

    [Header("Text")]
    [Tooltip("Nhãn level trên thẻ; {0} = level.")]
    [SerializeField] private string levelFormat = "Lv.{0}";
    [Tooltip("Xem trước chỉ số chính ở cấp tới; {0} = lượng tăng.")]
    [SerializeField] private string previewFormat = "+{0}";
    [Tooltip("Tên dòng phụ chưa mở.")]
    [SerializeField] private string lockedSubstatName = "Substat";
    [Tooltip("Điều kiện mở dòng phụ; {0} = level.")]
    [SerializeField] private string unlockAtFormat = "Unlocks at Lv.{0}";
    [Tooltip("Gợi ý khi cấp tới mở dòng phụ mới; {0} = level.")]
    [SerializeField] private string hintUnlockFormat = "Lv.{0}: unlocks a new substat";
    [Tooltip("Gợi ý khi cấp tới cộng vào một dòng phụ ngẫu nhiên; {0} = level.")]
    [SerializeField] private string hintBoostFormat = "Lv.{0}: boosts a random substat";
    [Tooltip("Khối Skill khi đã mở; {0} = tên skill, {1} = mô tả.")]
    [SerializeField] private string skillFormat = "<b>{0}</b>\n{1}";
    [Tooltip("Khối Skill khi còn khóa; {0} = tên skill, {1} = level cần, {2} = mô tả.")]
    [SerializeField] private string skillLockedFormat = "<b>{0}</b> (Lv.{1})\n{2}";
    [Tooltip("Khối Set; {0} = tên set, {1} = số món đang mặc, {2} = số món cần, {3} = tên skill, {4} = mô tả set.")]
    [SerializeField] private string setFormat = "<b>{0} {1}/{2}</b> · {3}\n{4}";
    [Tooltip("Một dòng set trên màn chính; {0} = tên set, {1} = số món đang mặc, {2} = số món cần.")]
    [SerializeField] private string setLineFormat = "{0} {1}/{2}";
    [Tooltip("Màu dòng set đã bật trên màn chính (mã hex, không có #).")]
    [SerializeField] private string activeSetColor = "8CFF73";
    [SerializeField] private string equipLabel = "Equip";
    [SerializeField] private string unequipLabel = "Unequip";
    [Tooltip("Nút Level Up; {0} = giá.")]
    [SerializeField] private string upgradeFormat = "Level Up {0}";
    [SerializeField] private string maxLabel = "MAX";

    #endregion

    #region Dependencies And State

    private static readonly EquipmentSlot[] Slots = (EquipmentSlot[])Enum.GetValues(typeof(EquipmentSlot));

    private IEquipment equipment;
    private IPermanentBonuses bonuses;
    private IPlayerSelection selection;
    private IGameState game;
    private EquipmentRulesSO rules;
    private bool subscribed;
    private bool themeValid;

    private EquipmentSlot? tab; // null = tab "Tất cả" (mặc định).
    private EquipmentItem popupItem; // Món đang xem trong popup; null khi popup đóng.
    private readonly List<EquipmentItem> gridItems = new List<EquipmentItem>(32); // Thứ tự = thứ tự thẻ trên lưới.
    private readonly List<EquipmentCardModel> gridModels = new List<EquipmentCardModel>(32);
    private readonly List<float> substatsBefore = new List<float>(4); // Giá trị dòng phụ trước khi nâng, để biết dòng nào đổi.

    #endregion

    #region Navigation State (HomeNavigationBar)

    /// <summary>Màn đang mở theo yêu cầu điều hướng (đổi ngay khi mở/đóng, không chờ hiệu ứng trượt xong).</summary>
    public bool IsShown => WantsVisible;

    /// <summary>Phát khi màn mở/đóng; thanh bar đọc lại <see cref="IsShown"/>.</summary>
    public event Action NavigationChanged;

    #endregion

    #region Lifecycle

    /// <summary>Zenject inject cả object đang tắt; đăng ký event khi object được bật.</summary>
    [Inject]
    public void Construct(IEquipment equipmentService, IPermanentBonuses permanentBonuses, IPlayerSelection playerSelection,
        IGameState gameState, EquipmentDatabaseSO equipmentDatabase)
    {
        Unsubscribe();
        equipment = equipmentService;
        bonuses = permanentBonuses;
        selection = playerSelection;
        game = gameState;
        rules = equipmentDatabase.Rules;
        if (isActiveAndEnabled) Subscribe();
    }

    // Kiểm tra Theme một lần, lúc mở màn lần đầu (object có thể đang tắt từ đầu nên không dựa vào Awake).
    private bool EnsureThemeValid()
    {
        if (themeValid) return true;
        try
        {
            if (theme == null) throw new InvalidOperationException("Assign Equipment Theme on EquipmentScreenController.");
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
        if (subscribed || equipment == null) return;
        subscribed = true;
        equipment.Changed += Redraw;
        bonuses.BonusesChanged += Redraw;
        selection.SelectionChanged += OnSelectionChanged;

        loadout.SlotClicked += OnSlotClicked;
        loadout.UnequipAllClicked += OnUnequipAll;
        loadout.PropertyClicked += OnProperty;
        inventory.TabClicked += OnTabClicked;
        inventory.AllTabClicked += OnAllTabClicked;
        inventory.CardClicked += OnCardClicked;
        infoPopup.EquipClicked += OnEquip;
        infoPopup.UpgradeClicked += OnUpgrade;
        infoPopup.Closed += OnPopupClosed;
    }

    private void Unsubscribe()
    {
        if (!subscribed) return;
        subscribed = false;
        equipment.Changed -= Redraw;
        bonuses.BonusesChanged -= Redraw;
        selection.SelectionChanged -= OnSelectionChanged;

        loadout.SlotClicked -= OnSlotClicked;
        loadout.UnequipAllClicked -= OnUnequipAll;
        loadout.PropertyClicked -= OnProperty;
        inventory.TabClicked -= OnTabClicked;
        inventory.AllTabClicked -= OnAllTabClicked;
        inventory.CardClicked -= OnCardClicked;
        infoPopup.EquipClicked -= OnEquip;
        infoPopup.UpgradeClicked -= OnUpgrade;
        infoPopup.Closed -= OnPopupClosed;
    }

    #endregion

    #region Open And Visibility

    /// <summary>Mở màn Equipment. Chỉ mở được khi game đang ở Home.</summary>
    public void Open()
    {
        if (game == null || game.State != GameState.Home || !EnsureThemeValid()) return;
        RequestVisibility(true);
    }

    /// <summary>Đóng màn về Home; hiệu ứng trượt ra do <see cref="SetVisible"/> lo.</summary>
    public void Close() => RequestVisibility(false);

    /// <inheritdoc/>
    // Cả màn trượt vào/ra (SlideTransition trên EquipmentUI). UIManager gọi Home.SetVisible cùng lúc.
    public override void SetVisible(bool visible)
    {
        NavigationChanged?.Invoke();
        if (!visible)
        {
            infoPopup.Hide();
            statsPopup.Hide();
            if (TryGetComponent(out SlideTransition slideOut)) slideOut.PlayOut();
            else gameObject.SetActive(false);
            return;
        }
        if (TryGetComponent(out SlideTransition slideIn)) slideIn.PlayIn();
        else gameObject.SetActive(true);
        Redraw();
    }

    #endregion

    #region View Events

    // Ô có đồ: mở popup. Ô trống: chuyển lưới sang tab của ô đó để chọn đồ mặc vào.
    private void OnSlotClicked(EquipmentSlot slot)
    {
        EquipmentItem item = equipment.GetEquipped(slot);
        if (item != null)
        {
            ShowPopup(item, true);
            return;
        }
        tab = slot;
        DrawInventory();
    }

    private void OnTabClicked(EquipmentSlot slot)
    {
        if (tab == slot) return;
        tab = slot;
        DrawInventory();
    }

    private void OnAllTabClicked()
    {
        if (tab == null) return;
        tab = null;
        DrawInventory();
    }

    private void OnCardClicked(int index)
    {
        if (index < 0 || index >= gridItems.Count) return;
        ShowPopup(gridItems[index], true);
    }

    private void OnUnequipAll()
    {
        if (equipment.UnequipAll(out bool saved) && !saved)
            Debug.LogWarning("Unequip all was applied but not saved yet; it will be retried.", this);
        // Vẽ lại qua Changed.
    }

    private void OnProperty()
    {
        if (!TryGetSelectedCharacter(out CharacterDataSO character)) return;
        BaseStats final = CalculateFinalStats(character);
        IReadOnlyList<CharacterScreenThemeSO.StatEntry> entries = theme.StatTheme.Stats;
        var lines = new EquipmentStatLineModel[entries.Count];
        for (int i = 0; i < entries.Count; i++)
        {
            StatType stat = entries[i].Stat;
            lines[i] = new EquipmentStatLineModel(entries[i].Icon, theme.GetStatName(stat),
                theme.FormatStat(stat, final.Get(stat)), string.Empty, false);
        }
        statsPopup.Show(lines);
    }

    // Đang mặc thì tháo, chưa mặc thì mặc (đổi chỗ nếu ô đã có món khác). Đóng popup để thấy món đổi chỗ.
    private void OnEquip()
    {
        EquipmentItem item = popupItem;
        if (item == null) return;
        EquipmentSlot slot = item.Definition.Slot;
        bool wearing = equipment.GetEquipped(slot) == item;
        infoPopup.Hide();
        bool changed;
        bool saved;
        if (wearing)
            changed = equipment.Unequip(slot, out saved);
        else
            changed = equipment.Equip(item, out saved);
        if (changed && !saved)
            Debug.LogWarning($"Equipment change on {slot} was applied but not saved yet; it will be retried.", this);
        if (changed && !wearing)
            loadout.PlayEquipped(slot);
    }

    private void OnUpgrade()
    {
        EquipmentItem item = popupItem;
        if (item == null) return;
        int countBefore = item.Substats.Count;
        substatsBefore.Clear();
        foreach (StatModifier substat in item.Substats)
            substatsBefore.Add(substat.value);
        bool upgraded = equipment.TryUpgrade(item, out bool saved);
        if (upgraded && !saved)
            Debug.LogWarning($"Level up of {item.Definition.Id} was applied but not saved yet; it will be retried.", this);
        if (!upgraded) return;
        // Popup đã vẽ lại qua Changed; giờ chỉ báo dòng nào vừa mở hoặc vừa được cộng.
        infoPopup.PlayLevelUp(FindChangedSubstat(item, countBefore));
    }

    private void OnPopupClosed() => popupItem = null;

    #endregion

    #region Service Events

    private void OnSelectionChanged(CharacterType type) => Redraw();

    #endregion

    #region Drawing

    // Chỉ vẽ khi màn đang hiện; dữ liệu đọc mới hoàn toàn mỗi lần để không giữ bản sao lệch với service.
    private void Redraw()
    {
        if (!themeValid || equipment == null || !isActiveAndEnabled) return;
        DrawLoadout();
        DrawInventory();
        if (popupItem == null)
            return;
        if (Contains(equipment.Items, popupItem))
            ShowPopup(popupItem, false);
        else
            infoPopup.Hide();
    }

    private void DrawLoadout()
    {
        TryGetSelectedCharacter(out CharacterDataSO character);
        var slots = new EquipmentCardModel[Slots.Length];
        bool anyEquipped = false;
        foreach (EquipmentSlot slot in Slots)
        {
            EquipmentItem item = equipment.GetEquipped(slot);
            slots[(int)slot] = item != null ? BuildCard(item) : BuildEmptyCard(slot);
            anyEquipped |= item != null;
        }
        loadout.Bind(new EquipmentLoadoutModel(character != null ? character.UIVisualPrefab : null,
            character != null ? character.Portrait : null, character != null ? character.DisplayName : string.Empty,
            slots, DescribeSets(), anyEquipped));
    }

    // Lưới chỉ chứa đồ chưa mặc của tab (tab "Tất cả" = mọi ô), sắp theo độ hiếm rồi level giảm dần,
    // bằng nhau thì theo thứ tự ô rồi món nhận trước đứng trước.
    private void DrawInventory()
    {
        gridItems.Clear();
        foreach (EquipmentItem item in equipment.Items)
        {
            EquipmentSlot slot = item.Definition.Slot;
            bool inTab = tab == null || slot == tab;
            if (inTab && equipment.GetEquipped(slot) != item)
                gridItems.Add(item);
        }
        gridItems.Sort(CompareForGrid);
        gridModels.Clear();
        foreach (EquipmentItem item in gridItems)
            gridModels.Add(BuildCard(item));
        inventory.Bind(tab, gridModels);
    }

    private static int CompareForGrid(EquipmentItem left, EquipmentItem right)
    {
        int byRarity = right.Rarity.CompareTo(left.Rarity);
        if (byRarity != 0) return byRarity;
        int byLevel = right.Level.CompareTo(left.Level);
        if (byLevel != 0) return byLevel;
        int bySlot = left.Definition.Slot.CompareTo(right.Definition.Slot);
        if (bySlot != 0) return bySlot;
        return left.Uid.CompareTo(right.Uid);
    }

    // Mỗi set đang mặc ít nhất một món: một dòng "Tên x/y", tô màu khi đã bật.
    private string DescribeSets()
    {
        var text = new StringBuilder();
        var described = new HashSet<EquipmentSet>();
        foreach (EquipmentSlot slot in Slots)
        {
            EquipmentItem item = equipment.GetEquipped(slot);
            if (item == null || item.SetData == null || !described.Add(item.SetData.Set)) continue;
            EquipmentSetData set = item.SetData;
            int count = equipment.CountEquipped(set.Set);
            string line = string.Format(CultureInfo.InvariantCulture, setLineFormat, set.DisplayName, count, set.RequiredPieces);
            if (set.IsActive(count))
                line = "<color=#" + activeSetColor + ">" + line + "</color>";
            if (text.Length > 0) text.Append('\n');
            text.Append(line);
        }
        return text.ToString();
    }

    private void ShowPopup(EquipmentItem item, bool animate)
    {
        popupItem = item;
        infoPopup.Show(BuildInfo(item), animate);
    }

    #endregion

    #region Model Building

    private EquipmentCardModel BuildCard(EquipmentItem item)
    {
        EquipmentThemeSO.SlotEntry slot = theme.GetSlot(item.Definition.Slot);
        string level = string.Format(CultureInfo.InvariantCulture, levelFormat, item.Level);
        return new EquipmentCardModel(false, item.Definition.Icon, level, slot.Icon, slot.EmptyIcon, theme.GetRarity(item.Rarity));
    }

    private EquipmentCardModel BuildEmptyCard(EquipmentSlot slot)
    {
        EquipmentThemeSO.SlotEntry entry = theme.GetSlot(slot);
        return new EquipmentCardModel(true, null, string.Empty, entry.Icon, entry.EmptyIcon, null);
    }

    private EquipmentInfoModel BuildInfo(EquipmentItem item)
    {
        EquipmentDataSO definition = item.Definition;
        EquipmentThemeSO.SlotEntry slot = theme.GetSlot(definition.Slot);
        EquipmentThemeSO.RarityEntry rarity = theme.GetRarity(item.Rarity);
        UpgradeQuote quote = equipment.GetQuote(item);
        bool atMax = quote.Block == UpgradeBlock.MaxReached;
        bool wearing = equipment.GetEquipped(definition.Slot) == item;
        return new EquipmentInfoModel(slot.Icon, slot.Text, definition.DisplayName, rarity.Text, rarity.TextColor,
            BuildCard(item), definition.Description, BuildMainLine(item, atMax), BuildSubstatLines(item),
            BuildSkillBlock(item), BuildSetBlock(item), BuildHint(item, atMax),
            wearing ? unequipLabel : equipLabel, BuildUpgradeLabel(quote), quote.CanUpgrade);
    }

    // Dòng chính kèm "+X" là lượng tăng ở cấp tới (đọc từ cùng công thức với gameplay).
    private EquipmentStatLineModel BuildMainLine(EquipmentItem item, bool atMax)
    {
        string preview = string.Empty;
        if (!atMax)
        {
            float next = rules.GetMainValue(item.Definition.Slot, item.Rarity, item.Level + 1);
            preview = string.Format(CultureInfo.InvariantCulture, previewFormat, theme.FormatStat(item.MainStat, next - item.MainValue));
        }
        return new EquipmentStatLineModel(theme.GetStatIcon(item.MainStat), theme.GetStatName(item.MainStat),
            theme.FormatStat(item.MainStat, item.MainValue), preview, false);
    }

    // Luôn đủ số dòng tối đa: dòng đã mở hiện chỉ số, dòng chưa mở hiện level sẽ mở.
    private EquipmentStatLineModel[] BuildSubstatLines(EquipmentItem item)
    {
        var lines = new EquipmentStatLineModel[rules.MaxSubstats];
        for (int i = 0; i < lines.Length; i++)
        {
            if (i < item.Substats.Count)
            {
                StatModifier substat = item.Substats[i];
                lines[i] = new EquipmentStatLineModel(theme.GetStatIcon(substat.stat), theme.GetStatName(substat.stat),
                    theme.FormatStat(substat.stat, substat.value), string.Empty, false);
                continue;
            }
            string condition = string.Empty;
            if (rules.TryGetSubstatUnlockLevel(i + 1, out int level))
                condition = string.Format(CultureInfo.InvariantCulture, unlockAtFormat, level);
            lines[i] = new EquipmentStatLineModel(null, lockedSubstatName, string.Empty, condition, true);
        }
        return lines;
    }

    private EquipmentBonusBlockModel BuildSkillBlock(EquipmentItem item)
    {
        SkillDataSO skill = item.Definition.Skill;
        if (skill == null)
            return EquipmentBonusBlockModel.Hidden;
        // Skill trang bị chỉ có một cấp.
        string description = skill.GetRunLevelDescription(1);
        bool unlocked = item.Definition.TryGetSkill(item.Level, out _);
        string text = unlocked
            ? string.Format(CultureInfo.InvariantCulture, skillFormat, skill.DisplayName, description)
            : string.Format(CultureInfo.InvariantCulture, skillLockedFormat, skill.DisplayName, item.Definition.SkillUnlockLevel, description);
        return new EquipmentBonusBlockModel(true, skill.Icon, text, unlocked);
    }

    private EquipmentBonusBlockModel BuildSetBlock(EquipmentItem item)
    {
        EquipmentSetData set = item.SetData;
        if (set == null)
            return EquipmentBonusBlockModel.Hidden;
        int count = equipment.CountEquipped(set.Set);
        string text = string.Format(CultureInfo.InvariantCulture, setFormat,
            set.DisplayName, count, set.RequiredPieces, set.Skill.DisplayName, set.Description);
        return new EquipmentBonusBlockModel(true, set.Icon, text, set.IsActive(count));
    }

    // Chỉ gợi ý khi cấp tới là mốc dòng phụ; cùng luật WillUnlockSubstat mà EquipmentGrowth dùng.
    private string BuildHint(EquipmentItem item, bool atMax)
    {
        int next = item.Level + 1;
        if (atMax || !rules.IsMilestone(next))
            return string.Empty;
        string format = rules.WillUnlockSubstat(next, item.Substats.Count) ? hintUnlockFormat : hintBoostFormat;
        return string.Format(CultureInfo.InvariantCulture, format, next);
    }

    private string BuildUpgradeLabel(UpgradeQuote quote)
    {
        if (quote.Block == UpgradeBlock.MaxReached || quote.Costs.Count == 0)
            return maxLabel;
        string cost = quote.Costs[0].Amount.ToString("N0", CultureInfo.InvariantCulture);
        return string.Format(CultureInfo.InvariantCulture, upgradeFormat, cost);
    }

    #endregion

    #region Helpers

    // Dòng mới mở là dòng cuối; nếu không có dòng mới thì là dòng có giá trị tăng. -1 nếu cấp này không phải mốc.
    private int FindChangedSubstat(EquipmentItem item, int countBefore)
    {
        if (item.Substats.Count > countBefore)
            return item.Substats.Count - 1;
        for (int i = 0; i < item.Substats.Count && i < substatsBefore.Count; i++)
        {
            if (item.Substats[i].value > substatsBefore[i])
                return i;
        }
        return -1;
    }

    private bool TryGetSelectedCharacter(out CharacterDataSO character)
    {
        CharacterType selected = selection.SelectedCharacterType;
        foreach (CharacterDataSO candidate in selection.PlayableCharacters)
        {
            if (candidate.CharacterType == selected)
            {
                character = candidate;
                return true;
            }
        }
        character = null;
        return false;
    }

    // Chỉ số cuối = chỉ số gốc + bonus vĩnh viễn (level + trang bị), cùng công thức với Player khi vào trận.
    private BaseStats CalculateFinalStats(CharacterDataSO character)
    {
        StatBonus bonus = bonuses.GetBonus(character.CharacterType);
        return StatCalculator.Calculate(character.BaseStats, new[] { bonus.ToModifiers() });
    }

    private static bool Contains(IReadOnlyList<EquipmentItem> items, EquipmentItem item)
    {
        foreach (EquipmentItem candidate in items)
        {
            if (candidate == item)
                return true;
        }
        return false;
    }

    #endregion
}
