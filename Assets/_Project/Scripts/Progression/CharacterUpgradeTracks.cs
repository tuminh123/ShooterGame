using System;

// Họ strategy NỘI BỘ của Progression: mỗi loại nâng cấp (CharacterUpgradeKind) là một track.
// ProgressionManager chọn track bằng một switch; miền khác chỉ thấy ICharacterUpgrades.
// Không bind Zenject. Demo chưa kiểm tra và chưa trừ tiền (xem các TODO).

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
/// Level vĩnh viễn theo thanh EXP: mỗi lần bấm tiêu <c>AmountPerUpgrade</c> nguyên liệu và nạp đúng lượng đó
/// vào thanh (1:1), giống nhặt EXP trong trận. Trần và công thức đọc từ CharacterLevelConfigSO qua <see cref="CharacterLevels"/>.
/// </summary>
internal sealed class LevelTrack : IUpgradeTrack
{
    private readonly CharacterLevels levels;

    /// <summary>Dùng chung CharacterLevels của ProgressionManager, không giữ bản sao tiến trình.</summary>
    public LevelTrack(CharacterLevels levels)
    {
        this.levels = levels ?? throw new ArgumentNullException(nameof(levels));
    }

    /// <inheritdoc/>
    public UpgradeQuote GetQuote(CharacterType type)
    {
        CharacterLevelConfigSO config = levels.GetConfig(type);
        int level = levels.GetLevel(type);
        int max = config.MaxLevel;
        if (!levels.TryGetRequiredPoints(type, out int required))
            return new UpgradeQuote(Math.Min(level, max), max, 0, 0, Array.Empty<CurrencyCost>(), UpgradeBlock.MaxReached);

        // Lần nạp chạm trần chỉ tốn đúng phần còn thiếu: EXP dư trong trận bị bỏ thì không sao, còn đây là tiền thật.
        long amount = Math.Min(config.AmountPerUpgrade, levels.GetPointsToMaxLevel(type));
        // Demo tạm dùng Gold; loại tiền sẽ nằm trong config khi làm phần trả tiền.
        var costs = new[] { new CurrencyCost(CurrencyType.Gold, amount) };
        // TODO: kiểm tra số dư qua Wallet; thiếu tiền thì trả UpgradeBlock.NotEnoughCurrency.
        return new UpgradeQuote(level, max, levels.GetUpgradePoints(type), required, costs, UpgradeBlock.None);
    }

    /// <inheritdoc/>
    public bool TryUpgrade(CharacterType type, out bool saved)
    {
        saved = false;
        // Kiểm tra lại: UI có thể đang giữ một quote cũ.
        UpgradeQuote quote = GetQuote(type);
        if (!quote.CanUpgrade) return false;
        // Nguyên liệu tiêu chính là EXP nạp vào thanh (1:1).
        int amount = (int)quote.Costs[0].Amount;
        // TODO: trừ tiền trong cùng một lần save với việc nạp điểm.
        return levels.TryAddUpgradePoints(type, amount, out saved);
    }
}

#endregion

#region Star Track

/// <summary>
/// Sao vĩnh viễn: đọc/ghi số sao qua <see cref="CharacterLevels"/>. Mỗi lần bấm mở thêm một sao tới <see cref="MaxStars"/>.
/// Giá bằng mảnh nhân vật và perk của từng sao làm ở Task S2/S3 (Docs/Architecture/Character-Stars-Tasks.md).
/// </summary>
internal sealed class StarTrack : IUpgradeTrack
{
    /// <summary>Số sao tối đa, khớp 5 ô sao trên UI.</summary>
    public const int MaxStars = 5;

    // Lưu ý: CỜ TEST TẠM: true = nâng sao miễn phí để test UI và save trước khi có mảnh nhân vật.
    // Task S2 thay bằng giá mảnh thật (CurrencyType.Shard) và xóa cờ này; đặt false để khóa lại nút nâng sao.
    // static readonly thay vì const để compiler không báo "unreachable code" cho nhánh còn lại.
    private static readonly bool FreeUpgradesForTesting = true;

    private readonly CharacterLevels levels;

    /// <summary>Dùng chung CharacterLevels của ProgressionManager (chủ duy nhất của key character_levels).</summary>
    public StarTrack(CharacterLevels levels)
    {
        this.levels = levels ?? throw new ArgumentNullException(nameof(levels));
    }

    /// <inheritdoc/>
    public UpgradeQuote GetQuote(CharacterType type)
    {
        // Save cũ có thể nhiều sao hơn trần hiện tại; chỉ hiển thị tới trần, không tự sửa save.
        int stars = Math.Min(levels.GetStars(type), MaxStars);
        if (!FreeUpgradesForTesting)
            return new UpgradeQuote(stars, MaxStars, 0, 0, Array.Empty<CurrencyCost>(), UpgradeBlock.NotAvailable);
        if (stars >= MaxStars)
            return new UpgradeQuote(stars, MaxStars, 0, 0, Array.Empty<CurrencyCost>(), UpgradeBlock.MaxReached);
        // Chưa có giá: danh sách Costs rỗng nên nút chỉ hiện chữ UPGRADE.
        return new UpgradeQuote(stars, MaxStars, 0, 0, Array.Empty<CurrencyCost>(), UpgradeBlock.None);
    }

    /// <inheritdoc/>
    public bool TryUpgrade(CharacterType type, out bool saved)
    {
        saved = false;
        // Kiểm tra lại: UI có thể đang giữ một quote cũ.
        if (!GetQuote(type).CanUpgrade) return false;
        return levels.TryAddStar(type, MaxStars, out saved);
    }
}

#endregion
