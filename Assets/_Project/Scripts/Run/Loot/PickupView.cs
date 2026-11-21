using UnityEngine;

/// <summary>Vật phẩm nằm dưới đất; LootSystem (miền Run) kiểm tra khoảng cách nhặt, không cần collider hay Update riêng.</summary>
[RequireComponent(typeof(SpriteRenderer))]
public sealed class PickupView : MonoBehaviour
{
    #region Pickup Data

    /// <summary>Chọn phần thưởng EXP hoặc vàng.</summary>
    public PickupType Type { get; private set; }
    /// <summary>Lượng nhận khi nhặt, được chốt tại lúc rơi.</summary>
    public int Amount { get; private set; }
    private SpriteRenderer spriteRenderer; // Cache renderer của clone trong pool.

    #endregion

    #region Spawn & Reset

    /// <summary>Nạp dữ liệu cho vỏ vừa lấy từ pool; chốt loại và lượng thưởng tại lúc rơi.</summary>
    internal void Initialize(PickupType type, int amount, Sprite sprite)
    {
        Type = type;
        Amount = amount;
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        spriteRenderer.sprite = sprite;
        spriteRenderer.color = Color.white;
        // Pool khôi phục scale từ prefab mỗi lần spawn; giữ nguyên kích thước đã chỉnh trong Inspector.
    }

    private void OnDisable()
    {
        Amount = 0;
        Type = default;
    }

    #endregion
}
