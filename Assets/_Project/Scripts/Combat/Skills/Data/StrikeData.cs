using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Một vòng tròn đánh của đòn từ trời: tâm, bán kính, trễ thêm so với lúc đòn rơi.</summary>
public readonly struct StrikeCircle
{
    public readonly Vector2 Center;
    public readonly float Radius;
    public readonly float Delay;

    public StrikeCircle(Vector2 center, float radius, float delay)
    {
        Center = center;
        Radius = radius;
        Delay = delay;
    }
}

/// <summary>
/// Strategy hình dạng của đòn từ trời (<see cref="StrikeData"/>): dựng các vòng tròn đánh từ điểm nhắm.
/// Không giữ state (dùng chung trong asset).
/// </summary>
public interface IStrikeShape
{
    void Validate();

    /// <summary>Ghi các vòng tròn đánh vào <paramref name="results"/> (không xóa trước).</summary>
    void Build(Vector2 origin, Vector2 aimPoint, Vector2 direction, List<StrikeCircle> results);
}

/// <summary>Một vòng tròn tại điểm nhắm (thiên thạch, sét, búa).</summary>
[Serializable]
public sealed class CircleStrike : IStrikeShape
{
    [SerializeField, Min(0.01f)] private float radius = 0.4f;

    public void Validate() { if (!(radius > 0f)) throw new InvalidOperationException("Invalid circle strike."); }

    public void Build(Vector2 origin, Vector2 aimPoint, Vector2 direction, List<StrikeCircle> results)
        => results.Add(new StrikeCircle(aimPoint, radius, 0f));
}

/// <summary>
/// Các vòng nối tiếp thành hàng từ CHỦ về phía điểm nhắm (gai băng mọc dần); nhiều hàng thì xòe quạt.
/// </summary>
[Serializable]
public sealed class LineStrike : IStrikeShape
{
    [Tooltip("Số vòng mỗi hàng.")][SerializeField, Min(1)] private int count = 4;
    [Tooltip("Khoảng cách giữa hai vòng liên tiếp (world unit).")][SerializeField, Min(0.01f)] private float spacing = 0.2f;
    [SerializeField, Min(0.01f)] private float radius = 0.12f;
    [Tooltip("Trễ thêm của mỗi vòng sau vòng trước (giây): gai mọc dần.")][SerializeField, Min(0f)] private float stepDelay = 0.06f;
    [Tooltip("Số hàng; > 1 thì xòe đều trong Spread Angle.")][SerializeField, Min(1)] private int lines = 1;
    [SerializeField, Range(0f, 180f)] private float spreadAngle = 30f;

    public void Validate()
    {
        if (count < 1 || count > 32 || lines < 1 || lines > 8 || !(spacing > 0f) || !(radius > 0f) || !(stepDelay >= 0f))
            throw new InvalidOperationException("Invalid line strike.");
    }

    public void Build(Vector2 origin, Vector2 aimPoint, Vector2 direction, List<StrikeCircle> results)
    {
        Vector2 forward = direction.sqrMagnitude > 0.000001f ? direction.normalized : Vector2.right;
        for (int line = 0; line < lines; line++)
        {
            float angle = lines == 1 ? 0f : -spreadAngle * 0.5f + spreadAngle * line / (lines - 1);
            Vector2 heading = ShotGeometry.Rotate(forward, angle);
            for (int step = 0; step < count; step++)
                results.Add(new StrikeCircle(origin + heading * (spacing * (step + 1)), radius, stepDelay * step));
        }
    }
}

/// <summary>Hình chữ thập tại điểm nhắm: một vòng giữa và các vòng nối dọc/ngang (phán xét của Paladin).</summary>
[Serializable]
public sealed class CrossStrike : IStrikeShape
{
    [Tooltip("Số vòng mỗi nhánh (không tính vòng giữa).")][SerializeField, Min(1)] private int armCircles = 2;
    [Tooltip("Bán kính mỗi vòng; nhánh dài = arm × 2 × radius.")][SerializeField, Min(0.01f)] private float radius = 0.1f;

    public void Validate()
    {
        if (armCircles < 1 || armCircles > 16 || !(radius > 0f)) throw new InvalidOperationException("Invalid cross strike.");
    }

