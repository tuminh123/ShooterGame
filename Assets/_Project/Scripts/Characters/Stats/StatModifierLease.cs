using System;

// Quyền sở hữu một nhóm modifier; thay nguyên nhóm tại chỗ hoặc gỡ đúng nguồn.
public sealed class StatModifierLease : IDisposable
{
    #region Ownership

    private Action release; // Gỡ modifier một lần.
    private Action<StatModifier[]> update; // Thay config của cùng nguồn, không tháo/gắn lại.

    // Runtime gắn callback với ID và generation của đúng spawn.
    public StatModifierLease(Action release, Action<StatModifier[]> update)
    {
        this.release = release;
        this.update = update;
    }

    #endregion

    #region Modifier Lifecycle

    // Một skill/effect có thể tác động nhiều stat; runtime snapshot mảng mới và chỉ phát Changed một lần.
    public void Update(params StatModifier[] modifiers) => update?.Invoke(modifiers);

    // Bỏ callback trước khi gọi để cleanup lồng nhau an toàn.
    public void Dispose()
    {
        Action action = release;
        release = null;
        update = null;
        action?.Invoke();
    }

    #endregion
}
