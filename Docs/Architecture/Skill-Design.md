# Thiết kế skill (đề xuất, chưa code)

Cập nhật: 2026-11-28. Tài liệu thiết kế; phần lớn skill dưới đây **chưa** có trong code (skill đã có ghi *(có sẵn)*, khi làm sẽ chỉnh lại theo bảng này).

## 1. Quy tắc đã chốt

| Loại | Số cấp | Ghi chú |
|---|---|---|
| Skill trong trận (chọn khi lên cấp) | 5 | Skill tấn công: **Lv5 = tiến hóa**, tự động, **không cần xúc tác** |
| Skill chủ động của nhân vật | 5 | Lv5 tiến hóa; riêng từng nhân vật |
| Skill bị động của nhân vật | 1 | |
| Đòn thường của quái / quái tinh anh | 1 | Tinh anh dùng chung đòn với quái thường |
| Boss | ≥ 3 skill | Thiết kế riêng từng boss |

Số skill theo độ hiếm nhân vật: Common/Uncommon 1 skill; Rare/Epic 1 chủ động + 1 bị động; Legendary 1 chủ động + 2 bị động.

- **Không bó buộc theo nguyên tố.** Nguyên tố (Fire, Ice, Lightning, Poison, Light) chỉ là nhãn quyết định hiệu ứng phụ.
- **Chỉ số tăng chủ yếu: Damage, HP, DEF, Crit.** Move Speed gần như không tăng (dễ phá game); không có skill trong trận nào tăng tốc chạy.
- **Phản ứng nguyên tố: đang cân nhắc** (Cháy + Đóng băng → Vỡ x2; Sét trúng quái độc → độc lây; Ánh sáng trúng quái choáng → luôn chí mạng).

### Cách đọc số

- **Sát thương ghi theo % Damage của nhân vật** (ATK). Ví dụ 80% = 0.8 × Damage hiện tại (đã gồm cấp, trang bị, buff).
- **CD** = hồi chiêu (giây). **Vùng** = bán kính (world unit). Quái thường cao khoảng 0.2 unit.
- Lv2 → Lv4 chỉ tăng số; **Lv5 đổi tên, đổi hình và thêm cơ chế** (in đậm).
- Số liệu là điểm bắt đầu để cân bằng, sẽ chỉnh khi test.

## 2. 22 skill tấn công

### Đạn

**1. Fire Bolt → Inferno Orb** *(có sẵn, Fire)* — Đạn thẳng vào quái gần nhất, nổ vùng nhỏ, gây cháy.

| Lv | Nội dung |
|---|---|
| 1 | 1 đạn, 80% sát thương, nổ vùng 0.25, cháy 10%/giây trong 2s. CD 1.2s |
| 2 | 2 đạn |
| 3 | 2 đạn, 100%, nổ vùng 0.3 |
| 4 | 3 đạn, cháy 3s |
| 5 | **Inferno Orb**: 3 quả cầu lớn 130%, nổ vùng 0.4, **để lại vũng lửa 2s** (20%/0.5s) |

**2. Ice Bolt → Glacial Spear** *(có sẵn, Ice)* — Quạt mảnh băng, xuyên quái, làm chậm.

| Lv | Nội dung |
|---|---|
| 1 | 3 mảnh quạt 30°, 60%, xuyên 1 quái, chậm 30% trong 1.5s. CD 1.5s |
| 2 | 4 mảnh |
| 3 | 4 mảnh, 75%, xuyên 2 |
| 4 | 5 mảnh, quạt 45° |
| 5 | **Glacial Spear**: 5 giáo băng 110%, **xuyên tất cả**, **đóng băng 1s** |

**3. Poison Bolt → Venom Burst** *(có sẵn, Poison)* — Đạn độc, trúng thì tách đạn nhỏ.

