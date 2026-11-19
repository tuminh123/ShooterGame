using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Cấu hình số lượng object/runtime tạo sẵn trước lượt chơi. Chỉ đọc lúc chạy;
/// <c>GamePrewarm</c> và installer đọc qua getter để không ai ghi nhầm vào asset dùng chung.
/// </summary>
/// <remarks>
/// Tên field private giữ nguyên tên cũ (trước đây là field public) để Unity vẫn đọc được giá trị đã lưu trong asset.
/// </remarks>
[CreateAssetMenu(menuName = "Shooter Game/Pooling/Prewarm Settings", fileName = "PoolPrewarmSettings")]
public sealed class PoolPrewarmSettingsSO : ScriptableObject
{
    #region Types

    /// <summary>Một prefab bổ sung cần tạo sẵn cùng số clone.</summary>
    [Serializable]
    public struct PrefabEntry
    {
        [Tooltip("Prefab cần chuẩn bị thêm trước lượt chơi; bắt buộc khi Count lớn hơn 0.")]
        [SerializeField] private GameObject prefab;
        [Tooltip("Số clone chuẩn bị cho prefab này. 0 = bỏ qua; pool có thể tạo thêm khi vượt số lượng đã chuẩn bị.")]
        [SerializeField, Min(0)] private int count;

        /// <summary>Prefab cần tạo sẵn.</summary>
        public GameObject Prefab => prefab;
        /// <summary>Số clone tạo sẵn; 0 nghĩa là bỏ qua.</summary>
        public int Count => count;
    }

    #endregion

    #region Fields

    [Tooltip("Số object tối đa tạo trong một batch trước khi nhường frame để Loading cập nhật.")]
    [SerializeField, Min(1)] private int objectsPerFrame = 16;

    [Tooltip("Số vỏ CharacterView dùng chung được tạo sẵn cho Player, Enemy và Boss; không phải giới hạn số nhân vật.")]
    [SerializeField, Min(0)] private int characterShells = 64;
    [Tooltip("Số vỏ đạn dùng chung và số visual cho MỖI loại đạn được tạo sẵn. Tăng theo lượng đạn đồng thời dự kiến.")]
    [SerializeField, Min(0)] private int bullets = 256;
    [Tooltip("Số vỏ VFX và số visual tạo sẵn cho MỖI EffectType; nhiều loại dùng chung prefab sẽ cộng số clone.")]
    [SerializeField, Min(0)] private int effectShells = 16;
    [Tooltip("Số vỏ PickupView tạo sẵn cho EXP và tiền; hình ảnh lấy từ Loot Database.")]
    [SerializeField, Min(0)] private int pickups = 128;
    [Tooltip("Số clone visual tạo sẵn cho MỖI template thuộc phe Player.")]
    [SerializeField, Min(0)] private int playerVisuals = 1;
    [Tooltip("Số clone visual tạo sẵn cho MỖI template Enemy thường; Boss dùng Boss Visuals.")]
    [SerializeField, Min(0)] private int enemyVisuals = 64;
    [Tooltip("Số clone visual tạo sẵn cho MỖI template Enemy có Is Boss bật.")]
    [SerializeField, Min(0)] private int bossVisuals = 2;
    [Tooltip("Số đối tượng dữ liệu runtime nhân vật chuẩn bị sẵn; tách khỏi pool vỏ GameObject.")]
    [SerializeField, Min(0)] private int characterRuntimes = 64;
    [Tooltip("Số runtime hiệu ứng Slow chuẩn bị sẵn cho các mục tiêu đồng thời.")]
    [SerializeField, Min(0)] private int slowRuntimes = 32;
    [Tooltip("Số runtime sát thương định kỳ chuẩn bị sẵn, dùng cho các hiệu ứng như Burn/Poison.")]
    [SerializeField, Min(0)] private int damageOverTimeRuntimes = 64;
    [Tooltip("Danh sách prefab bổ sung như vùng skill hoặc object thế giới. Số lượng được cộng nếu nhiều entry dùng chung prefab.")]
    [SerializeField] private PrefabEntry[] additionalPrefabs = Array.Empty<PrefabEntry>();

    #endregion

    #region Properties

    /// <summary>Số object tối đa tạo trong một batch trước khi nhường frame.</summary>
    public int ObjectsPerFrame => objectsPerFrame;
    /// <summary>Số vỏ <c>CharacterView</c> dùng chung tạo sẵn.</summary>
    public int CharacterShells => characterShells;
    /// <summary>Số vỏ đạn và số visual cho mỗi loại đạn tạo sẵn.</summary>
    public int Bullets => bullets;
    /// <summary>Số vỏ VFX và visual cho mỗi EffectType tạo sẵn.</summary>
    public int EffectShells => effectShells;
    /// <summary>Số vỏ pickup (EXP, tiền) tạo sẵn.</summary>
    public int Pickups => pickups;
    /// <summary>Số visual tạo sẵn cho mỗi template phe Player.</summary>
    public int PlayerVisuals => playerVisuals;
    /// <summary>Số visual tạo sẵn cho mỗi template Enemy thường.</summary>
    public int EnemyVisuals => enemyVisuals;
    /// <summary>Số visual tạo sẵn cho mỗi template Boss.</summary>
    public int BossVisuals => bossVisuals;
    /// <summary>Số runtime nhân vật tạo sẵn.</summary>
    public int CharacterRuntimes => characterRuntimes;
    /// <summary>Số runtime hiệu ứng Slow tạo sẵn.</summary>
    public int SlowRuntimes => slowRuntimes;
    /// <summary>Số runtime sát thương định kỳ (Burn/Poison) tạo sẵn.</summary>
    public int DamageOverTimeRuntimes => damageOverTimeRuntimes;
    /// <summary>Prefab bổ sung cần tạo sẵn; trả về dạng chỉ đọc để không ai sửa mảng của asset.</summary>
    public IReadOnlyList<PrefabEntry> AdditionalPrefabs => additionalPrefabs;

    #endregion

    #region Validation

    /// <summary>Chặn cấu hình âm hoặc entry thiếu prefab trước khi installer tạo service.</summary>
    public void Validate()
    {
        if (objectsPerFrame < 1 || characterShells < 0 || bullets < 0 || effectShells < 0 || pickups < 0 ||
            playerVisuals < 0 || enemyVisuals < 0 || bossVisuals < 0 || characterRuntimes < 0 ||
            slowRuntimes < 0 || damageOverTimeRuntimes < 0 || additionalPrefabs == null)
            throw new InvalidOperationException("Prewarm counts must be nonnegative.");
        foreach (PrefabEntry entry in additionalPrefabs)
            if (entry.Count < 0 || (entry.Count > 0 && entry.Prefab == null))
                throw new InvalidOperationException("Assign every additional prewarm prefab with a positive count.");
    }

    #endregion
}
