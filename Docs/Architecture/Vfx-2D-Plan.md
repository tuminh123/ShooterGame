# Kế hoạch hệ thống VFX 2D

> Trạng thái: **kế hoạch**, chưa triển khai từ Task 1 trở đi. Đối chiếu với code trước khi coi tài liệu này là mô tả hiện trạng.

Mục tiêu: dùng prefab của hai pack **Hovl Studio – Toon Projectiles 2** và **AllIn1VfxToolkit** cho đạn, impact và vùng tác động trong game 2D. Không sửa asset gốc, chạy tốt trên Android, và theo rule của dự án: Combat sở hữu VFX, chỉ tạo interface khi có từ 2 hành vi thay thế nhau trở lên, enum game nằm trong `GameEnums`.

---

## 1. Bối cảnh

| Phần | Hiện tại |
|---|---|
| Đạn (`CombatAssetsSO.bullets`) | 4 loại Holy, Fire, Ice, Poison. Mỗi viên là **1 SpriteRenderer**, scale 0,03, pool 256 viên mỗi loại |
| Impact (`CombatAssetsSO.effects`) | 4 sprite + `ObjectEffectVisual`. Đã có sẵn `ParticleEffectVisual` cho impact dạng hạt |
| Vùng tác động (FrostAura, OrbitSaw) | `SkillAreaVisual`: mesh tròn sinh bằng code |
| Môi trường | Camera 2D orthographic size 1,8 (khoảng 533 px/đơn vị), quái cao khoảng 0,17 đơn vị, Built-in pipeline, Linear, build Android, **không có Bloom** |
| `ProjectileView` | Lấy visual con từ pool, xoay quanh Z (+X là hướng bay). Không gọi Play/Clear trên visual |

### Vấn đề khi dùng thẳng prefab của hai pack

| Vấn đề | Ảnh hưởng |
|---|---|
| Thiết kế 3D, đạn bay theo trục Z | Phải xoay (0, 90, 0) cho trục X của game |
| Vùng/khiên AllIn1 nằm trên mặt đất XZ | Nhìn từ camera 2D bị dẹt thành vệt; phải xoay (-90, 0, 0) |
| Quá to (impact rộng 2–3 đơn vị) | Phải scale khoảng 0,05 |
| `ProjectileMover`, `AutoDestroy`, Rigidbody, Collider, Light | Tự bay, tự hủy và tốn GPU, xung đột với pool và `ProjectileSystem` |
| Vệt đuôi tính theo tọa độ thế giới | Tái dùng từ pool sẽ kéo vệt từ chỗ cũ; phải Clear mỗi lần lấy ra |
| Soft Particles (`SOFTPART_ON`) | Cần depth texture, camera 2D không có nên hiển thị sai |
| Distortion / GrabPass | Rất tốn trên Android |
| Impact sống 1,4–3,6 giây | Pool impact phải lớn hơn, hoặc rút ngắn thời gian |
| Particle không tự gộp draw call như sprite | Draw call tăng; mỗi đạn chỉ nên có 1–2 Particle System |

---

## 2. Đã xong

### 2.1 Chép template về dự án

- **86 prefab** cùng mọi material, texture, model, mesh và animation cần thiết được chép vào `Assets/_Project/Vfx/Templates/`:
  - Hovl: đủ 45 prefab.
  - AllIn1: 41 prefab đáng dùng.
  - Tổng cộng 595 file, 72,5 MB.
- Mọi tham chiếu trong bản sao đã trỏ sang bản sao.
- Shader Hovl được chép và **đổi tên** thành `ShooterGame/Vfx/Hovl/...` để không trùng tên với bản gốc.
- Render so sánh gốc và bản sao: sai lệch điểm ảnh bằng 0.
- Pack gốc giữ nguyên làm template.

```text
Assets/_Project/Vfx/Templates/
├── Hovl/    Prefabs · Materials · Textures · Models · Shaders
└── AllIn1/  Prefabs · Materials · Textures · Meshes · Animation
```

**Dùng chung có chủ ý (không chép):**

| Dùng chung | Lý do |
|---|---|
| Script C# của hai pack | Chép `.cs` sẽ tạo hai class cùng tên và gây lỗi biên dịch |
| Shader AllIn1 | Công cụ chỉnh material của AllIn1 tìm shader theo tên qua `Resources.Load`; có bản trùng thì công cụ hoạt động sai |

### 2.2 Bóc script, vật lý và đèn khỏi bản sao

