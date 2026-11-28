using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Hành vi gọi pet: pet không có HP, không bị quái nhắm; đi theo / lao cắn / đứng yên và đánh bằng chỉ số của chủ.
/// Pet thường trực được gọi ra ở lần tick đầu; pet có thời hạn (trụ) được đặt mỗi lần hết hồi chiêu. Data: <see cref="SummonData"/>.
/// </summary>
/// <remarks>Pet chỉ là hình + vị trí do skill giữ, không phải CharacterView, nên không vào spatial/movement của nhân vật.</remarks>
public sealed class SummonSkill : Skill
{
    #region Types

    private sealed class Pet
    {
        public SpriteRenderer View;
        public Vector2 Position;
        public float Angle, AttackTimer, MissileTimer, Life;
        public bool Lunging, Returning;
        public CharacterHandle Target;
    }

    #endregion

    #region Runtime State

    private SummonData data;
    private IGameplayEffect[] effectSnapshot;
    private readonly List<Pet> pets = new List<Pet>(4);
    private readonly Stack<Pet> spare = new Stack<Pet>();
    private bool paused;
    private static readonly StraightProjectileMove Straight = new StraightProjectileMove();
    private static readonly HomingProjectileMove Homing = new HomingProjectileMove();

    public override SkillBehaviourType Type => SkillBehaviourType.Summon;

    #endregion

    #region Configuration

    protected override void InitializeRuntime()
    {
        data = null;
        if (effectSnapshot == null) effectSnapshot = Array.Empty<IGameplayEffect>();
        paused = false;
    }

    public override void ApplyData(SkillBehaviourData configuration)
    {
        var next = configuration as SummonData ?? throw new ArgumentException("SummonSkill requires SummonData.");
        next.Validate();
        bool restyle = data != null && (data.Sprite != next.Sprite || data.SpriteSize != next.SpriteSize);
        effectSnapshot = SnapshotEffects(effectSnapshot, next.Effects);
        data = next;
        // Đổi số pet thường trực: bớt pet thừa ngay; pet thiếu được gọi ra ở tick sau.
        if (data.Permanent)
            while (pets.Count > data.Count) Despawn(pets.Count - 1);
        if (restyle) foreach (Pet pet in pets) Style(pet);
    }

    #endregion

    #region Spawn

    /// <summary>Đặt pet có thời hạn (trụ) tại chỗ chủ đứng. Pet thường trực không dùng hồi chiêu.</summary>
    public override bool TryExecute()
    {
        if (!Owner.CanRun || data == null || data.Permanent) return false;
        CharacterHandle any = Owner.Owner.Search.FindNearest(Owner.Owner.HitPosition, Owner.Identity.CombatRange * data.RangeScale,
            CharacterSearch.OpponentOf(Owner.Identity.Team), Owner.Owner);
        if (!any.IsAlive) return false; // Không có quái thì chưa đặt, giữ hồi chiêu.
        for (int index = 0; index < data.Count; index++)
        {
            if (pets.Count >= data.Count * 2) Despawn(0); // Giữ tối đa hai lượt đặt cùng lúc.
            Vector2 offset = data.Count == 1 ? Vector2.zero : ShotGeometry.Rotate(Vector2.right, 360f * index / data.Count) * 0.15f;
            Spawn((Vector2)Owner.Owner.HitPosition + offset, 0f);
        }
        return true;
    }

    private void Spawn(Vector2 position, float angle)
    {
        GameObject clone = Services.Pools.Spawn(Services.Assets.PetPrefab.gameObject, position, Quaternion.identity);
        if (clone == null) return;
        Pet pet = spare.Count > 0 ? spare.Pop() : new Pet();
        pet.View = clone.GetComponent<SpriteRenderer>();
        pet.Position = position;
        pet.Angle = angle;
        pet.AttackTimer = data.AttackInterval * 0.5f;
        pet.MissileTimer = data.MissileInterval;
        pet.Life = data.Lifetime > 0f ? data.Lifetime : float.PositiveInfinity;
        pet.Lunging = pet.Returning = false;
        pet.Target = default;
        Style(pet);
        pet.View.enabled = !paused;
        pets.Add(pet);
    }

