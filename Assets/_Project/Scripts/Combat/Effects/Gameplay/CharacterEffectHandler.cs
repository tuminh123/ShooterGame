using System;
using System.Collections.Generic;

// Giữ luật stacking/vòng đời effect của đúng spawn; mọi thay đổi chỉ số đi qua modifier chung.
public sealed class CharacterEffectHandler : IDisposable
{
    #region Owner And Effect State

    private readonly CharacterView owner; // Vỏ nhân vật nhận effect.
    private readonly CharacterRuntimeData identity; // Runtime của lượt spawn này.
    private readonly ulong generation; // Không gỡ modifier trên spawn mới.
    private readonly EffectManager manager; // Quản lý thời hạn và pool runtime effect.
    private readonly Dictionary<long, float> slows = new Dictionary<long, float>(); // Mức slow từng nguồn, không phải stat thứ hai.
    private StatModifierLease slowModifier; // Một modifier MoveSpeed cho mức slow mạnh nhất.
    private long nextSlowId; // Token riêng để từng aura/effect gỡ đúng nguồn slow.
    public bool IsDisposed { get; private set; }
    public int Count => IsDisposed || identity.Generation != generation ? 0 : manager.CountFor(owner, identity);

    #endregion

    #region Initialization

    // Handler chỉ nhận context; không áp dụng effect trước khi có nguồn yêu cầu.
    public CharacterEffectHandler(CharacterView owner, EffectManager manager)
    {
        this.owner = owner;
        identity = owner.RuntimeData;
        generation = identity.Generation;
        this.manager = manager;
    }

    #endregion

    #region Slow Stacking

    // Giữ API cũ: hệ số 0.7 được nối thành modifier MoveSpeed Percent -30.
    internal long AddSpeedMultiplier(float multiplier)
    {
        if (IsDisposed || identity.Generation != generation) throw new ObjectDisposedException(nameof(CharacterEffectHandler));
        if (float.IsNaN(multiplier) || float.IsInfinity(multiplier) || multiplier < 0f || multiplier > 1f)
            throw new ArgumentOutOfRangeException(nameof(multiplier));
        long id = checked(++nextSlowId);
        slows.Add(id, 1f - multiplier);
        try { RefreshSlowModifier(); }
        catch
        {
            slows.Remove(id);
            RefreshSlowModifier();
            throw;
        }
        return id;
    }

    // Ra khỏi aura/hết hạn chỉ gỡ đúng nguồn, không bỏ slow của nguồn khác.
    internal void RemoveSpeedMultiplier(long id)
    {
        if (slows.Remove(id)) RefreshSlowModifier();
    }

    // Giữ luật slow mạnh nhất hiện có; hệ stat chỉ nhận một modifier chung, không biết luật gameplay này.
    private void RefreshSlowModifier()
    {
        if (identity.Generation != generation) return;
        if (slows.Count == 0) { ReleaseSlowModifier(); return; }
        float strongest = 0f;
        foreach (float fraction in slows.Values) if (fraction > strongest) strongest = fraction;
        var value = new StatModifier(StatType.MoveSpeed, StatOperation.Percent, -strongest * 100f);
        if (slowModifier != null) { slowModifier.Update(value); return; }
        StatModifierLease token = identity.AddModifier(value);
        if (IsDisposed || identity.Generation != generation) token.Dispose();
        else slowModifier = token;
    }

    // Bỏ ownership trước callback tính lại stat/UI.
    private void ReleaseSlowModifier()
    {
        StatModifierLease previous = slowModifier;
        slowModifier = null;
        previous?.Dispose();
    }

    #endregion

    #region Cleanup

    // EndOperation của manager có thể hoãn Remove; handler vẫn dọn slow tức thời khi owner kết thúc.
    public void Dispose()
    {
        if (IsDisposed) return;
        IsDisposed = true;
        try { if (identity.Generation == generation) manager.ClearTarget(owner, identity); }
        finally { slows.Clear(); ReleaseSlowModifier(); }
    }

    #endregion
}
