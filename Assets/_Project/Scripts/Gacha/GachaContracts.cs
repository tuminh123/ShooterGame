using System;
using System.Collections.Generic;

#region Gacha (UI)

/// <summary>Kết quả một lần quay: lý do bị chặn hoặc các phần thưởng thực tế (nhân vật trùng đã thành mảnh).</summary>
public sealed class GachaPullResult
{
    public GachaPullResult(PurchaseBlock block) => Block = block;

    public GachaPullResult(RewardResult rewards, IReadOnlyList<GachaTier> tiers, bool saved)
    {
        Block = PurchaseBlock.None;
        Rewards = rewards;
        Tiers = tiers;
        Saved = saved;
        foreach (GachaTier tier in tiers)
            if (tier > HighestTier) HighestTier = tier;
    }

    /// <summary>None = đã quay; khác None thì không có gì thay đổi.</summary>
    public PurchaseBlock Block { get; }

    /// <summary>Phần thưởng thực tế đã trao và lưu.</summary>
    public RewardResult Rewards { get; }

    /// <summary>Bậc của từng lượt theo thứ tự quay.</summary>
    public IReadOnlyList<GachaTier> Tiers { get; } = Array.Empty<GachaTier>();

    /// <summary>Bậc cao nhất trong lần quay (UI đổi màu rương theo bậc này).</summary>
    public GachaTier HighestTier { get; }

    /// <summary>false = đã trao trong RAM nhưng ghi file lỗi (snapshot chờ ghi lại).</summary>
    public bool Saved { get; }
}

/// <summary>Gacha tướng và trang bị. Dùng bởi màn Gacha.</summary>
public interface IGacha
{
    /// <summary>Các banner theo thứ tự cấu hình.</summary>
    IReadOnlyList<GachaBannerSO> Banners { get; }

    /// <summary>Quay <paramref name="count"/> lượt (1 hoặc 10) được không lúc này. None = được.</summary>
    PurchaseBlock GetBlock(GachaBannerSO banner, int count);

    /// <summary>Còn bao nhiêu lượt nữa thì chắc chắn ra bậc pity; 0 nếu không có hard pity hoặc pool chưa có món ở bậc đó.</summary>
    int PullsUntilPity(GachaBannerSO banner);

    /// <summary>Trừ tiền, quay, trao quà và lưu pity trong MỘT lần ghi.</summary>
    GachaPullResult Pull(GachaBannerSO banner, int count);

    /// <summary>Phát sau mỗi lần quay (pity đổi).</summary>
    event Action Changed;
}

#endregion
