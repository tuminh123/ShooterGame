# Thiết kế hệ thống skill (từ 30 skill → kiến trúc)

Cập nhật: 2026-11-28. Đề xuất, **chưa code**. Danh sách skill và số liệu từng cấp: [Skill-Design.md](Skill-Design.md).

Mục tiêu: 30 skill (và skill nhân vật/boss sau này) chạy trên **ít họ hành vi nhất có thể**; phần khác nhau nằm ở **data + strategy nhỏ chọn trong Inspector**, đúng kiểu `IShooter` / `IProjectileMove` đang có. Thứ tự ưu tiên vẫn là Simple → DRY → SOLID.

## 1. Hiện trạng (đọc từ code)

| Phần | Đang có | Thiếu cho 30 skill |
|---|---|---|
| `ISkill` / `Skill` + `SkillBehaviourType` + `SkillFactory` (switch) | Khung hành vi, pool, cooldown, pause | Không sửa khung; chỉ thêm loại |
| `SkillLevelData` | Mỗi cấp chứa trọn bộ hành vi của cấp đó | Tên/icon riêng cho cấp tiến hóa |
| `ShootingSkill` + `IShooter` (thẳng/quạt/vòng/xoắn) + `IProjectileMove` (thẳng/đuổi) | Bắn theo mục tiêu gần nhất | % sát thương, xuyên, nổ/tách khi trúng, rơi tại điểm, nảy, boomerang, bắn theo hướng nhìn |
| `ProjectileView` | Trúng 1 quái là thu hồi | Nhớ quái đã trúng (xuyên), hành động khi trúng/hết giờ |
| `OrbitSaw`, `FrostAura` (`SlowAura`) | Lưỡi xoay; vùng làm chậm | % sát thương, nhịp nổ, đánh N quái ngẫu nhiên, hồi máu |
| `EffectSystem`: Slow, Burn, Poison | | Choáng, Đóng băng, Chảy máu, Đẩy lùi |
| `CharacterSearch`: Nearest, AllInCircle, FirstOnPath | | Ngẫu nhiên trong tầm, chỗ đông quái nhất, hình quạt, đoạn thẳng (tia) |
| `CharacterRuntimeData.CreateDamage(stats, team, multiplier)` | **Đã có hệ số** nhưng skill chưa dùng | Truyền % sát thương từ data |
| Hướng nhìn của nhân vật | Chưa có API | Hướng di chuyển cuối cùng (dao, tia, chém) |

## 2. Bốn khối dùng chung (làm trước, mọi họ đều dùng)

### 2.1 Gói đòn đánh `HitPayload` + `CombatHits.Apply`

Hiện mỗi nơi (đạn, cưa, aura) tự gây sát thương + áp effect + VFX. Gom về **một chỗ**:

```
HitPayload (struct, chốt lúc ra đòn):
  DamageInfo damage        // đã nhân % và tính chí mạng
  IGameplayEffect[] effects
  float effectHit
  EffectType impactVfx
  HitFlags flags           // BonusVsBoss, ... (mở rộng sau)

CombatHits.Apply(target handle, in HitPayload, Vector2 hitPoint) → DamageResult
```

Lý do: 22 skill tấn công + pet đều "đánh trúng" theo cùng một cách; sửa luật (thưởng chí mạng, kết liễu của Executioner, sát thương lên boss) chỉ sửa một hàm.

### 2.2 % sát thương trên mọi data

Mọi `XxxData` tấn công có `damagePercent` (100 = 1× Damage). Đổi `CreateDamage(stats, team)` → `CreateDamage(stats, team, damagePercent / 100f)`. Không đổi luật chí mạng hiện tại.

### 2.3 Chọn mục tiêu `ITargetSelector` (strategy, `[SerializeReference]`)

| Selector | Dùng cho |
|---|---|
| `NearestTarget` (có sẵn trong Shooting) | Đạn, chain, pet |
| `RandomInRange` | Static Field, Thunder Strike, Toxic Flask |
| `DensestCluster` | Meteor (rơi vào chỗ đông quái) |
| `FacingDirection` | Dao, tia, chém |

Lý do: Shooting, Strike, Zone, Chain, Pet đều cần "đánh vào đâu"; một họ strategy dùng chung thay vì mỗi hành vi tự viết.

### 2.4 Điểm phát `SkillOrigin`

