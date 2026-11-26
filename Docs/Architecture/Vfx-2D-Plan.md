# VFX 2D (particle) cho combat

> **Trạng thái:** ✅ Hiện hành · Kiểm tra với code và chơi thử qua MCP: 2026-11-27.
> Bản kế hoạch cũ (converter Editor, ngân sách VFX, VfxQuality) đã được rút gọn: xem mục 6.

Đạn, impact, vùng băng và trạng thái trên quái dùng particle từ hai pack **Hovl Studio – Toon Projectiles 2** và
**AllIn1VfxToolkit**. Không có công cụ chuyển đổi: mỗi prefab trong game là một **Prefab Variant** của template,
chỉnh tay (hoặc bằng script Editor một lần) rồi kéo vào data như sprite trước đây.

---

## 1. Template

- `Assets/_Project/Vfx/Templates/{Hovl,AllIn1}/`: bản sao 86 prefab cùng material, texture, mesh. Đã bóc script tự hủy,
  vật lý, đèn; shader Hovl đổi tên thành `ShooterGame/Vfx/Hovl/...`. Pack gốc giữ nguyên.
- `VfxSpin`, `VfxBob` (`Scripts/Combat/Vfx/`) thay script xoay/nhún của AllIn1.
- **Không xóa `Assets/AllIn1VfxToolkit`**: bản sao AllIn1 vẫn dùng shader của pack.
- Material của bản sao Hovl đã tắt `_Usedepth` (soft particle cần depth texture mà camera 2D không có).

## 2. Hệ thống đang chạy

### Script

| Script | Đặt ở đâu | Việc |
|---|---|---|
| `ParticleProjectileVisual` | Gốc prefab đạn, VFX vùng, VFX trạng thái | Bật: Clear + Play mọi hạt và `TrailRenderer` con. Tắt: Stop + Clear. Prefab dùng với pool nên mỗi lần lấy ra bắt đầu sạch, không kéo vệt từ chỗ cũ |
| `ParticleEffectVisual` (có sẵn) | Gốc prefab impact (cần `ParticleSystem` ở gốc) | `IEffectVisual`: phát một lần; `Advance` trả true khi mọi hạt tắt để `CombatVfx` trả về pool |
| `EffectView` / `ProjectileView` | Vỏ dùng chung trong pool | Thuê vỏ rồi gọi `Play` / `Launch` ngay sau đó (OnEnable không làm gì): không lambda `beforeActivate`, không cấp phát mỗi lần |
| `SkillAreaVisual` | Prefab vùng/lưỡi skill | Ẩn/hiện mesh **và** mọi VFX con (`SetVisible`) |
| `CharacterFeedback` | Do `CharacterView` sở hữu | Bật/tắt VFX trạng thái cùng màu trạng thái |

Không có interface mới: `ProjectileView` và `EffectView` không phân biệt sprite hay particle.

### Prefab trong game (`Assets/_Project/Prefabs/Vfx/`)

| Prefab | Nguồn | Scale | Chỉnh thêm |
|---|---|---|---|
| `Bullets/FireBullet` | Hovl Projectile 1 | 0,35 | |
| `Bullets/IceBullet` | AllIn1 Ice Projectile | 0,12 | Độ rộng `TrailRenderer` nhân theo scale |
| `Bullets/PoisonBullet` | Hovl Projectile 12 | 0,3 | |
| `Bullets/HolyBullet` | Hovl Projectile 11 | 0,2 | |
| `Impacts/FireImpact` | Hovl Hit 1 | 0,15 | Tắt lớp `Smoke` (khói xám làm đục màu) |
| `Impacts/IceImpact` | Hovl Hit 14 | 0,13 | Tắt `ShockWave` (Distortion/GrabPass) và `Smoke` |
| `Impacts/PoisonImpact` | Hovl Hit 5 | 0,15 | `Core`, `Water` đổi màu sang xanh lá (bản gốc xanh dương) |
| `Impacts/HolyImpact` | Hovl Hit 11 | 0,13 | |
| `Areas/FrostAuraVfx` | Lớp `SnowFlakes` + `IceFlares` của AllIn1 Ice Projectile | – | Phát trong vòng tròn bán kính 1, `scalingMode = Shape` |
| `Status/BurnStatus` | Hạt mới, material Hovl `Trail75bcg` + `Point11bcg` | – | Lưỡi lửa cam bốc lên |
| `Status/PoisonStatus` | Như trên, màu xanh lá | – | Khói + bọt độc |
| `Status/SlowStatus` | Hạt mới, material AllIn1 `SnowFlakes` | – | Bông tuyết xoay quanh thân |

Prefab sprite cũ (`Prefabs/VisualProjectile`, `Prefabs/VisualEffect`) giữ lại làm đường lui.

### Gán vào data

