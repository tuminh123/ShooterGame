using System;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

/// <summary>Mã đăng ký di chuyển; mặc định là không hợp lệ, không dùng chỉ số mảng làm danh tính.</summary>
public readonly struct MovementHandle
{
    // Token nhận diện MovementSystem đã cấp handle (so sánh bằng ReferenceEquals), để handle của
    // scene/container khác không bị nhận nhầm. Kiểu object vì bên cấp là system nội bộ, không phải interface.
    internal readonly object Owner;
    internal readonly long Id; // Mã tăng dần, không tái sử dụng khi pool trả lại cùng một GameObject.

    // Chỉ manager tạo handle khi đăng ký thành công.
    internal MovementHandle(object owner, long id)
    {
        Owner = owner;
        Id = id;
    }
}

/// <summary>
/// Di chuyển XY cho mọi Transform đã đăng ký (nhân vật, đạn, vật phẩm rơi) bằng một Burst job mỗi frame.
/// </summary>
/// <remarks>
/// Đối tượng tự quyết định hướng và gửi bằng <see cref="SetMovement"/>; job áp dụng trong <see cref="LateTick"/>,
/// sau khi mọi Update/Tick của frame đã chạy. Chỉ gọi từ main thread. Đổi vị trí Transform trực tiếp,
/// không giải quyết va chạm vật lý. Do <see cref="SimulationManager"/> tạo và gọi LateTick; không bind vào Zenject.
/// </remarks>
public sealed class MovementSystem : IDisposable
{
    #region Dependencies
    private readonly SpatialSystem spatial; // Báo lưới không gian dựng lại sau khi vị trí đã đổi.

    public MovementSystem(SpatialSystem spatialManager)
    {
        spatial = spatialManager ?? throw new ArgumentNullException(nameof(spatialManager));
    }

    #endregion

    #region Movement Data & Jobs

    // Dữ liệu main thread; Target không được đưa vào job.
    private struct Entry
    {
        public long Id; // Danh tính dùng cập nhật bảng tra cứu khi xóa bằng đổi chỗ.
        public Transform Target; // Transform cần đọc và áp dụng vị trí trong main thread.
        public Vector2 Velocity; // Vận tốc XY đã tính từ hướng chuẩn hóa và tốc độ.
        public float MaxDistance; // Giới hạn quãng đường mỗi bước, giúp đối tượng đuổi mục tiêu không đi vượt đích.
    }

    // Dữ liệu unmanaged liên tiếp trong NativeArray, mỗi phần tử dành cho một đối tượng.
    private struct MovementData
    {
        public float3 Position; // Vị trí world-space, giữ nguyên Z khi di chuyển trong mặt phẳng XY.
        public float2 Velocity; // Vận tốc world unit/giây theo XY.
        public float MaxDistance; // Giới hạn bước đi do đối tượng quyết định; vô cực cho chuyển động tự do.
    }

    // Burst tối ưu phép tính; Unity chia các chỉ số cho worker, không tạo job riêng từng enemy.
    [BurstCompile]
    private struct MovementJob : IJobParallelFor
    {
        public NativeArray<MovementData> Data; // Mỗi Execute chỉ đọc/ghi đúng phần tử index của mình.
        public float DeltaTime; // Thời gian bước mô phỏng lấy từ main thread, tính bằng giây.

        // Tính bước dịch chuyển có giới hạn, không gọi GameObject/Transform/LeanPool trong job.
        public void Execute(int index)
        {
            MovementData item = Data[index];
            float2 step = item.Velocity * DeltaTime;
            float distance = math.length(step);
            if (distance > item.MaxDistance && distance > 0f)
                step *= item.MaxDistance / distance;
            item.Position += new float3(step.x, step.y, 0f);
            Data[index] = item;
        }
    }

    #endregion

    #region Registry & Native State

