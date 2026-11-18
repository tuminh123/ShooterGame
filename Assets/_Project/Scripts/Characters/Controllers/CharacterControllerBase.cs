using System;
using UnityEngine;

/// <summary>
/// Phần dùng chung của mọi controller: nhận/gây sát thương, thay đổi HP/giáp, xử lý chết, đếm feedback trạng thái.
/// Class con (<see cref="PlayerController"/>, <see cref="EnemyController"/>) chỉ quyết định di chuyển và phản ứng khi chết.
/// </summary>
/// <remarks>
/// Một controller gắn với đúng một lượt spawn: lần đầu nhận (view, runtime) nó ghi nhớ danh tính, và mọi lời gọi
/// sau đó thuộc lượt spawn khác đều bị bỏ qua (xem <see cref="IsCurrent"/>).
/// </remarks>
public abstract class CharacterControllerBase : ICharacterController
{
    #region State

    private CharacterHandle spawn; // Lượt spawn mà controller này phục vụ; ghi nhớ ở lời gọi đầu tiên.
    private bool spawnBound;
    private CharacterRuntimeData deathHandled; // Chặn xử lý chết hai lần trên cùng lượt spawn.

    // Số effect đang bật theo từng loại (index = CharacterFeedbackKind). Màu chỉ tắt khi effect cuối cùng của loại đó hết.
    private readonly int[] effectCounts = new int[4];
    private bool feedbackDisposed;

    /// <inheritdoc/>
    public event Action<CharacterFeedbackKind, bool> FeedbackChanged;

    #endregion

    #region Frame Update

    /// <inheritdoc/>
    public void Tick(CharacterView view, CharacterRuntimeData runtime)
    {
        if (!IsCurrent(view, runtime) || !view.isActiveAndEnabled) return;
        // Giảm max HP bằng modifier cũng có thể đưa HP về 0; xử lý chết giống như khi nhận sát thương.
        if (!runtime.IsAlive) { CompleteDeath(view, runtime); return; }
        TickMovement(view, runtime);
    }

    /// <summary>Class con quyết định hướng di chuyển: input với Player, AI với Enemy.</summary>
    protected abstract void TickMovement(CharacterView view, CharacterRuntimeData runtime);

    /// <inheritdoc/>
    /// <remarks>Mặc định không làm gì; chỉ Enemy trong đội hình cần xử lý va chạm sau skill.</remarks>
    public virtual void CompletePatternContact(CharacterHandle spawn) { }

    /// <inheritdoc/>
    /// <remarks>Mặc định không có state cần pause.</remarks>
    public virtual void SetPaused(bool paused) { }

    /// <inheritdoc/>
    /// <remarks>Mặc định luôn được đánh.</remarks>
    public virtual bool CanAttack => true;

    #endregion

    #region Combat

    /// <inheritdoc/>
    public DamageResult ReceiveDamage(CharacterView view, CharacterRuntimeData runtime, DamageInfo damage)
    {
        if (!IsCurrent(view, runtime) || !view.isActiveAndEnabled || !runtime.IsAlive ||
            damage.SourceTeam == runtime.Team || damage.Amount <= 0f)
            return default;

        float health = runtime.CurrentHealth;
        float shield = runtime.CurrentShield;
        float absorbed = Mathf.Min(shield, damage.Amount);
        float healthLost = Mathf.Min(health, damage.Amount - absorbed);
        var result = new DamageResult(healthLost, absorbed, healthLost >= health);

        try
        {
            runtime.SetVitals(health - healthLost, shield - absorbed);
            // SetVitals phát Changed đồng bộ; listener có thể đã despawn nhân vật, nên kiểm tra lại trước mỗi bước.
            if (IsCurrent(view, runtime))
            {
                if (result.Accepted) PublishFeedback(CharacterFeedbackKind.Hit, true);
                if (IsCurrent(view, runtime)) view.NotifyDamageReceived(damage, result);
            }
        }
        finally
        {
            // Chết phải được xử lý kể cả khi listener ném lỗi, nếu không nhân vật sẽ kẹt ở 0 HP mà không chết.
            if (result.Killed) CompleteDeath(view, runtime);
        }
        return result;
    }

    /// <inheritdoc/>
    public virtual DamageResult DealDamage(CharacterView view, CharacterRuntimeData runtime,
        CharacterHandle target, float multiplier = 1f)
    {
        if (!IsCurrent(view, runtime) || !view.isActiveAndEnabled || !runtime.IsAlive ||
            !target.TryGetCharacter(out CharacterView victim)) return default;

        // Hướng "đẩy ra" từ nạn nhân về phía người đánh; hai bên trùng vị trí thì chọn hướng cố định theo instance ID.
        Vector2 away = view.HitPosition - victim.HitPosition;
        if (away.sqrMagnitude <= 0.000001f)
            away = view.GetInstanceID() < victim.GetInstanceID() ? Vector2.left : Vector2.right;

        DamageResult result = victim.ReceiveDamage(runtime.CreateDamage(multiplier));
        if (result.Accepted && IsCurrent(view, runtime) && runtime.IsAlive)
            OnAttack(view, runtime, away);
        return result;
    }

