
// * Bộ dữ liệu chỉ số cơ bản; Serializable cho phép Unity lưu và hiển thị khi được chứa trong field phù hợp.
// * CharacterRuntimeData sao chép bộ chỉ số này cho từng Player/Enemy và áp dụng bonus riêng của cá thể.
[System.Serializable]
public struct BaseStats
{

    #region Base Attributes

    [UnityEngine.Min(1f)] public float maxHealth;  // * Máu tối đa; máu hiện tại sẽ nằm riêng trong dữ liệu runtime.
    [UnityEngine.Min(0f)] public float damage;     // * Sát thương cơ bản, nhân với hệ số skill trước khi tính crit và defense.
    [UnityEngine.Min(0f)] public float defense;    // * Điểm phòng thủ; sát thương nhận = sát thương đầu vào × 100 / (100 + defense).
    [UnityEngine.Min(0f)] public float moveSpeed;  // * Tốc độ chạy, tính bằng đơn vị Unity mỗi giây.

    #endregion

    #region Combat Chances & Multipliers

    [UnityEngine.Min(0f)] public float critChance;            // * 1 = 100%, 1.5 = 150%; cho phép vượt 100% để tăng bậc crit.
    [UnityEngine.Min(1f)] public float critDamageMultiplier;  // * Tổng hệ số crit bậc 1; 1.5 tương ứng 150% sát thương đòn thường.

    #endregion

    #region Status Effect Accuracy & Resistance

    [UnityEngine.Min(0f)] public float dodge;     // * Điểm kháng hiệu ứng bất lợi; không né sát thương hoặc va chạm đạn.
    [UnityEngine.Min(0f)] public float accuracy;  // * Điểm chính xác hiệu ứng; đối kháng với dodge của mục tiêu.

    #endregion

    #region Default Template

    // * Trả về bản sao bộ chỉ số mặc định; new BaseStats() và default(BaseStats) vẫn có toàn bộ field bằng 0.
    public static BaseStats Default => new BaseStats
    {
        maxHealth = 100f,
        damage = 10f,
        defense = 0f,
        moveSpeed = 3f,
        critChance = 0.05f,
        critDamageMultiplier = 1.5f,
        dodge = 0f,
        accuracy = 0f
    };

    #endregion

}
