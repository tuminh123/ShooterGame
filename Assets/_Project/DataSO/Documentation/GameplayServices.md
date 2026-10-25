# Dịch vụ gameplay qua Zenject

Trong `SampleScene`, chọn `SceneContext` rồi xem component `GameSceneInstaller`:

- **Player Prefab**: prefab có component `Player`; hiện gán `BlessedGladiator`.
- **Joystick**: `VirtualJoystick` trên Canvas của scene.

`PlayerManager` và `PoolManager` là class thuần, được đăng ký singleton theo scene.
Không cần gắn các manager này lên GameObject hoặc truy cập singleton static.

## PlayerManager

Zenject gọi `Initialize()` để tạo player bằng prefab tại `Vector3.zero`.
Prefab được inject joystick trước khi bật. Không kéo joystick của scene vào prefab.

- `CurrentPlayer` / `PlayerTransform`: nhân vật đang chơi, có thể null.
- `SpawnPlayer()`: tạo khi chưa có; gọi lặp lại không sinh trùng.
- `RemovePlayer()`: thông báo gỡ player, tắt rồi hủy object.
- `PlayerChanged`: truyền player mới hoặc null. Bên đăng ký phải đọc cả `CurrentPlayer`
  vì player có thể đã được tạo trước khi bên đó đăng ký sự kiện.

Camera, nền và enemy đều nhận `PlayerManager` qua injection. Khi tắt component,
chúng hủy subscription; khi bật lại, chúng lấy player hiện tại rồi tiếp tục lắng nghe.
Sự kiện này là thay đổi vòng đời, không phải tín hiệu di chuyển mỗi frame.

## PoolManager

Enemy và đạn gọi dịch vụ này thay vì gọi trực tiếp `LeanPool`:

```csharp
GameObject enemy = poolManager.Spawn(
    enemyPrefab, position, Quaternion.identity, PoolGroup.Enemies);

GameObject bullet = poolManager.Spawn(
    bulletPrefab, position, rotation, PoolGroup.Bullets);

poolManager.Despawn(bullet);
```

Hierarchy lúc chạy:

```text
Pool Holders
  Enemies
  Bullets
  World
  Other
  Pools
    Preparing (inactive)
    LeanPool (tên prefab)
      các clone đã thu hồi
```

Một clone mới được lấy dưới nhánh `Preparing` đang tắt, inject một lần, sau đó
chuyển sang holder hoạt động. Các lượt tái sử dụng không inject lại. Pool do
dịch vụ tạo dùng `Notification=None`, nên component quản lý vòng đời qua
`OnEnable` / `OnDisable`; không phụ thuộc callback `IPoolable.OnSpawn`.

`Spawn` nhận vị trí world-space. Có thể truyền parent riêng ở tham số cuối;
chunk dùng cách này để tiếp tục nằm dưới `Ground Chunk Manager`.
Giữ transform của các holder ở giá trị mặc định.

Đối tượng dùng `MovementManager` vẫn cần đăng ký khi bật, hủy đăng ký khi tắt,
và tự quyết định hướng/tốc độ. PoolManager không quyết định cách di chuyển.

## Kết thúc scene

Installer cấu hình dispose theo thứ tự: pool, player, rồi buffer Jobs.
Chỉ pool do dịch vụ tạo được hủy; không gọi thu hồi toàn cục lên các pool khác.

