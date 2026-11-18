using System.Collections.Generic;

// Ranh giới của miền Combat: đây là TẤT CẢ những gì miền khác được phép dùng.
// Mọi interface trong file này do CombatManager implement. Skill, đạn, effect và VFX bên trong
// miền không được bind vào Zenject nên không nơi nào khác inject được chúng.
// Xem Docs/Architecture/Refactor-Architecture.md.

#region Skill Choice (UI chọn skill khi lên cấp)

/// <summary>Cấp hoặc nâng skill gameplay cho một nhân vật. Dùng bởi bảng chọn skill khi lên cấp.</summary>
public interface ISkillGrant
{
    /// <summary>
    /// Chưa có skill thì cấp mới ở cấp 1; đã có thì nâng thêm một cấp.
    /// Trả về false nếu skill không phải loại Gameplay, đã ở cấp tối đa, hoặc nhân vật không còn hợp lệ.
    /// </summary>
    bool GrantOrUpgrade(SkillDataSO definition, CharacterView owner);
}

/// <summary>Đọc các skill gameplay mà một nhân vật đang sở hữu. Dùng bởi UI chọn skill.</summary>
public interface IOwnedSkillReader
{
    /// <summary>Ghi các skill gameplay đang hoạt động của <paramref name="owner"/> vào <paramref name="result"/> (list được xóa trước).</summary>
    void GetOwnedSkills(CharacterView owner, List<SkillRuntime> result);

    /// <summary>Tìm runtime của một skill gameplay mà <paramref name="owner"/> đang sở hữu.</summary>
    bool TryGetRuntime(SkillDataSO definition, CharacterView owner, out SkillRuntime runtime);
}

#endregion

#region Game Flow (GameManager)

/// <summary>Dọn combat theo luồng game. Dùng bởi GameManager khi kết thúc trận, chơi lại hoặc về Home.</summary>
public interface ICombatLifecycle
{
    /// <summary>
    /// Thu hồi toàn bộ đạn, effect gameplay và VFX đang chạy, và dừng skill vốn có của mọi nhân vật còn lại
    /// (nhân vật đứng yên chờ kết quả hoặc sắp bị thu hồi). Skill gameplay dọn riêng bằng <see cref="ClearRunSkills"/>.
    /// </summary>
    void ClearCombat();

    /// <summary>Thu hồi mọi skill gameplay nhận được trong trận. Skill vốn có của nhân vật không bị ảnh hưởng.</summary>
    void ClearRunSkills();
}

#endregion

#region Prewarm (PoolPrewarmService)

/// <summary>Cấp phát trước bộ nhớ của combat trong bước loading, để không cấp phát giữa trận.</summary>
public interface ICombatCapacity
{
    /// <summary>Tạo sẵn runtime của effect làm chậm, effect sát thương theo thời gian, và chỗ cho số đạn tối đa.</summary>
    void PrewarmCapacity(int slowEffects, int damageOverTimeEffects, int projectiles);
}

#endregion
