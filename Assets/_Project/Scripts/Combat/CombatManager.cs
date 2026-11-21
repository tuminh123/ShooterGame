using System;
using System.Collections.Generic;
using Zenject;

/// <summary>
/// Manager của miền Combat: sở hữu và điều phối skill, đạn, effect gameplay và VFX.
/// Đây là class <b>duy nhất</b> của Combat được bind vào Zenject.
/// </summary>
/// <remarks>
/// <code>
/// CombatManager
/// ├── EffectRuntimePool → EffectSystem   effect gameplay (slow, burn, poison)
/// ├── CombatVfx                           VFX chạy một lần (trúng đòn, nổ)
/// ├── SkillAreaVisuals                    visual vùng/lưỡi skill đang hiển thị (aura, cưa xoay)
/// ├── ProjectileSystem                   trọn vòng đời đạn: spawn → bay → quét va chạm → thu hồi
/// ├── SkillServices → SkillFactory
/// ├── SkillSystem                        skill gameplay nhận trong trận
/// └── CharacterSkillSystem                skill vốn có của từng nhân vật; nghe vòng đời nhân vật từ miền Character
/// </code>
/// Các system con được tạo bằng <c>new</c> ngay trong constructor, nên toàn bộ cách nối dây nằm ở một chỗ,
/// đọc từ trên xuống. Miền khác chỉ thấy các interface trong <c>CombatContracts.cs</c>.
/// Chiều phụ thuộc: Combat → Character (nghe <see cref="ICharacterRegistry"/>/<see cref="ICharacterDeathEvents"/>);
/// miền Character không biết Combat tồn tại.
/// </remarks>
public sealed class CombatManager :
    DomainManager,
    ISkillGrant,
    IOwnedSkillReader,
    ICombatLifecycle,
    ICombatCapacity,
    ITickable,
    ILateTickable,
    IDisposable
{
    #region Owned Systems

    private readonly EffectRuntimePool effectRuntimes;
    private readonly EffectSystem effects;
    private readonly CombatVfx vfx;
    private readonly SkillAreaVisuals areaVisuals;
    private readonly ProjectileSystem projectiles;
    private readonly SkillFactory skillFactory;
    private readonly SkillSystem skills;
    private readonly CharacterSkillSystem characterSkills;
    private bool disposed;

    #endregion

    #region Construction

    /// <summary>
    /// Dựng toàn bộ miền Combat. Thứ tự tạo đi theo chiều phụ thuộc: system nào được dùng thì phải tạo trước.
    /// </summary>
    /// <param name="characters">Danh sách nhân vật (miền Character): tạo/dọn skill vốn có khi nhân vật xuất hiện/bị thu hồi.</param>
    /// <param name="characterDeaths">Sự kiện chết (miền Character): dừng skill và effect ngay khi nhân vật chết.</param>
    public CombatManager(
        IGameObjectPool pools,
        CombatAssetsSO combatAssets,
        IMovementRegistration movementRegistration, // Các vai trò Simulation mà đạn dùng (SimulationContracts.cs).
        IMovementCommand movementCommand,
        ISpatialResolution spatialResolution,
        ISpatialQuery spatialQuery,
        ISpatialQueryBuilder queryBuilder,
        ICharacterRegistry characters,
        ICharacterDeathEvents characterDeaths)
    {
        try
        {
            effectRuntimes = new EffectRuntimePool(); // Số lượng tạo sẵn do GamePrewarm gọi PrewarmCapacity.
            effects = new EffectSystem(effectRuntimes);
            vfx = new CombatVfx(pools, combatAssets);
            areaVisuals = new SkillAreaVisuals();
            // Đạn cần EffectSystem (áp effect khi trúng) và CombatVfx (phát VFX trúng đòn).
            projectiles = new ProjectileSystem(pools, combatAssets, movementRegistration, movementCommand,
                spatialResolution, spatialQuery, queryBuilder, effects, vfx);
            // Hành vi skill cần ProjectileSystem để bắn, pool để thuê visual, EffectSystem để áp aura/slow
            // và SkillAreaVisuals để visual vùng/lưỡi được tick.
            skillFactory = new SkillFactory(new SkillServices(projectiles, pools, effects, areaVisuals));
            skills = new SkillSystem(skillFactory);
            // Tạo sau cùng: khi nhân vật chết/thu hồi, nó dọn cả skill gameplay (SkillSystem) và effect (EffectSystem).
            characterSkills = new CharacterSkillSystem(characters, characterDeaths, skillFactory, skills, effects);
        }
        catch
        {
            // ProjectileSystem giữ NativeArray và đăng ký Camera.onPreCull ngay khi được tạo.
            // Nếu bước sau lỗi (ví dụ CombatAssetsSO cấu hình sai) thì phải dọn những gì đã tạo.
            Dispose();
            throw;
        }
    }

    #endregion

    #region Frame Update

    /// <summary>
    /// Thứ tự bắt buộc: effect → skill gameplay → skill vốn có của nhân vật → đạn.
    /// Effect hết hạn được gỡ trước (ví dụ slow hết hiệu lực), skill thấy chỉ số mới rồi mới bắn,
    /// và đạn vừa bắn được đăng ký kịp để cùng quét va chạm trong frame này.
    /// </summary>
    /// <remarks>
    /// Skill vốn có trước đây được tick trong CharacterView (Tick 300, sau controller). Giờ chúng chạy ở đây (Tick -100).
    /// An toàn vì controller chỉ gửi lệnh di chuyển, còn vị trí được áp ở LateTick, nên skill vẫn thấy đúng vị trí;
    /// và va chạm đội hình (CompletePatternContact) vẫn chạy sau skill, trong Tick của Character.
    /// </remarks>
    public override void Tick()
    {
        if (disposed) return;
        float deltaTime = UnityEngine.Time.deltaTime;
        effects.Tick();
        skills.Tick();
        characterSkills.Tick(deltaTime);
        projectiles.Tick();
    }

    /// <summary>
    /// Phần hình ảnh của Combat, sau khi Simulation đã áp di chuyển và visual nhân vật đã cập nhật:
    /// vùng/lưỡi skill bám theo chủ trước, rồi VFX kiểm tra đã phát xong để trả về pool.
    /// </summary>
    public override void LateTick()
    {
        if (disposed) return;
        areaVisuals.LateTick();
        vfx.LateTick();
    }

    #endregion

    #region ISkillGrant, IOwnedSkillReader

    /// <inheritdoc/>
    public bool GrantOrUpgrade(SkillDataSO definition, CharacterView owner)
        => skills.GrantOrUpgrade(definition, characterSkills.GetContext(owner));

    /// <inheritdoc/>
    public void GetOwnedSkills(CharacterView owner, List<SkillRuntime> result) => skills.GetOwnedSkills(owner, result);

    /// <inheritdoc/>
    public bool TryGetRuntime(SkillDataSO definition, CharacterView owner, out SkillRuntime runtime)
        => skills.TryGetRuntime(definition, owner, out runtime);

    #endregion

    #region ICombatLifecycle

    /// <inheritdoc/>
    public void ClearCombat()
    {
        // Đạn trước: đạn đang bay có thể áp effect và phát VFX khi trúng, nên dừng nguồn sinh trước.
        projectiles.ClearProjectiles();
        // Skill vốn có tiếp theo: aura/cưa xoay đang giữ visual và effect duy trì.
        characterSkills.StopAll();
        effects.ClearAll();
        vfx.ClearEffects();
    }

    /// <inheritdoc/>
    public void ClearRunSkills() => skills.ClearRunSkills();

    #endregion

    #region ICombatCapacity

    /// <inheritdoc/>
    public void PrewarmCapacity(int slowEffects, int damageOverTimeEffects, int projectileCount)
    {
        effectRuntimes.Prewarm(slowEffects, damageOverTimeEffects);
        projectiles.PrewarmCapacity(projectileCount);
    }

    #endregion

    #region Disposal

    /// <summary>
    /// Thứ tự: skill vốn có → skill gameplay → đạn → VFX → effect.
    /// Skill dọn trước vì hành vi skill có thể đang giữ visual, modifier chỉ số hoặc effect duy trì (aura);
    /// CharacterSkillSystem còn gỡ listener khỏi miền Character.
    /// Phần còn lại giữ đúng thứ tự cũ khi mỗi system còn được Zenject dispose riêng (175 → 160 → 150).
    /// Mỗi bước được cô lập để lỗi của một system không chặn việc dọn các system còn lại,
    /// đặc biệt là NativeArray của ProjectileSystem (không dọn sẽ bị leak).
    /// </summary>
    public override void Dispose()
    {
        if (disposed) return;
        disposed = true;
        DisposeSafely(characterSkills);
        DisposeSafely(skills);
        DisposeSafely(projectiles);
        DisposeSafely(vfx);
        DisposeSafely(effects);
        areaVisuals?.Clear();
    }

    // System có thể null nếu constructor lỗi giữa chừng.
    private static void DisposeSafely(IDisposable system)
    {
        if (system == null) return;
        try { system.Dispose(); }
        catch (Exception exception) { UnityEngine.Debug.LogException(exception); }
    }

    #endregion
}