    // Gán sprite và scale sao cho chiều cao hình bằng SpriteSize.
    private void Style(Pet pet)
    {
        if (pet.View == null) return;
        pet.View.sprite = data.Sprite;
        float height = data.Sprite != null ? data.Sprite.bounds.size.y : 1f;
        float scale = height > 0f ? data.SpriteSize / height : 1f;
        pet.View.transform.localScale = new Vector3(scale, scale, 1f);
    }

    private void Despawn(int index)
    {
        Pet pet = pets[index];
        pets.RemoveAt(index);
        if (pet.View != null) Services.Pools.Despawn(pet.View.gameObject);
        pet.View = null;
        pet.Target = default;
        spare.Push(pet);
    }

    #endregion

    #region Frame

    public override void Tick(float deltaTime)
    {
        if (data == null || paused || deltaTime <= 0f) return;
        if (!Owner.CanRun) return;
        Vector2 owner = Owner.Owner.HitPosition;
        if (data.Permanent)
            while (pets.Count < data.Count) Spawn(owner, 360f * pets.Count / data.Count);
        for (int index = pets.Count - 1; index >= 0; index--)
        {
            if (index >= pets.Count) continue; // Callback đánh có thể đã dọn skill.
            Pet pet = pets[index];
            pet.Life -= deltaTime;
            if (pet.Life <= 0f) { Despawn(index); continue; }
            Move(pet, owner, index, deltaTime);
            Fight(pet, deltaTime);
            if (data == null) return;
            if (pet.View != null) pet.View.transform.position = new Vector3(pet.Position.x, pet.Position.y, 0f);
        }
    }

    // Vị trí nghỉ quanh chủ: các pet chia đều vòng tròn và xoay chậm.
    private Vector2 RestPoint(Pet pet, Vector2 owner, int index, float deltaTime)
    {
        pet.Angle = Mathf.Repeat(pet.Angle + 60f * deltaTime, 360f);
        float angle = (pet.Angle + 360f * index / Mathf.Max(1, pets.Count)) * Mathf.Deg2Rad;
        return owner + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * data.OrbitRadius;
    }

    private void Move(Pet pet, Vector2 owner, int index, float deltaTime)
    {
        switch (data.Movement)
        {
            case PetMovement.Stationary:
                return;
            case PetMovement.Follow:
                Step(pet, RestPoint(pet, owner, index, deltaTime), deltaTime);
                return;
            case PetMovement.Lunge:
                if (pet.Lunging && pet.Target.TryGetCharacter(out CharacterView prey))
                {
                    Step(pet, prey.HitPosition, deltaTime * 1.6f); // Lao nhanh hơn khi đi.
                    if (((Vector2)prey.HitPosition - pet.Position).sqrMagnitude < 0.1f * 0.1f) Bite(pet);
                    return;
                }
                pet.Lunging = false;
                Step(pet, RestPoint(pet, owner, index, deltaTime), deltaTime);
                return;
        }
    }

    private void Step(Pet pet, Vector2 destination, float deltaTime)
    {
        Vector2 offset = destination - pet.Position;
        float step = data.MoveSpeed * deltaTime;
        pet.Position = offset.sqrMagnitude <= step * step ? destination : pet.Position + offset.normalized * step;
        if (pet.View != null && Mathf.Abs(offset.x) > 0.01f) pet.View.flipX = offset.x < 0f;
    }

    #endregion

    #region Attacks

