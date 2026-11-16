using System;
using System.Collections.Generic;

#region CharacterRegistration

/// <summary>Thêm hoặc gỡ nhân vật khỏi registry đang hoạt động.</summary>
public interface ICharacterRegistration
{
    void Register(CharacterView character, CharacterDataSO definition);
    void Unregister(CharacterView character);
}

#endregion

#region CharacterRegistry

/// <summary>Đọc registry và theo dõi nhân vật được thêm hoặc thu hồi.</summary>
public interface ICharacterRegistry
{
    IReadOnlyDictionary<CharacterView, CharacterDataSO> Characters { get; }
    event Action<CharacterView, CharacterDataSO> CharacterAdded;
    event Action<CharacterView> CharacterRemoved;
}

#endregion

#region CharacterDeathEvents

/// <summary>Nhận thông báo chết và hoàn tất trình bày kết quả của enemy.</summary>
public interface ICharacterDeathEvents
{
    event Action<CharacterView> CharacterDied;
    event Action EnemyCelebrationsCompleted;
}

#endregion

#region System Contract

/// <summary>Gom hợp đồng của CharacterManager; consumer nhận vai trò nhỏ cần dùng.</summary>
public interface ICharacterSystem
{
    ICharacterRegistration GetCharacterRegistration();
    ICharacterRegistry GetCharacterRegistry();
    ICharacterDeathEvents GetCharacterDeathEvents();
}

#endregion
