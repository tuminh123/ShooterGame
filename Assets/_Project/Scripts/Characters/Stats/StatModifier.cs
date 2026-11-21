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
        if (!Enum.IsDefined(typeof(StatType), stat) || !Enum.IsDefined(typeof(StatOperation), operation) ||
            float.IsNaN(value) || float.IsInfinity(value))
            throw new ArgumentException("Invalid stat modifier.");
    }

    #endregion
}
