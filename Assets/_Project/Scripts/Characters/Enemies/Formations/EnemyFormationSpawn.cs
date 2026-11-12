using System;
using UnityEngine;

/// <summary>Một lượt spawn đội hình trong Wave. Đây là class dữ liệu thuần, không phải MonoBehaviour/DataSO.</summary>
[Serializable]
public sealed class EnemyFormationSpawn
{
    [Tooltip("Hình dùng riêng cho lượt này. Số slot quyết định số enemy được tạo cùng lúc.")]
    public EnemyFormationSO formation;

    [Tooltip("Đường dùng riêng cho lượt này. Hai lượt có thể dùng hình giống nhau nhưng đường khác nhau.")]
    public EnemyPathSO path;

    [Tooltip("Thông số riêng của lượt: tốc độ, số lần chạy đường, nối đuôi/giữ khối, knockback và choáng.")]
    public EnemyFormationSettings settings = new EnemyFormationSettings();

    public void Validate()
    {
        if (formation == null || path == null || settings == null)
        {
            throw new InvalidOperationException("Mỗi lượt đội hình cần đủ Formation, Path và thông số di chuyển.");
        }

        formation.Validate();
        path.Validate();
        settings.Validate();
    }
}