| Đã bóc | Số lượng |
|---|---|
| `ProjectileMover`, `AutoDestroyPS` (Hovl) | 15 + 10 |
| `AllIn1VfxAutoDestroy`, `AllIn1VfxFadeLight` | 10 + 8 |
| `AllIn1DoShake`, `AllIn1LookAt`, `AllIn1VfxComponent`, `All1VfxRandomTimeSeed`, `AllIn1VfxScrollShaderProperty` | 2 + 2 + 5 + 1 + 1 |
| Rigidbody / Collider / Light | 15 / 15 / 25 |

**Thay script chuyển động bằng script của dự án** (`Scripts/Combat/Vfx/`):

| Script mới | Thay cho | Đặc điểm |
|---|---|---|
| `VfxSpin` | `AllIn1AutoRotate` (11 component / 6 prefab) | Xoay quanh trục local; dùng `Time.deltaTime` nên dừng khi game pause; khi tắt trả về góc gốc |
| `VfxBob` | `AllIn1VfxBounceAnimation` (3 prefab) | Nhún theo sin quanh vị trí **local**, nên đi theo object cha; khi tắt trả về vị trí gốc |

**Kiểm tra sau khi bóc:**
- Missing Script: 0.
- Script lạ còn lại: 0.
- Rigidbody / Collider / Light còn lại: 0.
- Render gốc so với bản sao: không khác.
- Bỏ đèn không mất hình ảnh, vì shader của cả hai pack đều unlit.

**Phụ thuộc còn lại:**
- Bản sao Hovl độc lập hoàn toàn.
- Bản sao AllIn1 chỉ còn dùng **shader AllIn1** và 6 texture mặc định của shader đó.
- **Không được xóa thư mục `AllIn1VfxToolkit`.**

---

## 3. Đánh giá prefab

### Hovl – Toon Projectiles 2

| Nên dùng | Không nên |
|---|---|
| **P1** vệt lửa vàng-cam · **P9** phi tiêu băng · **P10** cầu lửa kèm khói · **P11** cầu sáng vàng · **P12** phi tiêu xanh lá · **P13** mũi tên tím · **P2** cầu sét tím | **P3 / H3** dùng Distortion · **P6** tảng đá 3D · **P7** quá to · **P4, P5, P15** quá nhỏ, chủ yếu là vệt |
| **H1** nổ cam · **H4** sao đỏ · **H5** mảnh xanh lá · **H8** xoáy tím · **H11** sao vàng · **H12** xanh lá + đỏ · **H14** băng · **H2** tia sét | **H10** khói xám đục trên nền cát · **H13, H6** mờ, khó thấy |

### AllIn1VfxToolkit

| Prefab | Dùng cho |
|---|---|
| Ice Projectile | Đạn băng hình sao chổi, có bông tuyết |
| Plasma Ball / Dark Magic Orb | Đạn năng lượng tím |
| Magic Spiral, Green Portal, Blue Pixel Portal | Aura tròn; hạt quay mặt về camera nên đúng trong 2D |
| Pixel Fire, Blue Fire | Lửa/độc bám trên quái |

**Không dùng:** Toon Explosion, Explosion Bomb, Blue Area + Distort Sphere (GrabPass).

### Cách ghép đã chốt

| Loại | Đạn | Impact |
|---|---|---|
| Fire | Hovl Projectile 1 | Hovl Hit 1 |
| Ice | AllIn1 Ice Projectile | Hovl Hit 14 (bỏ lớp distortion) |
| Poison | Hovl Projectile 12 | Hovl Hit 5 |
| Holy | Hovl Projectile 11 | Hovl Hit 11 |
| Lưỡi OrbitSaw | Hovl Projectile 9 | |
| Vùng FrostAura | AllIn1 Magic Spiral hoặc Blue Pixel Portal | |

---

## 4. Kiến trúc

```text
① EDITOR – chuyển đổi một lần           ② RUNTIME                                     ③ KIỂM SOÁT
Templates/ (đã bóc sạch)                 CombatManager (sở hữu, như hiện tại)           VfxBudget trong CombatVfx
   │  Vfx2DConverter                      ├── ProjectileSystem → ParticleProjectileVisual   giới hạn impact/frame + tổng
   ▼  theo VfxImportProfileSO             ├── CombatVfx → ParticleEffectVisual (có sẵn)     bỏ qua ngoài màn hình
Prefabs/Vfx/ (dùng trong game)            └── SkillAreaVisual → AreaVfxScaler               Check VFX Prefabs (Edit Mode)
```

**Lý do không thêm interface mới:** sprite và particle đều chỉ là `GameObject` con trong pool, `ProjectileView` không cần phân biệt. Theo rule "Simple trước", component tự lo phần Play/Clear của mình là đủ.

