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
/// ├── EffectRuntimePool → EffectManager   effect gameplay (slow, burn, poison)
/// ├── CombatVfx                           VFX chạy một lần (trúng đòn, nổ)
/// ├── ProjectileManager                   trọn vòng đời đạn: spawn → bay → quét va chạm → thu hồi
/// ├── SkillServices → SkillFactory
/// ├── SkillManager                        skill gameplay nhận trong trận
/// └── CharacterSkillSystem                skill vốn có của từng nhân vật; nghe vòng đời nhân vật từ miền Character
/// </code>
/// Các system con được tạo bằng <c>new</c> ngay trong constructor, nên toàn bộ cách nối dây nằm ở một chỗ,
/// đọc từ trên xuống. Miền khác chỉ thấy các interface trong <c>CombatContracts.cs</c>.
/// Chiều phụ thuộc: Combat → Character (nghe <see cref="ICharacterRegistry"/>/<see cref="ICharacterDeathEvents"/>);
/// miền Character không biết Combat tồn tại.
/// </remarks>
public sealed class CombatManager :
    ISkillGrant,
    IOwnedSkillReader,
    ICombatLifecycle,
    ICombatCapacity,
    ITickable,
    IDisposable
{
    #region Owned Systems

    private readonly EffectRuntimePool effectRuntimes;
    private readonly EffectManager effects;
    private readonly CombatVfx vfx;
    private readonly ProjectileManager projectiles;
    private readonly SkillFactory skillFactory;
    private readonly SkillManager skills;
    private readonly CharacterSkillSystem characterSkills;
    private bool disposed;

    #endregion

    #region Construction

    /// <summary>
    /// Dựng toàn bộ miền Combat. Thứ tự tạo đi theo chiều phụ thuộc: system nào được dùng thì phải tạo trước.
    /// </summary>
    /// <param name="characters">Danh sách nhân vật (miền Character): tạo/dọn skill vốn có khi nhân vật xuất hiện/bị thu hồi.</param>
    /// <param name="characterDeaths">Sự kiện chết (miền Character): dừng skill và effect ngay khi nhân vật chết.</param>
    /// <param name="slowRuntimes">Pool runtime làm chậm; installer bind kèm số lượng tạo sẵn.</param>
    /// <param name="damageOverTimeRuntimes">Pool runtime sát thương theo thời gian (burn, poison dùng chung).</param>
    public CombatManager(
        IPoolSystem poolSystem,
        CombatAssetsSO combatAssets,
        IMovementRegistration movementRegistration, // Các vai trò Simulation mà đạn dùng (SimulationContracts.cs).
        IMovementCommand movementCommand,
        ISpatialResolution spatialResolution,
        ISpatialQuery spatialQuery,
        ISpatialQueryBuilder queryBuilder,
        ICharacterRegistry characters,
        ICharacterDeathEvents characterDeaths,
        ILevelService levelService,
        ClassPool<SlowEffectRuntime> slowRuntimes,
        ClassPool<DamageOverTimeEffectRuntime> damageOverTimeRuntimes)
    {
        try
        {
            effectRuntimes = new EffectRuntimePool(slowRuntimes, damageOverTimeRuntimes);
            effects = new EffectManager(effectRuntimes);
            vfx = new CombatVfx(poolSystem.GetGameObjectPool(), combatAssets);
            // Đạn cần EffectManager (áp effect khi trúng) và CombatVfx (phát VFX trúng đòn).
            projectiles = new ProjectileManager(poolSystem, combatAssets, movementRegistration, movementCommand,
                spatialResolution, spatialQuery, queryBuilder, effects, vfx);
            // Hành vi skill cần ProjectileManager để bắn, pool để thuê visual, và EffectManager để áp aura/slow.
            skillFactory = new SkillFactory(new SkillServices(projectiles, poolSystem.GetGameObjectPool(), effects));
            skills = new SkillManager(levelService, skillFactory);
            // Tạo sau cùng: khi nhân vật chết/thu hồi, nó dọn cả skill gameplay (SkillManager) và effect (EffectManager).
            characterSkills = new CharacterSkillSystem(characters, characterDeaths, skillFactory, skills, effects);
        }
        catch
        {
            // ProjectileManager giữ NativeArray và đăng ký Camera.onPreCull ngay khi được tạo.
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
    public void Tick()
    {
        if (disposed) return;
        float deltaTime = UnityEngine.Time.deltaTime;
        effects.Tick();
        skills.Tick();
        characterSkills.Tick(deltaTime);
        projectiles.Tick();
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
        effectRuntimes.Prewarm<SlowEffectRuntime>(slowEffects);
        effectRuntimes.Prewarm<DamageOverTimeEffectRuntime>(damageOverTimeEffects);
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
    /// đặc biệt là NativeArray của ProjectileManager (không dọn sẽ bị leak).
    /// </summary>
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        DisposeSafely(characterSkills);
        DisposeSafely(skills);
        DisposeSafely(projectiles);
        DisposeSafely(vfx);
        DisposeSafely(effects);
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
