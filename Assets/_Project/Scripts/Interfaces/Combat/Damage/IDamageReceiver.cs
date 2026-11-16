#region DamageReceiver

/// <summary>Nhận sát thương qua cửa vào của nhân vật.</summary>
public interface IDamageReceiver
{
    DamageResult ReceiveDamage(DamageInfo damage);
}

#endregion