    private void Fight(Pet pet, float deltaTime)
    {
        pet.AttackTimer -= deltaTime;
        if (data.MissileInterval > 0f)
        {
            pet.MissileTimer -= deltaTime;
            if (pet.MissileTimer <= 0f && FireMissile(pet)) pet.MissileTimer = data.MissileInterval;
        }
        if (pet.AttackTimer > 0f || pet.Lunging) return;
        CharacterHandle target = FindTarget(pet.Position);
        if (!target.TryGetCharacter(out CharacterView victim)) return;
        switch (data.Attack)
        {
            case PetAttack.Bite:
                if (data.Movement == PetMovement.Lunge) { pet.Lunging = true; pet.Target = target; return; }
                if (((Vector2)victim.HitPosition - pet.Position).sqrMagnitude > 0.15f * 0.15f) return;
                pet.Target = target;
                Bite(pet);
                return;
            case PetAttack.Shoot:
                Shoot(pet, target, victim);
                break;
            case PetAttack.Chain:
                HitPayload chain = Payload(data.DamageMultiplier);
                ChainLightning.Run(Services.Hits, Services.Vfx, Owner.Owner.Search, pet.Position, target, in chain,
                    data.ChainJumps, data.ChainRange, 0f, EffectType.LightningImpact, new Color(0.55f, 0.85f, 1f, 1f), 0.03f);
                break;
        }
        pet.AttackTimer = data.AttackInterval;
    }

    private CharacterHandle FindTarget(Vector2 from) => Owner.Owner.Search.FindNearest(from, Owner.Identity.CombatRange * data.RangeScale,
        CharacterSearch.OpponentOf(Owner.Identity.Team), Owner.Owner);

    private HitPayload Payload(float multiplier) => new HitPayload(Owner.Handle, Owner.Identity.FinalStats, Owner.Identity.FinalBonuses,
        Owner.Identity.Team, multiplier, effectSnapshot);

    private void Bite(Pet pet)
    {
        pet.Lunging = false;
        pet.AttackTimer = data.AttackInterval;
        if (!pet.Target.TryGetCharacter(out CharacterView victim)) return;
        HitPayload bite = Payload(data.DamageMultiplier);
        Services.Hits.Apply(pet.Target, in bite, victim.HitPosition);
        pet.Target = default;
    }

    private void Shoot(Pet pet, CharacterHandle target, CharacterView victim)
    {
        HitPayload shot = Payload(data.DamageMultiplier);
        Vector2 aim = ((Vector2)victim.HitPosition - pet.Position).normalized;
        var projectile = new ProjectileSpawnData(data.BulletType, in shot, data.ProjectileSpeed, 2f,
            data.Homing ? (IProjectileMove)Homing : Straight, null, float.PositiveInfinity, Owner.Owner.Search);
        for (int index = 0; index < data.ProjectileCount; index++)
        {
            float angle = data.ProjectileCount == 1 ? 0f : -data.SpreadAngle * 0.5f + data.SpreadAngle * index / (data.ProjectileCount - 1);
            Services.Projectiles.SpawnBullet(in projectile, pet.Position, ShotGeometry.Rotate(aim, angle), target);
        }
    }

    private bool FireMissile(Pet pet)
    {
        CharacterHandle target = FindTarget(pet.Position);
        if (!target.TryGetCharacter(out CharacterView victim)) return false;
        HitPayload missile = Payload(data.MissileDamageMultiplier);
        var projectile = new ProjectileSpawnData(BulletType.Fire, in missile, 2.2f, 2f, Homing, data.MissileTraits,
            float.PositiveInfinity, Owner.Owner.Search);
        Services.Projectiles.SpawnBullet(in projectile, pet.Position, ((Vector2)victim.HitPosition - pet.Position).normalized, target);
        return true;
    }

    #endregion

    #region Pause And Cleanup

    public override void SetPaused(bool value)
    {
        paused = value;
        foreach (Pet pet in pets) if (pet.View != null) pet.View.enabled = !value;
    }

    public override void Dispose()
    {
        while (pets.Count > 0) Despawn(pets.Count - 1);
    }

    #endregion
}
