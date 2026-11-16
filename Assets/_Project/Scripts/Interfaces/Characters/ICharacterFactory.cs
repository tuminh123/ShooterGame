using UnityEngine;

#region CharacterSpawn

/// <summary>Ghép template, runtime, controller và vỏ thành nhân vật.</summary>
public interface ICharacterSpawn
{
    CharacterView Spawn(CharacterDataSO template, StatBonus bonus, ICharacterController controller, Vector3 position);
    CharacterView Spawn(CharacterDataSO template, CharacterRuntimeData runtime, ICharacterController controller, Vector3 position);
}

#endregion

#region CharacterDespawn

/// <summary>Kết thúc lượt spawn và trả tài nguyên nhân vật về pool.</summary>
public interface ICharacterDespawn
{
    void Despawn(CharacterView character);
}

#endregion

#region System Contract

/// <summary>Gom hợp đồng của CharacterFactory; consumer nhận vai trò nhỏ cần dùng.</summary>
public interface ICharacterFactory
{
    ICharacterSpawn GetCharacterSpawn();
    ICharacterDespawn GetCharacterDespawn();
}

#endregion
