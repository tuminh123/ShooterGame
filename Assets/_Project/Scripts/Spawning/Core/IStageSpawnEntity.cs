using System;

/// <summary>Một đơn vị được roster sở hữu: enemy đơn hoặc cả nhóm Pattern.</summary>
public interface IStageSpawnEntity : IDisposable
{
    /// <summary>Số quái còn sống bên trong thực thể.</summary>
    int AliveCount { get; }
    /// <summary>Thực thể đã được thu hồi hoặc kết thúc vòng đời.</summary>
    bool IsReleased { get; }
}
