using System;

// Ranh giới của miền Stage. Mọi interface trong file này do StageManager implement.
// EnemySpawner, Wave/Formation/Boss, BossArena và StageRewards nằm bên trong miền, không được bind vào Zenject.
// Stage nằm trên Character, World và Simulation; Game và UI dùng Stage qua các vai trò dưới đây.
// Chiều ngược (màn đã sạch quái, lỗi, lịch cảnh báo) chỉ đi qua C# event.

#region Selection

/// <summary>Đọc danh mục màn và chọn màn trước khi bắt đầu lượt. Dành cho UI chọn màn.</summary>
public interface IStageSelection
{
    StageDataSO CurrentStage { get; }
    StageDatabaseSO Database { get; }

    /// <summary>Chọn màn theo ID ổn định; chỉ gọi khi không có lượt nào đang chạy.</summary>
    void SelectStage(string stageId);
}

#endregion

#region Battle Lifecycle

/// <summary>Kiểm tra cấu hình, bắt đầu hoặc dừng lượt chơi của màn. Dùng bởi GameManager.</summary>
public interface IStageBattleLifecycle
{
    /// <summary>Kiểm tra màn đang chọn (asset spawn, vùng sinh, thưởng) trước khi chơi.</summary>
    void Validate();

    /// <summary>Dọn lượt cũ, đặt map của màn và chuẩn bị lượt mới (spawn bắt đầu khi Player đã có).</summary>
    void BeginBattle();

    /// <summary>Hủy lịch sinh và thu hồi mọi quái của màn.</summary>
    void StopBattle();
}

#endregion

#region Results

/// <summary>Kết quả màn: báo đã sạch quái, kết thúc lượt và cấp/đọc thưởng. Dùng bởi GameManager và popup kết quả.</summary>
public interface IStageResults
{
    bool ResultGranted { get; }

    /// <summary>Mọi spawn đã xong và không còn quái sống: GameManager chuyển sang thắng.</summary>
    event Action Cleared;

    /// <summary>Lỗi khi chạy màn (cấu hình sai…); Stage đã tự dừng trước khi phát.</summary>
    event Action<Exception> Failed;

    /// <summary>Dừng màn (giữ nhân vật để phát animation kết quả) và cấp thưởng đúng một lần.</summary>
    void FinishBattle(bool won);

    /// <summary>Thưởng màn đã trao thực tế ở lượt này (chưa gồm vàng nhặt); null nếu chưa trao.</summary>
    RewardResult ResultRewards { get; }

    /// <summary>
    /// Trao thêm một lần nữa toàn bộ thưởng thắng của lượt (nút x2), kèm <paramref name="pickedGold"/> vàng nhặt trong trận.
    /// Chỉ một lần mỗi lượt; false thì không trao gì.
    /// </summary>
    bool TryDoubleRewards(int pickedGold, out RewardResult bonus);
}

#endregion

#region Schedule

/// <summary>Đồng hồ màn và lịch cảnh báo spawn (wave đồng loạt, Elite, Boss). Dùng bởi HUD.</summary>
public interface IStageSchedule
{
    float StageTime { get; }

    /// <summary>(loại cảnh báo, mốc giờ Stage sẽ sinh). UI tự chọn lúc hiển thị, không dời lịch spawn.</summary>
    event Action<StageSpawnWarning, float> SpawnScheduled;

    /// <summary>Lượt dừng: xóa mọi cảnh báo đang chờ.</summary>
    event Action WarningsCleared;
}

#endregion
