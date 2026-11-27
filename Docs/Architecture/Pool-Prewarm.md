# Pool preparation before gameplay

> **Trạng thái:** ✅ Hiện hành · Kiểm tra với code: 2026-11-19.

`GameSceneInstaller.poolPrewarmSettings` points to
`Assets/_Project/DataSO/Databases/PoolPrewarmSettings.asset`.

Prewarm is owned by the Game domain: `GameManager` creates `GamePrewarm` and runs it in
`GameManager.Initialize` (100), after SaveManager (-200), ProgressionManager (-160) and
CharacterManager (-150). It lives in Game rather than Pool because the plan needs Character,
Combat and Simulation capacities, which themselves depend on Pool. The Loading UI reads
progress through `ILoadingProgress`. A preparation failure puts the game in Faulted
before the initial player spawn. No loading scene or scene persistence is added.

The profile prepares shared character/bullet/effect/pickup shells, visual prefabs
from CharacterDatabase and CombatAssets, CharacterRuntimeData, the registered
slow/periodic-damage effect runtimes, and movement/projectile/spatial buffers. Cooldown and effect duration live directly on their managed runtimes; no timer buffer is allocated.
Additional Prefabs can include future VFX and world objects. Counts for a shared
prefab are added together. Zero disables preparation for that category.

GameObject preparation holds all requested clones under the inactive Preparing
holder until injection finishes, then returns them. It does not initialize a
character, activate gameplay, play VFX, or register live projectile lifetimes.
LeanPool's direct PreloadOneMore is deliberately not used: it creates an active
prefab at the scene root before deactivation, which can call OnEnable too early.

CharacterFactory rents CharacterRuntimeData for the normal player/enemy spawn
paths. CharacterView returns it after skills, effects, controller and release
callbacks are cleaned. HP, shield, bonuses and Changed subscribers are reset.
Generation is checked by cached targets, skill owners, effects, spatial entries
and modifier leases so old references cannot act on a reused runtime.

`GamePrewarm.PrewarmAsync()` is idempotent after success and exposes IsReady and
PreparedObjectCount (progress reaches the UI through `ILoadingProgress`). These are integration points for a later loading flow.
Pools still grow when usage exceeds prepared counts; the profile is a starting
budget, not a spawn cap or a zero-allocation guarantee. Controllers, skill
behaviors/closures and some per-spawn collections are still initialized during
spawn. Animator/UI first activation and rendering/shader warmup are separate from
inactive object preparation. Profile startup time, memory and peak counts on the
target device before increasing counts substantially.

Pool lifetime is the gameplay scene. Returning to another scene releases these
pools; a future cross-scene preparation flow will need a persistent container or
an explicit transfer of ownership.

## Vỏ và visual combat tách số (2026-11-27)

Visual đạn/impact giờ là particle (nặng hơn vỏ nhiều), nên số tạo sẵn tách làm hai:

| Field | Nghĩa | Giá trị |
|---|---|---|
| `bullets` | Số vỏ `ProjectileView` + sức chứa buffer di chuyển/đạn | 256 |
| `effectShells` | Số vỏ `EffectView` cho mỗi EffectType (cộng dồn) | 16 |
| `bulletVisualsPerType` | Visual tạo sẵn cho **mỗi** BulletType | 128 (64 khi đạn còn là particle; đạn sprite + vệt rẻ ~6 lần nên tăng lên) |
| `impactVisualsPerType` | Visual tạo sẵn cho **mỗi** EffectType | 32 |

Trước đây `bullets` cũng là số visual mỗi loại đạn (4 × 256 = 1024 particle) và `effectShells` cũng là số visual mỗi
loại impact (16, bài stress phải Instantiate giữa trận). Code: `PoolPrewarmSettingsSO`, `GamePrewarm.BuildObjectPlan`,
inspector `ProjectDataSOEditor` (section COMBAT VISUALS PER TYPE).

### Đo (Editor, máy dev, 2026-11-27)

**Chi phí tạo một visual** (Instantiate, Edit Mode, trung bình ở 256 bản/loại): đạn ~0,31 ms, ~21 KB; impact ~0,97 ms,
~161 KB (5–7 Particle System mỗi impact).