Hành vi hiện đọc thẳng `Owner.Owner.HitPosition`. Thay bằng `SkillOrigin` (vị trí + hướng nhìn). Mặc định = nhân vật; **pet/drone/trụ** truyền origin của chính nó → pet dùng lại nguyên `ShootingData`/chain để bắn, không viết lại logic bắn.

Cần thêm: hướng nhìn (hướng di chuyển cuối) của nhân vật, đọc từ MovementSystem.

## 3. Họ hành vi (SkillBehaviourType)

Giữ 7 loại cũ (số đã lưu trong asset, chỉ thêm ở cuối) và **thêm 6 loại**:

```
NormalAttack, Shooting, OrbitSaw, SlowAura, Regeneration, EmergencyShield, StatBuff,   // có sẵn (0–6)
Strike, Zone, Chain, Beam, Slash, Summon                                                // mới  (7–12)
```

### 3.1 Shooting (mở rộng) — họ lớn nhất

Đạn = **cách bắn** + **quỹ đạo** + **xuyên** + **hành động khi trúng / khi hết giờ**.

```
ShootingData (thêm):
  float damagePercent
  ITargetSelector target          // Nearest (mặc định) / FacingDirection / RandomInRange
  int   pierce                    // 0 = trúng là mất; -1 = xuyên vô hạn
  IProjectileImpact[] onHit       // Explode, Split, SpawnZone
  IProjectileImpact[] onExpire    // SpawnZone (bình độc rơi xuống), Explode
IShooter (thêm):  ForwardBurst (dao bắn nối đuôi)
IProjectileMove (thêm): Boomerang, BounceInCamera, LobToPoint (bay tới điểm rồi hết giờ)
```

- `IProjectileMove.GetDirection` cần thêm ngữ cảnh (tuổi đạn, vị trí chủ) cho Boomerang → đổi tham số thành struct `ProjectileMoveContext`.
- `ProjectileView` thêm danh sách nhỏ cố định "quái đã trúng" (cho xuyên/nảy), không cấp phát.
- **`IProjectileImpact`** là strategy dùng chung với Strike (mục 3.2): `Explode(radius, %)`, `Split(count, %)`, `SpawnZone(ZoneData)`.

### 3.2 Strike (mới) — đánh xuống một điểm sau khi báo vùng

```
StrikeData: damagePercent, radius, telegraphTime, count, interval (mưa), ITargetSelector, effects, IProjectileImpact[] onLand
```
Meteor (DensestCluster + onLand Explode/SpawnZone), Thunder Strike (RandomInRange, telegraph ≈ 0).

### 3.3 Zone (mới) — vùng nằm trên đất

`ZoneSystem` (giống `ProjectileSystem`: pool, tick theo lô, do CombatManager tạo):

```
ZoneSpawnData: radius, duration, tickInterval, HitPayload, followOwner, TriggerMode (Tick | Trap(charges)), onTrigger impacts
```
- Skill Zone dùng `IZonePlacement`: `AroundOwnerRandom` (bẫy gai), `AtTarget` (Toxic Flask nếu không ném).
- Đạn/Strike cũng sinh zone qua impact `SpawnZone` → vũng lửa của Inferno Orb, Armageddon, mây độc Venom Burst.

### 3.4 Aura (mở rộng `SlowAura` = FrostAura)

Không đổi tên class data (asset lưu tên class). Thêm field:
```
damagePercent, tickInterval, targetMode (All | RandomN), healPercentPerSecond,
IAuraPulse pulse  // mỗi X giây: Freeze / Knockback / Burst
```
Frost Aura, Static Field (RandomN), Holy Aura (heal), Absolute Zero (pulse Freeze), Sanctuary (pulse Knockback).

### 3.5 Orbit (mở rộng `OrbitSaw`)

Thêm `damagePercent`, `effects`, `radiusPulse` (co giãn bán kính, Buzzsaw Storm). Flame Ring = data khác của cùng họ. Sun Wheel = nhiều lưỡi + visual vòng liền.

### 3.6 Chain (mới) — đánh tức thì, nảy giữa quái

```
ChainData: damagePercent, jumps, jumpRange, falloffPercent, effects
```
Visual: tia sét vẽ bằng LineRenderer từ pool. Pet Tesla Tower dùng lại ChainData.

