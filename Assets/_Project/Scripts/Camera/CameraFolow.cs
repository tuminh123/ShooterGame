using UnityEngine;
using Zenject;

// * Camera bám theo mục tiêu với độ trễ mượt; cập nhật sau bước di chuyển thông thường.
public class CameraFolow : MonoBehaviour
{

    #region Follow Settings & State

    private Transform target;              // * Nhân vật hiện tại do PlayerManager cung cấp.
    private PlayerManager playerManager;   // * Nguồn sự kiện tạo/gỡ hoặc thay nhân vật.
    // * Khoảng lệch so với mục tiêu, tính bằng world unit; Z = -10 đặt camera trước mặt phẳng 2D.
    [SerializeField] private Vector3 offset = new Vector3(0f, 0f, -10f);
    // * Thời gian làm mượt gần đúng, tính bằng giây; nhỏ hơn giúp camera bám sát hơn.
    [SerializeField, Min(0f)] private float followSmoothTime = 0.12f;

    private Vector3 velocity;  // * Vận tốc nội bộ của SmoothDamp, được lưu giữa các frame để chuyển động liên tục.

    #endregion

    #region Player Tracking

    // * SceneContext inject; nhận player hiện tại dù manager tạo trước hay sau camera.
    [Inject]
    public void Construct(PlayerManager manager)
    {
        if (playerManager != null)
            playerManager.PlayerChanged -= OnPlayerChanged;
        playerManager = manager;
        if (isActiveAndEnabled)
            OnEnable();
    }

    // * Đăng ký khi camera bật và đọc trạng thái hiện tại, tránh bỏ lỡ sự kiện đã phát.
    private void OnEnable()
    {
        if (playerManager == null)
            return;
        playerManager.PlayerChanged -= OnPlayerChanged;
        playerManager.PlayerChanged += OnPlayerChanged;
        OnPlayerChanged(playerManager.CurrentPlayer);
    }

    // * Hủy subscription khi tắt để manager không giữ camera không còn dùng.
    private void OnDisable()
    {
        if (playerManager != null)
            playerManager.PlayerChanged -= OnPlayerChanged;
    }

    // * Nhận nhân vật mới và reset quán tính bám mục tiêu cũ.
    private void OnPlayerChanged(CharacterView player)
    {
        target = player != null ? player.transform : null;
        velocity = Vector3.zero;
    }

    #endregion

    #region Camera Movement

    // * Chạy sau Update: tính vị trí mục tiêu cộng offset rồi làm mượt vị trí camera.
    private void LateUpdate()
    {
        if (target == null)
            return;

        Vector3 desiredPosition = target.position + offset;  // * Vị trí camera mong muốn trong không gian thế giới.
        transform.position = Vector3.SmoothDamp(
            transform.position, desiredPosition, ref velocity, followSmoothTime);
    }

    #endregion

}
