using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Thực thể nhóm: sở hữu thành viên, hình dạng và tiến trình chuyển động chung.</summary>
public sealed class EnemyFormationRuntime : IStageSpawnEntity
{
    #region Danh tính và trạng thái nhóm

    /// <summary>Lưu danh tính thành viên, offset hình và trạng thái liên kết đội hình.</summary>
    private sealed class Member
    {
        public CharacterView view;
        public CharacterRuntimeData identity;
        public ulong generation;
        public EnemyController controller;
        public Vector2 offset, target;
        public float travelled;
        public bool finished, detached;
        /// <summary>Kiểm tra vỏ còn thuộc đúng runtime và generation đã ghi.</summary>
        public bool Current => view != null && identity != null &&
            ReferenceEquals(view.RuntimeData, identity) && identity.Generation == generation;
    }

    private readonly List<Member> members = new List<Member>();
    private readonly EnemyPatternType type;
    private readonly EnemyPathRoute route;
    private readonly Vector2[] offsets;
    private readonly float speed, shrinkDuration, knockbackDistance, knockbackDuration;
    private readonly AreaShrinkTiming shrinkTiming;
    private readonly int passes;
    private readonly Func<Vector2> playerCenter;
    private readonly Action<CharacterView> despawn;
    private float age, groupTravelled;
    private bool sealedGroup;
    private Vector2 anchorOffset;
    /// <summary>Nhóm đã kết thúc; không nhận thêm cập nhật hay thành viên.</summary>
    public bool IsBroken { get; private set; }
    public bool IsReleased => IsBroken;
    /// <summary>Đếm thành viên sống còn thuộc nhóm và đúng lượt spawn.</summary>
    public int AliveCount
    {
        get
        {
            int count = 0;
            foreach (var member in members)
                if (member.controller != null && member.Current && member.identity.IsAlive) count++;
            return count;
        }
    }

    #endregion

    #region Khởi tạo và nhận thành viên

    /// <summary>Chốt thông số, tạo offset và ghi tâm ban đầu; chưa sinh nhân vật.</summary>
    public EnemyFormationRuntime(EnemyPatternSO data, EnemyPathRoute route, Func<Vector2> playerCenter,
        Action<CharacterView> despawn)
        : this(data.CreateRuntimeData(), route, playerCenter, despawn) { }

    public EnemyFormationRuntime(EnemyPatternSO.RuntimeData data, EnemyPathRoute route, Func<Vector2> playerCenter,
        Action<CharacterView> despawn)
    {
        if (data.Type != EnemyPatternType.Area && route == null)
            throw new ArgumentNullException(nameof(route), "Hai chế độ Path cần route runtime.");
        type = data.Type; this.route = route; this.playerCenter = playerCenter;
        this.despawn = despawn;
        speed = data.Speed;
        knockbackDistance = data.KnockbackDistance; knockbackDuration = data.KnockbackDuration;
        shrinkDuration = data.ShrinkTiming.Duration; passes = data.Passes;
        shrinkTiming = data.ShrinkTiming;
        offsets = data.Slots;
        anchorOffset = CurrentAnchor;
    }

    /// <summary>Số slot hình học đã chốt, dùng chung cho cả ba cách spawn.</summary>
    public int SlotCount => offsets.Length;

    /// <summary>Area cộng slot vào tâm Player; hai kiểu Path cộng slot vào điểm đầu đường.</summary>
    public Vector2 GetSpawnPosition(int memberIndex)
    {
        if (memberIndex < 0 || memberIndex >= offsets.Length) throw new ArgumentOutOfRangeException(nameof(memberIndex));
        return type == EnemyPatternType.Area ? playerCenter() + offsets[memberIndex] : route.Sample(0f, passes) + offsets[memberIndex];
    }

    /// <summary>Nhận thành viên theo chỉ số ổn định; con chết không làm dồn vị trí hình.</summary>
    public void Add(CharacterView enemy, EnemyController controller, int memberIndex)
    {
        if (IsBroken || sealedGroup || memberIndex != members.Count || memberIndex >= offsets.Length)
            throw new InvalidOperationException("Pattern slot không hợp lệ hoặc nhóm đã kết thúc nhận thành viên.");
        members.Add(new Member { view = enemy, identity = enemy.RuntimeData, generation = enemy.RuntimeData.Generation,
            controller = controller, offset = offsets[memberIndex], target = enemy.transform.position });
        controller.JoinFormation(this, memberIndex, knockbackDistance, knockbackDuration);
    }

    /// <summary>Đánh dấu nhóm đã nhận đủ thành viên, không cho thêm slot.</summary>
    public void Seal() => sealedGroup = true;

    #endregion

    #region Tiến trình chuyển động

