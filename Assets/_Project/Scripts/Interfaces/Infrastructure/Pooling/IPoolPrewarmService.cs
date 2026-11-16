using System;
using System.Threading;
using Cysharp.Threading.Tasks;

#region PoolPrewarm

/// <summary>Chuẩn bị tài nguyên theo batch có hỗ trợ hủy.</summary>
public interface IPoolPrewarm
{
    UniTask PrewarmAsync(CancellationToken cancellationToken);
}

#endregion

#region PoolPrewarmProgress

/// <summary>Đọc tiến độ chuẩn bị tài nguyên và nhận thông báo thay đổi.</summary>
public interface IPoolPrewarmProgress
{
    bool IsReady { get; }
    int PreparedObjectCount { get; }
    float Progress { get; }
    event Action<float> ProgressChanged;
}

#endregion

#region System Contract

/// <summary>Gom hợp đồng của PoolPrewarmService; consumer nhận vai trò nhỏ cần dùng.</summary>
public interface IPoolPrewarmService
{
    IPoolPrewarm GetPoolPrewarm();
    IPoolPrewarmProgress GetPoolPrewarmProgress();
}

#endregion