| Phần VFX tạo sẵn | Số visual | Thời gian | Bộ nhớ |
|---|---|---|---|
| Cũ (256 đạn/loại, 16 impact/loại) | 1088 | ~380 ms | ~32 MB |
| **Mới (64/32)** | **384** | **~200 ms** | **~26 MB** |

Tổng object tạo sẵn (log `[PoolPrewarm] Ready`): 1673 → **1065**.

**Trong game** (Play Mode, Home, `timeScale` 1, cùng một loại đạn Holy / impact HolyImpact; CPU là "Main Thread" của Editor,
mốc nền 6,8 ms, 98 batches, 44 SetPass, 98 draw call, GC 12 KB/frame là của Editor):

| Đạn cùng lúc | Frame spawn | Tạo mới | CPU tb | Draw call tb | Batches | GC/frame |
|---|---|---|---|---|---|---|
| 10 | 0,9 ms* | 0 | 7,7 ms | 117 | 100 | = nền |
| 25 | 1,2 ms | 0 | 8,7 ms | 145 | 100 | = nền |
| 50 | 2,3 ms | 0 | 10,8 ms | 193 | 100 | = nền |
| 100 | **12,9 ms** | 36 | 13,5 ms | 281 | 100 | = nền |
| 200 | **30,4 ms** | 100 | 18,2 ms | 465 | 100 | = nền |
| 400 | **64,6 ms** | 200 (+144 vỏ) | 27,0 ms | 864 | 100 | = nền |
| 200 / 400 lần 2 (pool đã đủ) | 7,3 / 16,2 ms | 0 | | | | |

\* lần bắn đầu tiên của phiên còn JIT (9 ms); con số ở bảng là ~0,05–0,09 ms/viên sau khi nóng.

| Impact mỗi 0,4 s | Cùng lúc tối đa | CPU tb (max) | Batches tb | SetPass tb | Draw call tb | Pool sau đo |
|---|---|---|---|---|---|---|
| 10 | ~30 | 8,0 ms | 124 | 94 | 166 | 32 (đủ) |
| 25 | ~75 | 10,8 ms (19,7) | 156 | 158 | 264 | 75 |
| 50 | ~150 | 12,8 ms (18,7) | 209 | 263 | 431 | 150 |
| 100 | ~300 | 17,3 ms (26,2) | 312 | 471 | 739 | 300 |
| 200 | ~600 | 30,4 ms (**260**) | 496 | 839 | 1240 | 600 |

**Kết luận:**
- Bắn/phát từ pool không cấp phát (GC/frame bằng nền ở mọi mức) và rẻ: ~0,04–0,06 ms/cái.
- Vượt số tạo sẵn thì mỗi bản Instantiate thêm ~0,3 ms (đạn) / ~1 ms (impact) ngay trong frame đó: 100 viên cùng loại
  (vượt 64) làm frame spawn 2,3 → 12,9 ms; 200 impact mỗi đợt gây frame đỉnh 260 ms.
- Đạn gần như không tăng batches (cùng material được gộp), chỉ tăng draw call ~2/viên. **Impact đắt về render**: SetPass
  tăng ~4/impact vì mỗi impact 5–7 lớp material khác nhau. Giới hạn thật khi đông là render impact, không phải pool.
- (Số đo ở trên là lúc đạn còn là particle.) Sau khi đạn chuyển sang sprite + vệt (2026-11-27): tổng object tạo sẵn 1321
  với 128 đạn/loại; bắn 200 viên cùng loại chỉ tạo thêm 72 bản (~0,05 ms mỗi bản), frame spawn 10,8 ms; 400 viên lần 2
  (pool đủ) 18,3 ms (~0,046 ms/viên).
- 64/32 (đạn particle) đủ cho tới ~50 đạn cùng loại và ~30 impact cùng loại cùng lúc. Trận thật vượt mức đó thường xuyên thì tăng số
  tương ứng (đo đỉnh thật trên thiết bị trước), hoặc bớt lớp hạt của impact.
- Số đo trên Editor (máy đang thiếu RAM); thiết bị Android cần đo riêng.
