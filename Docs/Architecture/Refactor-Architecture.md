# Kiến trúc mới: Data-driven có khung OOP

Ngày chốt: 2026-11-18. Trạng thái: **đang refactor**. Code cũ vẫn tồn tại song song
cho tới khi từng giai đoạn ở mục 9 hoàn thành. Khi một miền đã chuyển xong, tài liệu này
là nguồn sự thật cho miền đó. `Systems-Overview.md` chỉ còn mô tả phần chưa chuyển.

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
⬆ ISaveStore

ProgressionManager
├── Wallet, CharacterLevels, Loadout, StageProgress
⬆ IWallet, IPermanentBonuses, IStageProgress      ⚡ WalletChanged

PoolManager
├── GameObjectPools, RuntimePools, Prewarmer
⬆ IPools

SimulationManager   (Jobs/Burst, chủ duy nhất của vị trí, hướng và hitbox)
├── MovementSystem, SpatialSystem   (Ground là miền World riêng, xem WorldManager)
⬆ IMovement, IHitboxes, ISpatialQuery

CharacterManager
├── CharacterFactory, CharacterRegistry, PlayerSystem, EnemyVisibility
└── ICharacterController ← PlayerController / EnemyController          (Strategy)
⬆ ICharacterSpawner, ITargetQuery, ICharacterDamage, ICharacterStats
⚡ Spawned, Died, Despawned

CombatManager
├── SkillSystem → SkillFactory
│     ├── ShootFactory → IShootPattern ← Straight/Fan/Circle/Spiral   (Strategy)
│     └── Saw / FrostZone …                                          (Strategy)
├── ProjectileSystem → IProjectileMove ← Linear / Homing …            (Strategy)
├── EffectSystem → IEffect ← Slow / Burn / Poison                     (Strategy)
└── CombatVfx
⬆ ISkillGrant, ISkillReader

WorldManager   (map nền; không biết wave, boss hay điều kiện thắng)
├── MapCatalog (MapDatabaseSO), GroundChunks (nền theo chunk, bám vị trí Player)
⬆ IWorldMap (đổi map theo key, đọc map hiện tại)

StageManager
├── WaveSystem → IFormation, IPath                                    (Strategy)
└── BossSystem
⬆ IStageSelection, IStageResult                    ⚡ StageCleared, BossSpawned
   (dùng IWorldMap để đặt map của màn; World không biết Stage)

RunManager
├── RunClock, RunLevel, LootSystem
⬆ IRunReader, ISkillChoice                         ⚡ LevelUp, LootCollected

GameManager
├── GameStateMachine, PauseControl
⬆ IGameFlow, IGameState                            ⚡ StateChanged

UIManager
├── ScreenNavigation, ScreenTransition, HomeScreen, HudScreen,
│   SkillChoicePopup, ResultPopup, PausePopup
⬆ (không có; UI là lá cuối)
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
Lên cấp:   Run.LevelUp ─event─▶ UI mở popup ─ISkillChoice.Choose()─▶ Run ─ISkillGrant─▶ Combat
Quái chết: Combat ─ICharacterDamage─▶ Character ─event Died─▶ Combat / Run / Stage
Hết trận:  Stage.StageCleared ─event─▶ Game.Win() ─event StateChanged─▶ UI hiện Result
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
- Nơi dùng không bao giờ kiểm tra kiểu bằng `is`/`as`.

### Kiểu dùng chung (thư mục `Scripts/Shared`)
- `CharacterHandle`: danh tính một lượt spawn (view + runtime + generation). Thay cho mọi so sánh generation viết tay.
- `DamageInfo`: định nghĩa sát thương duy nhất.

### Chỉ số nhân vật
- Công thức tính chỉ số cuối cùng **đã nằm ở một chỗ duy nhất**: `StatCalculator.Calculate`
  (base từ SO → cộng thẳng → nhân phần trăm).
- Hiện có hai vấn đề, sẽ xử lý ở **Giai đoạn 2** khi viết lại `Character`:
  1. `CharacterRuntimeData` gánh 3 việc: chỉ số, HP/giáp, thông báo và vòng đời pool.
  2. Có **hai cách thêm bonus**: `SetExternalBonus` (PlayerManager gom level vĩnh viễn + level trận + trang bị
     thành một giá trị rồi ghi đè) và `AddModifier` (skill, effect, wave thêm từng nguồn và nhận lease để gỡ).
- Hướng xử lý: tách class `StatSheet` khỏi `CharacterRuntimeData`. Class này gồm danh sách nguồn bonus,
  lời gọi `StatCalculator.Calculate`, `Final` và event `Changed`. Mọi nguồn bonus, dù vĩnh viễn hay tạm thời,
  đều thêm theo **một cách duy nhất** là `AddSource(...)` và nhận lease để gỡ. `StatSheet` không thêm công thức mới.

### Jobs
- `SimulationManager` sở hữu các `NativeArray` vị trí, hướng và hitbox.
- `Character` chỉ giữ handle để đọc, không lưu bản sao vị trí.

## 6. Vòng đời

Container chỉ đăng ký các Manager. Thứ tự nội bộ do từng Manager tự gọi trong code.

```text
Initialize: Save → Progression → Pool → Simulation → Character → Combat → Stage → Run → Game → UI
Dispose:    chạy ngược lại
Tick:       Run (đồng hồ) → Combat (effect → skill → đạn) → Stage (wave) → Character (controller)
LateTick:   Simulation (di chuyển) → Run (nhặt loot sau khi di chuyển)
```

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
| Simple | Không dùng `partial` |
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

