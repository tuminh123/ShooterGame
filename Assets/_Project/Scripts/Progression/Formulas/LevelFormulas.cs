using System;
using UnityEngine;

// Các công thức "lượng cần cho một mốc cấp" (strategy của ILevelFormula). Asset chọn công thức qua [SerializeReference].
// Thêm công thức mới: viết một class [Serializable] có constructor mặc định, implement ILevelFormula; Inspector tự hiện trong danh sách chọn.

#region Linear

/// <summary>
/// Cộng đều: mỗi mốc đắt hơn mốc trước một lượng cố định.
/// Lượng cần ở level L = <c>Base + Step × (L − 1)</c>. Ví dụ Base 100, Step 50 → 100, 150, 200, ...
/// </summary>
/// <remarks>Dễ đoán, hợp với đầu game. Step = 0 thì mọi mốc cần cùng một lượng.</remarks>
[Serializable]
public sealed class LinearFormula : ILevelFormula
{
    [Tooltip("Lượng cần cho mốc đầu tiên (Lv1 → 2); tối thiểu 1.")]
    [SerializeField, Min(1)] private int baseValue = 100;
    [Tooltip("Lượng cộng thêm cho mỗi level sau Lv1. Đặt 0 để mọi mốc cần cùng một lượng.")]
    [SerializeField, Min(0)] private int step = 50;

    /// <summary>Constructor mặc định cho Unity và bộ chọn công thức trong Inspector.</summary>
    public LinearFormula() { }

    /// <summary>Tạo công thức với tham số cho trước (dùng khi điền asset bằng code).</summary>
    public LinearFormula(int baseValue, int step)
    {
        this.baseValue = baseValue;
        this.step = step;
    }

    /// <summary>Lượng cần cho mốc Lv1 → 2.</summary>
    public int BaseValue => baseValue;

    /// <summary>Lượng cộng thêm mỗi level.</summary>
    public int Step => step;

    /// <inheritdoc/>
    public void Validate()
    {
        if (baseValue < 1 || step < 0)
            throw new InvalidOperationException("Linear formula requires Base >= 1 and Step >= 0.");
    }

    /// <inheritdoc/>
    public long Evaluate(int level)
    {
        LevelFormulaGuard.EnsureLevel(level);
        return checked(baseValue + (long)step * (level - 1));
    }
}

#endregion

#region Geometric

/// <summary>
/// Nhân dần: mỗi mốc đắt hơn mốc trước theo cùng một tỉ lệ.
/// Lượng cần ở level L = <c>Base × Multiplier^(L − 1)</c>, làm tròn lên. Ví dụ Base 100, ×1.15 → 100, 115, 133, ...
/// </summary>
/// <remarks>
/// Đầu game rẻ, càng về cuối càng tăng vọt (đường giá phổ biến của game RPG/idle).
/// Cẩn thận với Multiplier lớn khi Max Level cao: ×1.15 tới Lv100 đã lên hàng triệu.
/// </remarks>
[Serializable]
public sealed class GeometricFormula : ILevelFormula
{
    [Tooltip("Lượng cần cho mốc đầu tiên (Lv1 → 2); tối thiểu 1.")]
    [SerializeField, Min(1)] private int baseValue = 100;
    [Tooltip("Mỗi level nhân lượng cần với hệ số này. 1 = mọi mốc như nhau; 1.15 = mỗi mốc đắt hơn 15%.")]
    [SerializeField, Min(1f)] private float multiplier = 1.15f;

    /// <summary>Constructor mặc định cho Unity và bộ chọn công thức trong Inspector.</summary>
    public GeometricFormula() { }

    /// <summary>Tạo công thức với tham số cho trước (dùng khi điền asset bằng code).</summary>
    public GeometricFormula(int baseValue, float multiplier)
    {
        this.baseValue = baseValue;
        this.multiplier = multiplier;
    }

    /// <summary>Lượng cần cho mốc Lv1 → 2.</summary>
    public int BaseValue => baseValue;

    /// <summary>Hệ số nhân mỗi level.</summary>
    public float Multiplier => multiplier;

    /// <inheritdoc/>
    public void Validate()
    {
        if (baseValue < 1 || float.IsNaN(multiplier) || float.IsInfinity(multiplier) || multiplier < 1f)
            throw new InvalidOperationException("Geometric formula requires Base >= 1 and a finite Multiplier >= 1.");
    }

    /// <inheritdoc/>
    public long Evaluate(int level)
    {
        LevelFormulaGuard.EnsureLevel(level);
        // Tính bằng double rồi làm tròn lên; bỏ phần sai số nhỏ của số thực để 100 × 1.0 không thành 101.
        double value = baseValue * Math.Pow(multiplier, level - 1);
        return LevelFormulaGuard.RoundUp(value);
    }
}

#endregion

#region Shared Guards

/// <summary>Kiểm tra dùng chung cho mọi công thức, để các class công thức chỉ chứa đúng phép tính của mình.</summary>
internal static class LevelFormulaGuard
{
    /// <summary>Level bắt đầu từ 1; level nhỏ hơn là lỗi của bên gọi.</summary>
    public static void EnsureLevel(int level)
    {
        if (level < 1) throw new ArgumentOutOfRangeException(nameof(level), "Level starts at 1.");
    }

    /// <summary>
    /// Làm tròn lên thành số nguyên ≥ 1. Trừ một sai số rất nhỏ trước khi làm tròn để 150.0000001 vẫn là 150.
    /// Vượt <c>long</c> thì báo lỗi tràn số thay vì trả số sai.
    /// </summary>
    public static long RoundUp(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value) || value >= long.MaxValue)
            throw new OverflowException("Level formula result is too large.");
        return Math.Max(1L, (long)Math.Ceiling(value - 1e-6));
    }
}

#endregion
