using System;
using System.Collections.Generic;
using UnityEngine;

// Thực thể logic đại diện cả đội hình. Không tạo GameObject cha và không đổi parent của enemy trong pool.
// Wave/spawner sở hữu vòng đời nhóm; từng controller chỉ nhận vị trí mà nhóm đã quyết định.
public sealed class EnemyFormationRuntime : IDisposable
{
    #region Member Identity And Group State

    private sealed class Member
    {
        public CharacterView view;
        public CharacterRuntimeData identity;
        public ulong generation;
        public EnemyController controller;
        public Vector2 offset;

        public bool IsCurrent => view != null && identity != null &&
            ReferenceEquals(view.RuntimeData, identity) && identity.Generation == generation;
    }

    private readonly List<Member> members = new List<Member>();
    private readonly EnemyPathRoute route;
    private readonly EnemyFormationSettings settings;
    private readonly float speed;
    private float travelled;
    private bool sealedGroup;
    private bool reachedEnd;
    private readonly float baseKnockbackDistance;
    private Vector2 sampledAnchorOffset;

    public bool IsBroken { get; private set; }
    public Vector2 Center => route.Sample(travelled, settings.passes);
    public float KnockbackDistance => baseKnockbackDistance;
    public float KnockbackDuration => settings.knockbackDuration;

    #endregion

    #region Initialization And Membership

    public EnemyFormationRuntime(EnemyPathRoute route, EnemyFormationSettings settings, float baseKnockbackDistance, float speed)
    {
        this.route = route ?? throw new ArgumentNullException(nameof(route));
        if (settings == null)
        {
            throw new ArgumentNullException(nameof(settings));
        }

        if (float.IsNaN(speed) || float.IsInfinity(speed) || speed <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(speed));
        }

        this.speed = speed; // Chốt từ Formation lúc spawn, không đọc lại asset mỗi frame.
        settings.Validate();
        this.settings = settings.Copy();
        this.baseKnockbackDistance = Mathf.Max(0f, baseKnockbackDistance);
        sampledAnchorOffset = route.AnchorOffset;
    }

    public void Add(CharacterView enemy, EnemyController controller, Vector2 offset)
    {
        if (IsBroken || sealedGroup)
        {
            throw new InvalidOperationException("Không thể thêm thành viên khi nhóm đã chạy hoặc đã tan.");
        }

        int slot = members.Count;

        members.Add(new Member
        {
            view = enemy,
            identity = enemy.RuntimeData,
            generation = enemy.RuntimeData.Generation,
            controller = controller,
            offset = offset
        });

        controller.JoinFormation(this, slot);
    }

    public void Seal()
    {
        sealedGroup = true;
    }

    // Không dồn slot khi một con chết, tránh đổi đích của tất cả thành viên còn lại.
    public void Remove(EnemyController controller)
    {
        foreach (Member member in members)
        {
            if (ReferenceEquals(member.controller, controller))
            {
                member.controller = null;
                return;
            }
        }
    }

    #endregion

    #region Shared Group Movement

    public void Tick(float deltaTime)
    {
        if (!sealedGroup || IsBroken || deltaTime <= 0f)
        {
            return;
        }

        if (reachedEnd || !HasLivingMembers())
        {
            Dissolve(0f);
            return;
        }

        sampledAnchorOffset = route.AnchorOffset;
        float finishDistance = route.CompletionDistance(settings, members.Count);
        travelled = Mathf.Min(finishDistance, travelled + speed * deltaTime);

        // Giữ đích cuối một bước để MovementManager áp dụng vị trí cho cả khối trước khi trả về chase.
        reachedEnd = travelled >= finishDistance;
    }

    // Chạy sau camera LateUpdate và MovementManager: bù phần camera vừa dịch trong frame.
    // Chỉ dịch thành viên còn thuộc nhóm; không kéo enemy đang knockback/chase hoặc vỏ đã tái sử dụng.
    public bool ApplyCameraMotion()
    {
        if (IsBroken || !sealedGroup)
        {
            return false;
        }

        Vector2 currentOffset = route.AnchorOffset;
        Vector2 delta = currentOffset - sampledAnchorOffset;
        sampledAnchorOffset = currentOffset;

        if (delta.sqrMagnitude <= 0f)
        {
            return false;
        }

        foreach (Member member in members)
        {
            if (member.controller != null && member.IsCurrent && member.identity.IsAlive && member.view.isActiveAndEnabled)
            {
                member.view.transform.position += new Vector3(delta.x, delta.y, 0f);
            }
        }

        return true;
    }

    public bool TryGetTarget(int slot, out Vector2 target)
    {
        target = Center;

        if (IsBroken || slot < 0 || slot >= members.Count)
        {
            return false;
        }

        Member member = members[slot];

        if (member.controller == null || !member.IsCurrent)
        {
            return false;
        }

        target = route.MemberPosition(settings, travelled, slot, member.offset);
        return true;
    }

    private bool HasLivingMembers()
    {
        foreach (Member member in members)
        {
            if (member.controller != null && member.IsCurrent && member.identity.IsAlive)
            {
                return true;
            }
        }

        return false;
    }

    #endregion

    #region Contact And Cleanup

    public bool OnPlayerContact(EnemyController source, Vector2 playerPosition)
    {
        if (IsBroken)
        {
            return false;
        }

        foreach (Member member in members)
        {
            if (ReferenceEquals(member.controller, source) && member.IsCurrent && member.identity.IsAlive)
            {
                // Một va chạm giải phóng cả nhóm và phát phản lực riêng theo vị trí mỗi con.
                IsBroken = true;

                foreach (Member affected in members)
                {
                    if (affected.controller != null && affected.IsCurrent)
                    {
                        if (affected.identity.IsAlive)
                        {
                            Vector2 away = (Vector2)affected.view.HitPosition - playerPosition;
                            affected.controller.BreakFormation(this, affected.view, away,
                                KnockbackDistance, settings.knockbackDuration, settings.stunDuration);
                        }
                        else
                        {
                            affected.controller.LeaveFormation(this, affected.view, 0f);
                        }
                    }

                    affected.controller = null;
                }

                return true;
            }
        }

        return false;
    }

    public void Dissolve(float stunDuration)
    {
        if (IsBroken)
        {
            return;
        }

        IsBroken = true;

        foreach (Member member in members)
        {
            // Vẫn gỡ nhóm khi vỏ đang tắt tạm, nhưng không tác động lên lượt spawn mới đã tái sử dụng vỏ.
            if (member.controller != null && member.IsCurrent)
            {
                member.controller.LeaveFormation(this, member.view, stunDuration);
            }

            member.controller = null;
        }
    }

    public void Dispose()
    {
        Dissolve(0f);
    }

    #endregion
}
