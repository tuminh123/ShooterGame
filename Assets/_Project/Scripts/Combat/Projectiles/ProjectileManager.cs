using System;
using System.Collections.Generic;
using UnityEngine;
using Zenject;

// Một vòng cập nhật chung cho đạn; MovementManager vẫn là nơi duy nhất đổi vị trí.
public sealed class ProjectileManager : ITickable, IDisposable
{
    private readonly struct Registration
    {
        public readonly ProjectileView Projectile; // Viên đạn đang hoạt động.
        public readonly MovementHandle Handle; // Phân biệt các lần spawn của cùng object trong pool.

        public Registration(ProjectileView projectile, MovementHandle handle)
        {
            Projectile = projectile;
            Handle = handle;
        }
    }

    private readonly MovementManager movementManager; // Kiểm tra đăng ký còn thuộc lượt spawn hiện tại.
    private readonly List<Registration> activeProjectiles = new List<Registration>(128); // Đạn đang sống.
    private readonly List<Registration> tickSnapshot = new List<Registration>(128); // Bản duyệt ổn định khi callback spawn/despawn.
    private bool disposed; // Ngăn cập nhật sau khi scene giải phóng manager.

    public ProjectileManager(MovementManager movementManager)
        => this.movementManager = movementManager ?? throw new ArgumentNullException(nameof(movementManager));

    internal void Register(ProjectileView projectile, MovementHandle handle)
    {
        if (disposed) throw new ObjectDisposedException(nameof(ProjectileManager));
        activeProjectiles.Add(new Registration(projectile, handle));
    }

    internal void Unregister(MovementHandle handle)
    {
        for (int index = activeProjectiles.Count - 1; index >= 0; index--)
            if (activeProjectiles[index].Handle.Id == handle.Id &&
                ReferenceEquals(activeProjectiles[index].Handle.Owner, handle.Owner))
                activeProjectiles.RemoveAt(index);
    }

    public void Tick()
    {
        float deltaTime = Time.deltaTime;
        if (disposed || deltaTime <= 0f) return;

        // Đạn tạo trong callback sẽ được xử lý từ lượt kế tiếp, không chen vào bản đang duyệt.
        tickSnapshot.Clear();
        tickSnapshot.AddRange(activeProjectiles);
        for (int index = 0; index < tickSnapshot.Count; index++)
        {
            Registration registration = tickSnapshot[index];
            if (!movementManager.IsRegistered(registration.Handle) ||
                registration.Projectile == null || !registration.Projectile.isActiveAndEnabled) continue;
            try { registration.Projectile.Tick(deltaTime); }
            catch (Exception exception)
            {
                Debug.LogException(exception, registration.Projectile);
                // Không thu hồi nhầm object nếu callback đã trả nó về pool và spawn lại.
                if (movementManager.IsRegistered(registration.Handle)) registration.Projectile.Release();
            }
        }
        tickSnapshot.Clear();
    }

    public void Dispose()
    {
        disposed = true;
        activeProjectiles.Clear();
        tickSnapshot.Clear();
    }
}
