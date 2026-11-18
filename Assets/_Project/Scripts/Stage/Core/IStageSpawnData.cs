/// <summary>Loại cảnh báo spawn; số lớn hơn được ưu tiên khi xuất hiện cùng lúc.</summary>
public enum StageSpawnWarning { SimultaneousWave, Elite, Boss }

/// <summary>
/// Đánh dấu một DataSO spawn được phép nằm trong danh sách Spawn Data của <see cref="StageDataSO"/>
/// (WaveConfigSO, EnemyPatternSO, BossSpawnDataSO). Inspector và StageDataSO dùng nó để chặn gán nhầm asset.
/// </summary>
/// <remarks>
/// Data không tự tạo system: StageManager chọn system theo loại data bằng một switch duy nhất (xem StageManager.Execute).
/// Thêm loại spawn mới = thêm DataSO + system + một nhánh trong switch đó.
/// </remarks>
public interface IStageSpawnData
{
    /// <summary>Kiểm tra cấu hình asset trước khi khởi chạy.</summary>
    void Validate();
}
