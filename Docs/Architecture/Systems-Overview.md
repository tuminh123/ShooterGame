# Tổng quan các hệ thống ShooterGame

> **Trạng thái:** ⚠️ Hiện hành, có chỗ lệch · Kiểm tra với code: 2026-11-19. Link `Scripts/Interfaces/README.md` đã chết (thư mục không còn), và "interface tổng hợp" đã bị bỏ từ Giai đoạn 6. Bảng hệ thống và luồng khởi động vẫn đúng.

Ngày ghi nhận theo code hiện tại: 2026-11-18 (cập nhật miền Stage sau Task 3.5).

Tài liệu này là điểm bắt đầu để tìm code và tài liệu chi tiết. Một số tài liệu
thiết kế cũ mô tả blueprint hoặc phiên bản trước; không mặc định mọi nội dung trong đó đã được triển khai.

Contract của mỗi miền nằm trong `XxxContracts.cs` cạnh Manager của miền đó; cách đăng ký Zenject xem
[Refactor architecture](Refactor-Architecture.md) và `CLAUDE.md`.

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
| **Save** (kiến trúc mới) | **SaveManager**, SaveContracts, SaveStorage, SaveMigration | Manager duy nhất được bind: mở file, migration ngay trong Initialize, đọc/ghi theo key, retry snapshot lỗi. Miền khác chỉ dùng `ISaveStore`/`ISaveMaintenance`. |
| **Progression** (kiến trúc mới) | **ProgressionManager**, ProgressionContracts | Manager duy nhất được bind; tự tạo Wallet, CharacterLevels, EquipmentInventory, EquipmentLoadout, StageProgress. Miền khác chỉ dùng `IWallet`, `IPermanentBonuses`, `ICharacterProgress`, `ICharacterUpgrades`, `ICharacterCollection`, `IStageProgress`, `IEquipment`. |
| ↳ Ví | Wallet, CurrencyType | Số dư vàng/kim cương (các `CurrencyType.Shard*` còn trong enum nhưng không dùng: mảnh nay thuộc riêng từng nhân vật); cộng/trừ và lưu ví ngay khi thay đổi. `CanAfford` / `SpendWith` / `ApplyWith`: trừ/cộng tiền và ghi tiến trình trong cùng một lần save. |
| ↳ Bộ sưu tập nhân vật | CharacterCollection (`Progression/Characters/`), luật trong LevelDatabaseSO | Sở hữu, mảnh riêng của từng nhân vật (`CharacterShards`; gacha trùng cộng mảnh theo độ hiếm), ghép bằng mảnh, nâng sao bằng mảnh. Dữ liệu nằm trong CharacterLevels (key `character_levels`). UI: thẻ xám + thanh mảnh, nút UPGRADE hiện "cần/có". Xem [Character-Collection-Tasks](Character-Collection-Tasks.md). |
| ↳ Level vĩnh viễn | CharacterLevels, CharacterLevelConfigSO, LevelDatabaseSO | Level vĩnh viễn theo nhân vật, bonus; Gold mỗi mốc = công thức chung trong LevelDatabaseSO × hệ số theo độ hiếm nhân vật. |
| ↳ Công thức mốc cấp | ILevelFormula, LinearFormula, GeometricFormula, RarityScaledFormula (`Progression/Formulas/`) | Một họ strategy cho "lượng cần cho mốc Lv → Lv+1": Gold nâng level nhân vật, Gold nâng cấp trang bị, EXP trong trận. Chọn trong Inspector bằng `[SerializeReference]`; có độ hiếm thì nhân hệ số tăng dần. Xem `DataSO/README.md` mục 4b. |
| ↳ Trang bị | EquipmentInventory, EquipmentGrowth, EquipmentLoadout, EquipmentDatabaseSO/DataSO/RulesSO; UI: EquipmentScreenController | Túi đồ, 6 ô mặc, độ hiếm, dòng phụ theo mốc, set, skill đồ; màn Equipment mở từ nút Bag. Xem `Equipment-Tasks.md`. |
| ↳ Mốc vượt màn | StageProgress | Đánh dấu màn đã thắng lần đầu và cấp thưởng thắng trong cùng một lần lưu. |
| **Character** (kiến trúc mới) | **CharacterManager**, CharacterContracts | Manager duy nhất được bind của miền; tự tạo các system bên dưới. Miền khác chỉ dùng CharacterContracts.cs. |
| ↳ Spawn | CharacterFactory, CharacterRegistry, CharacterRuntimePool | Ghép vỏ, runtime và hành vi; danh sách nhân vật đang sống; phát CharacterAdded/Removed/Died. |
| ↳ View | CharacterView, CharacterPresentation, CharacterFeedback | Vỏ MonoBehaviour điều phối vòng đời; hướng quay/animation/ẩn hiện; nháy trắng và màu trạng thái. |
| ↳ Chỉ số | CharacterRuntimeData, StatSheet, StatCalculator | HP/giáp của lượt spawn; mọi nguồn bonus thêm qua `Stats.AddSource` (lease); một công thức duy nhất. |
| ↳ Tìm mục tiêu | CharacterSearch, CharacterSearchFactory | Strategy gần nhất / trúng đầu tiên trên đường quét / mọi mục tiêu trong vòng tròn. |
| ↳ Player | PlayerSystem, PlayerController | Nhân vật đang chơi, input; giữ một nguồn bonus level/trang bị và cập nhật tại chỗ. |
| Enemy/boss | EnemySpawner (trong Stage), EnemyController, WaveConfigSO | Chọn đợt/quái, sinh theo vùng, đuổi player, tách đàn và thu hồi. |
| Visual | CharacterVisualSettings, EnemyVisibility | Prefab visual theo loại, layout, hitbox và bật/tắt visual enemy theo camera. |
| **Pool** (kiến trúc mới) | **PoolManager**, PoolContracts | Manager duy nhất được bind: thuê/trả GameObject qua LeanPool; không inject clone. Kế hoạch prewarm nằm ở miền Game (GamePrewarm). |
| **Combat** (kiến trúc mới) | **CombatManager**, CombatContracts | Manager duy nhất được bind của miền; tự tạo các system bên dưới. Miền khác chỉ dùng CombatContracts.cs. |
| ↳ Skill | CharacterSkills, SkillRuntime, SkillSystem, SkillFactory, SkillServices | Data đầy đủ theo cấp, ghép hành vi bằng enum; tách nguồn Character/Gameplay, quản lý cooldown và vòng đời. |
| ↳ Shooting | ShootingSkill, IShooter, ProjectileSystem | Kiểu bắn do asset chọn ([SerializeReference]); ProjectileSystem sở hữu trọn vòng đời đạn. |
| ↳ Effect gameplay | EffectSystem, EffectRuntimePool | Áp dụng/lập lịch/reset runtime làm chậm và sát thương theo thời gian. |
| ↳ Effect hình ảnh | CombatAssetsSO, CombatVfx, EffectView | Tra và phát prefab VFX; tách khỏi trạng thái effect gameplay. |
| **Stage** (kiến trúc mới) | **StageManager**, StageContracts, StageRewards | Manager duy nhất được bind của miền; tự tạo EnemySpawner, ba spawn system (Wave/Formation/Boss) và BossArena. Chọn map, xác nhận clear và cấp thưởng kết quả một lần. |
| ↳ Stage results | StageRewards | Thưởng thắng/thua + first-clear, chặn cấp hai lần. |
| **Battle** (kiến trúc mới) | **BattleManager**, BattleContracts | Manager duy nhất được bind của miền; tự tạo các system bên dưới. Miền khác chỉ dùng BattleContracts.cs (`IBattleProgress`, `IBattleReset`). |
| ↳ Đồng hồ, thống kê | BattleTimer, BattleStatistics | Giây gameplay khi Player sống; vàng nhặt được và số quái bị hạ (chỉ thống kê). |
| ↳ Level trận | InBattleLevel, InBattleLevelProgress, InBattleLevelConfigSO | EXP/level của lượt; tự thêm một nguồn bonus vào `StatSheet` của Player khi Player xuất hiện. |
| ↳ Loot | LootSystem, PickupView, LootDatabaseSO | Rơi EXP/vàng khi quái chết, nhặt theo bán kính ở LateTick; BattleManager chuyển EXP → InBattleLevel, vàng → ví + thống kê. |
| **Game** (kiến trúc mới) | **GameManager**, GameContracts, GameStateMachine, GamePause, GamePrewarm | Manager duy nhất được bind: loading → Home → trận → kết quả. UI dùng `IGameState`, `IGameFlow`, `IGamePause`, `ILoadingProgress`. |
| UI | UIManager, ScreenTransitionController, BattleHud, CharacterUI, PauseUI, SkillChoiceController | Panel là MonoBehaviour trong scene (tự inject). UIManager chọn panel theo GameState; chỉ ScreenTransitionController được bind (`UIContracts.cs`). Bảng chọn skill chỉ trình bày; logic nằm ở `ISkillChoice` (Battle). |
| ↳ Màn Character | CharacterScreenController, CharacterCollectionView, CharacterDetailView, CharacterScreenThemeSO (`UI/CharacterScreen/`) | Mở từ Home (nút sách). Controller gọi `IPlayerSelection` (Character), `ICharacterUpgrades` + `IPermanentBonuses` (Progression); View chỉ hiển thị. Chỉ số = `StatCalculator.Calculate(base, bonus vĩnh viễn)`. Nâng level trừ tiền thật (mỗi mốc chia đều Clicks Per Level lần bấm; thiếu tiền thì nút rung + báo); nâng sao tạm miễn phí tới khi có mảnh. Xem [Character-Screen-Tasks](Character-Screen-Tasks.md). |
| ↳ Màu theo độ hiếm (UI) | RarityStyleDatabaseSO (`UI/`, asset `DataSO/Databases/RarityStyleDatabase`) | Một asset, hai phần: **Character** (nhãn, màu thẻ Collection, nền + PanelGlow màn Detail) và **Equipment** (màu khung thẻ đồ, nhãn). CharacterScreenThemeSO và EquipmentThemeSO chỉ tham chiếu và đọc qua `GetRarity`. Mỗi lớp hình là một `ImageStyle` (`UI/ImageStyle.cs`): chọn **Color** (giữ sprite gốc, tô màu) hoặc **Sprite** (kéo ảnh vào thay); View dùng `StyledImage` để nhớ sprite gốc khi đổi qua lại. |
| **Simulation** (kiến trúc mới) | **SimulationManager**, SimulationContracts, MovementSystem, SpatialSystem | Burst/Jobs với buffer dùng lại: cập nhật movement và truy vấn không gian. Miền khác chỉ dùng SimulationContracts.cs. |
| **World** (kiến trúc mới) | **WorldManager**, WorldContracts, GroundChunks | Map nền theo key (Stage gọi `IWorldMap.SetMap`) và nền lặp theo vị trí Player. Cấu hình ở GameSceneInstaller > World. |
| **Clock** (`Scripts/Clock/`) | **ClockManager**, ClockContracts, ClockSources (`IClockSource` ← `DeviceClockSource` / `ServerClockSource`) | Nơi duy nhất đọc giờ. Data chọn nguồn bằng `ClockSource` (Device = giờ máy, Server = header `Date` của URL HTTPS trong GameSceneInstaller > Clock, cộng thời gian thực đã trôi). Server chưa trả lời/mất mạng thì `TryGetUtcNow` trả false; hỏi lại khi app quay lại từ nền (`IClockSync`). Không phụ thuộc miền nào. Phép tính ngày/tuần dùng chung ở `Utils/TimeCycles.cs`. |
| **Mission** | **MissionManager**, MissionContracts, MissionTrackers, MissionDatabaseSO | Nghe event gameplay, reset theo kỳ (Daily/Weekly/Event), trao thưởng qua `IRewardGrant`. Mỗi database chọn `ClockSource`: Daily/Weekly dùng Device, Event dùng Server (mất mạng thì tab Event ẩn, giữ tiến độ). |
| **Login Check-in** (`Scripts/LoginCheckIn/`) | **LoginCheckInManager**, LoginCheckInContracts, LoginCheckInCalendar, LoginCheckInCalendarSO; UI: LoginCheckInPopup, LoginCheckInDayView, AttentionPulse | Quà đăng nhập theo lịch (SevenDay/Month/Period/Event): mỗi ngày nhận một ô kế tiếp, bỏ ngày không quay về ô 1. Trao thưởng + cờ đã nhận trong một lần ghi (key `login_check_in`). Popup đè lên Home, mở từ nút quà (chấm than đỏ khi còn quà). Xem [LoginCheckIn](LoginCheckIn.md). |
| **Shop** (`Scripts/Shop/`) | **ShopManager**, ShopContracts, ShopPackRuntime, PurchaseChecks, ShopPackSO + IShopPrice; UI: ShopScreenController, ShopPackView, ShopConfirmPopup | Gói = phần thưởng (`IReward`) + giá (tài nguyên / miễn phí / tiền thật) + giới hạn (Daily/Weekly/Lifetime). Kiểm tra điều kiện qua `IPurchaseCheck` (Currency, Store — store hiện giả lập). Trừ tiền + trao quà + số lần mua trong một lần ghi (`IRewardGrant.PurchaseRewards`, key `shop`). Màn Shop mỗi tab một panel, popup xác nhận. Xem [Shop](Shop.md). |
| **Notification** (`Scripts/Notification/`) | **NotificationManager**, NotificationContracts, NotificationCounter, NotificationSources; UI: NotificationBadgeView | Tầng trên cùng: chỉ đọc interface của Mission/LoginCheckIn/Shop/Progression/Character. Mỗi loại thông báo một nguồn (Quest, Login, Shop, Heroes, Gear) + khóa gộp Home. Báo UI một lần mỗi khóa mỗi frame; mỗi giây nhờ các miền kiểm tra sang ngày mới. Mọi chấm báo trên UI dùng `NotificationBadgeView`. Xem [Notification](Notification.md). |

