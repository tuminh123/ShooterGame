# Tổng quan các hệ thống ShooterGame

Ngày ghi nhận theo code hiện tại: 2026-11-18 (cập nhật miền Stage sau Task 3.5).

Tài liệu này là điểm bắt đầu để tìm code và tài liệu chi tiết. Một số tài liệu
thiết kế cũ mô tả blueprint hoặc phiên bản trước; không mặc định mọi nội dung trong đó đã được triển khai.

Xem [Service contracts và Zenject](../../Assets/_Project/Scripts/Interfaces/README.md) để tra các hợp đồng nhỏ,
interface tổng hợp và cách đăng ký service/component trong phiên bản hiện tại.

## Ranh giới dữ liệu và hành vi

```text
ScriptableObject: cấu hình dùng chung
        ↓
Service / factory: tạo, kết nối và quản lý vòng đời
        ↓
Runtime: trạng thái riêng của cá thể/lượt chơi
        ↓
View / UI: hiển thị và tương tác với Unity
```

Không ghi HP, cooldown hay bonus tạm của cá thể vào asset cấu hình dùng chung.

## Bản đồ hệ thống

| Hệ thống | Thành phần chính | Trách nhiệm hiện tại |
| --- | --- | --- |
| Khởi tạo | GameSceneInstaller, Zenject | Bind dependency, thứ tự khởi tạo và dọn dịch vụ. |
| Save | SaveManager, SaveMigration | Đọc/ghi theo key, retry snapshot lỗi; chuyển save cũ sang các section. |
| Tiền tệ | CurrencyService, CurrencyType | Sở hữu số dư vàng/kim cương; cộng/trừ và lưu ví ngay khi thay đổi. |
| **Character** (kiến trúc mới) | **CharacterManager**, CharacterContracts | Manager duy nhất được bind của miền; tự tạo các system bên dưới. Miền khác chỉ dùng CharacterContracts.cs. |
| ↳ Spawn | CharacterFactory, CharacterRegistry, CharacterRuntimePool | Ghép vỏ, runtime và hành vi; danh sách nhân vật đang sống; phát CharacterAdded/Removed/Died. |
| ↳ View | CharacterView, CharacterPresentation, CharacterFeedback | Vỏ MonoBehaviour điều phối vòng đời; hướng quay/animation/ẩn hiện; nháy trắng và màu trạng thái. |
| ↳ Chỉ số | CharacterRuntimeData, StatSheet, StatCalculator | HP/giáp của lượt spawn; mọi nguồn bonus thêm qua `Stats.AddSource` (lease); một công thức duy nhất. |
| ↳ Tìm mục tiêu | CharacterSearch, CharacterSearchFactory | Strategy gần nhất / trúng đầu tiên trên đường quét / mọi mục tiêu trong vòng tròn. |
| ↳ Player | PlayerManager, PlayerController | Nhân vật đang chơi, input; giữ một nguồn bonus level/trang bị và cập nhật tại chỗ. |
| Enemy/boss | EnemySpawner (trong Stage), EnemyController, WaveConfigSO | Chọn đợt/quái, sinh theo vùng, đuổi player, tách đàn và thu hồi. |
| Visual | CharacterVisualSettings, EnemyVisualManager | Prefab visual theo loại, layout, hitbox và bật/tắt visual enemy theo camera. |
| Pool | PoolManager, PoolPrewarmService, ClassPool | Tạo sẵn/tái sử dụng GameObject và object C# thuần. |
| **Combat** (kiến trúc mới) | **CombatManager**, CombatContracts | Manager duy nhất được bind của miền; tự tạo các system bên dưới. Miền khác chỉ dùng CombatContracts.cs. |
| ↳ Skill | CharacterSkills, SkillRuntime, SkillManager, SkillFactory, SkillServices | Data đầy đủ theo cấp, ghép hành vi bằng enum; tách nguồn Character/Gameplay, quản lý cooldown và vòng đời. |
| ↳ Shooting | ShootingSkill, IShooter, ProjectileManager | Kiểu bắn do asset chọn ([SerializeReference]); ProjectileManager sở hữu trọn vòng đời đạn. |
| ↳ Effect gameplay | EffectManager, EffectRuntimePool | Áp dụng/lập lịch/reset runtime làm chậm và sát thương theo thời gian. |
| ↳ Effect hình ảnh | CombatAssetsSO, CombatVfx, EffectView | Tra và phát prefab VFX; tách khỏi trạng thái effect gameplay. |
| Level | LevelManager, CharacterLevelService, RunLevelService | Level vĩnh viễn và EXP/level của lượt chơi. |
| Trang bị | PlayerEquipmentService, EquipmentDatabaseSO | Loadout và tổng bonus theo dữ liệu trang bị. |
| **Stage** (kiến trúc mới) | **StageManager**, StageContracts, StageRewards | Manager duy nhất được bind của miền; tự tạo EnemySpawner, ba spawn system (Wave/Formation/Boss) và BossArena. Chọn map, xác nhận clear và cấp thưởng kết quả một lần. |
| ↳ Stage results | StageRewards | Thưởng thắng/thua + first-clear, chặn cấp hai lần. |
| ↳ Stage progression | StageProgressService (tạm ở Stage/, sang Progression ở GĐ 5) | Đánh dấu màn đã vượt lần đầu và cấp thưởng vượt màn cùng lúc lưu. |
| Loot/lượt chơi | LootManager, PickupView, RunSessionManager | Vật phẩm rơi, nhận EXP/vàng, đồng hồ và số enemy bị hạ. |
| UI | UIManager, RunUI, CharacterUI, PauseUI | Trạm UI, HUD trận, máu/giáp theo nhân vật và pause. |
| **Simulation** (kiến trúc mới) | **SimulationManager**, SimulationContracts, MovementManager, SpatialManager | Burst/Jobs với buffer dùng lại: cập nhật movement và truy vấn không gian. Miền khác chỉ dùng SimulationContracts.cs. |
| **World** (kiến trúc mới) | **WorldManager**, WorldContracts, GroundChunks | Map nền theo key (Stage gọi `IWorldMap.SetMap`) và nền lặp theo vị trí Player. Cấu hình ở GameSceneInstaller > World. |