## 9. Lộ trình refactor

Mỗi giai đoạn chia thành task nhỏ. Kết thúc mỗi task, game vẫn phải biên dịch được;
kết thúc mỗi giai đoạn, game phải chơi được và qua smoke test trong Play Mode.

| Giai đoạn | Nội dung | Trạng thái |
| --- | --- | --- |
| 0. Nền tảng | ✅ 0.1 `CharacterHandle` · ✅ 0.2 `DamageInfo` vào `Shared/` · ✅ 0.3 chốt hướng chỉ số (chuyển sang GĐ 2) · ✅ 0.4 bỏ `GameUIEventBus` static (Game gọi xuống Combat/Character; UI nghe `IGameStateReader.StateChanged`) | ✅ Xong |
| 1. Combat | ✅ 1.1 làm phẳng strategy, xóa 2 factory trung gian · ✅ 1.2 `CombatManager` + `CombatContracts.cs`, `ProjectileView` không inject nữa · ✅ 1.3 bỏ interface nội bộ, `SkillServices` concrete · ✅ 1.4 tách `CombatSpawner` → `ProjectileManager` (trọn vòng đời đạn) + `CombatVfx` · ✅ 1.5 (gộp vào 1.2) · ✅ 1.6 `CharacterHandle` trong Combat · ✅ 1.7 thư mục, comment, tài liệu | ✅ Xong |
| 2. Simulation + Character | ✅ 2.1 làm phẳng `ICharacterController`, bỏ 4 chỗ kiểm tra kiểu · ✅ 2.2 `SimulationManager` (Movement + Spatial) + `SimulationContracts.cs` · ✅ 2.3 `CharacterManager` + `CharacterContracts.cs`, `CharacterView` không inject nữa (`CharacterContext`) · ✅ 2.4 bỏ interface nội bộ (gộp tìm mục tiêu thành `ICharacterSearch`) · ✅ 2.5 Combat nghe vòng đời Character (`CharacterSkillSystem`), xóa `ICombatOwners` · ✅ 2.6 tách `CharacterView` bằng composition (`CharacterPresentation`, `CharacterFeedback`) + `CharacterHandle` · ✅ 2.7 `StatSheet` + một cách thêm bonus (`AddSource`) · ✅ 2.8 thư mục, comment, tài liệu | ✅ Xong |
| 3. World + Stage | ✅ 3.1 **WorldManager** riêng (map nền + ground chunk; nằm trên Character vì cần vị trí Player, nằm dưới Stage vì Stage chọn map) · ✅ 3.2 dọn sạch khi rời gameplay (`ClearGameplayWorld` + lưới an toàn ở pool) · ✅ 3.3 StageManager tự tạo 3 spawn system + BossArena · ✅ 3.4 `EnemySpawner` C# thuần · ✅ 3.5 `StageContracts.cs` (4 vai trò), `StageRewards`, dời sang `Scripts/Stage/` + `Editor/Stage/`, cập nhật tài liệu | ✅ Xong (play test 2026-11-18 OK) |
| 4. Run | RunSession, RunLevel, Level, Loot | |
| 5. Save + Progression + Pool | Wallet, Levels, Loadout, StageProgress | |
| 6. Game + UI | GameStateMachine, UIManager | |
| 7. Dọn dẹp | Xóa `Interfaces/`, `ServiceContractVerification`, các runner `Tests/` cũ; cân nhắc xóa các SO effect legacy (`Combat/Effects/Data`: 3 asset + inspector trong `ProjectDataSOEditor`, code gameplay không còn đọc); cập nhật docs | |

Giai đoạn 3 đã kiểm chứng ngày 2026-11-18: compile sạch (không `error CS`), **Check Service Contracts**
PASS 89 contract, **Report Missing Scripts** 0 lỗi, play test thủ công trong Editor không lỗi.
**Run Service Smoke Checks** (Play Mode) chưa chạy lại sau Task 3.5, nên chạy khi có dịp: bộ này
assert binding, effect, đạn, skill và stale handle chứ không chỉ kiểm tra khai báo interface.

Khi chuyển MonoBehaviour hoặc ScriptableObject, **phải giữ file `.meta` (GUID)**, nếu không prefab
và scene sẽ báo "Missing Script". Sau mỗi task có đụng tới MonoBehaviour/SO, chạy
**Shooter Game > Verification > Report Missing Scripts**.

Trong lúc chuyển tiếp, Manager mới có thể tạm implement các interface cũ mà code chưa chuyển
vẫn đang dùng. Các interface tạm này bị xóa ở giai đoạn 7.

## 10. Lỗi đã biết (chờ xử lý)

| Phát hiện | Mô tả | Hướng xử lý | Dự kiến |
| --- | --- | --- | --- |
| 2026-11-18, sau Task 2.5 | Về Home giữa trận: Player biến mất nhưng **các quái đang spawn dở vẫn hiện** trên scene. | ✅ **Task 3.2**: `GameManager.ClearGameplayWorld()` là chỗ dọn duy nhất cho cả về Home và vào trận mới (Stage → Combat → Player → `ICharacterDespawn.DespawnAll()` → RunReset/loot), cộng lưới an toàn `IPoolCleanup.DespawnGameplayClones()` thu hồi mọi clone gameplay còn sót (giữ World/UI) và log warning kèm tên. | Nếu warning xuất hiện khi play test: dùng tên object trong log để tìm miền quên dọn. |
