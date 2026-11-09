using System;
using UnityEngine;

// Shared combat and innate-skill activation. Derived controllers specialize movement and death.
public abstract class CharacterControllerBase : ICharacterController
{
    #region Runtime Identity And Lifecycle

    // Controller có state pause riêng mới cần override.
    public virtual void SetPaused(bool paused) { }
    // Controller có tài nguyên riêng mới cần thu hồi.
    public virtual void Dispose() { }
    private CharacterRuntimeData deathHandled; // Chặn xử lý chết nhiều lần trên cùng lượt spawn.
    private CharacterRuntimeData identity; // Danh tính runtime đang thuộc controller.
    private ulong generation; // Không dùng controller cũ khi pool tái sử dụng runtime.
    // Xử lý trạng thái chết do thay stats trước khi cập nhật chuyển động.
    public void Tick(CharacterView view, CharacterRuntimeData runtime)
    {
        if (!IsCurrent(view, runtime) || !view.isActiveAndEnabled) return;
        // Giảm max HP từ modifier cũng có thể làm current về 0; xử lý chết như khi nhận damage.
        if (!runtime.IsAlive) { CompleteDeath(view, runtime); return; }

        TickMovement(view, runtime);
    }

    // Lớp con quyết định input/AI và hướng di chuyển.
    protected abstract void TickMovement(CharacterView view, CharacterRuntimeData runtime);
    // Lớp con xử lý hành vi sau khi kết thúc combat lifetime.
    public abstract void OnDeath(CharacterView view, CharacterRuntimeData runtime);

    #endregion

    #region Combat Mutation

    // Giáp hấp thụ trước HP; runtime ghi lượng đã mất rồi phát Changed cho UI.
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
            if (IsCurrent(view, runtime))
                view.NotifyDamageReceived(damage, result);
        }
        finally
        {
            if (result.Killed) CompleteDeath(view, runtime);
        }
        return result;
    }

    // Thay đổi HP trực tiếp bỏ qua giáp; phần cộng tăng max/current và phát Changed, không hồi sinh nhân vật đã chết.
    public void ChangeHealth(CharacterView view, CharacterRuntimeData runtime, float amount)
    {
        if (float.IsNaN(amount) || float.IsInfinity(amount))
            throw new ArgumentOutOfRangeException(nameof(amount));
        if (!IsCurrent(view, runtime) || !view.isActiveAndEnabled || !runtime.IsAlive) return;
        try
        {
            runtime.SetVitals(Mathf.Max(0f, runtime.CurrentHealth + amount),
                runtime.CurrentShield);
        }
        finally
        {
            if (!runtime.IsAlive) CompleteDeath(view, runtime);
        }
    }

    // Gửi damage tới đúng lượt spawn của mục tiêu.
    public DamageResult DealDamage(CharacterView view, CharacterRuntimeData runtime,
        CharacterTarget target, float multiplier = 1f)
    {
        if (!IsCurrent(view, runtime) || !view.isActiveAndEnabled || !runtime.IsAlive ||
            !target.TryGetCharacter(out CharacterView victim)) return default;
        Vector2 away = view.HitPosition - victim.HitPosition;
        if (away.sqrMagnitude <= 0.000001f)
            away = view.GetInstanceID() < victim.GetInstanceID() ? Vector2.left : Vector2.right;
        DamageResult result = victim.ReceiveDamage(runtime.CreateDamage(multiplier));
        if (result.Accepted && IsCurrent(view, runtime) && runtime.IsAlive)
            OnAttack(view, runtime, away);
        return result;
    }

    // Lớp con có thể thêm phản ứng sau một đòn đánh thành công.
    protected virtual void OnAttack(CharacterView view, CharacterRuntimeData runtime, Vector2 away) { }

    // Cấp giáp qua runtime để max/current và thông báo UI được cập nhật cùng nhau.
    public void GrantShield(CharacterView view, CharacterRuntimeData runtime, float amount)
    {
        if (float.IsNaN(amount) || float.IsInfinity(amount) || amount < 0f)
            throw new ArgumentOutOfRangeException(nameof(amount));
        if (IsCurrent(view, runtime) && view.isActiveAndEnabled && runtime.IsAlive)
            runtime.AddShield(amount);
    }

    #endregion

    #region Owner Lifetime Guards

    // Chết từ damage hoặc giảm max đều dùng cùng một đường cleanup.
    private void CompleteDeath(CharacterView view, CharacterRuntimeData runtime)
    {
        if (!IsCurrent(view, runtime) || runtime.IsAlive || ReferenceEquals(deathHandled, runtime)) return;
        deathHandled = runtime;
        try { view.EndCombatLifetime(); }
        finally { if (IsCurrent(view, runtime)) OnDeath(view, runtime); }
    }

    // Callback không được tác động vào một spawn khác đã dùng lại cùng vỏ/runtime.
    private bool IsCurrent(CharacterView view, CharacterRuntimeData runtime)
    {
        if (view == null || runtime == null || !ReferenceEquals(view.RuntimeData, runtime)) return false;
        if (identity == null) { identity = runtime; generation = runtime.Generation; }
        return ReferenceEquals(identity, runtime) && runtime.Generation == generation;
    }
    #endregion
}