    public void Build(Vector2 origin, Vector2 aimPoint, Vector2 direction, List<StrikeCircle> results)
    {
        results.Add(new StrikeCircle(aimPoint, radius, 0f));
        float step = radius * 2f;
        for (int index = 1; index <= armCircles; index++)
        {
            results.Add(new StrikeCircle(aimPoint + Vector2.up * step * index, radius, 0f));
            results.Add(new StrikeCircle(aimPoint + Vector2.down * step * index, radius, 0f));
            results.Add(new StrikeCircle(aimPoint + Vector2.left * step * index, radius, 0f));
            results.Add(new StrikeCircle(aimPoint + Vector2.right * step * index, radius, 0f));
        }
    }
}

/// <summary>
/// Thông số của <see cref="StrikeSkill"/>: chọn N chỗ trong tầm chiến đấu, báo vùng rồi đánh xuống
/// (thiên thạch, sét, búa phán xét, gai băng, chữ thập).
/// </summary>
/// <remarks>Chỉ đọc. Không đổi tên class hoặc field: Unity lưu chúng trong asset skill.</remarks>
[Serializable]
public sealed class StrikeData : SkillBehaviourData
{
    #region Configuration

    [SerializeField, Min(0f)] private float cooldown = 3f;
    [Tooltip("% Damage mỗi vòng đánh lên mỗi mục tiêu trong vòng.")][SerializeField, Min(0f)] private float damagePercent = 150f;
    [Tooltip("Luật chọn chỗ đánh trong tầm chiến đấu.")][SerializeReference] private ITargetSelector target = new NearestTarget();
    [Tooltip("Số chỗ đánh mỗi lần dùng (luật chọn có thể trả ít hơn).")][SerializeField, Min(1)] private int targetCount = 1;
    [Tooltip("Hình dạng vùng đánh tại mỗi chỗ.")][SerializeReference] private IStrikeShape shape = new CircleStrike();
    [Tooltip("Thời gian báo vùng trước khi đánh (giây). 0 = đánh ngay.")][SerializeField, Min(0f)] private float telegraph = 0.5f;
    [Tooltip("Trễ giữa các chỗ đánh liên tiếp trong một lần dùng (mưa thiên thạch). 0 = cùng lúc.")][SerializeField, Min(0f)] private float interval;
    [SerializeReference] private List<IGameplayEffect> effects = new List<IGameplayEffect>();
    [Tooltip("Đẩy lùi (dương) / kéo (âm) mục tiêu ra khỏi tâm vòng.")][SerializeField] private float knockback;
    [Tooltip("VFX khi đánh xuống, tại tâm mỗi vòng.")][SerializeField] private EffectType landVfx = EffectType.FireImpact;
    [Tooltip("Màu vùng báo trên đất.")][SerializeField] private Color telegraphColor = new Color(1f, 0.25f, 0.15f, 0.3f);
    [Tooltip("Tác động tại tâm mỗi vòng khi đánh xuống (để lại vũng lửa, vùng băng…).")]
    [SerializeReference] private List<ISkillImpact> onLand = new List<ISkillImpact>();

    public override SkillBehaviourType Type => SkillBehaviourType.Strike;
    public override float Cooldown => cooldown;
    public float DamageMultiplier => damagePercent / 100f;
    public ITargetSelector Target => target;
    public int TargetCount => targetCount;
    public IStrikeShape Shape => shape;
    public float Telegraph => telegraph;
    public float Interval => interval;
    public IReadOnlyList<IGameplayEffect> Effects => effects;
    public float Knockback => knockback;
    public EffectType LandVfx => landVfx;
    public Color TelegraphColor => telegraphColor;
    public IReadOnlyList<ISkillImpact> OnLand => onLand;

    #endregion

    #region Validation

    public override void Validate()
    {
        if (!NonNegative(cooldown) || !NonNegative(damagePercent) || target == null || shape == null || targetCount < 1 ||
            targetCount > 32 || !NonNegative(telegraph) || !NonNegative(interval) || float.IsNaN(knockback))
            throw new InvalidOperationException("Invalid strike data.");
        target.Validate();
        shape.Validate();
        ValidateEffects(effects);
        SkillImpacts.Validate(onLand);
    }

    #endregion
}
