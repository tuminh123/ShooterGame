# Kiến trúc mới: Data-driven có khung OOP

> **Trạng thái:** ✅ Hiện hành, là nguồn sự thật cho kiến trúc · Kiểm tra với code: 2026-11-19.

Ngày chốt: 2026-11-18. Trạng thái: **refactor xong (Giai đoạn 0–7)**. Mọi miền đã chuyển; tài liệu này
là nguồn sự thật cho kiến trúc. `Systems-Overview.md` là bản đồ hệ thống → class để tra nhanh.

## 1. Nguyên tắc ưu tiên

Thứ tự ưu tiên khi thiết kế và viết code: **Simple → DRY → SOLID**.

- Dễ đọc là quan trọng nhất. Nếu một giải pháp "đúng chuẩn" hơn nhưng khó hiểu hơn, chọn giải pháp đơn giản.
- Mọi đề xuất thay đổi kiến trúc đều phải kèm lý do.

## 2. Ba tầng

```text
┌───────────────────────────────────────────────────────────────┐
│ DATA (ScriptableObject, chỉ đọc)    "Cái gì, bao nhiêu"         │
├───────────────────────────────────────────────────────────────┤
│ STRATEGY (OOP)                      "Làm thế nào" khi hành vi    │
│   interface + các class thay được cho nhau, data chọn qua enum  │
├───────────────────────────────────────────────────────────────┤
│ STRUCTURE (OOP)                     "Ai sở hữu ai, ai gọi ai"    │
│   cây Manager → System → Factory, vòng đời, sự kiện             │
└───────────────────────────────────────────────────────────────┘
```

Mọi đối tượng trong game đi theo cùng một luồng:

```text
Definition (SO)  →  Runtime (class C# thuần)  →  View (MonoBehaviour)
CharacterDataSO  →  Character                 →  CharacterView
SkillDataSO      →  Skill                     →  VFX
StageDataSO      →  StageRun                  →  HUD
```

- Runtime giữ tham chiếu tới Definition để đọc, không bao giờ ghi ngược vào SO.
- View chỉ hiển thị. View không inject Manager và không chứa logic game.

## 3. Cây Manager

Chữ thường là class concrete do Manager tự tạo bằng `new`; `I…` là interface.
**⬆** là các interface vai trò mà miền khác được dùng. **⚡** là các sự kiện C# của miền.

```text
SaveManager
├── SaveStorage, SaveMigration
⬆ ISaveStore, ISaveMaintenance

ProgressionManager
├── Wallet, CharacterLevels, Loadout, StageProgress
⬆ IWallet, IPermanentBonuses, IStageProgress      ⚡ BalanceChanged, BonusesChanged

PoolManager   (bọc LeanPool; pool runtime C# thuần thuộc miền sở hữu runtime đó)
⬆ IGameObjectPool, IPoolPreparation, IPoolCleanup
   (Prewarm nằm ở miền Game — GamePrewarm: kế hoạch cần Character/Combat/Simulation, đặt trong Pool sẽ tạo vòng phụ thuộc)

SimulationManager   (Jobs/Burst, chủ duy nhất của vị trí, hướng và hitbox)
├── MovementSystem, SpatialSystem   (Ground là miền World riêng, xem WorldManager)
⬆ IMovementRegistration, IMovementCommand, IMovementArena, ISpatialRegistration, ISpatialInvalidation,
  ISpatialResolution, ISpatialQuery, ISpatialQueryBuilder, ISimulationCapacity

CharacterManager
├── CharacterRuntimePool, CharacterRegistry, CharacterFactory, PlayerSystem, EnemyVisibility
├── CharacterSearch → ISingleCharacterSearch / IMultipleCharacterSearch            (Strategy)
└── ICharacterController ← PlayerController / EnemyController                      (Strategy)
⬆ ICharacterSpawn, ICharacterDespawn, ICharacterRegistry, ICharacterDeathEvents,
  IPlayerReader, IPlayerLifecycle, ICharacterSearch, ICharacterCapacity
⚡ CharacterAdded, CharacterRemoved, CharacterDied, PlayerChanged

CombatManager
├── EffectRuntimePool → EffectSystem → IGameplayEffect ← Slow / DamageOverTime    (Strategy)
├── CombatVfx
├── ProjectileSystem → IProjectileMove ← Straight / …                             (Strategy)
├── SkillServices → SkillFactory → ISkill ← Shooting / Saw / Aura / …              (Strategy)
│     └── ShootingSkill → IShooter ← Straight / Fan / Circle / Spiral              (Strategy)
├── SkillSystem              skill gameplay nhận trong trận
└── CharacterSkillSystem     skill vốn có của nhân vật (nghe vòng đời Character)
⬆ ISkillGrant, IOwnedSkillReader, ICombatLifecycle, ICombatCapacity

WorldManager   (map nền; không biết wave, boss hay điều kiện thắng)
├── MapCatalog (MapDatabaseSO), GroundChunks (nền theo chunk, bám vị trí Player)
⬆ IWorldMap (đổi map theo key, đọc map hiện tại)

StageManager
├── EnemySpawner (dùng chung), WaveSpawnSystem, FormationSpawnSystem, BossSpawnSystem + BossArena
└── StageRewards
⬆ IStageSelection, IStageRunLifecycle, IStageResults, IStageSchedule   ⚡ Cleared, Failed, SpawnScheduled
   (dùng IWorldMap để đặt map của màn; World không biết Stage)

RunManager
├── RunClock, RunStatistics, RunLevel (+ nguồn bonus trên Player), LootSystem, SkillChoice
⬆ IRunReader, IRunLifecycle, ISkillChoice          ⚡ LevelUp, RunReset, PendingChanged, ChoicesCanceled
   (`LootSystem.Collected` là event nội bộ, chỉ RunManager nghe)

GameManager
├── GameStateMachine, GamePause, GamePrewarm
⬆ IGameState, IGameFlow, IGamePause, ILoadingProgress   ⚡ StateChanged

UI (MonoBehaviour trong scene, SceneContext tự inject; không có Manager C# thuần)
├── UIManager           chọn panel theo GameState (không bind: không ai inject nó)
├── ScreenTransitionController   binding UI duy nhất (Home + menu pause dùng chung)
└── Home, Loading, HUD (RunUI), SkillChoice, Result, Pause …
⬆ (không có cho miền khác; UIContracts.cs chỉ dùng giữa các panel)
```

