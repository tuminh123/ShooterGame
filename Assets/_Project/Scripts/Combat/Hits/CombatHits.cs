using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Nơi <b>duy nhất</b> skill gây đòn lên mục tiêu: tính sát thương lúc trúng, trừ giáp/HP, áp effect, đẩy/kéo và phát VFX trúng đòn.
/// Đạn, cưa, vùng, sét, chém, pet đều gọi <see cref="Apply"/>.
/// </summary>
/// <remarks>
/// Lý do gom về một chỗ: luật chiến đấu (thêm sát thương lên boss, kết liễu, hút máu, dấu săn, mục tiêu đang cháy…) chỉ sửa ở đây
/// và tự đúng cho mọi skill. Trước đây mỗi hành vi tự viết "trừ máu → effect → VFX" nên mỗi luật phải sửa nhiều chỗ.
/// <para>
/// Không đi qua đây: đòn cắn của quái (controller của miền Character, cần đẩy lùi) và sát thương theo tick của cháy/độc
/// (sát thương của hiệu ứng, không chí mạng, không ăn luật đòn đánh).
/// </para>
/// Do <see cref="CombatManager"/> tạo; class concrete vì chỉ có một cách gây đòn.
/// </remarks>
public sealed class CombatHits
{
    #region Dependencies

    private readonly EffectSystem effects;
    private readonly CombatVfx vfx;
    private readonly StunEffect critStun = new StunEffect(CritStunSeconds); // Dùng chung cho luật "chí mạng gây choáng".

    /// <summary>Thời gian choáng của luật chí mạng gây choáng (Keen Eye Lv5).</summary>
    public const float CritStunSeconds = 0.2f;

    /// <summary>Thời gian của một cú đẩy/kéo do đòn đánh (giây).</summary>
    public const float PushSeconds = 0.15f;

    #endregion

    #region Events

    /// <summary>
    /// Một đòn của skill vừa giết mục tiêu: (người đánh, nạn nhân). Phát sau khi đòn đã xong.
    /// Skill "giết quái thì cộng dồn" (Soul Harvest) nghe event này; sát thương theo tick của cháy/độc không tính.
    /// </summary>
    public event Action<CharacterHandle, CharacterView> Killed;

    #endregion

    #region Constructor

    /// <summary>Nhận hai system cùng miền đã được CombatManager tạo trước.</summary>
    public CombatHits(EffectSystem effects, CombatVfx vfx)
    {
        this.effects = effects ?? throw new ArgumentNullException(nameof(effects));
        this.vfx = vfx ?? throw new ArgumentNullException(nameof(vfx));
    }

    #endregion

    #region Apply

    /// <summary>
    /// Gây một đòn lên đúng lượt spawn của <paramref name="target"/>. Mục tiêu đã chết/thu hồi thì không làm gì.
    /// Effect, đẩy/kéo và VFX chỉ chạy khi đòn được nhận (mất giáp hoặc HP).
    /// </summary>
    /// <param name="impactVfx">VFX trúng đòn của vật mang đòn (ví dụ theo loại đạn); None = không phát.</param>
    /// <param name="pushDirection">Hướng đẩy của <see cref="HitPayload.Knockback"/>; zero = ra xa người đánh (kéo: về phía người đánh).</param>
    public DamageResult Apply(CharacterHandle target, in HitPayload hit, Vector3 hitPoint, EffectType impactVfx = EffectType.None,
        Vector2 pushDirection = default)
    {
        if (!target.TryGetCharacter(out CharacterView victim) || victim.RuntimeData == null) return default;

        DamageInfo damage = CreateDamage(victim, hit, out Vector2 attackerPosition, out bool hasAttacker);
        DamageResult result = victim.ReceiveDamage(damage);
        if (!result.Accepted) return result;

        // EffectSystem tự kiểm tra lại handle: quái vừa chết hoặc vỏ đã đổi chủ thì effect không áp sang cá thể mới.
        IReadOnlyList<IGameplayEffect> hitEffects = hit.Effects;
        for (int index = 0; index < hitEffects.Count; index++)
            effects.Apply(hitEffects[index], target, hit.Team, hit.EffectHit, hit.Stats.damage);
        if (damage.IsCritical && hit.Bonuses.critStun > 0f && UnityEngine.Random.value * 100f < hit.Bonuses.critStun)
            effects.Apply(critStun, target, hit.Team);
        Push(target, hit, hitPoint, pushDirection, attackerPosition, hasAttacker);
        vfx.Spawn(impactVfx, hitPoint);
        StealLife(hit, result);
        if (result.Killed) Killed?.Invoke(hit.Attacker, victim);
        return result;
    }