Boss thuộc phe Enemy; `IsBoss` đánh dấu loại nhân vật để hiển thị thanh máu.
Không suy ra boss đã có hệ phase/behavior riêng chỉ từ flag này.

## Luồng khởi động

```text
Bind dịch vụ và inject scene
→ SaveManager.Initialize (-200)
→ SaveMigration.Initialize (-180)
→ CurrencyService / CharacterLevelService / PlayerEquipmentService / CharacterManager.Initialize (-150)
→ RunSessionManager.Initialize (-50)
→ Các service scene còn lại Initialize
→ GameManager.Initialize (100)
→ prewarm → Home → PreparingRun → reset trận → spawn Player → Playing
→ Gameplay/spawner được phép hoạt động
```

Prewarm lấy prefab từ character database, combat assets, loot database và danh
sách bổ sung. Cấu hình nằm ở `Assets/_Project/DataSO/Databases/PoolPrewarmSettings.asset`.
Đây là chuẩn bị đồng bộ trong gameplay scene, chưa có loading scene riêng.

## Luồng nhân vật

```text
CharacterDataSO + bonus
→ thuê CharacterRuntimeData
→ lấy CharacterView từ pool dưới nhánh Preparing đang tắt
→ gắn controller, visual, hitbox và layout UI
→ bật vỏ và đăng ký movement/spatial/skill
→ chơi
→ dọn skill/effect/controller, gỡ đăng ký và thông báo release
→ trả runtime, visual và vỏ về pool
```

`CharacterRuntimeData.Generation` phân biệt các lượt sử dụng cùng runtime. Code giữ mục tiêu/callback qua lượt spawn
dùng `CharacterHandle` (`IsCurrent`, `IsAlive`, `TryGetCharacter`), không so sánh generation bằng tay
và không nhận diện cá thể chỉ bằng reference tới vỏ hoặc runtime.
Controller và một số behavior/collection vẫn có chi phí khởi tạo khi spawn; prewarm
hiện không phải cam kết không cấp phát bộ nhớ trong gameplay.

