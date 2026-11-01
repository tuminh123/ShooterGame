using System;

// * Dữ liệu của MỘT skill; không biết manager, danh sách hoặc cách tự gỡ.
// * Đây là kiểu giá trị: bản sao không tự đồng bộ cooldown/trạng thái với bản manager đang giữ.
public struct SkillRuntime
{
    public long Id { get; } // * Mã duy nhất của lần đăng ký; dùng để tìm đúng runtime dù bên ngoài đang giữ bản sao cũ.
    internal readonly ISkill Behavior; // * Instance hành vi riêng; sao chép struct chỉ sao chép tham chiếu, không nhân bản behavior.
    internal readonly Func<bool> CanRun; // * Hàm cho phép cập nhật; false thì manager tạm dừng cả timer, null thì không chặn.
    internal readonly float Cooldown; // * Thời gian chờ sau mỗi lần thực hiện thành công, tính bằng giây.
    public bool IsActive { get; internal set; } // * Trạng thái tại thời điểm đọc; manager sở hữu bản dữ liệu hiện hành.
    public float CooldownRemaining { get; internal set; } // * Số giây còn chờ; manager ghi lại vào danh sách sau khi cập nhật.

    // * Chỉ khởi tạo dữ liệu; default(SkillRuntime) có Id = 0 và không đại diện cho skill đã đăng ký.
    internal SkillRuntime(long id, ISkill behavior, float cooldown, Func<bool> canRun)
    {
        Id = id;
        Behavior = behavior;
        Cooldown = cooldown;
        CanRun = canRun;
        IsActive = true;
        CooldownRemaining = 0f;
    }
}
