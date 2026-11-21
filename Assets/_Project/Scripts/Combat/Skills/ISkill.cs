using System;

/// <summary>
/// Strategy của một <b>hành vi skill</b> (bắn, cưa xoay, aura làm chậm, hồi máu…).
/// </summary>
/// <remarks>
/// <see cref="SkillDataSO"/> chỉ lưu loại hành vi (<see cref="SkillBehaviourType"/>); <see cref="SkillFactory"/>
/// tạo đúng class bằng một switch duy nhất. <see cref="SkillRuntime"/> gọi các hàm
/// theo thứ tự: <see cref="ApplyData"/> khi mở hoặc lên cấp → mỗi frame <see cref="Tick"/> rồi
/// <see cref="TryExecute"/> khi hết cooldown → <see cref="IDisposable.Dispose"/> khi skill kết thúc.
///
/// Mọi hành vi đều thay thế được cho nhau (LSP): nơi dùng không kiểm tra kiểu cụ thể.
/// Class nền <see cref="Skill"/> cung cấp hành vi mặc định cho các hàm không bắt buộc.
/// </remarks>
public interface ISkill : IDisposable
{
    /// <summary>Loại hành vi. Dùng để ghép template với data của từng cấp; mỗi skill có tối đa một hành vi mỗi loại.</summary>
    SkillBehaviourType Type { get; }

    /// <summary>Nhận thông số của cấp hiện tại. Được gọi lại mỗi lần lên cấp, không reset cooldown đang chạy.</summary>
    void ApplyData(SkillBehaviourData data);

    /// <summary>Cập nhật hành vi liên tục (cưa xoay, aura…). Chạy mọi frame, kể cả khi đang chờ cooldown.</summary>
    void Tick(float deltaTime);

    /// <summary>
    /// Thực hiện một đòn chủ động khi hết cooldown. Trả về true nếu đòn thực sự diễn ra;
    /// trả về false (ví dụ không có mục tiêu) thì cooldown không bị tiêu.
    /// </summary>
    bool TryExecute();

    /// <summary>Dừng hoặc tiếp tục hành vi, gồm cả visual và timer riêng của nó.</summary>
    void SetPaused(bool paused);
}
