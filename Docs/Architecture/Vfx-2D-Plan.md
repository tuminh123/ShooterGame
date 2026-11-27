# VFX combat (đạn, impact, vùng, trạng thái)

> **Trạng thái:** ✅ Hiện hành · Kiểm tra với code và Play Mode: 2026-11-27.

Mọi VFX của game nằm trong **một thư mục tự đủ** `Assets/_Project/Vfx/` – không phụ thuộc pack bên thứ ba nào
(Hovl Studio và AllIn1VfxToolkit đã bị xóa):

```text
Assets/_Project/Vfx/
  Prefabs/Bullets/   HolyBullet, FireBullet, IceBullet, PoisonBullet   (sprite + vệt)
  Prefabs/Impacts/   HolyImpact, FireImpact, IceImpact, PoisonImpact   (particle)
  Prefabs/Status/    BurnStatus, PoisonStatus, SlowStatus              (particle bám trên nhân vật)
  Prefabs/Areas/     FrostAuraVfx                                      (bông tuyết của vùng băng)
  Materials/  Textures/  Models/  Shaders/                             (chỉ những gì các prefab trên dùng)
```

| Loại | Hình | Script gốc | Ai sở hữu / tick |
|---|---|---|---|
| Đạn (4 `BulletType`) | Sprite + vệt `TrailRenderer` (con `Trail`) | `ParticleProjectileVisual` | `ProjectileSystem` (thuê/trả qua `ProjectileView`) |
| Impact (4 `EffectType`) | Particle | `ParticleSystem` rỗng ở gốc + `ParticleEffectVisual` | `CombatVfx` (hỏi `Advance` mỗi LateTick) |
| Vùng băng | Mesh (`Prefabs/FrostAuraVisual`) + `FrostAuraVfx` con | `ParticleProjectileVisual` | `SkillAreaVisuals` |
| Trạng thái cháy/độc/chậm | Particle | `ParticleProjectileVisual` | `CharacterFeedback` (bám `HitPosition` ở LateTick) |

Gán vào data: `DataSO/Databases/CombatAssets.asset` (đạn, impact), `Prefabs/Character.prefab` → `CharacterView`
(Poison/Burn/Slow Vfx), `DataSO/Databases/PoolPrewarmSettings.asset` (`additionalPrefabs`: 3 VFX trạng thái × 32).
Không prefab VFX nào có script `Update` (luật T1/T6 trong [Performance-Rules](Performance-Rules.md)).

## Đạn: sprite + vệt

- Sprite là prefab đạn cũ (GUID cũ, nên `hitRadius`/`hitOffset` trong `CombatAssets` vẫn khớp).
- Con `Trail` (`TrailRenderer`): material `Trail75bcg` (Holy vàng, Fire cam) và `Trail54bcg` (Ice xanh nhạt, Poison xanh
  lá), màu qua `colorGradient` (alpha 1 → 0), `time` 0,15 s, `widthMultiplier` 0,09 (thon về 0,2), căn View, order 9
  (sprite 10), tắt bóng đổ/probe/motion vector. Hai material cho cả 4 loại nên gộp batch tốt.
- `ParticleProjectileVisual` ở gốc xóa vệt khi bật/tắt: đạn lấy lại từ pool không kéo vệt từ chỗ cũ (đã kiểm tra).
- Đo (Editor): tạo một bản ~0,05 ms, vài KB; bật/tắt vệt trên 200 viên không đổi CPU, mỗi viên thêm ~0,5 draw call.

## Impact, trạng thái, vùng: particle

- Đã dựng cho camera 2D: lớp căn View, tắt bóng đổ/probe, impact sống ≤ 1 s, không Distortion.
- Shader: bản copy của Hovl (`ShooterGame/Vfx/Hovl/Add_CenterGlow`, `Blend_CenterGlow`, `Blend_TwoSides`) trong
  `Vfx/Shaders`, và shader particle có sẵn của Unity cho `SnowFlakes` (`Legacy Shaders/Particles/Alpha Blended`, tint 0,8)
  và `IceFlares` (`Legacy Shaders/Particles/Additive`, tint 0,45) – trước đây dùng shader AllIn1; độ sáng chỉnh để khớp hình.
- Thứ tự vẽ theo material (order 3–19, material to vẽ dưới): cùng material thì vẽ liền nhau giữa các impact, gộp batch
  (4 loại trộn: SetPass thêm mỗi impact −39%, batches −54%).
- **Giới hạn** (`CombatAssetsSO`): `maxNewEffectsPerFrame` = 8, `maxActiveEffects` = 64; vượt thì `CombatVfx.Spawn` bỏ
  qua (chỉ là hình). Đợt 200 impact mỗi 0,4 s: CPU tb 30,4 → 7,6 ms, đỉnh 260 → 11 ms.
- Texture VFX trên Android ≤ 256 (flipbook 512): override nền tảng trong `.meta` của texture trong `Vfx/Textures`.

## Prewarm

`bulletVisualsPerType` = 128, `impactVisualsPerType` = 32, vỏ đạn 256, vỏ VFX 16 mỗi loại; VFX trạng thái 3 × 32.
Xem [Pool-Prewarm](Pool-Prewarm.md).

## Thêm hoặc đổi VFX

1. **Đạn:** sprite (+ con `Trail` như trên) + `ParticleProjectileVisual` ở gốc; trục +X là hướng bay; gán vào
   `CombatAssets.bullets`.
2. **Impact:** prefab particle một lần (không loop, không Play On Awake, sống ngắn), `ParticleSystem` ở gốc +
   `ParticleEffectVisual`; lớp căn View; không Distortion/GrabPass; gán vào `CombatAssets.effects`.
3. Đặt prefab và mọi material/texture/shader nó dùng trong `Assets/_Project/Vfx/`; không tham chiếu ra ngoài thư mục này.
4. Không thêm script có `Update`; chuyển động trang trí dùng module của Particle System.
5. Kiểm tra: **Report Missing Scripts**, **Check Object Pools**; chơi thử: không kéo vệt khi tái dùng, impact tự về pool.

## Đã bỏ (2026-11-27)

Pack **Hovl Studio** và **AllIn1VfxToolkit**, đạn particle 3D, thư viện 86 VFX dựng lại, tool chuyển đổi, `VfxSpin`/`VfxBob`
(vi phạm T1), `AnimationEffectVisual`/`ObjectEffectVisual`, prefab impact sprite cũ, các template không dùng.
`Assets/Plugins/Easy Save 3/Resources/ES3/ES3Defaults.asset` vẫn ghi tên assembly của AllIn1 (Easy Save tự thêm khi pack
được nhập; plugin không sửa tay – tên assembly không còn tồn tại thì bị bỏ qua).