| Lv | Nội dung |
|---|---|
| 1 | 1 đạn 70%, độc 8%/giây trong 3s, trúng tách 2 đạn nhỏ (35%). CD 1.3s |
| 2 | Độc cộng dồn tối đa 3 lớp |
| 3 | 2 đạn, 85% |
| 4 | Tách 3 đạn nhỏ |
| 5 | **Venom Burst**: tách 4 đạn, **để lại mây độc 2s**, độc cộng dồn 5 lớp |

**4. Holy Bolt → Divine Volley** *(Light)* — Đạn đuổi mục tiêu, mạnh với boss.

| Lv | Nội dung |
|---|---|
| 1 | 1 đạn đuổi 90%, +20% sát thương lên boss/tinh anh. CD 1.5s |
| 2 | 2 đạn |
| 3 | 2 đạn, 110% |
| 4 | 3 đạn, CD 1.2s |
| 5 | **Divine Volley**: 3 tia sáng 140% **xuyên quái**, mỗi lần trúng **hồi 1% HP** |

**5. Throwing Knives → Thousand Blades** — Dao bắn theo hướng chạy (hướng nhìn).

| Lv | Nội dung |
|---|---|
| 1 | 2 dao liên tiếp, 40% mỗi dao, xuyên 1. CD 0.8s |
| 2 | 3 dao |
| 3 | 3 dao, 50% |
| 4 | 4 dao, xuyên 2 |
| 5 | **Thousand Blades**: bắn **liên thanh** 10 dao/giây khi đang chạy, 55% |

**6. Bouncing Ball → Wrecking Ball** — Bóng nảy giữa quái và mép màn hình.

| Lv | Nội dung |
|---|---|
| 1 | 1 bóng 70%, nảy 3 lần, tồn tại 3s. CD 3s |
| 2 | Nảy 5 lần |
| 3 | 2 bóng, 85% |
| 4 | Bóng to hơn 30%, tồn tại 4s |
| 5 | **Wrecking Ball**: bóng khổng lồ 120%, **nảy vô hạn trong 5s, đẩy lùi quái** |

**7. Boomerang → Twin Chakram** — Ném ra rồi bay về, trúng cả lượt đi và về.

| Lv | Nội dung |
|---|---|
| 1 | 1 boomerang 80%, tầm 1.2. CD 2s |
| 2 | Tầm 1.5 |
| 3 | 2 boomerang, 95% |
| 4 | Bay nhanh hơn 30%, CD 1.6s |
| 5 | **Twin Chakram**: 2 chakram 120%, **khi bay về tách thêm 4 lưỡi nhỏ** (40%) |

### Xoay quanh người / hào quang

**8. Orbit Saw → Buzzsaw Storm** *(có sẵn)* — Lưỡi cưa xoay quanh người.

| Lv | Nội dung |
|---|---|
| 1 | 1 lưỡi, 100% mỗi lần chạm, bán kính 0.4 |
| 2 | 2 lưỡi |
| 3 | 3 lưỡi, 115% |
| 4 | 4 lưỡi, xoay nhanh hơn 25% |
| 5 | **Buzzsaw Storm**: 6 lưỡi 140%, **bán kính co giãn** 0.3 ↔ 0.7 |

**9. Flame Ring → Sun Wheel** *(Fire)* — Cầu lửa xoay quanh người, gây cháy.

| Lv | Nội dung |
|---|---|
| 1 | 2 cầu lửa 60%, cháy 10%/giây 2s, bán kính 0.5 |
| 2 | 3 cầu |
| 3 | 3 cầu, 75%, bán kính 0.6 |
| 4 | 4 cầu, cháy 3s |
| 5 | **Sun Wheel**: thành **vòng lửa liền** liên tục, 40%/0.3s, cháy mạnh gấp đôi |

**10. Frost Aura → Absolute Zero** *(có sẵn, Ice)* — Hào quang làm chậm và gây sát thương nhẹ.

