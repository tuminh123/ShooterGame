using System;

/// <summary>
/// Các dịch vụ combat mà hành vi skill được dùng: bắn đạn, thuê visual và áp effect.
/// Mọi hành vi của mọi skill dùng chung một instance do <see cref="CombatManager"/> tạo.
/// </summary>
/// <remarks>
/// Thay cho ba interface lồng nhau trước đây (<c>Services.GetShooting().Spawner</c>,
/// <c>Services.GetEffects().Effects</c>…). Giờ hành vi gọi thẳng <c>Services.Projectiles</c>, <c>Services.Effects</c>.
/// Hành vi chỉ dùng các dịch vụ này, không sở hữu và không dispose chúng.
/// </remarks>
public sealed class SkillServices
{
    /// <summary>Bắn từng viên đạn (hành vi tự chia hướng bằng <see cref="IShooter"/>).</summary>
    public ProjectileManager Projectiles { get; }

    /// <summary>Thuê và trả visual của hành vi (cưa xoay, vùng aura…).</summary>
    public IGameObjectPool Pools { get; }

    /// <summary>Áp effect một lần hoặc effect duy trì, và kiểm tra/gỡ handle của effect duy trì.</summary>
    public EffectManager Effects { get; }

    public SkillServices(ProjectileManager projectiles, IGameObjectPool pools, EffectManager effects)
    {
        Projectiles = projectiles ?? throw new ArgumentNullException(nameof(projectiles));
        Pools = pools ?? throw new ArgumentNullException(nameof(pools));
        Effects = effects ?? throw new ArgumentNullException(nameof(effects));
    }
}
