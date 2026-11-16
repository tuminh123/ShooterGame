using System;

// Snapshot calculated by the source. Crit is a single roll; overflow increases its damage.
public readonly struct DamageInfo
{

    #region Dependencies And State
    public float Amount { get; }
    public CharacterTeam SourceTeam { get; }
    public bool IsCritical { get; }

    #endregion

    #region Initialization

    public DamageInfo(float amount, CharacterTeam sourceTeam, bool isCritical = false)
    {
        if (float.IsNaN(amount) || float.IsInfinity(amount) || amount < 0f)
            throw new ArgumentOutOfRangeException(nameof(amount));
        Amount = amount;
        SourceTeam = sourceTeam;
        IsCritical = isCritical;
    }

    #endregion

}

// Shield-only hits are accepted hits too, so projectiles, skills and feedback still respond.
public readonly struct DamageResult
{

    #region Dependencies And State
    public float HealthLost { get; }
    public float ShieldLost { get; }
    public float TotalLost => HealthLost + ShieldLost;
    public bool Accepted => TotalLost > 0f;
    public bool Killed { get; }

    #endregion

    #region Initialization

    public DamageResult(float healthLost, float shieldLost, bool killed)
    {
        HealthLost = healthLost;
        ShieldLost = shieldLost;
        Killed = killed;
    }

    #endregion
}