## 4. Chiều phụ thuộc và giao tiếp

```text
            Save
              ↑
        Progression     Pool     Simulation        ← hạ tầng, không biết gameplay
              ↑           ↑          ↑
              └──────── Character ───┘
                      ↑        ↑
                      │      World          ← map nền (đọc vị trí Player)
                      │        ↑
                  Combat     Stage          ← Stage gọi World để đặt map
                      ↑        ↑
                      └── Run ─┘
                           ↑
                         Game
                           ↑
                           UI
```

1. **Chiều xuôi:** gọi trực tiếp qua interface vai trò của Manager bên dưới.
2. **Chiều ngược:** chỉ dùng **C# `event`** đặt trên interface vai trò. Subscribe trong constructor, unsubscribe trong `Dispose`.
3. **Trong một miền:** cha gọi con trực tiếp. Các System anh em không gọi nhau; nếu cần, cha truyền vào constructor của con.
4. Không dùng static event bus hay Zenject `SignalBus`.

Ví dụ các luồng chính:

```text
Lên cấp:   Run.LevelUp → SkillChoice ─event PendingChanged─▶ UI mở popup ─ISkillChoice.Choose()─▶ Run ─ISkillGrant─▶ Combat
Quái chết: Combat ─ICharacterDamage─▶ Character ─event Died─▶ Combat / Run / Stage
Hết trận:  Stage.Cleared ─event─▶ GameManager (thắng) ─event StateChanged─▶ UI hiện Result
```

## 5. Quy tắc thiết kế

### Interface
Chỉ tạo interface khi có một trong hai lý do:
1. **Ranh giới miền:** vai trò của Manager. Mỗi vai trò phục vụ một nhóm nơi dùng cụ thể; không chia trước khi có người dùng.
2. **Họ Strategy:** có từ 2 hành vi thay được cho nhau trở lên.

Ngoài hai trường hợp này thì dùng class concrete. Toàn bộ interface vai trò và event của một miền
nằm trong **một file** `XxxContracts.cs` đặt cạnh Manager.

### Data
- Field trong SO là `[SerializeField] private`, chỉ có getter. Không có setter public.
- Data chỉ chứa **tham số**: số, enum chọn strategy, tham chiếu asset.
- Nếu một cờ làm thay đổi luồng xử lý, chuyển nó thành strategy (ví dụ `isHoming` → `ProjectileMoveType.Homing`).
- Dùng `OnValidate` để bắt lỗi cấu hình ngay trong Editor.

