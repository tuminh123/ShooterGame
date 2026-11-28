using System;

/// <summary>
/// Các dịch vụ combat mà hành vi skill được dùng: bắn đạn, gây đòn, vùng trên đất, VFX, buff có thời hạn, áp effect.
/// Mọi hành vi của mọi skill dùng chung một instance do <see cref="CombatManager"/> tạo.
/// </summary>
/// <remarks>Hành vi chỉ dùng các dịch vụ này, không sở hữu và không dispose chúng.</remarks>
public sealed class SkillServices
{
    /// <summary>Bắn từng viên đạn (hành vi tự chia hướng bằng <see cref="IShooter"/>).</summary>
    public ProjectileSystem Projectiles { get; }

    /// <summary>Gây đòn trực tiếp lên mục tiêu. Nơi duy nhất tính sát thương của skill.</summary>
    public CombatHits Hits { get; }

    /// <summary>Thuê và trả visual của hành vi (cưa xoay, vùng aura, vùng báo, pet…).</summary>
    public IGameObjectPool Pools { get; }

    /// <summary>Áp effect một lần hoặc effect duy trì, và kiểm tra/gỡ handle của effect duy trì.</summary>
    public EffectSystem Effects { get; }

    /// <summary>Danh sách visual vùng/lưỡi đang hiển thị; truyền vào <see cref="SkillAreaVisual.Initialize"/> để visual được tick.</summary>
    public SkillAreaVisuals AreaVisuals { get; }

    /// <summary>Tác động phụ dùng chung (nổ, tách đạn, tạo vùng…).</summary>
    public ImpactServices Impacts { get; }

    /// <summary>VFX một lần và đường sét/tia.</summary>
    public CombatVfx Vfx => Impacts.Vfx;

    /// <summary>Buff chỉ số có thời hạn.</summary>
    public TimedBuffSystem Buffs => Impacts.Buffs;

    /// <summary>Prefab dùng chung: vòng tròn đứng yên, vỏ pet.</summary>
    public CombatAssetsSO Assets { get; }

    /// <summary>Skill nhận trong trận (đếm số skill cho buff theo số skill). Gán sau khi SkillSystem được tạo.</summary>
    public SkillSystem GameplaySkills { get; internal set; }

    /// <summary>CombatManager tạo một lần; mọi hành vi skill dùng chung.</summary>
    public SkillServices(ProjectileSystem projectiles, CombatHits hits, IGameObjectPool pools, EffectSystem effects,
        SkillAreaVisuals areaVisuals, ImpactServices impacts, CombatAssetsSO assets)
    {
        Projectiles = projectiles ?? throw new ArgumentNullException(nameof(projectiles));
        Hits = hits ?? throw new ArgumentNullException(nameof(hits));
        Pools = pools ?? throw new ArgumentNullException(nameof(pools));
        Effects = effects ?? throw new ArgumentNullException(nameof(effects));
        AreaVisuals = areaVisuals ?? throw new ArgumentNullException(nameof(areaVisuals));
        Impacts = impacts ?? throw new ArgumentNullException(nameof(impacts));
        Assets = assets != null ? assets : throw new ArgumentNullException(nameof(assets));
    }
}
