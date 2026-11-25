/// <summary>
/// Base của Manager theo miền (Save, Progression, Pool, Simulation, Character, World, Combat, Stage, Battle, Game).
/// Lifecycle là virtual rỗng: Manager chỉ override phase mình cần.
/// </summary>
/// <remarks>
/// Gom cả 10 Manager về một kiểu chung để composition root gọi lifecycle theo danh sách viết trong code,
/// thay vì số thứ tự rải rác. Chỉ dùng cho Manager (class thuần, không kế thừa gì khác), không dùng cho
/// MonoBehaviour: View trong scene/pool để Manager tick hộ.
/// </remarks>
public abstract class DomainManager
{
    /// <summary>Chạy một lần sau khi mọi Manager đã được tạo, theo thứ tự khởi tạo của composition root.</summary>
    public virtual void Initialize() { }

    /// <summary>Chạy mỗi frame (gameplay gửi lệnh: đồng hồ, skill, AI, spawn).</summary>
    public virtual void Tick() { }

    /// <summary>Chạy mỗi frame sau mọi Tick (áp di chuyển, camera, visual, nhặt loot).</summary>
    public virtual void LateTick() { }

    /// <summary>Giải phóng khi đóng scene, theo thứ tự dọn của composition root.</summary>
    public virtual void Dispose() { }
}
