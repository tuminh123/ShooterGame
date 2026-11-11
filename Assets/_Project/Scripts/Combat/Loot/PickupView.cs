using UnityEngine;

public enum PickupType { Experience, Gold }

// Vật phẩm nằm dưới đất; LootManager kiểm tra khoảng cách nhặt, không cần collider hay Update riêng.
[RequireComponent(typeof(SpriteRenderer))]
public sealed class PickupView : MonoBehaviour
{
    #region Pickup Data

    public PickupType Type { get; private set; } // Chọn phần thưởng EXP hoặc vàng.
    public int Amount { get; private set; } // Lượng nhận khi nhặt, được chốt tại lúc rơi.
    private SpriteRenderer spriteRenderer; // Cache renderer của clone trong pool.

    #endregion

    #region Spawn & Reset

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