## Skill và shooting

- `CharacterDataSO.Skills` là danh sách skill được trang bị sẵn cho loại nhân vật.
- `SkillDataSO` giữ cấu hình; behavior runtime thuộc cá thể/lượt chơi.
- `CharacterSkills` quản lý skill gắn sẵn; `SkillManager` quản lý lịch skill của lượt chơi.
- Shooting hiện bắn theo cooldown và `ProjectileCount`.
- Cơ chế băng đạn/nạp đạn cùng UI số đạn/thanh nạp đã được gỡ.

## UI và visual hiện tại

- Hai prefab trong `Prefabs/CharacterType` dùng `CharacterVisualHandle` nhận enum/event từ `CharacterView` để quay mặt toàn mô hình và phát animation. Xem [Character visual events](Character-Visual-Events.md).
- `ResultController` dùng chung cho Won/Lost: mở popup Victory/Defeat có sẵn trong SampleScene, gọi `StaggeredPopupTween` hiện có để scale nội dung và cho Continue bắt đầu trận mới. Popup chạy bằng unscaled time; Player được phát hết Death trước khi chuyển Lost. Điều kiện thắng vẫn do gameplay gọi `GameManager.WinRun()`.

- Thanh máu dùng `Slider_Player` trên Canvas world space của character.
- Player và boss hiển thị máu/giáp; enemy thường không bật thanh này.
- Giáp là `ShieldBorderGraphic` với shader viền bên ngoài, giảm theo tỉ lệ giáp.
- Layout/visual/hitbox gom trong `CharacterVisualSettings`, có công cụ căn trong Editor.
- Visual vẫn là prefab con lấy từ pool. VisualRoot cố định là đề xuất trong
  [Character-Visual-Rendering.md](Character-Visual-Rendering.md), chưa chuyển code.

## Chưa thuộc phạm vi hiện tại

- Scene chuẩn bị/loading và chuyển scene sau thắng/thua.
- Giữ pool xuyên scene hoặc chuyển quyền sở hữu pool sang gameplay scene khác.
- Chuyển sang renderer/animator cố định trên vỏ nhân vật.
- Bảo đảm không cấp phát hoặc không khựng frame: cần đo bằng Profiler trên thiết bị mục tiêu.

## Tài liệu liên quan

| Tài liệu | Dùng để đọc gì |
| --- | --- |
| [Visual rendering](Character-Visual-Rendering.md) | So sánh visual cố định và prefab riêng; kế hoạch chuyển đổi. |
| [Pool prewarm](Pool-Prewarm.md) | Cấu hình, thứ tự chuẩn bị và giới hạn pooling. |
| [Skill/shooting pipeline](Skill-Shooting-Pipeline.md) | Cấu hình skill, kiểu bắn, đạn và effect. |
| [Loot/HUD](Loot-and-Hud.md) | Luồng vật phẩm rơi và HUD; phần UI cũ cần đối chiếu với code mới. |
| [Script folders](Script-Folders.md) | Bản đồ thư mục source. |
| [DataSO](../../Assets/_Project/DataSO/README.md) | Cấu hình level, save và trang bị. |
| [Character stats](../../Assets/_Project/DataSO/Documentation/CharacterStats.md) | Chỉ số và công thức bonus. |
| [Combat and skills](../../Assets/_Project/DataSO/Documentation/CombatAndSkills.md) | Tài liệu combat/skill bổ sung. |
| [Simulation](../../Assets/_Project/Scripts/Simulation/README.md) | Movement, spatial và batch truy vấn; vai trò của miền và ai dùng. |
| [Refactor architecture](Refactor-Architecture.md) | Kiến trúc đích, quy tắc và tiến độ từng giai đoạn. |
| [Character blueprint](Character-EventDriven-OOP.md) | Tham khảo thiết kế ban đầu, không phải mô tả đầy đủ code hiện tại. |
| [Character demo](Character-Architecture-Demo.md) | Demo/blueprint nhân vật, có phần chưa triển khai. |
