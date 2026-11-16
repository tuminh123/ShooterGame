using System;

#region CharacterControl

/// <summary>Cập nhật hành vi, đồng bộ pause và xử lý sau khi chết.</summary>
public interface ICharacterControl
{
    void SetPaused(bool paused);
    void Tick(CharacterView view, CharacterRuntimeData runtime);
    void OnDeath(CharacterView view, CharacterRuntimeData runtime);
}

#endregion

#region CharacterDamageReceiver

/// <summary>Xử lý sát thương nhận vào bằng runtime của nhân vật.</summary>
public interface ICharacterDamageReceiver
{
    DamageResult ReceiveDamage(CharacterView view, CharacterRuntimeData runtime, DamageInfo damage);
}

#endregion

#region CharacterDamageDealer

/// <summary>Gây sát thương lên đúng lượt spawn của mục tiêu.</summary>
public interface ICharacterDamageDealer
{
    DamageResult DealDamage(CharacterView view, CharacterRuntimeData runtime, CharacterTarget target, float multiplier = 1f);
}

#endregion

#region CharacterVitals

/// <summary>Thay đổi HP hoặc cấp giáp qua controller của nhân vật.</summary>
public interface ICharacterVitals
{
    void ChangeHealth(CharacterView view, CharacterRuntimeData runtime, float amount);
    void GrantShield(CharacterView view, CharacterRuntimeData runtime, float amount);
}

#endregion

#region Aggregate Contract

/// <summary>Gom các vai trò của CharacterController; không thêm hành vi.</summary>
public interface ICharacterController
{
    ICharacterControl GetControl();
    ICharacterDamageReceiver GetDamageReceiver();
    ICharacterDamageDealer GetDamageDealer();
    ICharacterVitals GetVitals();
    IDisposable GetLifetime();
}

#endregion
