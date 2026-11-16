using System;

#region SkillConfiguration

/// <summary>Nhận cấu hình hành vi theo cấp do runtime lựa chọn.</summary>
public interface ISkillConfiguration
{
    SkillBehaviourType Type { get; }
    void ApplyData(SkillBehaviourData data);
}

#endregion

#region SkillExecution

/// <summary>Thực thi đòn chủ động và cập nhật hành vi liên tục.</summary>
public interface ISkillExecution
{
    bool TryExecute();
    void Tick(float deltaTime);
}

#endregion

#region SkillLifecycle

/// <summary>Đồng bộ pause và giải phóng tài nguyên hành vi.</summary>
public interface ISkillLifecycle : IDisposable
{
    void SetPaused(bool paused);
}

#endregion

#region Aggregate Contract

/// <summary>Gom các vai trò của Skill; không thêm hành vi.</summary>
public interface ISkill
{
    ISkillConfiguration GetConfiguration();
    ISkillExecution GetExecution();
    ISkillLifecycle GetLifecycle();
}

#endregion
