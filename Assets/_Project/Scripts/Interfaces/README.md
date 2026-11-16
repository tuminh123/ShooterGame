# Service contracts và Zenject

## Quy tắc hợp đồng

- Interface chức năng khai báo tối đa ba phương thức. Mỗi overload tính riêng.
- Property/event thuộc cùng trách nhiệm với hợp đồng; không dùng property để giấu API rộng.
- Interface tổng hợp chỉ có getter trả về các interface nhỏ; không kế thừa các vai trò và không giới hạn số getter.
- Zenject chỉ bind/inject đầu mối tổng của từng hệ thống. Consumer gọi getter để lấy và giữ vai trò cần dùng.
- Đối tượng runtime do factory tạo vẫn có thể nhận trực tiếp vai trò nhỏ; chúng không resolve vai trò đó từ container.
- Không tạo singleton cho controller, skill runtime hoặc effect runtime theo từng owner.

## Vị trí mã nguồn

Toàn bộ 49 file interface dùng chung được gom tại `Assets/_Project/Scripts/Interfaces/`.
Thư mục con phân theo hệ thống, tương ứng với cấu trúc implementation để dễ tra cứu.
Mỗi file vẫn chứa các vai trò nhỏ và đầu mối getter cùng loại, chia region theo chức năng.
File C# và `.meta` được di chuyển cùng nhau để giữ nguyên GUID.

```text
Interfaces/
├── README.md
├── Service-Contracts-Change-Report.md
├── Characters/
│   ├── CharacterSearch/
│   ├── Controllers/
│   └── Player/
├── Combat/
│   ├── Damage/
│   ├── Effects/
│   │   ├── Gameplay/
│   │   └── Visuals/
│   ├── ShootingSkill/
│   │   └── Projectiles/
│   └── Skills/
├── Core/
│   ├── Currency/
│   ├── GameFlow/
│   ├── JobSystem/
│   │   ├── Movement/
│   │   └── Spatial/
│   └── Run/
├── Equipment/
├── Infrastructure/
│   ├── Persistence/
│   └── Pooling/
├── Levels/
├── Spawning/
│   ├── Bosses/
│   ├── Core/
│   └── Stages/
├── UI/
│   └── Core/
└── World/
```

Implementation vẫn nằm trong các thư mục gameplay tương ứng. Kiểu dữ liệu như
`DamageInfo`, `StatBonus`, `CharacterRuntimeData` và ScriptableObject vẫn là dữ liệu cụ thể.
Interface private lồng trong implementation, như `SaveManager.IPendingWrite`, giữ nguyên phạm vi nội bộ.

Xem [báo cáo thay đổi](Service-Contracts-Change-Report.md) để tra danh sách file và lịch sử di chuyển.

## CharacterView và skill

`CharacterView` giữ vai trò phối hợp vòng đời nhân vật. Zenject inject các đầu mối tổng;
trong `Construct`, view gọi getter để lấy các vai trò sau:

| Dependency | Quyền cần dùng |
| --- | --- |
| `IMovementRegistration` | Đăng ký, kiểm tra và gỡ lượt movement |
| `IMovementCommand` | Gửi hướng/tốc độ |
| `ISpatialRegistration` | Đăng ký/gỡ hitbox |
| `ICharacterTargetSearch`, `ICharacterRegionSearch` | Tìm một hoặc nhiều mục tiêu |
| `ISkillOwnerLifecycle` | Pause và gỡ skill gameplay của owner |
| `IEffectOwnerLifecycle` | Pause và dọn effect của owner |
| `ISkillRuntimeFactory` | Tạo skill gắn sẵn khi spawn |
| `IGameObjectPool` | Thuê/trả visual |
| `ICharacterRuntimeLease` | Trả runtime theo generation |

`CharacterSkills` nhận `ISkillRuntimeFactory` do view lấy từ `ISkillFactory.GetRuntimeFactory()`;
không biết `SkillManager`. `SkillRuntime` nhận `ISkillBehaviourFactory` để mở hành vi theo cấp.
`Skill` nhận `ISkillServices`, rồi gọi `GetShooting()` hoặc `GetEffects()` để lấy context
bắn/visual/effect. Context này không cung cấp API quản lý skill gameplay toàn cục.

`CharacterView.TargetSearch` và `RegionSearch` thay cho property `Search` rộng trước đây. Thứ tự khởi tạo, tắt tạm thời, chết và thu hồi vẫn giữ nguyên. Movement/spatial/effect handles tiếp tục kiểm tra manager sở hữu và phiên bản.