    #endregion

    #region Rules

    // Luật theo mục tiêu, chạy trước khi tính con số: kết liễu, thêm sát thương lên boss, mục tiêu bị đánh dấu,
    // mục tiêu đang cháy, mục tiêu ở xa.
    private DamageInfo CreateDamage(CharacterView victim, in HitPayload hit, out Vector2 attackerPosition, out bool hasAttacker)
    {
        CharacterRuntimeData runtime = victim.RuntimeData;
        CombatBonuses bonuses = hit.Bonuses;
        hasAttacker = hit.Attacker.TryGetCharacter(out CharacterView attacker);
        attackerPosition = hasAttacker ? (Vector2)attacker.HitPosition : default;
        // Kết liễu: đòn bằng đúng lượng máu + giáp còn lại (giáp hấp thụ trước), không tung chí mạng.
        if (!victim.IsBoss && bonuses.executeThreshold > 0f && runtime.MaxHealth > 0f &&
            runtime.CurrentHealth / runtime.MaxHealth * 100f < bonuses.executeThreshold)
            return new DamageInfo(runtime.CurrentHealth + runtime.CurrentShield, hit.Team);

        float multiplier = hit.DamageMultiplier;
        if (victim.IsBoss) multiplier *= 1f + bonuses.bossDamage / 100f;
        // Dấu săn nằm trên MỤC TIÊU (bonus chiến đấu của nạn nhân), áp cho mọi nguồn đánh.
        multiplier *= 1f + runtime.FinalBonuses.damageTaken / 100f;
        if (bonuses.burningDamage > 0f && effects.HasEffect<BurnEffect>(runtime))
            multiplier *= 1f + bonuses.burningDamage / 100f;
        if (bonuses.farDamage > 0f && hasAttacker && attacker.RuntimeData != null)
        {
            float half = attacker.RuntimeData.CombatRange * 0.5f;
            if (((Vector2)victim.HitPosition - attackerPosition).sqrMagnitude > half * half)
                multiplier *= 1f + bonuses.farDamage / 100f;
        }
        // Chí mạng tung ở đây (lúc trúng), dùng đúng công thức có sẵn để chỉ có một công thức sát thương.
        return CharacterRuntimeData.CreateDamage(hit.Stats, hit.Team, multiplier);
    }

    // Đẩy (dương) hoặc kéo (âm). Hướng: theo pushDirection nếu có, không thì từ người đánh tới nạn nhân.
    private static void Push(CharacterHandle target, in HitPayload hit, Vector3 hitPoint, Vector2 pushDirection,
        Vector2 attackerPosition, bool hasAttacker)
    {
        if (hit.Knockback == 0f || !target.TryGetCharacter(out CharacterView victim) || !victim.RuntimeData.IsAlive) return;
        Vector2 away = pushDirection;
        if (away.sqrMagnitude < 0.000001f)
            away = (Vector2)victim.HitPosition - (hasAttacker ? attackerPosition : (Vector2)hitPoint);
        if (away.sqrMagnitude < 0.000001f) return;
        float distance = hit.Knockback;
        if (distance < 0f)
        {
            // Kéo về phía người đánh, không kéo quá tâm (dừng cách người đánh một chút).
            away = -away;
            distance = Mathf.Min(-distance, Mathf.Max(0f, ((Vector2)victim.HitPosition - attackerPosition).magnitude - 0.12f));
        }
        victim.Push(away, distance, PushSeconds);
    }

    // Luật sau khi trúng: hồi cho người đánh theo lượng HP thật đã trừ (không tính giáp). Người đánh đã chết thì bỏ qua.
    // Hồi phần máu ĐÃ MẤT (Restore), không phình HP tối đa.
    private static void StealLife(in HitPayload hit, DamageResult result)
    {
        if (hit.Bonuses.lifeSteal <= 0f || result.HealthLost <= 0f) return;
        if (!hit.Attacker.TryGetCharacter(out CharacterView attacker) || attacker.RuntimeData == null || !attacker.RuntimeData.IsAlive) return;
        attacker.Restore(result.HealthLost * hit.Bonuses.lifeSteal / 100f);
    }

    #endregion
}
