using System;

// * Lượng chỉ số cộng thêm từ level; mặc định toàn bộ bằng 0, khác với bộ chỉ số gốc BaseStats.
[Serializable]
public struct StatBonus
{
    #region Additive Bonuses

    public float maxHealth;             // * Số HP tối đa cộng thêm, không tự hồi máu hiện tại.
    public float damage;                // * Sát thương cơ bản cộng thêm.
    public float defense;               // * Điểm phòng thủ cộng thêm.
    public float moveSpeed;             // * Tốc độ cộng thêm, đơn vị Unity mỗi giây.
    public float critChance;            // * Cộng theo điểm phần trăm; 0.05 tương ứng thêm 5 điểm phần trăm crit.
    public float critDamageMultiplier;  // * Phần cộng vào hệ số; +0.25 biến 1.5 thành 1.75.
    public float dodge;                 // * Điểm kháng hiệu ứng bất lợi cộng thêm.
    public float accuracy;              // * Điểm chính xác hiệu ứng cộng thêm.

    #endregion

    #region Bonus Aggregation

    // * Cộng hai nguồn bonus theo từng field; không nhân phần trăm hoặc thay đổi template.
    public static StatBonus operator +(StatBonus left, StatBonus right)
    {
        return new StatBonus
        {
            maxHealth = left.maxHealth + right.maxHealth,
            damage = left.damage + right.damage,
            defense = left.defense + right.defense,
            moveSpeed = left.moveSpeed + right.moveSpeed,
            critChance = left.critChance + right.critChance,
            critDamageMultiplier = left.critDamageMultiplier + right.critDamageMultiplier,
            dodge = left.dodge + right.dodge,
            accuracy = left.accuracy + right.accuracy
        };
    }

    // * Nhân một bộ bonus với số lần đã lên cấp; không cần duyệt bảng từng level.
    public static StatBonus operator *(StatBonus bonus, int count)
    {
        if (count < 0)
            throw new ArgumentOutOfRangeException(nameof(count));

        return new StatBonus
        {
            maxHealth = bonus.maxHealth * count,
            damage = bonus.damage * count,
            defense = bonus.defense * count,
            moveSpeed = bonus.moveSpeed * count,
            critChance = bonus.critChance * count,
            critDamageMultiplier = bonus.critDamageMultiplier * count,
            dodge = bonus.dodge * count,
            accuracy = bonus.accuracy * count
        };
    }

    // * Tạo bản chỉ số mới từ chỉ số gốc và bonus; giới hạn theo quy tắc chỉ số, không thay đổi bản gốc.
    // ! Chỉ xử lý cộng thẳng; buff phần trăm và máu hiện tại sẽ do hệ thống runtime phụ trách.
    public BaseStats ApplyTo(BaseStats source)
    {
        source.maxHealth = UnityEngine.Mathf.Max(1f, source.maxHealth + maxHealth);
        source.damage = UnityEngine.Mathf.Max(0f, source.damage + damage);
        source.defense = UnityEngine.Mathf.Max(0f, source.defense + defense);
        source.moveSpeed = UnityEngine.Mathf.Max(0f, source.moveSpeed + moveSpeed);
        source.critChance = UnityEngine.Mathf.Max(0f, source.critChance + critChance);
        source.critDamageMultiplier = UnityEngine.Mathf.Max(1f, source.critDamageMultiplier + critDamageMultiplier);
        source.dodge = UnityEngine.Mathf.Max(0f, source.dodge + dodge);
        source.accuracy = UnityEngine.Mathf.Max(0f, source.accuracy + accuracy);
        return source;
    }

    #endregion
}