### Strategy
- Chọn biến thể bằng **enum + một switch duy nhất** trong factory của họ đó.
- Ví dụ: hành vi skill. Asset lưu `SkillBehaviourType[]`, `SkillFactory.New` là switch duy nhất tạo class hành vi.
- Ngoại lệ đã dùng trong code: các strategy chỉ chứa tham số cấu hình (`IShooter`, `IProjectileMove`, `IGameplayEffect`)
  được chọn thẳng trong data bằng `[SerializeReference]`, nên không cần enum + switch. Class có state chạy trong trận
  thì không dùng `[SerializeReference]` làm template (tránh `MemberwiseClone` và dùng chung collection giữa nhân vật).
- Nơi dùng không bao giờ kiểm tra kiểu bằng `is`/`as`.

### Kiểu dùng chung (thư mục `Scripts/Shared`)
- `CharacterHandle`: danh tính một lượt spawn (view + runtime + generation). Thay cho mọi so sánh generation viết tay.
- `DamageInfo`: định nghĩa sát thương duy nhất.

### Enum
- Mọi enum của code game nằm trong `Scripts/Core/GameEnums.cs`, mỗi miền một `#region`. Tìm hoặc thêm enum chỉ cần mở một file.
- Asset và save lưu enum bằng **số**: thêm giá trị mới ở cuối hoặc gán số rõ ràng; không đổi số của giá trị đã dùng.
- Enum chỉ dùng trong Editor (ví dụ `DataInspectorTone`) ở lại `Assembly-CSharp-Editor`, vì code game không được thấy Editor.

### Cấu hình Manager
- Mỗi Manager cần cấu hình có class `Settings` lồng bên trong, installer bind bằng `BindInstance(new XxxManager.Settings(...))`.
- Không dùng `WithArguments`: Zenject ghép tham số theo kiểu chứ không theo tên.

### Pool
- Pool GameObject: `PoolManager` (LeanPool). Clone không được Zenject inject; Manager sở hữu truyền dữ liệu qua `beforeActivate`/`Initialize`.
- Pool class C# thuần: `Stack<T>` do Manager sở hữu tự `new` (ví dụ `CharacterRuntimePool`, `EffectRuntimePool`). Không dùng `MemoryPool` của Zenject.

### Chặn callback cũ bằng version
Callback chạy trễ (tween xong, animation xong, effect hết hạn) có thể thuộc một lượt đã bị hủy. Mẫu chung:
```csharp
int myVersion = ++version;               // bắt đầu lượt mới
tween.OnComplete(() =>
{
    if (myVersion != version) return;    // callback của lượt cũ → bỏ qua
    ...
});
```
- Tăng `version` mỗi khi lượt hiện tại bị hủy hoặc thay thế (Hide, Clear, đổi data).
- Tên biến nói rõ lượt nào: `presentationVersion` (popup kết quả), `playbackVersion` (VFX), `celebrationVersion`
  (quái ăn mừng), `revision` (data của skill đổi giữa frame). Giữ tên theo nghĩa thay vì ép một tên chung.
- Với nhân vật trong pool, dùng `CharacterHandle` thay cho version viết tay.

### Chỉ số nhân vật
- Công thức tính chỉ số cuối cùng **đã nằm ở một chỗ duy nhất**: `StatCalculator.Calculate`
  (base từ SO → cộng thẳng → nhân phần trăm).
- Hiện có hai vấn đề, sẽ xử lý ở **Giai đoạn 2** khi viết lại `Character`:
  1. `CharacterRuntimeData` gánh 3 việc: chỉ số, HP/giáp, thông báo và vòng đời pool.
  2. Có **hai cách thêm bonus**: `SetExternalBonus` (PlayerSystem gom level vĩnh viễn + level trận + trang bị
     thành một giá trị rồi ghi đè) và `AddModifier` (skill, effect, wave thêm từng nguồn và nhận lease để gỡ).
- Hướng xử lý: tách class `StatSheet` khỏi `CharacterRuntimeData`. Class này gồm danh sách nguồn bonus,
  lời gọi `StatCalculator.Calculate`, `Final` và event `Changed`. Mọi nguồn bonus, dù vĩnh viễn hay tạm thời,
  đều thêm theo **một cách duy nhất** là `AddSource(...)` và nhận lease để gỡ. `StatSheet` không thêm công thức mới.

### Jobs
- `SimulationManager` sở hữu các `NativeArray` vị trí, hướng và hitbox.
- `Character` chỉ giữ handle để đọc, không lưu bản sao vị trí.

