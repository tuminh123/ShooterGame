using Unity.Collections;
using UnityEngine;

// Ranh giới của miền Simulation (di chuyển và truy vấn không gian bằng Jobs/Burst).
// Mọi interface trong file này do SimulationManager implement. MovementManager, SpatialManager và
// SpatialSearchFactory nằm bên trong miền, không được bind vào Zenject.
// Simulation là hạ tầng: không biết gameplay (skill, wave, loot); miền khác dùng nó, không có chiều ngược lại.
// Mỗi vai trò dưới đây ứng với một nhóm nơi dùng thực tế, ghi trong summary.

#region Movement

/// <summary>Đăng ký một Transform vào job di chuyển. Dùng bởi CharacterView và đạn.</summary>
public interface IMovementRegistration
{
    /// <summary>Đăng ký Transform và nhận handle của lượt đăng ký này.</summary>
    MovementHandle Register(Transform target);

    /// <summary>Handle còn thuộc đúng lượt đăng ký hiện tại (đối tượng chưa bị gỡ hoặc tái sử dụng).</summary>
    bool IsRegistered(MovementHandle handle);

    /// <summary>Gỡ đăng ký. Handle cũ hoặc đã gỡ thì trả về false và không làm gì.</summary>
    bool Unregister(MovementHandle handle);
}

/// <summary>Gửi lệnh di chuyển cho một đối tượng đã đăng ký. Dùng bởi CharacterView và đạn.</summary>
public interface IMovementCommand
{
    /// <summary>
    /// Đặt hướng (sẽ được chuẩn hóa), tốc độ và quãng đường tối đa cho mỗi bước.
    /// Lệnh giữ nguyên cho tới khi bị thay; Job áp dụng ở LateTick.
    /// </summary>
    bool SetMovement(MovementHandle handle, Vector2 direction, float speed, float maxDistance = float.PositiveInfinity);
}

/// <summary>Giới hạn mọi chuyển động trong một vùng đấu trường. Dùng bởi boss khi mở đấu trường.</summary>
public interface IMovementArena
{
    /// <summary>Đặt vùng giới hạn; <c>null</c> để gỡ giới hạn.</summary>
    void SetArenaBounds(Bounds? bounds);
}

#endregion

#region Spatial

/// <summary>Đăng ký hitbox của nhân vật vào lưới không gian. Dùng bởi CharacterView.</summary>
public interface ISpatialRegistration
{
    SpatialHandle Register(CharacterView view);
    bool IsRegistered(SpatialHandle handle);
    void Unregister(SpatialHandle handle);
}

/// <summary>Báo lưới không gian cần dựng lại trong frame này (sau khi dịch chuyển tức thời). Dùng bởi spawn theo đội hình.</summary>
public interface ISpatialInvalidation
{
    void Invalidate();
}

/// <summary>Chuyển handle hoặc kết quả truy vấn thành nhân vật còn hợp lệ. Dùng bởi tìm mục tiêu và đạn.</summary>
public interface ISpatialResolution
{
    bool TryResolve(SpatialHandle handle, out CharacterHandle target);
    bool TryResolve(SpatialHit hit, out CharacterHandle target);
}

/// <summary>Chạy truy vấn không gian: một lần, theo lô (gom nhiều query vào một job), hoặc lấy mọi kết quả. Dùng bởi tìm mục tiêu và đạn.</summary>
public interface ISpatialQuery
{
    SpatialHit Query(SpatialQuery query, SpatialHandle exclude = default);
    void ExecuteQueries(NativeArray<SpatialQuery> queries, NativeArray<SpatialHit> results, int count);

    /// <summary>Buffer trả về được dùng lại ở lần gọi sau; đọc xong trước khi truy vấn tiếp.</summary>
    NativeArray<SpatialHit> FindAll(SpatialQuery query, SpatialHandle exclude = default);
}

/// <summary>Tạo dữ liệu truy vấn hợp lệ (không thực thi). Dùng bởi tìm mục tiêu và đạn.</summary>
public interface ISpatialQueryBuilder
{
    SpatialQuery Nearest(Vector2 center, float radius, CharacterTeam team);
    SpatialQuery AllInCircle(Vector2 center, float radius, CharacterTeam team);
    SpatialQuery Sweep(Vector2 start, Vector2 end, float radius, CharacterTeam team);
}

#endregion

#region Prewarm

/// <summary>Cấp phát trước buffer của Jobs trong bước loading, để không resize giữa trận. Dùng bởi PoolPrewarmService.</summary>
public interface ISimulationCapacity
{
    /// <param name="movers">Số đối tượng di chuyển tối đa (nhân vật, đạn, vật phẩm rơi).</param>
    /// <param name="hitboxes">Số hitbox nhân vật tối đa.</param>
    void PrewarmCapacity(int movers, int hitboxes);
}

#endregion
