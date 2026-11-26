using System;

/// <summary>
/// Công thức quay của gacha (logic thuần, không Unity): trọng số → pity (soft/hard) → bảo đảm theo nhóm lượt.
/// Dùng chung cho mọi banner; Inspector và kiểm tra tự động cũng gọi để tính tỉ lệ thực.
/// </summary>
/// <remarks>
/// <code>
/// Xác suất bậc i          = w_i / Σw                     (chỉ các bậc pool có)
/// Bậc pity ở lượt thứ k   = p                     khi k &lt; S
///                         = p + Δ·(k − S + 1)     khi S ≤ k &lt; N
///                         = 1                     khi k = N
/// Lượt trung bình         E = Σ k · p_k · Π_{j&lt;k}(1 − p_j);   tỉ lệ thực = 1 / E
/// Bảo đảm                 lượt thứ G liên tiếp chưa có bậc ≥ T thì chỉ quay trong các bậc ≥ T
/// </code>
/// p = tỉ lệ gốc của bậc pity, S = Soft Pity Start, Δ = Soft Pity Step, N = Hard Pity, T = Guarantee Tier, G = Guarantee Every.
/// </remarks>
public static class GachaRoller
{
    #region Roll

    /// <summary>
    /// Quay một lượt và cập nhật bộ đếm pity <paramref name="counter"/> (bản sao của bên gọi: chỉ chép vào save khi ghi thành công).
    /// </summary>
    public static GachaTier Roll(GachaBannerSO banner, GachaSaveData.Banner counter, Random random)
    {
        counter.sinceTop++;
        counter.sinceGuarantee++;
        GachaTier tier;
        if (random.NextDouble() < TopChance(banner, counter.sinceTop)) tier = banner.PityTier;
        else
        {
            bool forced = banner.GuaranteeEvery > 0 && counter.sinceGuarantee >= banner.GuaranteeEvery;
            // Bậc pity đã quay riêng ở trên, nên ở đây chỉ chia giữa các bậc còn lại (tỉ lệ gốc tổng vẫn đúng w_i / Σw).
            if (!TryPick(banner, forced ? banner.GuaranteeTier : (GachaTier)0, random, out tier))
                tier = forced && Weight(banner, banner.PityTier) > 0f ? banner.PityTier : Fallback(banner, random);
        }
        if (tier == banner.PityTier) counter.sinceTop = 0;
        if (tier >= banner.GuaranteeTier) counter.sinceGuarantee = 0;
        return tier;
    }

    // Không còn bậc nào ngoài bậc pity: trả bậc pity (banner chỉ có một bậc).
    private static GachaTier Fallback(GachaBannerSO banner, Random random)
        => TryPick(banner, (GachaTier)0, random, out GachaTier tier) ? tier : banner.PityTier;

    // Quay theo trọng số trong các bậc >= minTier, trừ bậc pity.
    private static bool TryPick(GachaBannerSO banner, GachaTier minTier, Random random, out GachaTier tier)
    {
        float total = 0f;
        foreach (GachaBannerSO.Rate rate in banner.Rates)
            if (rate.Tier >= minTier && rate.Tier != banner.PityTier) total += Weight(banner, rate.Tier);
        tier = default;
        if (total <= 0f) return false;
        double roll = random.NextDouble() * total;
        foreach (GachaBannerSO.Rate rate in banner.Rates)
        {
            if (rate.Tier < minTier || rate.Tier == banner.PityTier) continue;
            float weight = Weight(banner, rate.Tier);
            if (weight <= 0f) continue;
            tier = rate.Tier;
            roll -= weight;
            if (roll < 0d) return true;
        }
        return true; // Sai số làm tròn: lấy bậc hợp lệ cuối cùng.
    }

    #endregion

    #region Rates

    /// <summary>Tổng trọng số của các bậc pool có.</summary>
    public static float TotalWeight(GachaBannerSO banner)
    {
        float total = 0f;
        foreach (GachaBannerSO.Rate rate in banner.Rates) total += Weight(banner, rate.Tier);
        return total;
    }

    /// <summary>Tỉ lệ gốc của một bậc (0..1), chưa tính pity. Hiện trên UI.</summary>
    public static float BaseChance(GachaBannerSO banner, GachaTier tier)
    {
        float total = TotalWeight(banner);
        return total > 0f ? Weight(banner, tier) / total : 0f;
    }

    /// <summary>Xác suất ra bậc pity ở lượt thứ <paramref name="pull"/> kể từ lần ra gần nhất (tính cả lượt này).</summary>
    public static float TopChance(GachaBannerSO banner, int pull)
    {
        float chance = BaseChance(banner, banner.PityTier);
        if (chance <= 0f) return 0f;
        if (banner.HardPity > 0 && pull >= banner.HardPity) return 1f;
        if (banner.SoftPityStart > 0 && pull >= banner.SoftPityStart)
            chance += banner.SoftPityStep * (pull - banner.SoftPityStart + 1);
        return Math.Min(1f, chance);
    }

    /// <summary>Trung bình bao nhiêu lượt thì ra bậc pity (E). Tỉ lệ thực = 1 / E. 0 = không bao giờ ra.</summary>
    public static float AveragePullsForTop(GachaBannerSO banner)
    {
        if (BaseChance(banner, banner.PityTier) <= 0f) return 0f;
        int limit = banner.HardPity > 0 ? banner.HardPity : 100000;
        double survive = 1d, expected = 0d;
        for (int pull = 1; pull <= limit && survive > 1e-9; pull++)
        {
            double chance = TopChance(banner, pull);
            expected += pull * survive * chance;
            survive *= 1d - chance;
        }
        return (float)expected;
    }

    // Bậc pool không có thì coi như trọng số 0.
    private static float Weight(GachaBannerSO banner, GachaTier tier)
    {
        if (banner.Pool == null || !banner.Pool.HasTier(tier)) return 0f;
        foreach (GachaBannerSO.Rate rate in banner.Rates)
            if (rate.Tier == tier) return rate.Weight;
        return 0f;
    }

    #endregion
}
