using System;

// Họ strategy NỘI BỘ của Progression: mỗi loại nâng cấp (CharacterUpgradeKind) là một track.
// ProgressionManager chọn track bằng một switch; miền khác chỉ thấy ICharacterUpgrades.
// Không bind Zenject. Level trả Gold, sao trả mảnh riêng của nhân vật; nhân vật chưa sở hữu thì không nâng được.

#region Track Contract

/// <summary>Một loại nâng cấp vĩnh viễn: hỏi giá và thực hiện một lần nâng (một lần bấm).</summary>
internal interface IUpgradeTrack
{
    /// <summary>Cấp hiện tại/tối đa, tiến độ thanh, giá và lý do bị chặn.</summary>
    UpgradeQuote GetQuote(CharacterType type);

    /// <summary>Thực hiện một lần nâng nếu <see cref="GetQuote"/> cho phép; <paramref name="saved"/> là kết quả ghi file.</summary>
    bool TryUpgrade(CharacterType type, out bool saved);
}

#endregion

#region Level Track

/// <summary>
/// Level vĩnh viễn theo thanh: mỗi mốc cần một lượng Gold (công thức theo độ hiếm trong LevelDatabaseSO),
/// chia đều cho <c>ClicksPerLevel</c> lần bấm. Mỗi lần bấm tiêu một phần Gold và nạp đúng lượng đó vào thanh (1:1),
/// giống nhặt EXP trong trận; đầy thanh thì lên cấp. Giá trên nút vì vậy tăng theo level và độ hiếm.
/// </summary>
/// <remarks>
/// Ví dụ 4 lần bấm: Common Lv1 (cần 100) → 25 mỗi lần; Rare Lv1 (cần 200) → 50; Common Lv10 (cần 550) → 138, 138, 138, 136.
/// Lần bấm cuối của mốc chỉ tốn phần còn thiếu, nên tổng Gold của mốc đúng bằng công thức và một lần bấm không bao giờ tràn sang mốc sau.
/// </remarks>
internal sealed class LevelTrack : IUpgradeTrack
{
    private readonly CharacterLevels levels;
    private readonly Wallet wallet; // Chỉ đọc số dư để báo NotEnoughCurrency; việc trừ tiền do CharacterLevels làm cùng lần save.

    /// <summary>Dùng chung CharacterLevels và Wallet của ProgressionManager, không giữ bản sao tiến trình.</summary>
    public LevelTrack(CharacterLevels levels, Wallet wallet)
    {
        this.levels = levels ?? throw new ArgumentNullException(nameof(levels));
        this.wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
    }

    /// <inheritdoc/>
    public UpgradeQuote GetQuote(CharacterType type)
    {
        CharacterLevelConfigSO config = levels.GetConfig(type);
        int level = levels.GetLevel(type);
        int max = config.MaxLevel;
        // Chưa sở hữu thì chỉ xem thông tin, không nâng được.
        if (!levels.IsOwned(type))
            return new UpgradeQuote(level, max, 0, 0, Array.Empty<CurrencyCost>(), UpgradeBlock.NotAvailable);
        if (!levels.TryGetRequiredPoints(type, out int required))
            return new UpgradeQuote(Math.Min(level, max), max, 0, 0, Array.Empty<CurrencyCost>(), UpgradeBlock.MaxReached);

        int progress = levels.GetUpgradePoints(type);
        // Giá một lần bấm = Gold của mốc chia đều cho số lần bấm (làm tròn lên).
        // Lần cuối chỉ tốn phần còn thiếu của thanh: đây là tiền thật nên không được thu dư.
        long perClick = (required + (long)config.ClicksPerLevel - 1) / config.ClicksPerLevel;
        long amount = Math.Min(perClick, required - progress);
        var costs = new[] { new CurrencyCost(levels.UpgradeCurrency, amount) };
        UpgradeBlock block = wallet.CanAfford(costs) ? UpgradeBlock.None : UpgradeBlock.NotEnoughCurrency;
        return new UpgradeQuote(level, max, progress, required, costs, block);
    }

    /// <inheritdoc/>
    public bool TryUpgrade(CharacterType type, out bool saved)
    {
        saved = false;
        // Kiểm tra lại: UI có thể đang giữ một quote cũ.
        UpgradeQuote quote = GetQuote(type);
        if (!quote.CanUpgrade) return false;
        // Gold tiêu chính là lượng nạp vào thanh (1:1); tiền và điểm được ghi cùng một lần save.
        int amount = (int)quote.Costs[0].Amount;
        return levels.TryAddUpgradePoints(type, amount, quote.Costs, out saved);
    }
}

#endregion

#region Star Track

/// <summary>
/// Sao vĩnh viễn: mỗi lần bấm mở thêm một sao tới <see cref="MaxStars"/>, trả bằng mảnh riêng của chính nhân vật
/// (Star Cost trong LevelDatabaseSO, theo độ hiếm). Mảnh không nằm trong ví nên quote báo giá qua <see cref="UpgradeQuote.ShardCost"/>.
/// Phần thưởng của mỗi sao nằm trong CharacterDataSO: các bước skill (điều kiện Stars), hoặc chỉ số dự phòng khi sao không có skill.
/// </summary>
/// <remarks>Luật trả giá nằm ở <see cref="CharacterCollection"/>; track này chỉ dựng quote và chặn nhân vật chưa sở hữu.
/// Quote không có <see cref="UpgradeQuote.Costs"/> (không tốn tiền ví), nên UI đọc <see cref="UpgradeQuote.ShardCost"/>.</remarks>
internal sealed class StarTrack : IUpgradeTrack
{
    /// <summary>Số sao tối đa, khớp 5 ô sao trên UI.</summary>
    public const int MaxStars = CharacterLevelConfigSO.MaxStars;

    private readonly CharacterLevels levels;
    private readonly CharacterCollection collection;

    /// <summary>Dùng chung CharacterLevels và CharacterCollection của ProgressionManager.</summary>
    public StarTrack(CharacterLevels levels, CharacterCollection collection)
    {
        this.levels = levels ?? throw new ArgumentNullException(nameof(levels));
        this.collection = collection ?? throw new ArgumentNullException(nameof(collection));
    }

    /// <inheritdoc/>
    public UpgradeQuote GetQuote(CharacterType type)
    {
        // Save cũ có thể nhiều sao hơn trần hiện tại; chỉ hiển thị tới trần, không tự sửa save.
        int stars = Math.Min(levels.GetStars(type), MaxStars);
        if (!collection.IsOwned(type))
            return new UpgradeQuote(stars, MaxStars, default, 0, UpgradeBlock.NotAvailable);
        if (stars >= MaxStars)
            return new UpgradeQuote(stars, MaxStars, default, 0, UpgradeBlock.MaxReached);
        var cost = new CharacterShards(type, collection.GetNextStarCost(type));
        UpgradeBlock block = collection.CanAffordNextStar(type) ? UpgradeBlock.None : UpgradeBlock.NotEnoughShards;
        return new UpgradeQuote(stars, MaxStars, cost, collection.GetStatus(type).Shards, block);
    }

    /// <inheritdoc/>
    public bool TryUpgrade(CharacterType type, out bool saved)
    {
        saved = false;
        // Kiểm tra lại: UI có thể đang giữ một quote cũ.
        if (!GetQuote(type).CanUpgrade) return false;
        return collection.TryAddStar(type, out saved);
    }
}

#endregion