    private readonly List<Entry> entries = new List<Entry>(128); // Danh sách đặc để duyệt nhanh; chỉ cấp phát thêm khi vượt dung lượng hiện có.
    // Tra mã đăng ký ra chỉ số hiện tại; xóa phần tử không làm hỏng handle còn sống.
    private readonly Dictionary<long, int> indices = new Dictionary<long, int>(128);
    // Ngăn đăng ký cùng một Transform nhiều lần khiến đối tượng bị di chuyển hai lần.
    private readonly Dictionary<Transform, long> targets = new Dictionary<Transform, long>(128);
    private NativeArray<MovementData> buffer; // Buffer persistent dùng lại qua các frame, tăng gấp đôi khi cần.
    private JobHandle pendingJob; // Job gần nhất; luôn Complete trước khi sửa/giải phóng buffer.
    private long nextId; // Mã tiếp theo; không reset trong vòng đời dịch vụ.
    private bool disposed; // Chặn đăng ký hoặc chạy sau khi đã giải phóng.
    private Bounds? arenaBounds;
    // Tra tên layer một lần (NameToLayer tìm theo chuỗi); Simulate chạy mỗi frame cho mọi object di chuyển.
    private readonly int playerLayer = LayerMask.NameToLayer(GameConstants.PhysicsLayers.Player);
    private readonly int enemyLayer = LayerMask.NameToLayer(GameConstants.PhysicsLayers.Enemy);

    public int Count => entries.Count; // Số đối tượng đang đăng ký, phục vụ theo dõi trong debugger/profiler.

    /// <summary>Giới hạn chuyển động XY trong đấu trường; null sẽ gỡ giới hạn.</summary>
    public void SetArenaBounds(Bounds? bounds) => arenaBounds = bounds;

    #endregion

    #region Registration & Movement Commands

    // Đăng ký một lần khi bật/spawn; đối tượng bắt đầu đứng yên tới khi được gửi hướng.
    public MovementHandle Register(Transform target)
    {
        if (disposed)
            throw new ObjectDisposedException(nameof(MovementSystem));
        if (target == null)
            throw new ArgumentNullException(nameof(target));
        if (targets.TryGetValue(target, out long existingId))
            return new MovementHandle(this, existingId);

        long id = checked(++nextId);
        indices.Add(id, entries.Count);
        targets.Add(target, id);
        entries.Add(new Entry { Id = id, Target = target, MaxDistance = float.PositiveInfinity });
        return new MovementHandle(this, id);
    }

    // Kiểm tra handle còn thuộc đăng ký hiện tại; handle cũ sau despawn không điều khiển object mới.
    public bool IsRegistered(MovementHandle handle)
    {
        return !disposed && ReferenceEquals(handle.Owner, this) && indices.ContainsKey(handle.Id);
    }

    // direction chỉ quyết định hướng; speed là world unit/giây, maxDistance là giới hạn mỗi bước.
    // Hướng zero hoặc tốc độ zero sẽ dừng; hướng và tốc độ giữ nguyên tới lần cập nhật tiếp theo.
    public bool SetMovement(MovementHandle handle, Vector2 direction, float speed,
        float maxDistance = float.PositiveInfinity)
    {
        if (!IsRegistered(handle))
            return false;
        if (!math.all(math.isfinite(new float2(direction.x, direction.y))) ||
            !math.isfinite(speed) || float.IsNaN(maxDistance))
            throw new ArgumentException("Movement values must be finite, except an infinite distance limit.");

        int index = indices[handle.Id];
        Entry entry = entries[index];
        entry.Velocity = direction.normalized * Mathf.Max(0f, speed);
        entry.MaxDistance = Mathf.Max(0f, maxDistance);
        entries[index] = entry;
        return true;
    }

    // Hủy khi OnDisable/despawn; gọi nhiều lần hoặc gửi handle cũ đều an toàn.
    public bool Unregister(MovementHandle handle)
    {
        if (!IsRegistered(handle))
            return false;
        RemoveAt(indices[handle.Id]);
        return true;
    }

    #endregion

    #region Movement Simulation

    // SimulationManager gọi đầu pha cuối frame (LateTick), sau khi gameplay đã chốt hướng di chuyển.
    public void LateTick()
    {
        Simulate(Time.deltaTime);
    }