| Lv | Nội dung |
|---|---|
| 1 | Bán kính 0.5, chậm 20%, 10%/0.5s |
| 2 | Chậm 25% |
| 3 | Bán kính 0.6, 15%/0.5s |
| 4 | Chậm 30%, bán kính 0.7 |
| 5 | **Absolute Zero**: mỗi 4s **nổ băng đóng băng 1s** mọi quái trong vùng, 150% |

**11. Static Field → Tesla Dome** *(Lightning)* — Hào quang giật quái ngẫu nhiên trong tầm.

| Lv | Nội dung |
|---|---|
| 1 | Bán kính 0.6, mỗi 1s giật 1 quái 90% |
| 2 | Giật 2 quái |
| 3 | 110%, bán kính 0.7 |
| 4 | Giật 3 quái, mỗi 0.8s |
| 5 | **Tesla Dome**: **giật mọi quái trong vòm** mỗi 1s, 80%, **choáng 0.3s** |

**12. Holy Aura → Sanctuary** *(Light)* — Hào quang gây sát thương và hồi máu.

| Lv | Nội dung |
|---|---|
| 1 | Bán kính 0.5, 15%/0.5s, hồi 0.5% HP/giây |
| 2 | Hồi 0.75% HP/giây |
| 3 | Bán kính 0.6, 20%/0.5s |
| 4 | Hồi 1% HP/giây |
| 5 | **Sanctuary**: hồi 1.5% HP/giây, **mỗi 3s đẩy lùi quái** ra mép vùng, 25%/0.5s |

### Vùng trên đất / từ trời

**13. Toxic Flask → Plague Cauldron** *(Poison)* — Ném bình độc vào vị trí ngẫu nhiên gần quái, tạo vũng độc.

| Lv | Nội dung |
|---|---|
| 1 | 1 bình, vũng 0.35 trong 3s, 15%/0.5s + độc. CD 3s |
| 2 | 2 bình |
| 3 | Vũng 0.45, 20%/0.5s |
| 4 | 3 bình, vũng 4s |
| 5 | **Plague Cauldron**: vũng lớn 0.7 trong 5s, **làm chậm 30%**, độc cộng dồn 5 lớp |

**14. Spike Trap → Caltrops Field** — Rải bẫy gai quanh người, quái giẫm bị thương.

| Lv | Nội dung |
|---|---|
| 1 | Rải 3 bẫy, mỗi bẫy 120%, tồn tại 6s. CD 4s |
| 2 | 4 bẫy |
| 3 | 4 bẫy, 150% |
| 4 | 5 bẫy, bẫy đánh 2 lần mới mất |
| 5 | **Caltrops Field**: 6 bẫy 180%, **bẫy nổ dây chuyền** sang bẫy cạnh |

**15. Meteor → Armageddon** *(Fire)* — Báo vùng đỏ 0.6s rồi thiên thạch rơi vào chỗ đông quái.

| Lv | Nội dung |
|---|---|
| 1 | 1 thiên thạch 200%, vùng 0.4, cháy. CD 4s |
| 2 | Vùng 0.5 |
| 3 | 2 thiên thạch, 240% |
| 4 | CD 3.2s |
| 5 | **Armageddon**: **mưa 6 thiên thạch** trong 2s, 260%, để lại vũng lửa |

**16. Thunder Strike → Thunder God** *(Lightning)* — Sét đánh xuống quái ngẫu nhiên trên màn hình.

| Lv | Nội dung |
|---|---|
| 1 | 1 tia 150%, vùng 0.2. CD 2s |
| 2 | 2 tia |
| 3 | 2 tia, 180%, choáng 0.3s |
| 4 | 3 tia |
| 5 | **Thunder God**: **sét đánh liên tục** 4 tia mỗi 1s, 160%, choáng 0.5s |

### Nảy / tia / cận chiến

**17. Chain Lightning → Storm Chain** *(Lightning)* — Đánh quái gần nhất rồi nảy sang quái khác.

