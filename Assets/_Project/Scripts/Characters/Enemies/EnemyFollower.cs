using UnityEngine;
using Zenject;

// * Quái di chuyển thẳng về Player; chạm Player thì trả về Lean Pool, chưa gây sát thương.
// * RequireComponent yêu cầu GameObject có Rigidbody2D.
[RequireComponent(typeof(Rigidbody2D))]
public class EnemyFollower : MonoBehaviour
{

    #region Enemy Settings & State

    [SerializeField, Min(0f)] private float moveSpeed = 1.5f;  // * Tốc độ đuổi theo, world unit/giây; Min giới hạn giá trị nhập trong Inspector.

    private Rigidbody2D body;                 // * Tham chiếu thành phần vật lý, lưu lại để thiết lập khi khởi tạo.
    private Transform player;                 // * Mục tiêu hiện tại được đồng bộ từ PlayerManager.
    private MovementManager movementManager;  // * Dịch vụ di chuyển do Zenject cung cấp; enemy chỉ quyết định hướng đuổi mục tiêu.
    private MovementHandle movementHandle;    // * Handle đăng ký hiện tại; phải hủy khi về pool và lấy handle mới khi bật lại.

    private PlayerManager playerManager;  // * Nguồn player và sự kiện thay đổi nhân vật.
    private PoolManager poolManager;      // * Thu hồi về pool và holder thuộc scene.

    #endregion

    #region Service Injection

    // * PoolManager inject trước OnEnable; vẫn hỗ trợ inject object đang bật trong scene/test.
    [Inject]
    public void Construct(MovementManager manager, PlayerManager players, PoolManager pools)
    {
        if (playerManager != null)
            playerManager.PlayerChanged -= OnPlayerChanged;
        if (movementManager != null)
            movementManager.Unregister(movementHandle);
        movementManager = manager;
        playerManager = players;
        poolManager = pools;
        if (isActiveAndEnabled)
        {
            SubscribePlayer();
            RegisterMovement();
        }
    }

    #endregion

    #region Activation & Pool Lifecycle

    // * Lấy Rigidbody2D, đặt Kinematic và tắt trọng lực trước khi cập nhật chuyển động.
    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        body.gravityScale = 0f;
        // * Cho phép tiếp xúc vật lý đầy đủ hơn giữa Kinematic và các loại body khác.
        body.useFullKinematicContacts = true;
    }

    // * Chạy mỗi lần quái được bật, gồm cả khi Lean Pool tái sử dụng: cập nhật lại mục tiêu.
    private void OnEnable()
    {
        SubscribePlayer();
        RegisterMovement();
    }

    // * Khi về pool/tắt component, ngừng di chuyển và vô hiệu hóa handle cũ.
    private void OnDisable()
    {
        if (playerManager != null)
            playerManager.PlayerChanged -= OnPlayerChanged;
        player = null;
        if (movementManager != null)
            movementManager.Unregister(movementHandle);
        movementHandle = default;
    }

    #endregion

    #region Chase Movement

    // * Đăng ký khi đã được inject; OnEnable trước injection không tạo dịch vụ dự phòng.
    private void RegisterMovement()
    {
        if (movementManager == null)
            return;
        movementHandle = movementManager.Register(transform);
        UpdateMovement();
    }

    // * Enemy quyết định hướng trong Update; manager tính và áp dụng vị trí trong LateTick.
    private void Update()
    {
        UpdateMovement();
    }

    // * Giới hạn bước bằng khoảng cách tới player để giữ hành vi MoveTowards, không vượt đích.
    private void UpdateMovement()
    {
        if (movementManager == null)
            return;
        Vector2 direction = player != null ? (Vector2)(player.position - transform.position) : Vector2.zero;
        movementManager.SetMovement(movementHandle, direction, moveSpeed, direction.magnitude);
    }

    #endregion

    #region Player Contact

    // * Unity gọi khi collider đi vào trigger 2D; other là collider vừa tiếp xúc.
    private void OnTriggerEnter2D(Collider2D other)
    {
        DespawnIfPlayer(other);
    }

    // * Unity gọi khi bắt đầu va chạm 2D; chuyển collider của đối tượng kia sang bước kiểm tra.
    private void OnCollisionEnter2D(Collision2D collision)
    {
        DespawnIfPlayer(collision.collider);
    }

    // * other là component vừa chạm; tìm Player trên nó hoặc đối tượng cha để nhận cả collider con.
    // * Nếu đúng người chơi thì thu hồi quái để pool dùng lại.
    private void DespawnIfPlayer(Component other)
    {
        if (other.GetComponentInParent<Player>() != null)
            poolManager?.Despawn(gameObject);
    }

    #endregion

    #region Player Tracking

    // * Lắng nghe khi đang bật, nhận cả trường hợp player đã tạo trước khi enemy spawn.
    private void SubscribePlayer()
    {
        if (playerManager == null)
            return;
        playerManager.PlayerChanged -= OnPlayerChanged;
        playerManager.PlayerChanged += OnPlayerChanged;
        OnPlayerChanged(playerManager.CurrentPlayer);
    }

    // * Thay mục tiêu khi tạo/gỡ player; không cần tìm scene hoặc singleton mỗi frame.
    private void OnPlayerChanged(Player current)
    {
        player = current != null ? current.transform : null;
        UpdateMovement();
    }

    #endregion

}