    // Chạy một bước cho cả nhóm; có thể gọi thủ công để kiểm tra với deltaTime cố định.
    // Chỉ dùng cho kiểm tra thủ công (smoke test); không gọi trong frame đã chạy LateTick, tránh di chuyển hai lần.
    public void Simulate(float deltaTime)
    {
        if (disposed)
            return;
        if (!math.isfinite(deltaTime) || deltaTime < 0f)
            throw new ArgumentOutOfRangeException(nameof(deltaTime));

        for (int i = entries.Count - 1; i >= 0; i--)
            if (entries[i].Target == null)
                RemoveAt(i);
        if (entries.Count == 0 || deltaTime == 0f)
            return;

        EnsureCapacity(entries.Count);
        for (int i = 0; i < entries.Count; i++)
        {
            Entry entry = entries[i];
            Vector3 position = entry.Target.position;
            buffer[i] = new MovementData
            {
                Position = new float3(position.x, position.y, position.z),
                Velocity = new float2(entry.Velocity.x, entry.Velocity.y),
                MaxDistance = entry.MaxDistance
            };
        }

        pendingJob = new MovementJob { Data = buffer, DeltaTime = deltaTime }
            .Schedule(entries.Count, 64);
        // Lưu ý: Hoàn tất trong cùng bước để callback pool không thể thay đổi danh sách khi job đang chạy.
        // Có song song trong nhóm; main thread chờ ở đây trước khi ghi Transform.
        pendingJob.Complete();
        for (int i = 0; i < entries.Count; i++)
        {
            float3 position = buffer[i].Position;
            // Đấu trường boss chỉ giữ nhân vật lại; đạn và loot vẫn bay/rơi tự do.
            if (arenaBounds.HasValue && IsCharacter(entries[i].Target))
            {
                Bounds bounds = arenaBounds.Value;
                position.x = math.clamp(position.x, bounds.min.x, bounds.max.x);
                position.y = math.clamp(position.y, bounds.min.y, bounds.max.y);
            }
            entries[i].Target.position = new Vector3(position.x, position.y, position.z);
        }
        // Snapshot không gian phải lấy vị trí mới nếu có truy vấn sau LateTick trong cùng frame.
        spatial.Invalidate();
    }

    private bool IsCharacter(Transform target)
    {
        int layer = target.gameObject.layer;
        return layer == playerLayer || layer == enemyLayer;
    }

    #endregion

    #region Buffer & Registry Maintenance

    public void PrewarmCapacity(int count)
    {
        if (disposed) throw new ObjectDisposedException(nameof(MovementSystem));
        if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
        EnsureCapacity(count);
        if (entries.Capacity < count) entries.Capacity = count;
        indices.EnsureCapacity(count);
        targets.EnsureCapacity(count);
    }

    // Chỉ cấp phát khi cần tăng dung lượng, giữ buffer cũ khi số lượng giảm.
    private void EnsureCapacity(int count)
    {
        if (buffer.IsCreated && buffer.Length >= count)
            return;
        pendingJob.Complete();
        int capacity = Mathf.NextPowerOfTwo(Mathf.Max(128, count));
        if (buffer.IsCreated)
            buffer.Dispose();
        buffer = new NativeArray<MovementData>(capacity, Allocator.Persistent,
            NativeArrayOptions.UninitializedMemory);
    }

    // Xóa O(1) bằng chuyển phần tử cuối vào chỗ trống và cập nhật bảng tra handle.
    private void RemoveAt(int index)
    {
        Entry removed = entries[index];
        targets.Remove(removed.Target);
        indices.Remove(removed.Id);
        int last = entries.Count - 1;
        if (index != last)
        {
            Entry moved = entries[last];
            entries[index] = moved;
            indices[moved.Id] = index;
        }
        entries.RemoveAt(last);
    }

    #endregion

    #region Native Memory Cleanup

    // SimulationManager gọi khi scene kết thúc; Complete job trước khi Dispose để không rò native memory.
    public void Dispose()
    {
        if (disposed)
            return;
        pendingJob.Complete();
        if (buffer.IsCreated)
            buffer.Dispose();
        entries.Clear();
        indices.Clear();
        targets.Clear();
        disposed = true;
    }

    #endregion

}