---

## 5. Các task tiếp theo

Mỗi task phải biên dịch được, có kiểm tra bằng MCP, và báo lại trước khi sang task sau.

### Task 1. Converter 2D (Editor)

**Mục tiêu:** từ `Templates/`, tạo ra prefab dùng được trong game tại `Assets/_Project/Prefabs/Vfx/{Bullets,Impacts,Areas}/`.

| File | Nội dung |
|---|---|
| `Editor/Vfx/VfxImportProfileSO.cs` (mới) | Mục `{ prefab nguồn, kiểu chuyển, kích thước mong muốn, tên đầu ra }` + giới hạn chung |
| `Editor/Vfx/Vfx2DConverter.cs` (mới) | Menu **Tools/Shooter Game/VFX 2D Converter**: nút Convert + báo cáo |
| Enum `VfxConvertKind` (Editor) | `Projectile`, `Impact`, `GroundArea`, `Billboard` |
| `DataSO/Vfx/VfxImportProfile.asset` (mới) | Profile cho 4 cặp đạn/impact |
| `Editor/Verification/VfxVerification.cs` (mới) | Menu **Check VFX Prefabs** |

**Converter làm theo thứ tự:**
1. Tạo bản sao từ template (không sửa template).
2. **Xoay hướng:**
   - `Projectile`: (0, 90, 0).
   - `GroundArea`: (-90, 0, 0).
   - `Impact` / `Billboard`: giữ nguyên.
3. **Chuẩn kích thước:**
   - Mô phỏng hạt, đo khung bao thật, rồi scale về kích thước mong muốn.
   - Đặt `scalingMode = Hierarchy`.
4. **Sửa cho 2D:**
   - Thống nhất sorting layer/order: đạn và impact trên nhân vật, vùng dưới nhân vật.
   - Tắt shadow, light probe, reflection probe, motion vector.
5. **Bỏ phần nặng:**
   - Tắt `SOFTPART_ON`.
   - Xóa object con dùng shader Distortion/GrabPass.
6. **Cắt dư thừa:**
   - `maxParticles` đặt bằng lượng phát thật (tốc độ phát × thời gian sống + burst), có giới hạn trên.
   - Impact: tắt loop, `duration` ≤ 0,8 giây.
7. **Gắn component runtime:**
   - Impact: `ParticleEffectVisual`.
   - Đạn: `ParticleProjectileVisual`, làm ở Task 2.
8. **Lưu và báo cáo:** chạy lại thì ghi đè đúng file và **giữ GUID**.

**Báo cáo cho từng prefab:** số Particle System, tổng số hạt tối đa, shader, kích thước sau chuyển, cảnh báo.

**Check VFX Prefabs kiểm tra:**
- Không có Rigidbody, Collider, Light hay script ngoài danh sách cho phép (`ParticleEffectVisual`, `ParticleProjectileVisual`, `VfxSpin`, `VfxBob`, `AreaVfxScaler`).
- Impact không loop.
- Không dùng shader GrabPass/Distortion.
- Số hạt và kích thước nằm trong giới hạn.

**Kiểm tra task:**
- Convert hai lần không tạo file mới, GUID giữ nguyên.
- Check VFX Prefabs đạt.
- Render bằng camera size 1,8, đặt cạnh sprite đạn hiện tại để so kích thước.

**Cần chốt:** kích thước, đề xuất **đạn khoảng 0,10 đơn vị** (khoảng 60% chiều cao quái) và **impact khoảng 0,30 đơn vị**.

### Task 2. Đạn và impact particle trong game

| File | Thay đổi |
|---|---|
| `Scripts/Combat/Vfx/ParticleProjectileVisual.cs` (mới) | `OnEnable`: `Clear` + `Play` hạt và vệt. `OnDisable`: `Stop` + `Clear`. Cache `ParticleSystem[]`/`TrailRenderer[]` trong `Awake`, không cấp phát lúc chạy |
| `Vfx2DConverter` | Gắn component trên vào đầu ra loại `Projectile` |
| `CombatAssets.asset` | Đổi `visualPrefab` của 4 đạn và 4 impact; **giữ nguyên `hitRadius`/`hitOffset`** |
| `PoolPrewarmSettings.asset` | Đạn từ 256 xuống **khoảng 64** mỗi loại; impact khoảng 24 mỗi loại |

- **Không sửa** `ProjectileView` hay `ProjectileSystem`.
- **Đường lui:** giữ prefab sprite cũ; muốn quay lại chỉ cần đổi lại `visualPrefab`.