| Chỗ gán | Nội dung |
|---|---|
| `DataSO/Databases/CombatAssets.asset` | `visualPrefab` của 4 đạn và 4 impact (giữ nguyên `hitRadius`/`hitOffset`) |
| `Prefabs/FrostAuraVisual.prefab` | `FrostAuraVfx` là object con; mesh tròn mờ vẫn giữ làm viền vùng |
| `Prefabs/Character.prefab` → `CharacterView` | `Poison Vfx`, `Burn Vfx`, `Slow Vfx` (trống = chỉ đổi màu) |
| `DataSO/Databases/PoolPrewarmSettings.asset` | `additionalPrefabs`: 3 VFX trạng thái × 32 |

### Luồng

```text
Đạn:      ProjectileSystem thuê vỏ → ProjectileView.Launch: thuê visual con (FireBullet…), đăng ký movement
          → OnEnable của ParticleProjectileVisual: Clear + Play
          → trúng/hết tuổi/ra khỏi camera → trả visual (OnDisable: Stop + Clear) → trả vỏ
Impact:   CombatVfx.Spawn thuê vỏ → EffectView.Play: thuê visual con (FireImpact…), Play
          → mỗi LateTick CombatVfx hỏi Advance → true (hạt tắt hết) → trả visual → trả vỏ
Vùng băng: SkillAreaVisual (localScale = bán kính) chứa FrostAuraVfx → hạt phát trong vòng bán kính 1 nên tự khớp vùng
Trạng thái: EffectSystem → controller (đếm nguồn) → FeedbackChanged → CharacterFeedback
          bật: thuê VFX (pool group Other) · LateTickFrame: bám HitPosition · tắt/chết/kết quả/thu hồi: trả về pool
```

**Vì sao VFX trạng thái không làm con của vỏ nhân vật:** trạng thái có thể bật/tắt ngay trong `OnEnable`/`OnDisable`
của vỏ (Combat dừng effect khi vỏ tắt), lúc Unity cấm đổi cha của object con. VFX nằm ngoài và bám theo ở LateTick,
sau khi Simulation đã áp di chuyển.

## 3. Thêm một VFX mới

1. Tạo **Prefab Variant** từ template trong `Vfx/Templates` (không sửa template), lưu vào `Prefabs/Vfx/...`.
2. Đặt transform gốc: vị trí 0; đạn Hovl/AllIn1 xoay **(0, 90, 0)** để bay theo +X; chọn scale.
3. Mọi Particle System: `scalingMode = Hierarchy`.
   - Đạn, vùng, trạng thái: **Loop** + Play On Awake.
   - Impact: tắt Loop, `duration` và thời gian sống của hạt ≤ 0,6 giây.
4. Mọi renderer: sorting layer Default, tắt shadow/probe/motion vector. Order:
   - đạn, impact: **10**;
   - VFX trạng thái: **15** (quái order 1, vỏ 20);
   - vùng dưới nhân vật: **0** (mesh vùng: -1).
5. `TrailRenderer`: nhân `widthMultiplier` theo scale (độ rộng vệt không co theo transform).
6. Tắt (không xóa được trong variant) object con dùng shader **Distortion/GrabPass** và lớp khói đục.
7. Gắn script gốc: đạn/vùng/trạng thái → `ParticleProjectileVisual`; impact → `ParticleEffectVisual`.
8. Kéo vào data (bảng mục 2). VFX trạng thái mới cần thêm dòng prewarm.
9. Kiểm tra: **Report Missing Scripts**, **Check Object Pools**; chơi thử: vệt không kéo khi tái dùng, impact tự về pool,
   về Home không có cảnh báo `[PoolManager] Rời gameplay còn sót`.

## 4. Lưu ý đã gặp

- **Kích thước:** sprite đạn cũ dài khoảng 0,24 đơn vị (to hơn quái cao 0,17). Đạn Hovl ở scale 0,05–0,08 chỉ còn một chấm;
  phải khoảng 0,2–0,35. Vệt đuôi dài theo quãng bay thật nên không phụ thuộc scale.
- **Shader AllIn1 không nhận màu qua `startColor`** (màu lấy từ texture/gradient của material). Muốn đổi màu thì dùng
  material Hovl (nhận màu tốt) hoặc nhân bản material.
- Prefab lửa/aura của AllIn1 (Pixel Fire, Real Fire, Magic Spiral, Holly Aura, Water Shield) làm cho cỡ lớn và camera 3D:
  ở cỡ quái gần như chỉ còn khói/distortion. Blue Pixel Portal là cổng dựng đứng hình bầu dục, không phải vòng tròn.
- Không có Bloom nên hiệu ứng nhạt hơn ảnh demo; nền cát làm lớp Additive yếu đi.
- Test qua MCP khi cửa sổ Unity không được focus: đặt `Application.runInBackground = true` trong Play Mode, nếu không
  Editor gần như đứng frame.

## 5. Đã kiểm tra (2026-11-27)

