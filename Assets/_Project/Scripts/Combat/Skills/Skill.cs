using System;

/// <summary>
/// Class nền cho mọi hành vi skill. Cung cấp phần tạo bản sao runtime và hành vi mặc định
/// cho các hàm không bắt buộc của <see cref="ISkill"/>.
/// </summary>
/// <remarks>
/// Instance nằm trong <see cref="SkillDataSO"/> là <b>template</b> và không bao giờ chạy trực tiếp.
/// Mỗi skill đang chạy dùng bản sao từ <see cref="CreateRuntime"/>, nhờ đó trạng thái runtime
/// (field <c>[NonSerialized]</c>) không bao giờ bị ghi vào asset dùng chung.
///
/// Không đổi tên class con của Skill: Unity lưu tên class trong asset (<c>[SerializeReference]</c>),
/// nên đổi tên sẽ làm mất dữ liệu đã cấu hình.
/// </remarks>
[Serializable]
public abstract class Skill : ISkill
{
    #region Runtime Context

    /// <summary>Nhân vật sở hữu skill, đúng lượt spawn hiện tại. Không ghi vào asset.</summary>
    [NonSerialized] protected CharacterSkills Owner;

    /// <summary>Các dịch vụ combat dùng chung của scene. Hành vi chỉ dùng, không sở hữu vòng đời của chúng.</summary>
    [NonSerialized] protected SkillServices Services;

    /// <inheritdoc/>
    public abstract SkillBehaviourType Type { get; }

    #endregion

    #region Runtime Creation

    /// <summary>
    /// Tạo bản sao chạy được từ template và gắn context. Data theo cấp được áp dụng sau,
    /// khi <see cref="SkillRuntime"/> đã nhận quyền sở hữu bản sao này.
    /// </summary>
    internal Skill CreateRuntime(CharacterSkills owner, SkillServices services)
    {
        // MemberwiseClone chỉ sao chép nông: field collection vẫn trỏ chung tới template,
        // vì vậy class con có state dạng collection phải cấp phát lại trong InitializeRuntime.
        var runtime = (Skill)MemberwiseClone();
        runtime.Owner = owner;
        runtime.Services = services;
        runtime.InitializeRuntime();
        return runtime;
    }

    /// <summary>Hook cho class con reset hoặc cấp phát lại state runtime ngay sau khi clone.</summary>
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
