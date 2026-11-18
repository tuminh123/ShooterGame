using System;
using System.Collections.Generic;

// Ranh giới của miền Run: đây là TẤT CẢ những gì miền khác được phép dùng.
// Mọi interface trong file này do RunManager implement. RunClock, RunStatistics, RunLevel, LootSystem và SkillChoice
// nằm bên trong miền, không được bind vào Zenject.
// Run nằm trên Character và Combat (gọi xuống trực tiếp); Game và UI dùng Run qua các vai trò dưới đây.
// Chiều ngược (lên cấp, số liệu trận thay đổi, reset) chỉ đi qua C# event.
// Xem Docs/Architecture/Refactor-Architecture.md.

#region Reader (HUD, popup kết quả, bảng chọn skill)

/// <summary>
/// Đọc tiến trình và số liệu của lượt chơi hiện tại. Dùng bởi HUD trận (RunUI), popup kết quả và bảng chọn skill.
/// </summary>
/// <remarks>Một vai trò đọc chung vì cả ba nơi dùng đều là UI hiển thị cùng một lượt chơi.</remarks>
public interface IRunReader
{
    /// <summary>Level hiện tại trong trận, bắt đầu từ 1.</summary>
    int Level { get; }

    /// <summary>EXP dư ở cấp hiện tại (không phải tổng EXP đã nhặt).</summary>
    int Experience { get; }

    /// <summary>EXP cần cho lần lên cấp tiếp theo; 0 khi đã đạt trần.</summary>
    int ExperienceToNextLevel { get; }

    /// <summary>Vàng nhặt được trong trận (đã cộng vào ví ngay lúc nhặt; đây chỉ là thống kê).</summary>
    int Gold { get; }

    /// <summary>Số giây gameplay của lượt, chỉ tăng khi Player còn sống; chịu ảnh hưởng timeScale.</summary>
    int ElapsedSeconds { get; }

    /// <summary>Số quái bị hạ trong lượt.</summary>
    int EnemiesDefeated { get; }

    /// <summary>
    /// (level cũ, level mới). Một lần nhận EXP có thể tăng nhiều cấp: chênh lệch là số lượt chọn skill cần mở.
    /// </summary>
    event Action<int, int> LevelUp;

    /// <summary>(EXP dư, EXP cần cho cấp tiếp); yêu cầu bằng 0 khi đạt trần.</summary>
    event Action<int, int> ExperienceChanged;

    event Action<int> GoldChanged;
    event Action<int> TimeChanged;
    event Action<int> EnemiesDefeatedChanged;

    /// <summary>
    /// Lượt mới bắt đầu (level 1, EXP 0, số liệu về 0). Phát sau khi mọi system của Run đã reset,
    /// nên bên nghe đọc được giá trị mới ngay trong callback.
    /// </summary>
    event Action RunReset;
}

#endregion

#region Lifecycle (GameManager)

/// <summary>Reset lượt chơi theo luồng game. Dùng bởi GameManager khi vào trận mới, chơi lại hoặc về Home.</summary>
public interface IRunLifecycle
{
    /// <summary>
    /// Dọn skill gameplay (Combat), thu hồi loot trên mặt đất, đưa level/EXP, đồng hồ và thống kê về đầu lượt,
    /// rồi phát <see cref="IRunReader.RunReset"/>. Không được gọi từ trong callback của chính miền Run.
    /// </summary>
    void ResetRun();
}

#endregion

#region Skill Choice (bảng chọn skill khi lên cấp)

/// <summary>
/// Lượt chọn skill khi lên cấp. Dùng bởi SkillChoiceController (UI): UI lo pause, slow motion và panel;
/// Run lo đếm lượt, roll lựa chọn và áp dụng qua Combat.
/// </summary>
public interface ISkillChoice
{
    /// <summary>Số lượt chọn đang chờ.</summary>
    int PendingChoices { get; }

    /// <summary>Còn lượt chờ và Player nhận skill còn sống.</summary>
    bool CanChoose { get; }

    /// <summary>Player nhận skill (để UI hiển thị các skill đang sở hữu); null khi không có Player.</summary>
    CharacterView ChoiceOwner { get; }

    /// <summary>(số lượt trước, số lượt sau): tăng khi lên cấp, giảm khi chọn xong hoặc mọi skill đã max.</summary>
    event Action<int, int> PendingChanged;

    /// <summary>Mọi lượt chờ bị hủy: reset lượt, đổi Player, Player chết/bị thu hồi, hoặc UI gọi <see cref="CancelChoices"/>.</summary>
    event Action ChoicesCanceled;

    /// <summary>
    /// Roll tối đa 3 lựa chọn vào <paramref name="result"/> (list bị xóa trước). <paramref name="reroll"/> = true thì ưu tiên
    /// skill khác các lựa chọn đang có trong list. Không còn ứng viên hợp lệ thì list rỗng.
    /// </summary>
    void Roll(List<SkillChoiceOffer> result, bool reroll);

    /// <summary>Áp dụng lựa chọn; true nếu đã tiêu một lượt. Lỗi gameplay được log và skill đó bị chặn tới khi retry.</summary>
    bool Choose(SkillChoiceOffer offer);

    /// <summary>Hủy mọi lượt chờ (UI gọi khi rời Playing).</summary>
    void CancelChoices();

    /// <summary>Bỏ chặn các skill đã lỗi để roll lại.</summary>
    void RetryFailedChoices();
}

#endregion
