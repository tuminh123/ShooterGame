using System;

#region Stage Spawm System

/// <summary>Kiểm tra, nhận dữ liệu và dừng hệ thống spawn.</summary>
public interface IStageSpawnSystem
{
    #region Vòng đời hệ thống

    /// <summary>Kiểm tra dữ liệu và dependency cần cho loại spawn tương ứng.</summary>
    void Validate(IStageSpawnData data);
    /// <summary>Đăng ký một lượt spawn từ DataSO; mốc delay tính từ đầu Stage.</summary>
    void Execute(IStageSpawnData data);
    /// <summary>Hủy lịch sinh và thu hồi nhân vật thuộc hệ thống.</summary>
    void Stop();
    #endregion
}

/// <summary>Cập nhật phụ sau bước di chuyển chính của frame.</summary>
public interface ILateUpdateStage
{
    /// <summary>Bù chuyển động cuối frame khi Stage đang chạy.</summary>
    void LateTick();
}
/// <summary>Cung cấp tiến trình spawn để Stage xác định hoàn tất màn.</summary>
public interface IStageSpawnStatus
{
    /// <summary>Đã sinh xong; false nếu vẫn còn lượt spawn đang chờ.</summary>
    bool DeploymentComplete { get; }
    /// <summary>Số nhân vật còn sống thuộc hệ thống.</summary>
    int AliveCount { get; }
}
/// <summary>Cập nhật hệ thống theo đồng hồ gameplay chung của Stage.</summary>
public interface IUpdateStage
{
    /// <summary>Nhận thời gian từ đầu Stage và bước thời gian của frame hiện tại.</summary>
    void Tick(float stageTime, float deltaTime);
}
#endregion


/// <summary>Hợp đồng chung để Stage định tuyến các DataSO spawn.</summary>
public interface IStageSpawnData
{
    #region Định tuyến hệ thống

    /// <summary>Loại hệ thống dùng làm khóa trong registry của Stage.</summary>
    Type SystemType { get; }
    #endregion

    #region Khởi tạo và kiểm tra

    /// <summary>Tạo hệ thống mới bằng container, tự nhận dependency qua constructor.</summary>
    IStageSpawnSystem CreateSystem(Zenject.DiContainer container);
    /// <summary>Kiểm tra cấu hình asset trước khi khởi chạy.</summary>
    void Validate();
    #endregion
}

