using UnityEngine;

#region Time Source Contract

// Nguồn thời gian nằm trên main thread; job chỉ nhận DeltaTime dạng số.
public interface ITimeSource
{
    float DeltaTime { get; } // Số giây của bước cập nhật hiện tại.
}

#endregion

#region Gameplay Time

// Đồng hồ gameplay mặc định; có thể thay implementation qua Zenject.
public sealed class GameplayTimeSource : ITimeSource
{
    public float DeltaTime => Time.deltaTime; // Chịu timeScale: pause game thì bằng 0.
}

#endregion