## 6. Vòng đời

Container chỉ đăng ký các Manager. Thứ tự nội bộ do từng Manager tự gọi trong code.
Thứ tự giữa các Manager là **vị trí trong bốn danh sách** của `GameSceneInstaller` (mục *Lifecycle Order*);
`ConfigureLifecycleOrder` sinh số cho Zenject, không ai viết số tay. Chỉ Manager có Initialize/Tick/LateTick/Dispose
thật sự mới có dòng ở đó. Mọi Manager kế thừa `DomainManager` (lifecycle virtual).

```text
Initialize: Save → Progression → Character → World → Game
Tick:       Run (đồng hồ) → Combat (effect → skill → đạn) → Stage (wave) → Character (controller) → World (nền)
LateTick:   Simulation (di chuyển) → World (camera) → Stage (bù camera cho đội hình) → Character (visual)
            → Combat (vùng skill, VFX) → Run (nhặt loot) → UI (SkillChoiceController → GameplayController, ở UIInstaller)
Dispose:    Game → Stage → World → Run → Character → Combat → Pool → Progression → Save → Simulation
```

View trong scene/pool không tự đăng ký tick: Manager của miền tick hộ (camera trong World, `CharacterVisualHandle`
qua registry của Character, `SkillAreaVisual` và VFX trong Combat). **Check Service Contracts** chặn field `TickableManager`.

## 7. Cấu trúc thư mục đích

```text
Scripts/
├── Shared/          CharacterHandle, DamageInfo, GameConstants
├── Save/  Progression/  Pooling/
├── Simulation/      Movement/, Spatial/
├── Characters/      CharacterManager.cs, CharacterContracts.cs, Character.cs,
│                    CharacterView.cs, Controllers/, Data/
├── Combat/          CombatManager.cs, CombatContracts.cs, Skills/, Shooting/,
│                    Projectiles/, Effects/, Vfx/ (data SO nằm trong Data/ của từng nhóm)
├── Stage/  Run/  Game/  UI/
└── Installers/      GameSceneInstaller.cs
```

## 8. Quy tắc viết code

| Ưu tiên | Quy tắc |
| --- | --- |
| Simple | Method ngắn, return sớm, tối đa 2 cấp lồng |
| Simple | Tên nói rõ ý định, không viết tắt |
| Simple | Chỉ tối ưu (Span, Jobs) ở chỗ nóng đã đo bằng Profiler |
| Simple | Manager chỉ điều phối; nếu vượt khoảng 300 dòng, logic đang đặt sai chỗ |
| Simple | Không dùng `partial` (`GameSceneInstaller` đã gộp lại thành một file ở GĐ 6) |
| DRY | Code lặp đến lần thứ 3 mới tách ra |
| DRY | Mỗi khái niệm chỉ có một nơi định nghĩa |
| SOLID | Interface chỉ ở ranh giới miền hoặc họ strategy |

### Comment
- Viết bằng tiếng Việt.
- Mọi type và member public/internal có `/// <summary>` mô tả **vai trò** của nó.
- Comment trong thân hàm giải thích **tại sao**, cùng các điều kiện biên, thứ tự bắt buộc và lý do bảo vệ (guard). Không lặp lại những gì code đã nói rõ.

### Region
Chia `#region` theo **nhóm logic** khi class có từ 2 nhóm trở lên. Thứ tự gợi ý:
`Fields` → `Properties / Events` → `Constructor / Lifecycle` → các nhóm hành vi public → `Private Helpers`.
Không bọc một region quanh một member duy nhất.
Tên region viết bằng tiếng Anh (thống nhất toàn dự án); được thêm ghi chú tiếng Việt trong ngoặc, ví dụ `Spawn State (reset mỗi lượt spawn)`.

### Trình bày
- Mỗi lệnh một dòng; khối `{ ... }` nhiều lệnh thì xuống dòng.
- Một dòng trống giữa hai member.
- Comment cuối dòng cách code đúng một dấu cách, không căn lề bằng nhiều dấu cách. Không dùng ký hiệu `// *` / `// !` (viết "Lưu ý:").
- Field có `[Tooltip]` thì không viết lại cùng ý ở comment cuối dòng; chỉ comment khi có ý mà Tooltip không nói.

## 9. Lộ trình refactor