| Lv | Nội dung |
|---|---|
| 1 | 100%, nảy 2 lần (mỗi lần nảy giảm 15%). CD 1.6s |
| 2 | Nảy 3 |
| 3 | 120% |
| 4 | Nảy 4, CD 1.3s |
| 5 | **Storm Chain**: nảy 6, **không giảm sát thương, choáng 0.3s** mỗi quái |

**18. Radiant Lance → Solar Ray** *(Light)* — Tia laser bắn theo hướng nhìn.

| Lv | Nội dung |
|---|---|
| 1 | Tia dài 1.5, rộng 0.1, 50%/0.2s trong 1s. CD 3s |
| 2 | Dài 2, rộng 0.12 |
| 3 | 65%/0.2s |
| 4 | Kéo dài 1.5s |
| 5 | **Solar Ray**: tia **xoay 360° quanh người** trong 3s, 70%/0.2s |

**19. Blade Slash → Crescent Wave** — Chém vòng cung trước mặt.

| Lv | Nội dung |
|---|---|
| 1 | Chém cung 120°, tầm 0.4, 150%. CD 1.2s |
| 2 | Tầm 0.5 |
| 3 | 180%, cung 150° |
| 4 | Chém 2 lần (trước + sau lưng) |
| 5 | **Crescent Wave**: mỗi nhát **bắn ra sóng kiếm** bay xa 2 unit, 120%, xuyên tất cả |

### Pet / triệu hồi

Pet không bị quái đánh (không có HP), chỉ là "vật thể tự đi gây sát thương".

**20. Battle Wolf → Alpha Pack** — Sói chạy theo người, tự lao vào cắn quái gần.

| Lv | Nội dung |
|---|---|
| 1 | 1 sói, cắn 80% mỗi 0.8s |
| 2 | Cắn 95% |
| 3 | 2 sói |
| 4 | Cắn nhanh hơn (0.6s), 110% |
| 5 | **Alpha Pack**: 3 sói lớn 130%, **cắn gây chảy máu** (15%/giây trong 3s) |

**21. Gun Drone → Twin Drones** — Drone bay quanh người, tự bắn quái gần.

| Lv | Nội dung |
|---|---|
| 1 | 1 drone, bắn 40% mỗi 0.5s |
| 2 | 50% |
| 3 | Bắn 2 đạn mỗi lần |
| 4 | 2 drone |
| 5 | **Twin Drones**: 2 drone, **thêm tên lửa nổ vùng** 150% mỗi 2s |

**22. Sentry Turret → Tesla Tower** — Đặt trụ tại chỗ đứng, tự bắn quái trong tầm.

| Lv | Nội dung |
|---|---|
| 1 | 1 trụ tồn tại 8s, tầm 1.2, bắn 60% mỗi 0.5s. CD 10s |
| 2 | Tồn tại 10s |
| 3 | 75%, tầm 1.5 |
| 4 | 2 trụ |
| 5 | **Tesla Tower**: trụ bắn **sét nảy 3 quái**, 90%, tồn tại 12s |

## 3. 8 skill hỗ trợ (5 cấp, không tiến hóa; Lv5 có thưởng mốc)

Chỉ tăng Damage, HP, DEF, Crit, hồi máu, khiên và tầm hút. **Không tăng tốc chạy.**

