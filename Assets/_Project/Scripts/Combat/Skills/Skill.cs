using System;

// Các hàm mặc định cho hành vi; interface ISkill vẫn là hợp đồng thực thi.
[Serializable]
public abstract class Skill : ISkill
{
    #region Runtime Context

    [NonSerialized] protected CharacterSkills Owner; // Context của đúng lượt spawn, không ghi vào asset.
    [NonSerialized] protected SkillFactory Services; // Dịch vụ scene dùng chung, không sở hữu vòng đời của chúng.
    public abstract SkillBehaviourType Type { get; }

    #endregion

    #region Initialization

    // Factory chỉ clone mẫu; dữ liệu theo cấp được áp dụng sau khi runtime đã nhận ownership.
    internal Skill CreateRuntime(CharacterSkills owner, SkillFactory services)
    {
        var runtime = (Skill)MemberwiseClone();
        runtime.Owner = owner;
        runtime.Services = services;
        runtime.InitializeRuntime();
        return runtime;
    }

    // Hành vi có collection/state mutable phải cấp phát lại để không dùng chung với template.
    protected virtual void InitializeRuntime() { }

    #endregion

    #region Behaviour Lifecycle

    // Bắt buộc nhận đúng loại data; runtime chịu trách nhiệm chọn cấp.
    public abstract void ApplyData(SkillBehaviourData data);

    // Hành vi liên tục/buff không phải viết lại hàm cast rỗng.
    public virtual bool TryExecute() => false;

    // Chỉ cưa/aura hoặc hành vi duy trì cần override.
    public virtual void Tick(float deltaTime) { }

    // Hành vi có visual/state pause riêng mới cần override.
    public virtual void SetPaused(bool paused) { }

    // Hành vi sở hữu timer phụ, visual hoặc token stats phải thu hồi tại đây.
    public virtual void Dispose() { }

    #endregion
}
