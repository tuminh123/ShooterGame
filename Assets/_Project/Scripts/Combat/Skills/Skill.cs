using System;

/// <summary>
/// Class nền cho mọi hành vi skill. Cung cấp phần gắn context và hành vi mặc định
/// cho các hàm không bắt buộc của <see cref="ISkill"/>.
/// </summary>
/// <remarks>
/// <see cref="SkillFactory"/> tạo mỗi hành vi bằng <c>new</c> theo <see cref="SkillBehaviourType"/>, nên mỗi skill
/// đang chạy là một object riêng và không có gì được lưu vào asset. Asset chỉ lưu enum, vì vậy đổi tên class
/// hành vi không làm mất dữ liệu.
/// </remarks>
public abstract class Skill : ISkill
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
    /// Gắn chủ sở hữu và dịch vụ ngay sau khi factory <c>new</c>. Data theo cấp được áp dụng sau,
    /// khi <see cref="SkillRuntime"/> đã nhận quyền sở hữu hành vi này.
    /// </summary>
    internal void Bind(CharacterSkills owner, SkillServices services)
    {
        Owner = owner;
        Services = services;
        InitializeRuntime();
    }

    /// <summary>Hook cho class con chuẩn bị state runtime (buffer, collection) sau khi đã có Owner/Services.</summary>
    protected virtual void InitializeRuntime() { }

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
