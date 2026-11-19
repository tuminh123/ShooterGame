using System;

// Họ strategy NỘI BỘ của Progression: mỗi loại nâng cấp (CharacterUpgradeKind) là một track.
// ProgressionManager chọn track bằng một switch; miền khác chỉ thấy ICharacterUpgrades.
// Không bind Zenject. Demo chưa kiểm tra và chưa trừ tiền (xem các TODO).

#region Track Contract

/// <summary>Một loại nâng cấp vĩnh viễn: hỏi giá và nâng một cấp.</summary>
internal interface IUpgradeTrack
{
    /// <summary>Cấp hiện tại/tối đa, giá và lý do bị chặn.</summary>
    UpgradeQuote GetQuote(CharacterType type);

    /// <summary>Nâng một cấp nếu <see cref="GetQuote"/> cho phép; <paramref name="saved"/> là kết quả ghi file.</summary>
    bool TryUpgrade(CharacterType type, out bool saved);
}

#endregion

#region Level Track

/// <summary>Level vĩnh viễn: đọc trần và giá từ CharacterLevelConfigSO qua <see cref="CharacterLevels"/>.</summary>
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
        int level = levels.GetLevel(type);
        int max = levels.GetConfig(type).MaxLevel;
        if (!levels.TryGetUpgradeCost(type, out long cost))
            return new UpgradeQuote(Math.Min(level, max), max, Array.Empty<CurrencyCost>(), UpgradeBlock.MaxReached);

        // Demo tạm dùng Gold; loại tiền sẽ nằm trong config khi làm phần trả tiền.
        var costs = new[] { new CurrencyCost(CurrencyType.Gold, cost) };
        // TODO: kiểm tra số dư qua Wallet; thiếu tiền thì trả UpgradeBlock.NotEnoughCurrency.
        return new UpgradeQuote(level, max, costs, UpgradeBlock.None);
    }

    /// <inheritdoc/>
    public bool TryUpgrade(CharacterType type, out bool saved)
    {
        saved = false;
        // Kiểm tra lại: UI có thể đang giữ một quote cũ.
        if (!GetQuote(type).CanUpgrade) return false;
        // TODO: trừ tiền trong cùng một lần save với việc tăng level.
        return levels.TryIncreaseLevel(type, out saved);
    }
}

#endregion

#region Star Track

/// <summary>Sao: chưa có hệ thống (kế hoạch riêng, hướng C), nên luôn bị chặn với <see cref="UpgradeBlock.NotAvailable"/>.</summary>
internal sealed class StarTrack : IUpgradeTrack
{
    /// <summary>Số sao tối đa, khớp 5 ô sao trên UI.</summary>
    public const int MaxStars = 5;

    /// <inheritdoc/>
    public UpgradeQuote GetQuote(CharacterType type)
        => new UpgradeQuote(0, MaxStars, Array.Empty<CurrencyCost>(), UpgradeBlock.NotAvailable);

    /// <inheritdoc/>
    public bool TryUpgrade(CharacterType type, out bool saved)
    {
        saved = false;
        return false;
    }
}

#endregion
