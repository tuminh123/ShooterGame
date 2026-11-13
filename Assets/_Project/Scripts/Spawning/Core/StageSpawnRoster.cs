using System;
using System.Collections.Generic;

/// <summary>Sở hữu các thực thể spawn, gồm enemy đơn và cả nhóm Pattern.</summary>
internal sealed class StageSpawnRoster
{
    #region Danh tính và quyền sở hữu

    /// <summary>Bọc một enemy/boss; giữ danh tính pool để không thu hồi nhầm lượt mới.</summary>
    private sealed class CharacterEntity : IStageSpawnEntity
    {
        private readonly CharacterView view;
        private readonly CharacterRuntimeData identity;
        private readonly ulong generation;
        private readonly EnemySpawnManager spawner;
        private bool released;
        /// <summary>Chỉ hợp lệ khi vỏ vẫn chứa đúng runtime và generation đã ghi.</summary>
        private bool Current => view != null && identity != null &&
            ReferenceEquals(view.RuntimeData, identity) && identity.Generation == generation;

        public int AliveCount => !IsReleased && identity.IsAlive ? 1 : 0;
        public bool IsReleased => released || !Current;

        /// <summary>Ghi nhận đúng lượt spawn hiện tại của nhân vật.</summary>
        internal CharacterEntity(CharacterView view, EnemySpawnManager spawner)
        {
            this.view = view;
            this.spawner = spawner;
            identity = view.RuntimeData;
            generation = identity.Generation;
        }

        /// <summary>Thu hồi một lần nếu nhân vật vẫn thuộc đúng lượt spawn.</summary>
        public void Dispose()
        {
            if (IsReleased) return;
            released = true;
            spawner.Despawn(view);
        }
    }

    private readonly List<IStageSpawnEntity> entities = new List<IStageSpawnEntity>();
    private readonly EnemySpawnManager spawner;

    #endregion

    #region Khởi tạo và thống kê

    /// <summary>Nhận spawner dùng để thu hồi các nhân vật được theo dõi.</summary>
    internal StageSpawnRoster(EnemySpawnManager spawner) { this.spawner = spawner; }

    /// <summary>Cộng số quái sống của các thực thể và bỏ thực thể đã thu hồi.</summary>
    internal int AliveCount
    {
        get
        {
            int count = 0;
            for (int i = entities.Count - 1; i >= 0; i--)
            {
                var entity = entities[i];
                if (entity.IsReleased) entities.RemoveAt(i);
                else count += entity.AliveCount;
            }
            return count;
        }
    }

    #endregion

    #region Theo dõi và thu hồi

    /// <summary>Bọc enemy/boss đơn thành thực thể có bảo vệ danh tính pool.</summary>
    internal void Add(CharacterView member)
    {
        if (member == null || member.RuntimeData == null)
            throw new ArgumentException("Roster cần nhân vật đã được spawn.", nameof(member));
        Add(new CharacterEntity(member, spawner));
    }

    /// <summary>Nhận cả thực thể; nhóm tự quản lý và thu hồi các thành viên bên trong.</summary>
    internal void Add(IStageSpawnEntity entity)
    {
        if (entity == null) throw new ArgumentNullException(nameof(entity));
        if (entities.Contains(entity)) throw new InvalidOperationException("Thực thể đã thuộc roster.");
        entities.Add(entity);
    }

    /// <summary>Thu hồi các lượt còn thuộc roster; gom lỗi và tiếp tục dọn những thành viên khác.</summary>
    internal void Clear()
    {
        List<Exception> failures = null;
        while (entities.Count > 0)
        {
            int last = entities.Count - 1;
            var entity = entities[last];
            entities.RemoveAt(last);
            try { entity.Dispose(); }
            catch (Exception exception)
            {
                if (failures == null) failures = new List<Exception>();
                failures.Add(exception);
            }
        }
        if (failures != null) throw new AggregateException("Spawn roster cleanup failed.", failures);
    }
    #endregion
}
