using UnityEngine;

public sealed class ProjectileRuntimeData
{
    public ProjectileDataSO Data { get; }
    public DamageInfo Damage { get; }
    public float SourceAccuracy { get; }
    public float SourceDamage { get; }
    public CharacterTarget Target { get; }
    public IProjectileFlight Flight { get; }
    public IProjectileHit Hit { get; }
    public Vector2 Direction { get; internal set; }
    public float Age { get; internal set; }

    public ProjectileRuntimeData(ProjectileDataSO data, CharacterRuntimeData source, CharacterTarget target,
        Vector2 direction, IProjectileFlight flight, IProjectileHit hit)
    {
        Data = data;
        Damage = source.CreateDamage(data.DamageMultiplier);
        SourceAccuracy = source.FinalStats.accuracy;
        SourceDamage = source.FinalStats.damage;
        Target = target;
        Direction = direction.normalized;
        Flight = flight;
        Hit = hit;
    }
}