Mỗi giai đoạn chia thành task nhỏ. Kết thúc mỗi task, game vẫn phải biên dịch được;
kết thúc mỗi giai đoạn, game phải chơi được và qua smoke test trong Play Mode.

| Giai đoạn | Nội dung | Trạng thái |
| --- | --- | --- |
| 0. Nền tảng | ✅ 0.1 `CharacterHandle` · ✅ 0.2 `DamageInfo` vào `Shared/` · ✅ 0.3 chốt hướng chỉ số (chuyển sang GĐ 2) · ✅ 0.4 bỏ `GameUIEventBus` static (Game gọi xuống Combat/Character; UI nghe `IGameStateReader.StateChanged`) | ✅ Xong |
| 1. Combat | ✅ 1.1 làm phẳng strategy, xóa 2 factory trung gian · ✅ 1.2 `CombatManager` + `CombatContracts.cs`, `ProjectileView` không inject nữa · ✅ 1.3 bỏ interface nội bộ, `SkillServices` concrete · ✅ 1.4 tách `CombatSpawner` → `ProjectileSystem` (trọn vòng đời đạn) + `CombatVfx` · ✅ 1.5 (gộp vào 1.2) · ✅ 1.6 `CharacterHandle` trong Combat · ✅ 1.7 thư mục, comment, tài liệu | ✅ Xong |
| 2. Simulation + Character | ✅ 2.1 làm phẳng `ICharacterController`, bỏ 4 chỗ kiểm tra kiểu · ✅ 2.2 `SimulationManager` (Movement + Spatial) + `SimulationContracts.cs` · ✅ 2.3 `CharacterManager` + `CharacterContracts.cs`, `CharacterView` không inject nữa (`CharacterContext`) · ✅ 2.4 bỏ interface nội bộ (gộp tìm mục tiêu thành `ICharacterSearch`) · ✅ 2.5 Combat nghe vòng đời Character (`CharacterSkillSystem`), xóa `ICombatOwners` · ✅ 2.6 tách `CharacterView` bằng composition (`CharacterPresentation`, `CharacterFeedback`) + `CharacterHandle` · ✅ 2.7 `StatSheet` + một cách thêm bonus (`AddSource`) · ✅ 2.8 thư mục, comment, tài liệu | ✅ Xong |
| 3. World + Stage | ✅ 3.1 **WorldManager** riêng (map nền + ground chunk; nằm trên Character vì cần vị trí Player, nằm dưới Stage vì Stage chọn map) · ✅ 3.2 dọn sạch khi rời gameplay (`ClearGameplayWorld` + lưới an toàn ở pool) · ✅ 3.3 StageManager tự tạo 3 spawn system + BossArena · ✅ 3.4 `EnemySpawner` C# thuần · ✅ 3.5 `StageContracts.cs` (4 vai trò), `StageRewards`, dời sang `Scripts/Stage/` + `Editor/Stage/`, cập nhật tài liệu | ✅ Xong (play test 2026-11-18 OK) |
| 4. Run | ✅ `RunManager` + `RunContracts.cs` (`IRunReader`, `IRunLifecycle`) tự tạo `RunClock`, `RunStatistics`, `RunLevel`, `LootSystem` · xóa `RunSessionManager`, `RunLevelService`, `LootManager`, `IRunSession`, `IRunLevelService`; `LevelManager` chỉ còn level vĩnh viễn · Run tự thêm nguồn bonus level trận vào `StatSheet` của Player (PlayerSystem bỏ nghe ngược) · Run gọi `ICombatLifecycle.ClearRunSkills` khi reset (SkillSystem bỏ nghe ngược) · dời code sang `Scripts/Run/` (giữ `.meta`) | ✅ Xong (play test 2026-11-18 OK) |
| 5. Save + Progression + Pool | ✅ `SaveManager` (tự tạo `SaveStorage` + `SaveMigration`, bỏ binding SaveMigration riêng) + `SaveContracts.cs` (`ISaveStore`, `ISaveMaintenance`) · `ProgressionManager` (tự tạo `Wallet`, `CharacterLevels`, `Loadout`, `StageProgress`) + `ProgressionContracts.cs` (`IWallet`, `IPermanentBonuses`, `IStageProgress`) · `PoolManager` bind qua `BindInterfacesTo` + `PoolContracts.cs` (`IGameObjectPool`, `IPoolPreparation`, `IPoolCleanup`) · xóa `CurrencyService`, `CharacterLevelService`, `LevelManager`, `PlayerEquipmentService`, `StageProgressService` và 8 facade trong `Interfaces/` · kiểm tra save-data chuyển vào chính lớp section (`XxxSaveData.Validate`), Save không còn tham chiếu lên Progression/Character · dời code sang `Save/`, `Progression/`, `Pooling/` (giữ `.meta`) | ✅ Xong (play test 2026-11-18 OK) |
| 6. Game + UI | ✅ `GameManager` bind qua `BindInterfacesTo` + `GameContracts.cs` (`IGameState`, `IGameFlow`, `IGamePause`, `ILoadingProgress`), tự tạo `GameStateMachine`, `GamePause` (thay GameplayPauseService), `GamePrewarm` (thay PoolPrewarmService) · bỏ `WinRun`/`LoseRun` công khai (không ai gọi; thắng qua `IStageResults.Cleared`) · `ISkillChoice` (Run): lượt chờ, roll, áp dụng, chặn skill lỗi chuyển từ `SkillChoiceController` sang `Run/SkillChoice/`; controller chỉ còn trình bày · UI: bỏ binding `UIManager`, `ScreenTransitionController` qua `UI/UIContracts.cs` (`IScreenTransition`) · xóa 5 facade cuối trong `Interfaces/` và `SignalBusInstaller` (không dùng) · `GameSceneInstaller` gộp 3 file partial thành một class: bind một dòng/miền theo chiều phụ thuộc + thứ tự lifecycle · chưa thêm vai trò nâng level vĩnh viễn/đổi trang bị (chưa có UI dùng) | ✅ Xong (play test 2026-11-18 OK) |
| 7. Dọn dẹp | ✅ Đổi tên 7 system con đang mang tên `…Manager` (chỉ Manager của miền mới tên Manager): `MovementSystem`, `SpatialSystem`, `ProjectileSystem`, `SkillSystem`, `EffectSystem`, `PlayerSystem`, `EnemyVisibility` (giữ `.meta`; không asset/scene nào serialize theo tên kiểu) · xóa thư mục `Interfaces/` · xóa runner `Tests/` cũ (thay bằng `Docs/Tools/Verification.md`) · xóa 3 SO effect legacy (`Burn`, `Poison`, `Slow` — 0 tham chiếu) cùng script và inspector · `ServiceContractVerification` **giữ lại** (là bộ kiểm tra tự động duy nhất), bỏ phần facade, thêm quy tắc chặn facade tổng hợp quay lại · cập nhật cây Manager theo code thật và các doc lỗi thời | ✅ Code xong 2026-11-18: compile sạch, 53 contract PASS, Zenject Validate OK, 0 Missing Script; play test 2026-11-19 OK |