    /// <summary>Tiến tâm nhóm và các slot còn liên kết; Area hết hạn thu hồi cả nhóm.</summary>
    public void Tick(float deltaTime)
    {
        if (IsBroken || deltaTime <= 0f) return;
        float previousAge = age;
        age += deltaTime;
        if (type == EnemyPatternType.Area && age >= shrinkTiming.TotalDuration) { Dispose(); return; }
        // Chỉ ngân sách thời gian thu mới làm giảm bán kính; frame dài có thể đi qua nhiều pha.
        float shrinkStep = type == EnemyPatternType.Area
            ? Mathf.Max(0f, shrinkTiming.GetShrinkTime(age) - shrinkTiming.GetShrinkTime(previousAge)) : 0f;
        if (type == EnemyPatternType.Formation)
            groupTravelled = Mathf.Min(route.Length * passes, groupTravelled + speed * deltaTime);
        Vector2 anchorDelta = CurrentAnchor - anchorOffset;
        anchorOffset = CurrentAnchor;
        bool alive = false;
        for (int i = 0; i < members.Count; i++)
        {
            Member member = members[i];
            if (member.controller == null || !member.Current || !member.identity.IsAlive) continue;
            alive = true;
            if (member.finished) { Release(member); continue; }
            if (member.detached) continue;
            if (member.controller.IsKnockedBack)
            {
                member.target = member.view.transform.position;
                continue;
            }
            if (type == EnemyPatternType.Area)
            {
                Vector2 destination = playerCenter() + member.offset * shrinkTiming.GetScale(age);
                member.target = Vector2.MoveTowards((Vector2)member.view.transform.position + anchorDelta, destination,
                    Mathf.Max(0.01f, member.offset.magnitude / shrinkDuration) * shrinkStep);
            }
            else
            {
                member.travelled = type == EnemyPatternType.Formation ? groupTravelled :
                    Mathf.Min(route.Length * passes, member.travelled + speed * deltaTime);
                Vector2 destination = route.Sample(member.travelled, passes) + member.offset;
                member.target = Vector2.MoveTowards(member.target + anchorDelta, destination, speed * deltaTime);
                member.finished = member.travelled >= route.Length * passes && (member.target - destination).sqrMagnitude < 0.000001f;
            }
        }
        if (!alive && sealedGroup) Dispose();
    }

    #endregion

    #region Tách đội hình và truy vấn thành viên

    /// <summary>Giao Path còn lại và tốc độ nhóm cho controller; Area vẫn liên kết với viền.</summary>
    public void DetachMember(int memberIndex)
    {
        if (IsBroken || route == null || memberIndex < 0 || memberIndex >= members.Count) return;
        Member member = members[memberIndex];
        if (member.detached || member.controller == null || !member.Current || !member.identity.IsAlive) return;
        member.detached = true;
        member.finished = false;
        member.controller.FollowRemainingPath(route, member.travelled, passes, speed);
    }

    /// <summary>Lấy đích cho thành viên còn liên kết đội hình.</summary>
    public bool TryGetTarget(int memberIndex, out Vector2 target)
    {
        target = Vector2.zero;
        if (IsBroken || memberIndex < 0 || memberIndex >= members.Count) return false;
        Member member = members[memberIndex];
        if (member.detached || member.controller == null || !member.Current) return false;
        target = member.target;
        return true;
    }

    /// <summary>Bỏ controller khỏi slot mà không dồn lại vị trí các thành viên khác.</summary>
    public void Remove(EnemyController controller)
    {
        foreach (Member member in members)
            if (ReferenceEquals(member.controller, controller)) { member.controller = null; return; }
    }

    #endregion

    #region Bù chuyển động tâm

    /// <summary>Bù dịch tâm cho slot đang liên kết; controller tự xử lý con tách và knockback.</summary>
    public bool ApplyCameraMotion()
    {
        if (IsBroken) return false;
        Vector2 delta = CurrentAnchor - anchorOffset;
        anchorOffset = CurrentAnchor;
        bool moved = false;
        foreach (Member member in members)
            if (member.controller != null && member.Current && member.identity.IsAlive)
            {
                if (member.detached)
                {
                    moved |= member.controller.ApplyPathCameraMotion(member.view);
                    continue;
                }
                if (member.controller.IsKnockedBack || delta.sqrMagnitude <= 0f)
                {
                    continue;
                }
                member.target += delta;
                member.view.transform.position += new Vector3(delta.x, delta.y, 0f);
                moved = true;
            }
        return moved;
    }

    /// <summary>Lấy offset camera cho Path hoặc vị trí player cho Area.</summary>
    private Vector2 CurrentAnchor => route != null ? route.AnchorOffset : playerCenter();

    #endregion

    #region Thu hồi và giải phóng

    /// <summary>Thu hồi thành viên đúng danh tính; không phát sự kiện chết hoặc thưởng.</summary>
    private void Release(Member member)
    {
        if (member.controller == null || !member.Current) return;
        member.controller = null;
        despawn(member.view);
    }

    /// <summary>Kết thúc nhóm và thu hồi thành viên; gom lỗi nhưng vẫn dọn hết danh sách.</summary>
    public void Dispose()
    {
        if (IsBroken) return;
        IsBroken = true;
        List<Exception> failures = null;
        foreach (Member member in members)
        {
            try { Release(member); }
            catch (Exception exception)
            {
                if (failures == null) failures = new List<Exception>();
                failures.Add(exception);
            }
        }
        if (failures != null) throw new AggregateException("Pattern member cleanup failed.", failures);
    }
    #endregion
}
