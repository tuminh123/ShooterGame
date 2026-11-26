using System;
using System.Collections.Generic;

/// <summary>
/// Class nền cho mọi hành vi skill. Cung cấp phần gắn context và hành vi mặc định
/// cho các hàm không bắt buộc của <see cref="ISkill"/>.
/// </summary>
/// <remarks>
/// <see cref="SkillFactory"/> tạo hành vi theo <see cref="SkillBehaviourType"/> và <b>dùng lại</b> chúng qua
/// <see cref="Pool{T}"/> (mỗi loại một pool), nên không có gì được lưu vào asset. Asset chỉ lưu enum, vì vậy đổi tên
/// class hành vi không làm mất dữ liệu.
/// <para>
/// Vòng đời: Rent → <see cref="Bind"/> (gọi <see cref="InitializeRuntime"/>) → ApplyData → … → Dispose → trả pool
/// (<see cref="IPoolable.ResetForPool"/> cũng gọi <see cref="InitializeRuntime"/>). Vì vậy InitializeRuntime phải
/// <b>xóa</b> collection có sẵn thay vì tạo mới, và không được dùng Owner/Services.
/// </para>
/// </remarks>
public abstract class Skill : ISkill, IPoolable
{
    #region Runtime Context

    /// <summary>Nhân vật sở hữu skill, đúng lượt spawn hiện tại. Không ghi vào asset.</summary>
    protected CharacterSkills Owner;

    /// <summary>Các dịch vụ combat dùng chung của scene. Hành vi chỉ dùng, không sở hữu vòng đời của chúng.</summary>
    protected SkillServices Services;

    /// <inheritdoc/>
    public abstract SkillBehaviourType Type { get; }

    #endregion

    #region Runtime Creation

    /// <summary>
    /// Gắn chủ sở hữu và dịch vụ ngay sau khi factory lấy hành vi ra. Data theo cấp được áp dụng sau,
    /// khi <see cref="SkillRuntime"/> đã nhận quyền sở hữu hành vi này.
    /// </summary>
    internal void Bind(CharacterSkills owner, SkillServices services)
    {
        Owner = owner;
        Services = services;
        InitializeRuntime();
    }

    /// <summary>
    /// Đưa state runtime về ban đầu (data, cờ, bộ đếm). Gọi cả khi gắn chủ mới lẫn khi trả về pool:
    /// xóa collection có sẵn thay vì tạo mới, không dùng Owner/Services.
    /// </summary>
    protected virtual void InitializeRuntime() { }

    // Pool gọi sau Dispose: bỏ tham chiếu tới chủ và dịch vụ, rồi đưa state về ban đầu.
    void IPoolable.ResetForPool()
    {
        Owner = null;
        Services = null;
        InitializeRuntime();
    }

    /// <summary>
    /// Snapshot danh sách effect của cấp: dùng lại mảng cũ nếu cùng nội dung (cùng loại quái spawn lại thì không cấp phát),
    /// khác thì tạo mảng mới — đạn đang bay vẫn giữ đúng mảng của lúc bắn.
    /// </summary>
    protected static IGameplayEffect[] SnapshotEffects(IGameplayEffect[] current, IReadOnlyList<IGameplayEffect> source)
    {
        int count = source != null ? source.Count : 0;
        if (count == 0) return Array.Empty<IGameplayEffect>();
        if (current != null && current.Length == count)
        {
            bool same = true;
            for (int i = 0; i < count && same; i++) same = ReferenceEquals(current[i], source[i]);
            if (same) return current;
        }
        var snapshot = new IGameplayEffect[count];
        for (int i = 0; i < count; i++) snapshot[i] = source[i];
        return snapshot;
    }

    #endregion

    #region ISkill Defaults

    /// <inheritdoc/>
    public abstract void ApplyData(SkillBehaviourData data);

    /// <inheritdoc/>
    /// <remarks>Mặc định không có đòn chủ động, dành cho hành vi liên tục hoặc buff.</remarks>
    public virtual bool TryExecute() => false;

    /// <inheritdoc/>
    /// <remarks>Mặc định không làm gì; chỉ hành vi liên tục như cưa xoay hay aura mới override.</remarks>
    public virtual void Tick(float deltaTime) { }

    /// <inheritdoc/>
    /// <remarks>Mặc định không làm gì; chỉ hành vi có visual hoặc timer riêng mới override.</remarks>
    public virtual void SetPaused(bool paused) { }

    /// <summary>Thu hồi timer phụ, visual hoặc token chỉ số mà hành vi đang giữ.</summary>
    public virtual void Dispose() { }

    #endregion
}
