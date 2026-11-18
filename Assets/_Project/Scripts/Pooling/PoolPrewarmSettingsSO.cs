using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Shooter Game/Pooling/Prewarm Settings", fileName = "PoolPrewarmSettings")]
public sealed class PoolPrewarmSettingsSO : ScriptableObject
{
    [Tooltip("Số object tối đa tạo trong một batch trước khi nhường frame để Loading cập nhật.")]
    [Min(1)] public int objectsPerFrame = 16;

    [Serializable]
    public struct PrefabEntry
    {
        [Tooltip("Prefab cần chuẩn bị thêm trước lượt chơi; bắt buộc khi Count lớn hơn 0.")]
        public GameObject prefab;
        [Tooltip("Số clone chuẩn bị cho prefab này. 0 = bỏ qua; pool có thể tạo thêm khi vượt số lượng đã chuẩn bị.")]
        [Min(0)] public int count;
    }

    [Tooltip("Số vỏ CharacterView dùng chung được tạo sẵn cho Player, Enemy và Boss; không phải giới hạn số nhân vật.")]
    [Min(0)] public int characterShells = 64;
    [Tooltip("Số vỏ đạn dùng chung và số visual cho MỖI loại đạn được tạo sẵn. Tăng theo lượng đạn đồng thời dự kiến.")]
    [Min(0)] public int bullets = 256;
    [Tooltip("Số vỏ VFX và số visual tạo sẵn cho MỖI EffectType; nhiều loại dùng chung prefab sẽ cộng số clone.")]
    [Min(0)] public int effectShells = 16;
    [Tooltip("Số vỏ PickupView tạo sẵn cho EXP và tiền; hình ảnh lấy từ Loot Database.")]
    [Min(0)] public int pickups = 128;
    [Tooltip("Số clone visual tạo sẵn cho MỖI template thuộc phe Player.")]
    [Min(0)] public int playerVisuals = 1;
    [Tooltip("Số clone visual tạo sẵn cho MỖI template Enemy thường; Boss dùng Boss Visuals.")]
    [Min(0)] public int enemyVisuals = 64;
    [Tooltip("Số clone visual tạo sẵn cho MỖI template Enemy có Is Boss bật.")]
    [Min(0)] public int bossVisuals = 2;
    [Tooltip("Số đối tượng dữ liệu runtime nhân vật chuẩn bị sẵn; tách khỏi pool vỏ GameObject.")]
    [Min(0)] public int characterRuntimes = 64;
    [Tooltip("Số runtime hiệu ứng Slow chuẩn bị sẵn cho các mục tiêu đồng thời.")]
    [Min(0)] public int slowRuntimes = 32;
    [Tooltip("Số runtime sát thương định kỳ chuẩn bị sẵn, dùng cho các hiệu ứng như Burn/Poison.")]
    [Min(0)] public int damageOverTimeRuntimes = 64;
    [Tooltip("Danh sách prefab bổ sung như vùng skill hoặc object thế giới. Số lượng được cộng nếu nhiều entry dùng chung prefab.")]
    public PrefabEntry[] additionalPrefabs = Array.Empty<PrefabEntry>();

    public void Validate()
    {
        if (objectsPerFrame < 1 || characterShells < 0 || bullets < 0 || effectShells < 0 || pickups < 0 ||
            playerVisuals < 0 || enemyVisuals < 0 || bossVisuals < 0 || characterRuntimes < 0 ||
            slowRuntimes < 0 || damageOverTimeRuntimes < 0 || additionalPrefabs == null)
            throw new InvalidOperationException("Prewarm counts must be nonnegative.");
        foreach (PrefabEntry entry in additionalPrefabs)
            if (entry.count < 0 || (entry.count > 0 && entry.prefab == null))
                throw new InvalidOperationException("Assign every additional prewarm prefab with a positive count.");
    }
}
