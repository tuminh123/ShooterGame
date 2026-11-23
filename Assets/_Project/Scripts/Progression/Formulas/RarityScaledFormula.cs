using System;
using UnityEngine;

/// <summary>
/// Một công thức dùng chung cho mọi độ hiếm, nhân thêm hệ số riêng của từng độ hiếm.
/// Lượng cần = <c>Formula.Evaluate(level) × hệ số của độ hiếm</c>, làm tròn lên.
/// Ví dụ công thức Linear Base 10, Step 5; Common ×1, Legendary ×5 → Lv1 → 2: Common cần 10, Legendary cần 50.
/// </summary>
/// <remarks>
/// <para>Vì sao một công thức + hệ số (không phải mỗi độ hiếm một công thức): tỉ lệ giữa các độ hiếm giữ nguyên ở mọi mốc,
/// nên dễ cân bằng; đổi đường giá chỉ cần đổi một công thức, đổi khoảng cách giữa các độ hiếm chỉ cần chỉnh vài hệ số.</para>
/// <para>Luật "càng hiếm càng tốn" được kiểm tra trong <see cref="Validate"/>: hệ số phải tăng dần theo độ hiếm.</para>
/// <para>Dùng chung cho trang bị (<see cref="EquipmentRarity"/>) và nhân vật (<see cref="CharacterRarity"/>). Enum độ hiếm phải
/// đánh số liên tục từ 0 theo thứ tự hiếm dần, vì hệ số được lưu theo vị trí (phần tử 0 = độ hiếm số 0).</para>
/// </remarks>
[Serializable]
public sealed class RarityScaledFormula<TRarity> where TRarity : struct, Enum
{
    #region Fields

    [Tooltip("Công thức chung cho mọi độ hiếm. Chọn kiểu công thức trong danh sách (Linear, Geometric...).")]
    [SerializeReference] private ILevelFormula formula = new LinearFormula();
    [Tooltip("Hệ số nhân của từng độ hiếm, theo thứ tự enum (phần tử 0 = độ hiếm thấp nhất). Phải tăng dần.")]
    [SerializeField] private float[] rarityMultipliers = CreateDefaultMultipliers();

    #endregion

    #region Constructors

    /// <summary>Constructor mặc định cho Unity: công thức Linear mặc định, mọi hệ số = 1.</summary>
    public RarityScaledFormula() { }

    /// <summary>Tạo với công thức và hệ số cho trước (dùng khi điền asset bằng code).</summary>
    public RarityScaledFormula(ILevelFormula formula, float[] rarityMultipliers)
    {
        this.formula = formula;
        this.rarityMultipliers = rarityMultipliers;
    }

    #endregion

    #region Queries

    /// <summary>Công thức chung đang chọn (Inspector dùng để hiện bảng xem trước).</summary>
    public ILevelFormula Formula => formula;

    /// <summary>Hệ số nhân của <paramref name="rarity"/>.</summary>
    public float GetMultiplier(TRarity rarity) => rarityMultipliers[ToIndex(rarity)];

    /// <summary>Lượng cần để lên từ <paramref name="level"/> lên level kế tiếp, ở độ hiếm <paramref name="rarity"/>. Luôn ≥ 1.</summary>
    public long Evaluate(int level, TRarity rarity)
    {
        Validate();
        double value = (double)formula.Evaluate(level) * GetMultiplier(rarity);
        return LevelFormulaGuard.RoundUp(value);
    }

    #endregion

    #region Validation

    /// <summary>
    /// Kiểm tra: đã chọn công thức và công thức hợp lệ; đủ một hệ số cho mỗi độ hiếm; hệ số dương và tăng dần
    /// (độ hiếm cao không được rẻ hơn độ hiếm thấp).
    /// </summary>
    public void Validate()
    {
        if (formula == null)
            throw new InvalidOperationException($"Choose a level formula for {typeof(TRarity).Name}.");
        formula.Validate();
        int count = RarityCount;
        if (rarityMultipliers == null || rarityMultipliers.Length != count)
            throw new InvalidOperationException($"Need exactly {count} rarity multipliers ({typeof(TRarity).Name}).");
        for (int i = 0; i < count; i++)
        {
            float multiplier = rarityMultipliers[i];
            if (float.IsNaN(multiplier) || float.IsInfinity(multiplier) || multiplier <= 0f)
                throw new InvalidOperationException($"Multiplier of {RarityName(i)} must be a positive number.");
            if (i > 0 && multiplier < rarityMultipliers[i - 1])
                throw new InvalidOperationException(FormattableString.Invariant(
                    $"Multiplier of {RarityName(i)} ({multiplier}) is lower than {RarityName(i - 1)} ({rarityMultipliers[i - 1]}): rarer must cost more."));
        }
    }

    #endregion

    #region Private Helpers

    // Số độ hiếm; đồng thời kiểm tra enum đánh số liên tục 0..n-1 (hệ số lưu theo vị trí).
    private static int RarityCount
    {
        get
        {
            Array values = Enum.GetValues(typeof(TRarity));
            for (int i = 0; i < values.Length; i++)
            {
                if (Convert.ToInt32(values.GetValue(i)) != i)
                    throw new InvalidOperationException($"{typeof(TRarity).Name} must be numbered 0..{values.Length - 1} in rarity order.");
            }
            return values.Length;
        }
    }

    /// <summary>Tên độ hiếm ở vị trí <paramref name="index"/>, dùng trong thông báo lỗi và Inspector.</summary>
    public static string RarityName(int index) => Enum.GetName(typeof(TRarity), index);

    private static int ToIndex(TRarity rarity)
    {
        int index = Convert.ToInt32(rarity);
        if (index < 0 || index >= RarityCount)
            throw new ArgumentOutOfRangeException(nameof(rarity));
        return index;
    }

    // Mặc định mọi độ hiếm cùng giá (hệ số 1); người thiết kế chỉnh lại trong Inspector.
    private static float[] CreateDefaultMultipliers()
    {
        var multipliers = new float[Enum.GetValues(typeof(TRarity)).Length];
        for (int i = 0; i < multipliers.Length; i++)
            multipliers[i] = 1f;
        return multipliers;
    }

    #endregion
}
