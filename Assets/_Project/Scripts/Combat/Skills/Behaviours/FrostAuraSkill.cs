using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>Hành vi aura băng: làm chậm mọi đối thủ trong bán kính, và áp effect khi chúng đi vào vùng.</summary>
/// <remarks>Hành vi liên tục. Slow là effect duy trì, gỡ ngay khi mục tiêu rời vùng. Data: <see cref="FrostAuraData"/>.</remarks>
public sealed class FrostAuraSkill : Skill
{
    #region Runtime State

    private FrostAuraData data; // Cấu hình đang hoạt động.
    private IGameplayEffect[] effectSnapshot; // Danh sách effect của đúng cấp.
    private Dictionary<CharacterHandle, EffectHandle> slowed; // Handle runtime riêng của từng mục tiêu trong vùng.
    private SlowEffect slowEffect; // Cấu hình slow dùng chung; manager sở hữu runtime.
    private HashSet<CharacterHandle> inside; // Mục tiêu được query trong bước này.
    private List<CharacterHandle> leaving; // Buffer gỡ effect không còn trong vùng.
    private CharacterHandle[] targets; // Buffer query dùng lại.
    private SkillAreaVisual visual; // Visual đang thuê pool.
    private float radius; // Bán kính được dùng khi query.
    private bool paused; // Pause phải gỡ slow duy trì.
    private bool disposed; // Cleanup một lần.
    private int revision; // Chặn vòng Tick cũ sau callback thay data.
    public override SkillBehaviourType Type => SkillBehaviourType.SlowAura;

    #endregion

    #region Configuration

    // Cấp phát state độc lập cho đúng owner.
    protected override void InitializeRuntime()
    {
        data = null;
        effectSnapshot = Array.Empty<IGameplayEffect>();
        slowed = new Dictionary<CharacterHandle, EffectHandle>();
        slowEffect = null;
        inside = new HashSet<CharacterHandle>();
        leaving = new List<CharacterHandle>();
        targets = Array.Empty<CharacterHandle>();
        visual = null;
        radius = 0f;
        revision = 0;
        paused = disposed = false;
    }

    // Đổi data sẽ gỡ runtime cũ; bước Tick tiếp theo áp dụng đúng vùng và mức slow mới.
    public override void ApplyData(SkillBehaviourData configuration)
    {
        var next = configuration as FrostAuraData ?? throw new ArgumentException("FrostAuraSkill requires FrostAuraData.");
        next.Validate();
        revision++;
        ClearSlow();
        if (visual != null && data != null && data.VisualPrefab != next.VisualPrefab)
        {
            Services.Pools.Despawn(visual.gameObject);
            visual = null;
        }
        data = next;
        slowEffect = new SlowEffect(next.SlowFraction);
        radius = next.Radius;
        effectSnapshot = next.Effects.ToArray();
        if (visual != null) visual.SetRadius(radius);
    }

    #endregion

    #region Continuous Execution And Cleanup
    // Cập nhật vùng tác động bằng truy vấn không gian chung, không dùng Physics2D.
    public override void Tick(float deltaTime)
    {
        if (disposed || paused || !Owner.CanRun || deltaTime <= 0f) return;

        EnsureVisual();
        int found = Owner.Owner.Search.FindAll(Owner.Owner.HitPosition, radius,
            CharacterSearch.OpponentOf(Owner.Identity.Team), ref targets, Owner.Owner);
        inside.Clear();
        int tickRevision = revision; // Callback stats/damage có thể đổi data (revision++) giữa chừng: khi đó dừng vòng này.
        try
        {
            for (int i = 0; i < found && StillValid(tickRevision); i++)
                TryEnterAura(targets[i], tickRevision);
            RemoveTargetsThatLeft();
        }
        finally
        {
            Array.Clear(targets, 0, targets.Length);
            inside.Clear();
            leaving.Clear();
        }
    }

    // Aura vẫn chạy đúng lượt data của frame này: chưa dispose/pause, chủ còn hoạt động, data chưa bị thay.
    private bool StillValid(int tickRevision) => !disposed && !paused && Owner.CanRun && tickRevision == revision;

    // Thuê visual vùng băng lần đầu Tick; visual tự bám theo chủ qua SkillAreaVisuals.
    private void EnsureVisual()
    {
        if (visual != null) return;
        GameObject clone = Services.Pools.Spawn(data.VisualPrefab.gameObject, Owner.Owner.HitPosition, Quaternion.identity,
            beforeActivate: item => item.GetComponent<SkillAreaVisual>().Initialize(Owner.Owner, radius, Services.AreaVisuals));
        if (clone == null) throw new InvalidOperationException("Could not spawn frost visual.");
        visual = clone.GetComponent<SkillAreaVisual>();
    }

    // Mục tiêu đang trong vùng: ghi nhận, và nếu vừa bước vào thì gắn slow duy trì + effect một lần.
    private void TryEnterAura(CharacterHandle target, int tickRevision)
    {
        if (!target.TryGetCharacter(out _)) return;
        inside.Add(target);
        if (slowed.ContainsKey(target)) return;

        EffectHandle handle = Services.Effects.ApplyMaintained(slowEffect, target, Owner.Identity.Team);
        // Stats callback có thể vừa thu hồi chính chủ aura/mục tiêu.
        if (!StillValid(tickRevision) || !target.TryGetCharacter(out _))
        {
            Services.Effects.Remove(handle);
            return;
        }
        slowed.Add(target, handle);

        // Chỉ áp dụng lúc vào vùng; không spam hoặc gia hạn duration mỗi frame.
        if (Services.Effects == null) return;
        foreach (IGameplayEffect effect in effectSnapshot)
        {
            if (!Owner.CanRun || disposed || tickRevision != revision) break;
            Services.Effects.Apply(effect, target, Owner.Identity.Team, Owner.Identity.FinalStats.effectHit);
        }
    }

    // Gỡ slow của mục tiêu không còn trong vùng hoặc đã bị thu hồi.
    private void RemoveTargetsThatLeft()
    {
        leaving.Clear();
        foreach (CharacterHandle target in slowed.Keys)
            if (!inside.Contains(target) || !target.TryGetCharacter(out _)) leaving.Add(target);
        foreach (CharacterHandle target in leaving) RemoveSlow(target);
    }

    private void RemoveSlow(CharacterHandle target)
    {
        if (!slowed.TryGetValue(target, out var entry)) return;
        slowed.Remove(target); // Gỡ ownership trước callback tính lại stats.
        Services.Effects.Remove(entry);
    }

    private void ClearSlow()
    {
        // Không giữ enumerator khi gỡ effect có thể phát callback. Mỗi handle thuộc đúng một nguồn aura.
        while (slowed.Count > 0)
        {
            CharacterHandle target;
            using (var iterator = slowed.Keys.GetEnumerator()) { iterator.MoveNext(); target = iterator.Current; }
            try { RemoveSlow(target); }
            catch (Exception exception) { Debug.LogException(exception); }
        }
    }

    // Đồng bộ visual và trạng thái liên tục với pause của Owner.
    public override void SetPaused(bool value)
    {
        if (paused == value) return;
        paused = value;
        if (visual != null) visual.SetVisible(!value);
        if (value) ClearSlow();
    }

    // Thu hồi visual và state của đúng instance; gọi nhiều lần vẫn an toàn.
    public override void Dispose()
    {
        if (disposed) return;
        disposed = true;
        try { ClearSlow(); }
        finally
        {
            if (visual != null) Services.Pools.Despawn(visual.gameObject);
            visual = null;
            Array.Clear(targets, 0, targets.Length);
        }
    }


    #endregion
}
