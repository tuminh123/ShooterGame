using System;

/// <summary>Một tác động lên một chỉ số; thời hạn và mục tiêu thuộc skill/effect sở hữu modifier.</summary>
[Serializable]
public struct StatModifier
{
    #region Configuration

    public StatType stat; // Chỉ số bị tác động.
    public StatOperation operation; // Cộng trực tiếp hoặc tăng/giảm tương đối.
    public float value; // Âm là giảm, dương là tăng.

    // Tạo config dùng chung cho skill buff, debuff và slow.
    public StatModifier(StatType stat, StatOperation operation, float value)
    {
        this.stat = stat;
        this.operation = operation;
        this.value = value;
    }

    #endregion

    #region Validation

    // Không đưa enum sai hoặc NaN/infinity vào phép tính chỉ số.
    public void Validate()
    {
        if (!IsDefined(DefinedStats, (int)stat) || !IsDefined(DefinedOperations, (int)operation) ||
            float.IsNaN(value) || float.IsInfinity(value))
            throw new ArgumentException("Invalid stat modifier.");
        // Bonus chiến đấu có gốc 0: "+x% của 0" không có nghĩa, nên chỉ cho cộng thẳng.
        if (StatTypes.IsCombatBonus(stat) && operation != StatOperation.Add)
            throw new ArgumentException($"Combat bonus stat {stat} only supports Add.");
    }

    // Enum.IsDefined boxing giá trị mỗi lần gọi, mà Validate chạy mỗi lần thêm buff/slow: tra bảng dựng sẵn một lần.
    private static readonly bool[] DefinedStats = BuildDefined(typeof(StatType));
    private static readonly bool[] DefinedOperations = BuildDefined(typeof(StatOperation));

    private static bool IsDefined(bool[] table, int value) => value >= 0 && value < table.Length && table[value];

    private static bool[] BuildDefined(Type enumType)
    {
        int max = 0;
        Array values = Enum.GetValues(enumType);
        foreach (object value in values) max = Math.Max(max, Convert.ToInt32(value));
        var table = new bool[max + 1];
        foreach (object value in values)
        {
            int index = Convert.ToInt32(value);
            if (index >= 0) table[index] = true;
        }
        return table;
    }

    #endregion
}
