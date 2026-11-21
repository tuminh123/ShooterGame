using System;
using UnityEngine;

/// <summary>
/// Một perk mở khi nhân vật đạt một sao: mô tả hiển thị trên tab Stars và bonus chỉ số cộng vĩnh viễn.
/// </summary>
/// <remarks>Nằm trong <see cref="CharacterLevelConfigSO"/>; chỉ là dữ liệu, không giữ trạng thái đã mở.</remarks>
[Serializable]
public sealed class StarPerk
{
    [Tooltip("Mô tả hiện trên tab Stars, ví dụ \"+50 Max Health\".")]
    [SerializeField] private string description;
    [Tooltip("Icon của perk trên tab Stars; để trống thì giữ icon mặc định của dòng.")]
    [SerializeField] private Sprite icon;
    [Tooltip("Bonus cộng thẳng khi đã mở sao này; cộng chung với bonus level vĩnh viễn.")]
    [SerializeField] private StatBonus bonus;

    /// <summary>Mô tả hiển thị.</summary>
    public string Description => description;

    /// <summary>Icon hiển thị; có thể null.</summary>
    public Sprite Icon => icon;

    /// <summary>Bonus chỉ số của perk.</summary>
    public StatBonus Bonus => bonus;
}