| # | Skill | Lv1 | Lv2 | Lv3 | Lv4 | Lv5 (mốc) |
|---|---|---|---|---|---|---|
| 23 | **Might** | +8% Damage | +16% | +24% | +32% | +40%, **+10% sát thương lên boss** |
| 24 | **Vitality** | +10% HP tối đa | +20% | +30% | +40% | +50%, **hồi đầy máu khi đạt** |
| 25 | **Toughness** *(có sẵn)* | +4 DEF | +8 | +12 | +16 | +20, **DEF hồi 1/giây khi không bị đánh 3s** |
| 26 | **Keen Eye** | +4% Crit | +8% | +12% | +16% | +20%, **chí mạng gây choáng 0.2s** |
| 27 | **Executioner** | +15% Crit DMG | +30% | +45% | +60% | +75%, **quái dưới 15% HP bị kết liễu** (trừ boss) |
| 28 | **Regeneration** *(có sẵn)* | Hồi 1 HP/2s | 2 HP/2s | 3 HP/2s | 4 HP/2s | 5 HP/2s, **+1 HP tối đa mỗi lần hồi** |
| 29 | **Emergency Shield** *(có sẵn)* | Dưới 20% HP: +30 khiên, CD 20s | +40 khiên | CD 16s | +55 khiên | +70 khiên, **bất tử 1s khi kích hoạt** |
| 30 | **Magnet Stone** | +20% tầm hút | +40% | +60% | +80% | +100%, **mỗi 30s hút toàn bộ EXP trên màn** |

Ghi chú: DEF trong game là dung lượng khiên (Toughness hiện cộng max DEF và khiên hiện tại).

## 4. Skill nhân vật

Đòn thường của mọi nhân vật: bắn đạn thường (giữ như hiện tại). Skill nhân vật **không trùng** 30 skill trong trận nhưng chạy trên **cùng các họ hành vi** (xem [Skill-System-Design.md](Skill-System-Design.md)). Chủ động: 5 cấp, nâng bằng sao, Lv5 tiến hóa. Bị động: 1 cấp, có sẵn hoặc mở theo level nhân vật.

| Nhân vật | Bậc | Chủ động (Lv1) | Lv2 → Lv4 | Lv5 tiến hóa | Bị động |
|---|---|---|---|---|---|
| Blessed Gladiator | Common | **Judgment Hammer** *(Strike)*: búa thánh đập quái gần nhất, 150%, vùng 0.3, CD 3s | Vùng 0.35 → 2 búa → choáng 0.5s | **Divine Hammer**: 3 búa 200%, để lại vùng thánh hồi 1% HP/giây trong 3s | — |
| Forest Ranger | Common | **Piercing Arrow** *(Shooting)*: mũi tên xuyên 3 quái, 130%, CD 1.5s | Xuyên 5 → 2 mũi → 160% | **Hunter's Barrage**: mũi tên xuyên tất cả, trúng quái đầu tách 5 mũi | — |
| Apprentice Mage | Common | **Arcane Orb** *(Shooting)*: cầu phép bay chậm đuổi mục tiêu, nổ vùng 0.3, 140%, CD 2.5s | Vùng 0.35 → 2 cầu → 170% | **Arcane Nova**: cầu nổ ra 8 mảnh phép 60% | — |
| Iron Guard | Uncommon | **Shield Bash** *(Slash)*: đập khiên hình quạt 90°, 140%, đẩy lùi, CD 2.5s | 160% → quạt 120° → choáng 0.4s | **Guardian Wall**: mỗi cú đập cho +20% DEF trong 3s, đẩy lùi xa gấp đôi | — |
| Swamp Alchemist | Uncommon | **Acid Rain** *(Zone)*: mưa axit vào chỗ đông quái, vùng 0.5 trong 3s, 20%/0.5s, CD 5s | Vùng 0.6 → 25%/0.5s → 4s | **Corrosive Storm**: vùng 0.8, quái trong vùng **−30% DEF** (Corrode) | — |
| Wild Huntress | Uncommon | **Spear Volley** *(Shooting)*: ném 3 giáo quạt, xuyên 2, 110%, CD 2s | 4 giáo → 130% → xuyên 3 | **Returning Spears**: giáo bay về tay (Boomerang), trúng cả lượt về | — |
| Frost Sentinel | Rare | **Glacier Spike** *(Strike)*: 4 gai băng mọc nối tiếp theo hướng nhìn, 120% + chậm, CD 3s | 5 gai → 140% → đóng băng 0.5s | **Frozen Domain**: gai để lại vùng băng 3s làm chậm 50% | **Glacial Armor**: +10% DEF; quái đánh trúng mình bị chậm 30% trong 2s |
| Ember Sorceress | Rare | **Flame Pillar** *(Strike)*: cột lửa tại chỗ đông quái, 180%, vùng 0.35, cháy, CD 3.5s | Vùng 0.4 → 2 cột → 210% | **Inferno Pillars**: 3 cột, mỗi cột để lại vũng lửa 2s | **Kindling**: quái đang cháy nhận thêm 15% sát thương |
| Shadow Reaper | Epic | **Reaping Whirl** *(Slash 360°)*: xoay lưỡi hái quanh người 160%, hút 5% sát thương thành máu, CD 3s | 180% → vùng +20% → hút 8% | **Death's Embrace**: xoay 3 vòng liên tiếp, **kéo quái vào tâm** | **Soul Harvest**: mỗi quái giết được +1% Damage trong 5s (tối đa 20 lớp) |
| Celestial Paladin | Legendary | **Heaven's Judgment** *(Strike)*: 3 cột sáng xuống quái ngẫu nhiên, 200%, mỗi cột hồi 1% HP, CD 4s | 4 cột → 240% → choáng 0.5s | **Archangel**: thêm **thiên thần hộ vệ** (Summon) 8s bắn tia sáng 80% | **Aegis**: HP < 30% → khiên 30% HP tối đa + bất tử 1s (CD 30s) · **Aura of Valor**: +10% Damage, +10% Crit DMG |

