/// <summary>
/// Các dịch vụ mà một vỏ nhân vật cần trong suốt các lượt spawn. Mọi vỏ dùng chung một instance.
/// </summary>
/// <remarks>
/// Trước đây <see cref="CharacterView"/> tự inject 7 hệ thống qua Zenject khi pool tạo vỏ, nên các system nội bộ
/// của miền Character (tìm mục tiêu, runtime pool) phải được bind công khai. Giờ <see cref="CharacterFactory"/>
/// truyền context này vào <see cref="CharacterView.Initialize"/>, nên view không cần biết tới Zenject,
/// và miền Character chỉ cần bind <see cref="CharacterManager"/>.
/// </remarks>
public sealed class CharacterContext
{
    #region Simulation (miền khác, dùng qua interface)

    /// <summary>Đăng ký Transform vào job di chuyển.</summary>
    public IMovementRegistration MovementRegistration { get; }

    /// <summary>Gửi hướng và tốc độ di chuyển.</summary>
    public IMovementCommand MovementCommand { get; }

    /// <summary>Đăng ký hitbox vào lưới không gian.</summary>
    public ISpatialRegistration SpatialRegistration { get; }

    #endregion

    #region Pool (miền khác, dùng qua interface)

    /// <summary>Thuê và trả visual con của nhân vật.</summary>
    public IGameObjectPool Pools { get; }

    #endregion

    #region Character (cùng miền, dùng class trực tiếp)

    /// <summary>Tìm mục tiêu; skill của nhân vật dùng qua <see cref="CharacterView.Search"/>.</summary>
    public CharacterSearch Search { get; }

    /// <summary>Trả runtime về pool khi lượt spawn kết thúc.</summary>
    public CharacterRuntimePool Runtimes { get; }

    #endregion

    #region Constructor

    public CharacterContext(
        IMovementRegistration movementRegistration,
        IMovementCommand movementCommand,
        ISpatialRegistration spatialRegistration,
        IGameObjectPool pools,
        CharacterSearch search,
        CharacterRuntimePool runtimes)
    {
        MovementRegistration = movementRegistration;
        MovementCommand = movementCommand;
        SpatialRegistration = spatialRegistration;
        Pools = pools;
        Search = search;
        Runtimes = runtimes;
    }

    #endregion
}