### 3.7 Beam (mới) — tia

```
BeamData: length, width, duration, tickInterval, damagePercent, rotateDegreesPerSecond (0 = theo hướng nhìn)
```
Mỗi tick quét **đoạn thẳng** (thêm `FindAllOnSegment` vào CharacterSearch/Spatial).

### 3.8 Slash (mới) — chém hình quạt

```
SlashData: range, arcDegrees, hits (trước/sau), damagePercent, effects, ShootingData waveProjectile (tùy chọn, Crescent Wave)
```
Quét **hình quạt** (thêm `FindAllInSector`). Sóng kiếm = gọi lại Shooting.

### 3.9 Summon (mới) — pet/drone/trụ

`SummonSystem` quản lý pet (không phải CharacterView, **không có HP**, không bị quái nhắm):
```
SummonData: visualPrefab, count, lifetime (0 = vĩnh viễn), IPetMovement (FollowOwner | LungeAtTarget | Stationary),
            SkillBehaviourData attack   // ShootingData / ChainData / "Bite" (đánh cận chiến)
```
Pet chạy hành vi tấn công với `SkillOrigin` = vị trí pet (mục 2.4). Battle Wolf (Lunge + Bite), Gun Drone (Follow + Shooting), Sentry Turret (Stationary + Shooting/Chain).

### 3.10 Hỗ trợ (StatBuff, Regeneration, EmergencyShield + mới)

- Might/Vitality/Toughness/Keen Eye/Executioner: `StatBuff` theo cấp (có sẵn).
- Thưởng mốc Lv5 cần luật trong `CombatHits` (mục 2.1): `BonusVsBoss`, `ExecuteBelowPercent`, `CritStun` — đọc từ "cờ chiến đấu" của chủ.
- Magnet Stone: thêm role vào `Loot` (bán kính hút) — giao tiếp qua contract, không gọi thẳng.

### 3.11 Effect mới (EffectSystem)

`StunEffect` (ngừng di chuyển + ngừng đánh), `FreezeEffect` (= Stun + màu băng), `BleedEffect` (DoT như Burn/Poison), `KnockbackEffect` (đẩy qua MovementSystem). Thêm vào `CharacterFeedbackKind` để có màu riêng.

## 4. Bản đồ 30 skill → họ

| # | Skill | Họ | Strategy / field chính |
|---|---|---|---|
| 1 | Fire Bolt → Inferno Orb | Shooting | onHit Explode; Lv5 + SpawnZone |
| 2 | Ice Bolt → Glacial Spear | Shooting | Fan, pierce; Lv5 pierce -1 + Freeze |
| 3 | Poison Bolt → Venom Burst | Shooting | onHit Split; Lv5 + SpawnZone |
| 4 | Holy Bolt → Divine Volley | Shooting | Homing, BonusVsBoss; Lv5 pierce |
| 5 | Throwing Knives | Shooting | FacingDirection + ForwardBurst |
| 6 | Bouncing Ball | Shooting | BounceInCamera, pierce -1 |
| 7 | Boomerang | Shooting | Boomerang move; Lv5 onExpire Split |
| 8 | Orbit Saw | Orbit | radiusPulse (Lv5) |
| 9 | Flame Ring | Orbit | effects Burn |
| 10 | Frost Aura | Aura | pulse Freeze (Lv5) |
| 11 | Static Field | Aura | RandomN; Lv5 All + Stun |
| 12 | Holy Aura | Aura | heal; pulse Knockback (Lv5) |
| 13 | Toxic Flask | Shooting | LobToPoint + onExpire SpawnZone |
| 14 | Spike Trap | Zone | AroundOwnerRandom, Trap |
| 15 | Meteor | Strike | DensestCluster, telegraph; Lv5 count 6 + SpawnZone |
| 16 | Thunder Strike | Strike | RandomInRange, Stun |
| 17 | Chain Lightning | Chain | jumps, falloff |
| 18 | Radiant Lance | Beam | rotate (Lv5) |
| 19 | Blade Slash | Slash | Lv5 waveProjectile |
| 20 | Battle Wolf | Summon | Lunge + Bite, Bleed |
| 21 | Gun Drone | Summon | Follow + Shooting |
| 22 | Sentry Turret | Summon | Stationary + Shooting; Lv5 Chain |
| 23–27 | Might, Vitality, Toughness, Keen Eye, Executioner | StatBuff | + luật mốc trong CombatHits |
| 28 | Regeneration | Regeneration | |
| 29 | Emergency Shield | EmergencyShield | |
| 30 | Magnet Stone | StatBuff + Loot role | |

