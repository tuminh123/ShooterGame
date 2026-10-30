using System;

// * Đầu ra đã tính ở Character nguồn; đạn chỉ giữ bản sao, không tính lại theo stat mới.
public readonly struct DamageInfo
{
    public float Amount { get; }
    public CharacterTeam SourceTeam { get; }
    public int CritTier { get; }

    public DamageInfo(float amount, CharacterTeam sourceTeam, int critTier = 0)
    {
        if (float.IsNaN(amount) || float.IsInfinity(amount) || amount < 0f)
            throw new ArgumentOutOfRangeException(nameof(amount));
        Amount = amount;
        SourceTeam = sourceTeam;
        CritTier = critTier;
    }
}

// * Trả lượng HP thực sự mất để phần hiển thị dùng kết quả, không tự tính lại công thức.
public readonly struct DamageResult
{
    public float HealthLost { get; }
    public bool Accepted => HealthLost > 0f;
    public bool Killed { get; }

    public DamageResult(float healthLost, bool killed)
    {
        HealthLost = healthLost;
        Killed = killed;
    }
}

public interface IDamageReceiver
{
    DamageResult ReceiveDamage(DamageInfo damage);
}