Trạng thái kiểm chứng cuối (sau Giai đoạn 7, 2026-11-19): compile sạch (không `error CS`),
**Check Service Contracts** PASS 53 contract (giảm từ 89 vì đã xóa các facade cũ), **Zenject Validate
Current Scenes** OK, **Report Missing Scripts** 0 lỗi, play test thủ công toàn bộ luồng trong Editor không lỗi.
Checklist đầy đủ cho các thay đổi sau này xem `Docs/Tools/Verification.md`.

Khi chuyển MonoBehaviour hoặc ScriptableObject, **phải giữ file `.meta` (GUID)**, nếu không prefab
và scene sẽ báo "Missing Script". Sau mỗi task có đụng tới MonoBehaviour/SO, chạy
**Shooter Game > Verification > Report Missing Scripts**.

Trong lúc chuyển tiếp, Manager mới có thể tạm implement các interface cũ mà code chưa chuyển
vẫn đang dùng. Các interface tạm này bị xóa ở giai đoạn 7.

## 10. Lỗi đã biết (chờ xử lý)

| Phát hiện | Mô tả | Hướng xử lý | Dự kiến |
| --- | --- | --- | --- |
| 2026-11-18, sau Task 2.5 | Về Home giữa trận: Player biến mất nhưng **các quái đang spawn dở vẫn hiện** trên scene. | ✅ **Task 3.2**: `GameManager.ClearGameplayWorld()` là chỗ dọn duy nhất cho cả về Home và vào trận mới (Stage → Combat → Player → `ICharacterDespawn.DespawnAll()` → RunReset/loot), cộng lưới an toàn `IPoolCleanup.DespawnGameplayClones()` thu hồi mọi clone gameplay còn sót (giữ World/UI) và log warning kèm tên. | Nếu warning xuất hiện khi play test: dùng tên object trong log để tìm miền quên dọn. |
