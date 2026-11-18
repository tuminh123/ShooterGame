# Miền Simulation (Movement + Spatial)

Hạ tầng di chuyển và truy vấn không gian bằng Burst/Jobs. Simulation **không biết gameplay** (skill, wave, loot):
miền khác dùng nó, không có chiều ngược lại.

## Cấu trúc

```text
Simulation/
├── SimulationContracts.cs   Ranh giới miền: mọi interface vai trò (miền khác chỉ dùng file này)
├── SimulationManager.cs     Binding Zenject duy nhất; tự new các system bên dưới
├── Movement/
│   └── MovementManager.cs   Job di chuyển Transform, áp dụng ở LateTick
└── Spatial/
    ├── SpatialManager.cs          Lưới không gian của hitbox, thực thi truy vấn
    ├── SpatialSearchFactory.cs    Tạo SpatialQuery và chọn strategy theo loại truy vấn
    ├── ISpatialSearchStrategy.cs  Họ strategy thực thi truy vấn (gần nhất, quét, mọi mục tiêu trong vòng tròn)
    ├── SpatialSearchStrategies.cs Các implementation
    ├── SpatialJobs.cs / SpatialData.cs / SpatialGeometry.cs / HitboxData.cs
```

`SimulationManager` tạo `SpatialSearchFactory`, rồi `SpatialManager`, rồi `MovementManager`. Thứ tự này theo phụ thuộc:
movement cần spatial để báo dựng lại lưới.

## Vai trò (trong `SimulationContracts.cs`)

| Vai trò | Ai dùng |
| --- | --- |
| `IMovementRegistration`, `IMovementCommand` | `CharacterView`, đạn |
| `IMovementArena` | Boss khi mở đấu trường |
| `ISpatialRegistration` | `CharacterView` |
| `ISpatialInvalidation` | Spawn theo đội hình (dịch chuyển tức thời) |
| `ISpatialResolution`, `ISpatialQuery`, `ISpatialQueryBuilder` | Tìm mục tiêu (`CharacterSearch`) và đạn (`ProjectileManager`) |
| `ISimulationCapacity` | `PoolPrewarmService`, cấp phát trước buffer |

Vòng đời: LateTick ở thứ tự 0 (sau mọi Tick gameplay), Dispose ở -100 (movement trước, rồi spatial).

## Thời gian và cập nhật

- Không có timer manager riêng. Zenject gọi `ITickable`/`ILateTickable` trực tiếp; gameplay dùng `Time.deltaTime`, UI dùng `Time.unscaledTime` khi pause.
- Mỗi skill tự giữ cooldown. `EffectRuntime` giữ thời gian còn lại và nhịp tick, `ProjectileManager` cập nhật thời gian sống của đạn ngay trong vòng duyệt của nó.

## Spatial: những điều cần biết

- **Hitbox**: `CharacterDataSO.Hitbox` lưu kích thước/offset cục bộ. `CharacterView` đăng ký mỗi lần bật và gỡ khi tắt, chết hoặc despawn. Handle có version của slot, nên handle cũ không trỏ nhầm sang nhân vật khác trong pool.
- **Snapshot**: hộp định hướng trên mặt phẳng XY, kèm team và ID số. Hỗ trợ xoay quanh Z và scale; không hỗ trợ hierarchy bị shear. Lật sprite không ảnh hưởng hitbox.
- **Dựng lưới**: `SpatialManager` chụp vị trí ở truy vấn đầu tiên của frame, và dựng lại sau khi có đăng ký mới hoặc `Invalidate`. `MovementManager` tự invalidate sau khi áp vị trí; code dịch chuyển tức thời khác phải tự gọi `ISpatialInvalidation.Invalidate`.
- **Lưới**: mỗi tâm hitbox thuộc một ô (ô rộng 1 world unit). Truy vấn nới rộng theo hitbox lớn nhất, nên hitbox to không bị sót và kết quả không bị trùng. Một hitbox ngoại cỡ sẽ làm tăng số ứng viên.
- **Hình học** (`SpatialGeometry`): giao tròn/hộp, hộp/hộp định hướng, và quét hình tròn với hộp định hướng (tính cả chồng lấn ban đầu và góc bo, lấy điểm trúng sớm nhất). Truy vấn gần nhất: vòng tìm phải chạm hitbox, rồi xếp hạng theo khoảng cách tâm.
- **Đạn**: `ProjectileManager` gửi một lô truy vấn quét vào job song song, rồi xử lý kết quả trên main thread. Nếu một viên trước đã làm mục tiêu chung despawn, đường bay bị ảnh hưởng được truy vấn lại trước khi di chuyển viên đó.
- **Truy vấn tức thời của AI** chạy job đồng bộ bằng `Run`, không lên lịch một job cho mỗi truy vấn.
- **Strategy chỉ chạy trên main thread**, bên ngoài Burst; job chỉ nhận dữ liệu số.
- **Buffer `FindAll`** được dùng lại ở lần gọi sau: đọc xong rồi mới truy vấn tiếp. Không ghi vào input native khi job đang chạy; manager hoàn tất job trước callback, resize hoặc Dispose.

## Tìm nhân vật nằm ở miền Character

Tầng gameplay "tìm mục tiêu" nằm trong `Characters/CharacterSearch/`: `CharacterSearch` (vai trò `ICharacterSearch`) chọn
strategy `ISingleCharacterSearch`/`IMultipleCharacterSearch` theo `CharacterSearchType` (Nearest, FirstOnPath, AllInCircle).
Strategy tạo truy vấn qua `ISpatialQueryBuilder`, thực thi qua `ISpatialQuery` và đổi kết quả thành `CharacterHandle`
qua `ISpatialResolution`. Thêm một cách tìm gameplay mới không cần thêm loại job; thuật toán hình học mới thì đăng ký ở `SpatialSearchFactory`.

## Hiệu năng

Chưa đo mục tiêu 100.000 đối tượng. Truy vấn AI vẫn chạy từng cái; transform và callback gameplay vẫn ở main thread.
Hãy profile mật độ, số truy vấn và lượng event thực tế trước khi thêm timing wheel, gom lô truy vấn hay đổi cấu trúc dữ liệu.