**Kết quả:** 30 skill = 7 họ cũ (mở rộng) + 6 họ mới; khoảng 15 strategy nhỏ. Skill nhân vật/boss sau này chủ yếu là data trên các họ này.

## 5. Tiến hóa Lv5

`SkillLevelData` đã cho mỗi cấp một bộ hành vi riêng → Lv5 đổi hành vi/thông số thoải mái. Chỉ thêm:
- `evolvedName`, `evolvedIcon` (tùy chọn) trên `SkillLevelData`; UI chọn skill/HUD đọc tên/icon theo cấp hiện tại.
- Visual tiến hóa = `BulletType`/prefab khác trong data Lv5.

## 6. Hiệu năng (đối chiếu Performance-Rules)

- Zone, Strike, Summon, Chain-visual: hệ thống riêng, pool, **CombatManager tick** (không tự đăng ký TickableManager).
- Quét va chạm theo lô qua SpatialSystem như đạn; không `GetComponent`/LINQ/cấp phát trong hot path.
- Danh sách "quái đã trúng" của đạn: mảng cố định, reset khi về pool.
- Đo Batches/Draw calls sau mỗi họ mới.

## 7. Thứ tự làm

| Giai đoạn | Nội dung | Skill chạy được sau giai đoạn |
|---|---|---|
| G1 | Khối chung: `HitPayload`/`CombatHits`, % sát thương, `ITargetSelector`, `SkillOrigin`, hướng nhìn, tên/icon tiến hóa | 3 bolt cũ dùng % sát thương |
| G2 | Shooting mở rộng: xuyên, impacts (Explode/Split), moves (Boomerang/Bounce/Lob), ForwardBurst | 1–7 (trừ phần zone) |
| G3 | ZoneSystem + họ Zone + impact SpawnZone | 13, 14, vũng lửa/mây độc của 1, 3 |
| G4 | Strike + visual báo vùng | 15, 16 |
| G5 | Aura/Orbit mở rộng + effect Stun/Freeze/Knockback | 8–12 |
| G6 | Chain, Beam, Slash + truy vấn đoạn thẳng/hình quạt | 17–19 |
| G7 | SummonSystem + Bleed | 20–22 |
| G8 | Hỗ trợ: Might…Executioner, luật mốc, Magnet | 23–30 |

Mỗi giai đoạn: game biên dịch được, test trong trận, báo trước khi sang giai đoạn sau.

## 8. Bảng tổng hợp hành vi

### 8.1 Họ hành vi (SkillBehaviourType)

| # | Họ | Trạng thái | Làm gì | Thông số chính | Skill trong trận | Skill nhân vật |
|---|---|---|---|---|---|---|
| 0 | NormalAttack | Có sẵn | Đòn thường cận chiến của quái | cooldown, range | — | Quái thường, tinh anh |
| 1 | Shooting | Có sẵn → **mở rộng** | Bắn đạn | %dmg, cách bắn, quỹ đạo, xuyên, khi trúng/hết giờ, bulletType | 1–7, 13 | Piercing Arrow, Arcane Orb, Spear Volley; đòn thường nhân vật; tấn công của pet |
| 2 | OrbitSaw | Có sẵn → **mở rộng** | Vật thể xoay quanh người | %dmg, số lưỡi, bán kính, co giãn, effects | 8, 9 | — |
| 3 | SlowAura | Có sẵn → **mở rộng** | Hào quang quanh người | %dmg, bán kính, nhịp, All/RandomN, hồi máu, pulse | 10, 11, 12 | — |
| 4 | Regeneration | Có sẵn | Hồi máu theo thời gian | lượng, chu kỳ | 28 | — |
| 5 | EmergencyShield | Có sẵn | Khiên khi máu thấp | ngưỡng %, khiên, CD, bất tử | 29 | Aegis |
| 6 | StatBuff | Có sẵn | Cộng chỉ số khi sở hữu | StatType, giá trị | 23–27, 30 | Glacial Armor (DEF), Aura of Valor |
| 7 | Strike | **Mới** | Đánh xuống điểm sau khi báo vùng | %dmg, vùng, thời gian báo, số lần, khoảng cách, pattern (điểm / nối tiếp theo hướng), chọn mục tiêu, khi rơi | 15, 16 | Judgment Hammer, Glacier Spike, Flame Pillar, Heaven's Judgment |
| 8 | Zone | **Mới** | Vùng nằm trên đất | vùng, thời gian, nhịp, %dmg, effects, Tick/Trap, đặt ở đâu, đi theo chủ | 14 (+ vũng do đạn/strike tạo) | Acid Rain, vũng của Divine Hammer / Frozen Domain / Inferno Pillars |
| 9 | Chain | **Mới** | Đánh tức thì rồi nảy | %dmg, số lần nảy, tầm nảy, giảm mỗi lần nảy | 17 (+ Tesla Tower) | — |
| 10 | Beam | **Mới** | Tia quét đoạn thẳng | dài, rộng, thời gian, nhịp, xoay | 18 | Tia của Archangel |
| 11 | Slash | **Mới** | Đánh hình quạt / 360° | tầm, góc, số nhát, %dmg, đẩy/kéo, hút máu, sóng kiếm | 19 | Shield Bash, Reaping Whirl |
| 12 | Summon | **Mới** | Pet/drone/trụ không HP | prefab, số lượng, thời gian, kiểu di chuyển, đòn tấn công (data của họ khác) | 20, 21, 22 | Archangel |

