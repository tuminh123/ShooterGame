using System;

// Hợp đồng thực thi; cấp và cấu hình nằm ở SkillRuntime/SkillDataSO.
public interface ISkill : IDisposable
{
    SkillBehaviourType Type { get; } // Loại hành vi, không phải vị trí bắn phụ/chính.
    // Nhận trọn cấu hình của cấp hiện tại; không tự quyết định cấp.
    void ApplyData(SkillBehaviourData data);
    // Thành công mới bắt đầu cooldown; chưa có mục tiêu thì trả false.
    bool TryExecute();
    // Cập nhật hành vi liên tục kể cả khi cooldown cast chưa xong.
    void Tick(float deltaTime);
    // Đồng bộ visual/state của hành vi với pause của owner.
    void SetPaused(bool paused);
}

// Giá trị enum được serialize; chỉ thêm ở cuối để giữ tương thích asset.
public enum SkillBehaviourType { NormalAttack, Shooting, OrbitSaw, SlowAura, Regeneration, EmergencyShield, StatBuff }