- Chơi thử 4 loại đạn: bay đúng hướng, không bị thu hồi sớm theo bounds, không vệt kéo khi tái dùng, mọi visual nằm trong vỏ.
- 20 impact cùng lúc: phát hết (~0,6 giây) rồi tự về pool.
- Vùng băng cấp 1→5: VFX khớp vùng; `SetVisible(false/true)` tắt/bật đủ 3 renderer; vào trận mới lấy lại từ pool vẫn sạch.
- VFX trạng thái: bật/tắt theo số nguồn (2 nguồn bớt 1 vẫn giữ); bám đúng `HitPosition`; quái chết thì trả, không có VFX
  mồ côi; FrostAura thật làm chậm quái → hiện bông tuyết; về Home còn 0.
- Prewarm vẫn 1673 object; **Check Service Contracts** PASS, **Report Missing Scripts** 0, **Check Object Pools** PASS,
  **Zenject Validate** OK, **Run Service Smoke Checks** PASS.
- Chưa đo Profiler trên thiết bị Android.

### Đo hiệu năng (Editor, 2026-11-27)

Cách đo cấp phát chính xác: `ProfilerRecorder` ("GC Allocated In Frame", "GC Allocation In Frame Count"), game pause, mỗi
frame luân phiên *bắn 64 → dọn → nghỉ*, lấy trung vị 30 chu kỳ, trừ frame nghỉ (~25,5 KB/frame là của chính Editor).
(`GC.GetAllocatedBytesForCurrentThread` luôn trả 0 trên Mono của Unity; Editor không cho tắt GC.)

| Mỗi lần | Trước | Sau "thuê rồi phóng/phát" |
|---|---|---|
| Bắn 1 viên đạn | ~303 B, ~4,1 lần cấp phát (closure + delegate `beforeActivate`, mảng `GetComponentsInChildren`, boxing `Enum.IsDefined`) | **0** |
| Phát 1 impact | ~327 B, ~4 lần (2 closure + 2 delegate) | **0** |

Kịch bản chơi thật (4 skill cấp tối đa, 600 frame): main thread, batches (~61), draw call (~75) không đổi; phần GC tiết kiệm
(~100 B/frame) nằm trong nhiễu của Editor. So với đạn/impact sprite cũ cùng kịch bản: particle thêm khoảng **+12 batches,
+14 SetPass, +26 draw call**, CPU không đổi rõ.

Stress (spawn cùng một frame, đạn tỏa 360° + impact rải trong màn hình):

| Lượt | Frame spawn | GC frame spawn | Clone tạo thêm | Đỉnh draw call | Kết quả |
|---|---|---|---|---|---|
| 100/100 lần 1 | 95 ms | 138 KB | 72 (vượt 64 vỏ impact prewarm) | 812 | Thu hồi hết, 0 lạc |
| 100/100 lần 2 | 37 ms (nền 18) | = nền (0 B) | 0 | 812 | Thu hồi hết, 0 lạc |
| 1000/1000 lần 1 | 1551 ms | 2,5 MB | 2544 (Instantiate) | 4530 | Thu hồi hết, 0 lạc |
| 1000/1000 lần 2 | 257 ms (~0,12 ms/lần) | = nền (0 B) | 0 | 5003 | Thu hồi hết, 0 lạc |

Kết luận: đường spawn không cấp phát; chi phí lớn khi vượt prewarm là **Instantiate**, và khi rất đông là **render**
(mỗi impact 5–7 Particle System). Nếu trận thật có thể vượt 64 impact cùng lúc thì tăng `effectShells` hoặc bớt lớp hạt.

## 6. Chưa làm / đã bỏ

| Việc | Trạng thái |
|---|---|
| VFX khi lưỡi OrbitSaw chạm quái | Chưa làm (tùy chọn): thêm `EffectType` vào `OrbitSawData`, gọi `CombatVfx.Spawn` trong `TryHitTarget` |
| Bỏ vỏ `EffectView`, impact tự là object trong pool | **Bỏ**: giữ vỏ để linh hoạt (vỏ chung + visual theo loại); thay bằng "thuê rồi phát" + `Advance` trả bool |
| Gộp vỏ `ProjectileView` với visual đạn | **Bỏ**: mỗi loại một pool, prefab nào cũng phải gắn `ProjectileView`; thay bằng "thuê rồi phóng" |
| Đo Profiler (draw call, GC, prewarm) trên Android | Chưa làm |
| Converter Editor, Check VFX Prefabs | **Bỏ**: số prefab ít, chỉnh tay đơn giản hơn |
| Ngân sách impact mỗi frame, `VfxQuality` | **Bỏ** cho tới khi Profiler cho thấy cần |
| `Spawn<TState>` để bỏ closure | **Bỏ**: không cần nữa, "thuê rồi phóng/phát" đã bỏ closure mà không đổi contract Pool |
| `AreaVfxScaler` | **Bỏ**: `SkillAreaVisual` đã đặt `localScale = bán kính` |