Boss thuộc phe Enemy; `IsBoss` đánh dấu loại nhân vật để hiển thị thanh máu.
Không suy ra boss đã có hệ phase/behavior riêng chỉ từ flag này.

## Luồng khởi động

```text
Bind dịch vụ và inject scene
(thứ tự = danh sách InitializeOrder trong GameSceneInstaller)
→ SaveManager.Initialize: mở file + migration
→ ClockManager.Initialize: bắt đầu hỏi giờ server (không chờ; trả lời đến sau thì Mission tự mở nhóm Server)
→ ProgressionManager.Initialize: ví, level vĩnh viễn, trang bị, mốc vượt màn
→ CharacterManager.Initialize: nhân vật đang chọn
→ WorldManager.Initialize: đặt map ban đầu trước khi vào Home
→ MissionManager.Initialize: đọc tiến độ mission
→ LoginCheckInManager.Initialize: đọc tiến độ lịch đăng nhập (popup tự mở khi vào Home nếu còn quà)
→ ShopManager.Initialize: đọc số lần đã mua các gói
→ NotificationManager.Initialize: đánh dấu mọi khóa; Tick đầu tiên báo mọi chấm báo
→ GameManager.Initialize: GamePrewarm tạo sẵn pool/buffer
→ prewarm → Home → PreparingBattle → reset trận → spawn Player → Playing
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
- `CharacterSkills` quản lý skill gắn sẵn; `SkillSystem` quản lý lịch skill của lượt chơi.
- Shooting hiện bắn theo cooldown và `ProjectileCount`.
- Cơ chế băng đạn/nạp đạn cùng UI số đạn/thanh nạp đã được gỡ.

## UI và visual hiện tại

- Hai prefab trong `Prefabs/CharacterType` dùng `CharacterVisualHandle` nhận enum/event từ `CharacterView` để quay mặt toàn mô hình và phát animation. Xem [Character visual events](Character-Visual-Events.md).
- `ResultController` dùng chung cho Won/Lost: mở popup Victory/Defeat có sẵn trong SampleScene, gọi `StaggeredPopupTween` hiện có để scale nội dung. Nút của Victory ("Home") về Home, nút của Defeat ("Play Again") bắt đầu trận mới. Popup chạy bằng unscaled time; Player được phát hết Death trước khi chuyển Lost. Thắng khi Stage phát `IStageResults.Cleared`.

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
| [Refactor architecture](Refactor-Architecture.md) | Kiến trúc, quy tắc và lịch sử các giai đoạn refactor. |
| [Performance rules](Performance-Rules.md) | Luật hiệu năng: GC, pool, tick, render, cách đo, ngân sách và checklist review. |
| [VFX 2D](Vfx-2D-Plan.md) | Đạn/impact/vùng/trạng thái particle, cách thêm VFX mới, số đo hiệu năng. |
| [Verification](../Tools/Verification.md) | Các bước kiểm tra contract, binding Zenject và smoke test. |

## Tài liệu lịch sử (không mô tả code hiện tại)

Các file dưới đây có tiêu đề bắt đầu bằng **[LỊCH SỬ]** và banner 📜 ở đầu. Chỉ dùng để tra lại quá trình refactor.

| Tài liệu | Ghi lại gì |
| --- | --- |
| [Spawn-Folders](Spawn-Folders.md) | Danh sách di chuyển file khi gom spawn vào `Scripts/Spawning/` (trước Task 3.5). |
| [Stage-Spawn-Refactor](Stage-Spawn-Refactor.md) | Báo cáo tách Stage khỏi spawn manager, thời còn registry `IStageSpawnSystem`. |
| [Formation-Spawn-Separation](Formation-Spawn-Separation.md) | Tách hình học Formation khỏi cách spawn, thời còn `IStageSpawnSystem`; cấu hình hiện tại xem `Scripts/Stage/Patterns/README.md`. |