### 8.2 Strategy nhỏ (chọn trong Inspector bằng `[SerializeReference]`)

| Nhóm | Strategy | Trạng thái | Dùng ở |
|---|---|---|---|
| Cách bắn `IShooter` | Straight, Fan, Circle, Spiral | Có sẵn | Shooting |
| | ForwardBurst (bắn nối đuôi) | Mới | Throwing Knives |
| Quỹ đạo `IProjectileMove` | Straight, Homing | Có sẵn | Shooting |
| | Boomerang, BounceInCamera, LobToPoint | Mới | Boomerang, Returning Spears, Bouncing Ball, Toxic Flask |
| Chọn mục tiêu `ITargetSelector` | Nearest | Có (trong Shooting) → tách ra | Shooting, Chain, pet |
| | RandomInRange, DensestCluster, FacingDirection | Mới | Strike, Zone, Aura, dao/tia/chém |
| Khi trúng / hết giờ `IProjectileImpact` | Explode, Split, SpawnZone | Mới | Shooting, Strike |
| Đặt vùng `IZonePlacement` | AroundOwnerRandom, AtTarget, FollowOwner | Mới | Zone |
| Nhịp hào quang `IAuraPulse` | Freeze, Knockback, Burst | Mới | Aura Lv5 |
| Di chuyển pet `IPetMovement` | FollowOwner, LungeAtTarget, Stationary | Mới | Summon |

### 8.3 Hiệu ứng (EffectSystem)

| Hiệu ứng | Trạng thái | Dùng ở |
|---|---|---|
| Slow | Có sẵn | Ice, Frost Aura, Glacier Spike, Glacial Armor |
| Burn | Có sẵn | Fire Bolt, Flame Ring, Meteor, Flame Pillar |
| Poison | Có sẵn | Poison Bolt, Toxic Flask |
| Stun | Mới | Thunder Strike, Static Field, Chain Lv5, Judgment Hammer, Shield Bash |
| Freeze | Mới | Glacial Spear, Absolute Zero, Glacier Spike |
| Bleed | Mới | Alpha Pack |
| Knockback / Pull | Mới | Sanctuary, Wrecking Ball, Shield Bash, Death's Embrace |
| Corrode (−DEF) | Mới | Corrosive Storm |

### 8.4 Luật trong `CombatHits` (cờ của đòn / của chủ)

| Luật | Dùng ở |
|---|---|
| Thưởng sát thương lên boss/tinh anh | Holy Bolt, Might Lv5 |
| Kết liễu dưới X% HP (trừ boss) | Executioner Lv5 |
| Chí mạng gây choáng | Keen Eye Lv5 |
| Hút máu theo % sát thương | Reaping Whirl, Divine Volley (hồi khi trúng) |
| Thêm sát thương lên quái đang cháy | Kindling |
| Cộng dồn khi giết quái | Soul Harvest |