Các khả năng riêng của controller được truy cập qua `ICharacterActionPermission`,
`ICharacterContactCompletion` và `ICharacterSeparationGizmos` (chỉ Editor).
`CharacterView` không cần ép kiểu trực tiếp sang `EnemyController` để hỏi quyền dùng skill,
hoàn tất tiếp xúc Pattern hoặc vẽ gizmo tách đàn.

## Binding service

Service thuần dùng một binding đầu mối tổng:

```csharp
BindService<ISkillSystem, SkillManager>().AsSingle().NonLazy();
```

Helper đăng ký `ISkillSystem` cùng các hợp đồng lifecycle mà implementation hỗ trợ
(`IInitializable`, `ITickable`, `ILateTickable`, `IFixedTickable`, `IDisposable`).
Các hợp đồng lifecycle cần thiết để Zenject tiếp tục cập nhật và giải phóng service;
các interface nghiệp vụ nhỏ không được bind trực tiếp.

Ví dụ đầu mối tổng và consumer:

```csharp
public interface ICurrencyService
{
    ICurrencyReader GetReader();
    ICurrencyWallet GetWallet();
    ICurrencyPersistence GetPersistence();
    IStageRewardTransaction GetStageRewardTransaction();
}

// Trong constructor/Construct được Zenject inject:
wallet = currencies.GetWallet();
```

Implementation triển khai riêng đầu mối và các vai trò nhỏ, getter trả về `this`.
Không tạo thêm instance khi gọi getter. `SkillFactory` có hai đầu mối
`ISkillFactory` và `ISkillServices`; chúng được đăng ký cùng một binding singleton.

Consumer không resolve concrete manager. Implementation vẫn được nhắc tới trong installer để chọn class, truyền settings và khai báo execution order. Không cần self-binding để `BindTickableExecutionOrder<SkillManager>` hoạt động.

## Binding component scene

`GroundChunkManager` và `EnemySpawnManager` tự đăng ký với `TickableManager` trong `Start`.
Vì vậy chúng dùng `BindSceneSystem<TContract, TComponent>`: chỉ đăng ký đầu mối tổng,
không đăng ký vai trò nhỏ hoặc thêm `ITickable`/`ILateTickable` qua container.

Nếu dùng `BindInterfacesTo` cho hai component này, scheduler sẽ thấy cùng component thêm một lần nữa. Khi thêm component scene mới, phải chọn duy nhất một cơ chế đăng ký tick.

## Tổ chức installer

| File | Trách nhiệm |
| --- | --- |
| `GameSceneInstaller.cs` | Reference Inspector, validation, điều phối cài đặt và flush save theo ứng dụng |
| `GameSceneInstaller.Bindings.cs` | Binding theo hệ thống và helper cho component scene |
| `GameSceneInstaller.ExecutionOrder.cs` | Thứ tự initialize, tick, late tick và dispose |

Ba phần là cùng một partial class. File chính và GUID hiện có được giữ để scene tiếp tục tham chiếu đúng MonoInstaller.

## Tiền và phần thưởng màn

`ICurrencyWallet` chỉ cộng/chi ví thông thường. `IStageRewardTransaction` ghi thưởng
và mốc vượt màn cùng snapshot. `StageProgressService` nhận `ICurrencyService`,
lấy `GetStageRewardTransaction()` rồi giữ hợp đồng giao dịch đó để làm việc.

False từ thao tác lưu vẫn có nghĩa trạng thái RAM đã thay đổi nhưng snapshot chưa ghi thành công; caller chỉ retry lưu, không áp dụng phần thưởng lần nữa.

## Kiểm tra

Trong Unity Editor:

1. Chạy **Shooter Game > Verification > Check Service Contracts** để kiểm tra giới hạn 3 hàm của vai trò nhỏ và hình dạng getter của đầu mối tổng.
2. Vào Play Mode, chờ Home, chạy **Run Service Smoke Checks**.
3. Chạy **Report Missing Scripts** nếu cần xác định component thiếu script trên object/asset đã nạp.

Smoke checks kiểm tra getter trả về vai trò ổn định, vai trò nhỏ không có binding riêng,
singleton và lifecycle/scheduler, movement job, tìm mục tiêu, projectile sweep và trả pool,
cấp/nâng skill, effect handle cũ, sát thương chết, gỡ spatial/effect, pause nhiều nguồn,
reset runtime generation và trở về Home.

Kiểm tra chạy đồng bộ dưới token pause, dừng Stage trước khi tạo mục tiêu và không gọi kết quả thắng/thua. Vì vậy nó không cấp thưởng kết quả hoặc nhặt loot để ghi ví. Đây không phải kiểm thử hình ảnh/UI tương tác hoặc chơi trọn màn.