Ghi chú: Corrode (giảm DEF), hút máu và kéo quái là cơ chế mới, thêm vào bảng hành vi.

## 5. Họ hành vi (behaviour) cần có

Hiện có sẵn: `Shooting` (thẳng/quạt/vòng/xoắn ốc), `OrbitSaw`, `FrostAura`, `Regeneration`, `EmergencyShield`, `StatBuff`.

| Họ | Có sẵn? | Skill dùng |
|---|---|---|
| Bắn đạn + hiệu ứng khi trúng (nổ / xuyên / tách / đuổi) | `Shooting`, thiếu nổ/xuyên/tách | 1, 2, 3, 4, 5 |
| Đạn nảy | **Mới** (mở rộng đạn) | 6 |
| Bay ra rồi về | **Mới** | 7 |
| Xoay quanh người | `OrbitSaw` | 8, 9 |
| Hào quang / nổ theo nhịp | `FrostAura`, thiếu nổ theo nhịp | 10, 11, 12 |
| Vùng trên đất (vũng, bẫy) | **Mới** | 13, 14 |
| Đánh từ trời có báo vùng | **Mới** | 15, 16 |
| Nảy giữa quái (chain) | **Mới** | 17 |
| Tia (beam) | **Mới** | 18 |
| Chém cận chiến | **Mới** | 19 |
| Pet / triệu hồi (đi theo, đứng yên) | **Mới** | 20, 21, 22 |
| Chỉ số / hồi máu / khiên | `StatBuff`, `Regeneration`, `EmergencyShield` | 23–29 |
| Tầm hút | **Mới** (nối với Loot) | 30 |

## 6. Thứ tự làm (đề xuất)

1. **Khung chung**: tiến hóa Lv5 (đổi tên/icon/hình theo cấp); nhãn nguyên tố trên `SkillDataSO` (tùy chọn).
2. **Lô mẫu** — mỗi skill thử một họ mới, test kỹ trong trận: Fire Bolt → Inferno Orb (hiệu ứng khi trúng), Toxic Flask (vùng trên đất), Meteor (từ trời), Battle Wolf (pet).
3. Các họ còn lại: nảy, boomerang, chain, beam, chém, drone/trụ.
4. Phần lớn skill còn lại chỉ là data trên các họ đã có.
5. Skill hỗ trợ (23–30), rồi skill nhân vật và boss.
