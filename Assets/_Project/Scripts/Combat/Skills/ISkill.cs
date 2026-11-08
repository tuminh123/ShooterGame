// Serialized skill settings are separate from scheduled runtime actions.
public interface ISkillData { }

// Template có hỗ trợ cấp trong trận. UI không đoán khả năng nâng cấp từ MaxLevel cấu hình cũ.
public interface IRunLevelSkillData : ISkillData { }

// The scheduler knows only this behavior. Dependencies belong to each concrete skill.
public interface ISkill
{
    bool TryExecute();
}

// Cấp trong trận thuộc runtime, không ghi ngược vào SkillDataSO.Level (cấp cấu hình/vĩnh viễn).
public interface IRunLevelSkill
{
    void SetRunLevel(int level);
}

// Aura/quỹ đạo cần cập nhật liên tục, kể cả khi cooldown chưa sẵn sàng.
// Scheduler gọi interface này; không thêm Update lên từng behavior C# thuần.
public interface IContinuousSkill
{
    void Tick(float deltaTime);
    void SetPaused(bool paused);
}
