using System;
using UnityEngine;

#region PlayerReader

/// <summary>Đọc Player hiện tại và nhận sự kiện thay đổi owner.</summary>
public interface IPlayerReader
{
    CharacterView CurrentPlayer { get; }
    CharacterRuntimeData RuntimeData { get; }
    Transform PlayerTransform { get; }
    event Action<CharacterView> PlayerChanged;
}

#endregion

#region PlayerSelection

/// <summary>Đọc và lưu lựa chọn nhân vật cho lượt spawn tiếp theo.</summary>
public interface IPlayerSelection
{
    CharacterType SelectedCharacterType { get; }
    bool SelectCharacter(CharacterType type);
}

#endregion

#region PlayerLifecycle

/// <summary>Tạo hoặc thu hồi Player theo lệnh của luồng game.</summary>
public interface IPlayerLifecycle
{
    CharacterView SpawnPlayer();
    void RemovePlayer();
}

#endregion

#region System Contract

/// <summary>Gom hợp đồng của PlayerManager; consumer nhận vai trò nhỏ cần dùng.</summary>
public interface IPlayerSystem
{
    IPlayerReader GetPlayerReader();
    IPlayerSelection GetPlayerSelection();
    IPlayerLifecycle GetPlayerLifecycle();
}

#endregion