    /// <summary>Phản ứng sau một đòn đánh trúng (ví dụ Enemy bị đẩy lùi). Mặc định không làm gì.</summary>
    protected virtual void OnAttack(CharacterView view, CharacterRuntimeData runtime, Vector2 away) { }

    /// <inheritdoc/>
    public void ChangeHealth(CharacterView view, CharacterRuntimeData runtime, float amount)
    {
        if (float.IsNaN(amount) || float.IsInfinity(amount))
            throw new ArgumentOutOfRangeException(nameof(amount));
        if (!IsCurrent(view, runtime) || !view.isActiveAndEnabled || !runtime.IsAlive) return;

        float previousHealth = runtime.CurrentHealth;
        try
        {
            runtime.SetVitals(Mathf.Max(0f, runtime.CurrentHealth + amount), runtime.CurrentShield);
            if (IsCurrent(view, runtime) && runtime.CurrentHealth < previousHealth)
                PublishFeedback(CharacterFeedbackKind.Hit, true);
        }
        finally
        {
            if (!runtime.IsAlive) CompleteDeath(view, runtime);
        }
    }

    /// <inheritdoc/>
    public void GrantShield(CharacterView view, CharacterRuntimeData runtime, float amount)
    {
        if (float.IsNaN(amount) || float.IsInfinity(amount) || amount < 0f)
            throw new ArgumentOutOfRangeException(nameof(amount));
        // Cấp qua runtime để max/current và thông báo UI được cập nhật cùng lúc.
        if (IsCurrent(view, runtime) && view.isActiveAndEnabled && runtime.IsAlive)
            runtime.AddShield(amount);
    }

    #endregion

    #region Death

    /// <summary>Phản ứng riêng sau khi lượt spawn kết thúc combat (Player đứng yên chờ Lose, Enemy chờ trả về pool…).</summary>
    protected abstract void OnDeath(CharacterView view, CharacterRuntimeData runtime);

    // Chết vì sát thương hay vì giảm max HP đều đi qua đúng một đường dọn dẹp này.
    private void CompleteDeath(CharacterView view, CharacterRuntimeData runtime)
    {
        if (!IsCurrent(view, runtime) || runtime.IsAlive || ReferenceEquals(deathHandled, runtime)) return;
        deathHandled = runtime;
        try { view.EndCombatLifetime(); }
        finally { if (IsCurrent(view, runtime)) OnDeath(view, runtime); }
    }

    #endregion

    #region Feedback

    /// <inheritdoc/>
    public void NotifyEffect(CharacterHandle target, CharacterFeedbackKind kind, bool active)
    {
        // Hit không đi qua đây (nó là nháy tức thời, không có trạng thái bật/tắt).
        if (feedbackDisposed || !target.IsCurrent || !IsCurrent(target.View, target.View.RuntimeData) ||
            kind == CharacterFeedbackKind.Hit || !Enum.IsDefined(typeof(CharacterFeedbackKind), kind)) return;

        int index = (int)kind;
        int previous = effectCounts[index];
        effectCounts[index] = active ? previous + 1 : Math.Max(0, previous - 1);
        // Chỉ báo khi chuyển giữa "không có" và "có ít nhất một"; effect thứ hai cùng loại không đổi màu.
        if ((previous > 0) != (effectCounts[index] > 0)) PublishFeedback(kind, effectCounts[index] > 0);
    }

    private void PublishFeedback(CharacterFeedbackKind kind, bool active)
    {
        Action<CharacterFeedbackKind, bool> listeners = FeedbackChanged;
        if (feedbackDisposed || listeners == null) return;
        foreach (Action<CharacterFeedbackKind, bool> listener in listeners.GetInvocationList())
        {
            // Listener có thể vừa trả nhân vật về pool; dừng thông báo của lượt spawn cũ.
            if (feedbackDisposed || !spawnBound || !spawn.IsCurrent) break;
            try { listener(kind, active); } catch (Exception exception) { Debug.LogException(exception); }
        }
    }

    #endregion

    #region Editor Debug

    /// <inheritdoc/>
    /// <remarks>Mặc định không vẽ gì; chỉ Enemy có phạm vi tách đàn.</remarks>
    public virtual void DrawSeparationGizmo(Vector3 position) { }

    #endregion

    #region Lifetime

    /// <summary>Thu hồi tài nguyên của controller khi lượt spawn kết thúc. Class con override phải gọi base.</summary>
    public virtual void Dispose()
    {
        feedbackDisposed = true;
        FeedbackChanged = null;
        Array.Clear(effectCounts, 0, effectCounts.Length);
    }

    // Lời gọi đầu tiên ghi nhớ lượt spawn; mọi lời gọi sau thuộc lượt spawn khác (vỏ hoặc runtime đã bị pool tái sử dụng) bị bỏ qua.
    private bool IsCurrent(CharacterView view, CharacterRuntimeData runtime)
    {
        if (view == null || runtime == null || !ReferenceEquals(view.RuntimeData, runtime)) return false;
        var call = new CharacterHandle(view, runtime);
        if (!spawnBound) { spawn = call; spawnBound = true; }
        return spawn.Equals(call); // Cùng vỏ, cùng runtime, cùng generation.
    }

    #endregion
}
