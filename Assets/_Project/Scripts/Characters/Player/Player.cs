using System;
using Terresquall;
using UnityEngine;
using Zenject;

// * Điều khiển di chuyển và lật sprite của nhân vật 2D bằng joystick hoặc bàn phím.
// * RequireComponent yêu cầu Rigidbody2D đi kèm trên GameObject.
[RequireComponent(typeof(Rigidbody2D))]
public class Player : MonoBehaviour
{

    #region Input & Player State

    private VirtualJoystick joystick;  // * Joystick scene được inject từ GameSceneInstaller.
    private MovementManager movementManager;  // * Tính và áp dụng chuyển động trong LateTick.
    private MovementHandle movementHandle;    // * Đăng ký riêng cho mỗi lần Player được bật.

    
    public event Action<Player> Destroyed;  // * Báo PlayerManager khi object bị hủy từ bên ngoài.
    public CharacterRuntimeData RuntimeData { get; private set; }  // * Nguồn chỉ số duy nhất khi chơi.
    public CharacterType CharacterType => characterType;

    [Header("Character")]
    [SerializeField] private CharacterType characterType = CharacterType.BlessedGladiator;
    [SerializeField] private SpriteRenderer playerSprite;  // * Sprite cần lật trái/phải; tự tìm trong đối tượng và các con nếu chưa gán.

    private Rigidbody2D body;   // * Thành phần vật lý được thiết lập lúc khởi tạo; MovementManager cập nhật Transform.
    private Vector2 moveInput;  // * Hướng và cường độ di chuyển XY, độ dài được giới hạn không vượt quá 1.

    #endregion

    #region Service Injection

    // * Nhận dependency trước khi prefab bật; vẫn hỗ trợ inject Player đang hoạt động.
    [Inject]
    public void Construct(VirtualJoystick sceneJoystick, MovementManager manager)
    {
        UnregisterMovement();
        joystick = sceneJoystick;
        movementManager = manager;
        if (isActiveAndEnabled)
            RegisterMovement();
    }

    #endregion

    // * PlayerManager gắn runtime đã tính đủ bonus trước khi phát PlayerChanged.
    public void InitializeRuntime(CharacterRuntimeData data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));
        if (RuntimeData != null)
            throw new InvalidOperationException("Player runtime has already been initialized.");
        if (data.CharacterType != characterType)
            throw new ArgumentException("Runtime character type does not match the player prefab.", nameof(data));
        RuntimeData = data;

        // * Log một lần khi nhận runtime để kiểm tra chỉ số sau khi cộng các nguồn bonus.
        BaseStats stats = RuntimeData.FinalStats;  // * Bản sao chỉ số thực tế tại thời điểm tạo Player.

        Debug.Log(
            $"[Player Stats] {RuntimeData.DisplayName} ({RuntimeData.CharacterType})\n" +
            $"Permanent Level: {RuntimeData.PermanentLevel} | Run Level: {RuntimeData.RunLevel}\n" +
            $"HP: {RuntimeData.CurrentHealth:0.##}/{stats.maxHealth:0.##}\n" +
            $"Damage: {stats.damage:0.##} | Defense: {stats.defense:0.##}\n" +
            $"Move Speed: {stats.moveSpeed:0.##}\n" +
            $"Crit Chance: {stats.critChance * 100f:0.##}% | Crit Multiplier: {stats.critDamageMultiplier:0.##}x\n" +
            $"Dodge: {stats.dodge:0.##} | Accuracy: {stats.accuracy:0.##}",
            this);
    }

    #region Player Movement

    // * Chuẩn bị Rigidbody2D ở chế độ Kinematic, tắt trọng lực và tìm sprite nếu thiếu.
    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        body.gravityScale = 0f;

        if (playerSprite == null)
            playerSprite = GetComponentInChildren<SpriteRenderer>();
    }

    // * OnEnable có thể chạy trước injection; chỉ đăng ký khi manager đã sẵn sàng.
    private void OnEnable()
    {
        RegisterMovement();
    }

    // * Ngừng di chuyển ngay khi tắt Player hoặc component; lần bật sau nhận handle mới.
    private void OnDisable()
    {
        UnregisterMovement();
        moveInput = Vector2.zero;
    }

    private void RegisterMovement()
    {
        if (movementManager != null)
            movementHandle = movementManager.Register(transform);
    }

    // * Hủy nhiều lần vẫn an toàn, kể cả khi manager đã dispose lúc kết thúc scene.
    private void UnregisterMovement()
    {
        if (movementManager != null)
            movementManager.Unregister(movementHandle);
        movementHandle = default;
    }

    // * Player đọc input và cập nhật hướng nhìn; manager di chuyển sau Update.
    private void Update()
    {
        moveInput = ReadMoveInput();

        // * Ngưỡng 0.01 tránh đổi hướng nhìn vì nhiễu input rất nhỏ; đứng yên giữ hướng cũ.
        if (playerSprite != null && Mathf.Abs(moveInput.x) > 0.01f)
            playerSprite.flipX = moveInput.x < 0f;

        // * Manager chuẩn hóa hướng; nhân tốc độ với độ nghiêng để giữ chuyển động analog.
        if (movementManager != null)
        {
            float speed = RuntimeData != null && RuntimeData.IsAlive ? RuntimeData.FinalStats.moveSpeed : 0f;
            movementManager.SetMovement(movementHandle, moveInput, speed * moveInput.magnitude);
        }
    }

    // * Đọc joystick được inject; thiếu joystick thì bắt đầu từ Vector2.zero.
    // * Trong Editor/PC cộng thêm bàn phím, sau đó giới hạn độ dài để đi chéo không nhanh hơn.
    private Vector2 ReadMoveInput()
    {
        Vector2 input = joystick != null ? joystick.GetAxis() : Vector2.zero;

#if UNITY_EDITOR || UNITY_STANDALONE
        // * Dùng bàn phím để test nhanh trong Editor/PC.
        input += new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
#endif

        // * Clamp chỉ giới hạn độ lớn tối đa, vẫn giữ độ nghiêng nhỏ của joystick.
        return Vector2.ClampMagnitude(input, 1f);
    }

    #endregion

    #region Destruction Notification

    // * Gỡ trạng thái player hiện tại khi bị Destroy hoặc scene kết thúc.
    private void OnDestroy()
    {
        UnregisterMovement();
        Destroyed?.Invoke(this);
        Destroyed = null;
    }

    #endregion

}