**Kiểm tra:**
- Check Object Pools, Check Service Contracts, Check VFX Prefabs.
- Chơi bằng MCP: vệt không bị kéo dài khi tái dùng, impact tự trả về pool, Console sạch.
- **Đo Profiler, so trước/sau:** batches, SetPass, ms CPU/GPU, GC mỗi frame (mục tiêu 0 B), thời gian prewarm.

**Rủi ro:** draw call tăng.
- Xử lý trước: cho đạn cùng loại dùng chung material và sorting order.
- Nếu chưa đủ: chọn đạn có ít Particle System hơn.

### Task 3. Ngân sách VFX (chỉ khi Profiler thấy cần)

| File | Thay đổi |
|---|---|
| `Scripts/Combat/Vfx/CombatVfx.cs` | Tối đa **6 impact mới mỗi frame**, **40 impact đang chạy**. Vượt thì bỏ impact mới; ưu tiên giữ impact chí mạng và impact trúng Player |
| `CombatVfx` | Bỏ qua impact ngoài màn hình, dùng camera planes của `ProjectileSystem` |
| Settings của CombatManager / `CombatAssetsSO` | Đưa các giới hạn vào data (**cần duyệt**) |

**Kiểm tra:**
- Bắn 100 impact trong một frame thì đúng 6 impact được tạo.
- Chơi trận đông quái, so FPS và draw call trước/sau.

`VfxQuality` chưa làm ở đây. Converter đã đánh dấu sẵn hạt "chi tiết" để sau này chỉ cần bật/tắt.

### Task 4. Vùng tác động (FrostAura, OrbitSaw)

| File | Thay đổi |
|---|---|
| `Scripts/Combat/Vfx/AreaVfxScaler.cs` (mới) | `localScale = bán kính skill / bán kính gốc của VFX`, cập nhật khi bán kính đổi |
| `FrostAuraVisual` / `OrbitSawVisual` | Chỗ gắn prefab VFX tùy chọn; không có thì vẫn dùng mesh cũ |
| Profile converter | FrostAura: Magic Spiral hoặc Blue Pixel Portal (`Billboard`), tô xanh băng. Lưỡi OrbitSaw: Hovl Projectile 9 |

**Kiểm tra:**
- Nâng cấp skill để bán kính tăng: VFX phải khớp vùng gây sát thương.
- Pause game: vùng dừng chuyển động.

**Cần chốt:**
- FrostAura dùng Magic Spiral hay Blue Pixel Portal? Sẽ render cả hai để so.
- Có giữ mesh cũ dưới VFX để thấy rõ ranh giới không?

### Task 5. Mở rộng (tùy chọn)

| Việc | Cách làm |
|---|---|
| Chớp nòng súng | Thêm giá trị `EffectType` ở cuối enum; `ShootingSkill` gọi `CombatVfx.Spawn` tại nòng; dùng Hovl Flash cùng số với đạn |
| Lửa/độc trên quái | `EffectSystem` gắn/gỡ VFX lặp (AllIn1 Pixel Fire, hai màu) theo nhân vật khi hiệu ứng bắt đầu/kết thúc |
| `VfxQuality` trong Settings | Enum trong `GameEnums`, lưu trong save; Low tắt hạt "chi tiết" và giảm số hạt phát ra |

### Task 6. Tài liệu

- `Docs/Architecture/Vfx-2D.md`: mô tả hệ thống sau khi làm xong (luồng Templates → Converter → Prefabs/Vfx → CombatAssetsSO, cách thêm VFX mới, giới hạn hiệu năng, danh sách script được phép).
- CLAUDE.md và `Systems-Overview.md`: thêm vài dòng ở mục Combat.

---

## 6. Tóm tắt điểm cần chốt

| Task | Cần chốt |
|---|---|
| 1. Converter | Kích thước đạn 0,10 / impact 0,30 |
| 2. Đạn + impact | Pool prewarm 64 / 24 |
| 3. Ngân sách | Chỉ làm nếu Profiler thấy cần; có cho sửa `CombatVfx` và Settings của Combat không |
| 4. Vùng tác động | Magic Spiral hay Blue Pixel Portal; có giữ mesh cũ không |
| 5. Mở rộng | Làm phần nào |

## 7. Lưu ý chung

- Không sửa prefab gốc của Hovl/AllIn1. Mọi chỉnh sửa nằm trong `_Project`.
- Không xóa `Assets/AllIn1VfxToolkit`: bản sao vẫn dùng shader của nó.
- Không có Bloom, nên một số hiệu ứng AllIn1 trông nhạt hơn ảnh demo.
- Mỗi đạn nên có 1–2 Particle System, và luôn đo Profiler sau khi thay.
