using System;
using System.Collections.Generic;
using UnityEngine;

// Ranh giới của miền Character: đây là TẤT CẢ những gì miền khác được phép dùng.
// Mọi interface trong file này do CharacterManager implement. Factory, registry, runtime pool, Player,
// hiển thị enemy và tìm mục tiêu nằm bên trong miền, không được bind vào Zenject.
// Mỗi vai trò ứng với một nhóm nơi dùng thực tế, ghi trong summary.
// Xem Docs/Architecture/Refactor-Architecture.md.

#region Spawn And Despawn

/// <summary>Tạo một nhân vật từ template. Dùng bởi spawn quái (EnemySpawner của miền Stage) và PlayerSystem.</summary>
public interface ICharacterSpawn
{
    /// <summary>
    /// Thuê runtime và vỏ từ pool, gắn controller rồi bật nhân vật tại <paramref name="position"/>.
    /// Trả về null nếu pool không tạo được vỏ.
    /// </summary>
    CharacterView Spawn(CharacterDataSO template, StatBonus bonus, ICharacterController controller, Vector3 position);
}

/// <summary>Trả một nhân vật về pool. Dùng bởi GameManager (dọn trận), spawn quái và controller của quái.</summary>
public interface ICharacterDespawn
{
    /// <summary>Dừng hành vi, thu hồi visual, báo Released rồi trả vỏ. Gọi lại với nhân vật đã trả thì không làm gì.</summary>
    void Despawn(CharacterView character);

    /// <summary>
    /// Thu hồi mọi nhân vật đang tồn tại (Player, quái, boss, kể cả đang chạy animation chết). Dùng khi rời gameplay.
    /// Không phát Died nên không rơi loot; Player nên được gỡ trước qua <see cref="IPlayerLifecycle"/> để PlayerSystem đồng bộ.
    /// </summary>
    void DespawnAll();
}

#endregion

#region Registry And Events

/// <summary>Đọc các nhân vật đang có trên scene. Dùng bởi GameManager (dọn trận) và boss.</summary>
public interface ICharacterRegistry
{
    /// <summary>Mọi nhân vật đang hoạt động, kèm template đã dùng để spawn.</summary>
    IReadOnlyDictionary<CharacterView, CharacterDataSO> Characters { get; }

    event Action<CharacterView, CharacterDataSO> CharacterAdded;
    event Action<CharacterView> CharacterRemoved;
}

/// <summary>Nhận thông báo chết, và điều khiển phần ăn mừng của quái khi Player thua. Dùng bởi GameManager, loot và thống kê trận.</summary>
public interface ICharacterDeathEvents
{
    /// <summary>Một nhân vật (Player hoặc quái) vừa chết.</summary>
    event Action<CharacterView> CharacterDied;

    /// <summary>Phát khi mọi quái còn sống đã chạy xong animation ăn mừng (hoặc không có quái nào).</summary>
    event Action EnemyCelebrationsCompleted;

    /// <summary>
    /// Cho mọi quái còn sống chạy animation ăn mừng. Nếu không có quái nào cần chạy,
    /// <see cref="EnemyCelebrationsCompleted"/> được phát ngay trong lời gọi này.
    /// </summary>
    void PlayEnemyCelebrations();
}

#endregion

#region Player

/// <summary>Đọc Player hiện tại. Dùng bởi camera, nền, spawn quái, boss, loot, thống kê trận và UI chọn skill.</summary>
public interface IPlayerReader
{
    /// <summary>Vỏ Player đang chơi; null khi chưa vào trận hoặc đã bị thu hồi.</summary>
    CharacterView CurrentPlayer { get; }

    /// <summary>Runtime của Player hiện tại; null khi không có Player.</summary>
    CharacterRuntimeData RuntimeData { get; }

    /// <summary>Transform của Player hiện tại; null khi không có Player.</summary>
    Transform PlayerTransform { get; }

    /// <summary>Player mới được spawn, hoặc null khi Player bị thu hồi.</summary>
    event Action<CharacterView> PlayerChanged;
}

/// <summary>Tạo và thu hồi Player theo luồng trận. Dùng bởi GameManager.</summary>
public interface IPlayerLifecycle
{
    /// <summary>Spawn Player theo nhân vật đang chọn. Đã có Player thì trả về Player đó, không tạo trùng.</summary>
    CharacterView SpawnPlayer();

    /// <summary>Thu hồi Player hiện tại (nếu có) và phát <see cref="IPlayerReader.PlayerChanged"/> với null.</summary>
    void RemovePlayer();
}

/// <summary>Danh sách nhân vật Player và nhân vật đang chọn cho trận sau. Dùng bởi màn Character (UI).</summary>
public interface IPlayerSelection
{
    /// <summary>Các template thuộc phe Player, giữ thứ tự trong Character Database.</summary>
    IReadOnlyList<CharacterDataSO> PlayableCharacters { get; }

    /// <summary>Nhân vật sẽ được spawn ở trận sau (save chưa chọn thì là nhân vật mặc định).</summary>
    CharacterType SelectedCharacterType { get; }

    /// <summary>
    /// Chọn nhân vật cho trận sau, không đổi Player đang chơi. Trả về kết quả ghi file:
    /// false nghĩa là lỗi IO, lựa chọn vẫn đã đổi trong RAM và chờ ghi lại.
    /// </summary>
    bool SelectCharacter(CharacterType type);

    /// <summary>Phát sau khi lựa chọn đổi sang nhân vật khác.</summary>
    event Action<CharacterType> SelectionChanged;
}

#endregion

#region Target Search

/// <summary>
/// Tìm nhân vật quanh một điểm (qua lưới không gian của Simulation). Dùng bởi hành vi skill (Combat) và AI tách đàn
/// của quái, thông qua <see cref="CharacterView.Search"/>.
/// </summary>
public interface ICharacterSearch
{
    /// <summary>Nhân vật gần <paramref name="center"/> nhất trong bán kính, thuộc phe <paramref name="targetTeam"/>. Không có thì trả handle rỗng.</summary>
    CharacterHandle FindNearest(Vector2 center, float radius, CharacterTeam targetTeam, CharacterView exclude = null);

    /// <summary>
    /// Ghi mọi nhân vật trong bán kính vào <paramref name="results"/> và trả về số phần tử hợp lệ.
    /// Mảng thuộc bên gọi để dùng lại qua nhiều frame; chỉ được cấp phát lại khi không đủ chỗ.
    /// <c>ref</c>: khi không đủ chỗ, hàm thay biến của bên gọi bằng mảng lớn hơn. Bên gọi giữ biến đó làm field
    /// (không truyền mảng tạm) để các frame sau không cấp phát lại, tránh tạo rác cho GC.
    /// </summary>
    int FindAll(Vector2 center, float radius, CharacterTeam targetTeam, ref CharacterHandle[] results, CharacterView exclude = null);
}

#endregion

#region Prewarm

/// <summary>Tạo sẵn runtime nhân vật trong bước loading. Dùng bởi GamePrewarm (miền Game).</summary>
public interface ICharacterCapacity
{
    /// <summary>Bảo đảm có ít nhất <paramref name="count"/> runtime đang rảnh.</summary>
    void PrewarmRuntimes(int count);

    /// <summary>Số runtime đang rảnh trong pool (để log kết quả prewarm).</summary>
    int AvailableRuntimes { get; }
}

#endregion
