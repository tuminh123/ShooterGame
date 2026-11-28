# Thiết kế skill

Cập nhật: 2026-11-28. **Đã có trong code và data** (asset trong `DataSO/Skills`). Số liệu là điểm xuất phát để cân bằng; chỉnh trực tiếp trong asset.
Kiến trúc các họ hành vi: [Skill-System-Design.md](Skill-System-Design.md).

## 1. Quy tắc đã chốt

| Loại | Cấp | Ghi chú |
|---|---|---|
| Skill trong trận (31) | Lv1 → Lv5, chọn khi lên cấp | Skill tấn công: **Lv5 = tiến hóa** (đổi tên, icon, thêm cơ chế), không cần xúc tác |
| Skill tướng (chủ động) | 0★ → 5★ (6 bộ data), **không nâng trong trận** | Là **vũ khí khởi đầu** (game kiểu Vampire Survivors, không có đòn thường). Tiến hóa ở 5★ |
| Skill tướng (bị động) | 1 cấp, luôn có | Rare/Epic: 1 bị động; Legendary: 2 bị động; Common/Uncommon: không có |
| Skill đồ | — | Chưa thiết kế (giữ asset `EQ_*` cũ) |
| Boss | ≥ 3 skill/boss | Chưa thiết kế (vẫn dùng `BossFireCircle/Spiral`) |

- Skill trong trận, skill tướng, skill boss, skill đồ **không trùng nhau**: được dùng chung họ hành vi và thuộc tính (xuyên, cháy…), không trùng **cơ chế đặc trưng**.
- Giới hạn skill trong trận = trần level trận (20 → 19 lượt chọn). Trọng số roll cho skill đã có: **để sau**.
- Skill tướng không lên cấp trong trận nhưng **ăn mọi chỉ số** của tướng (Damage +1/level trận, Might, Crit, bonus chiến đấu…).
- **Tầm chiến đấu thuộc nhân vật** (`CharacterDataSO.combatRange`, Common = 1.1). Mọi skill nhắm/đặt trong tầm này; skill chỉ quyết định hình dạng. Bonus `CombatRange` (Eagle Eye) nới tầm cho mọi skill.

### Cách đọc số

- `%` = % Damage của nhân vật (đã gồm level, trang bị, buff). Cháy/độc/chảy máu cũng tính theo % Damage lúc trúng.
- CD = hồi chiêu (giây). Vùng = bán kính (world unit). Quái thường cao ≈ 0.2.
- **[đổi]** = cấp đó đổi/thêm hành vi, không chỉ tăng số. **[tiến hóa]** = đổi tên + icon.

## 2. Skill trong trận — 22 tấn công

Tên asset = tên trong ngoặc. Icon lấy từ pack `2D Minimal-CharacterMaker/Thumbnail` (chép sang `Art/SkillIcons`).

### Đạn (họ Shooting)

**1. Fire Bolt → Inferno Orb** (`FireBolt`) — đạn vào quái gần nhất, nổ vùng nhỏ, gây cháy.
| Lv | Nội dung |
|---|---|
| 1 | 1 đạn 80%, nổ vùng 0.25 (50%), cháy 10%/s 2s. CD 1.2 |
| 2 | 2 đạn |
| 3 | 100%, nổ 0.3 (60%) |
| 4 | 3 đạn, cháy 3s |
| 5 | **[tiến hóa] Inferno Orb**: 3 cầu lớn 130%, nổ 0.4, **để lại vũng lửa 2s** (20%/0.5s + cháy) |

**2. Ice Bolt → Glacial Spear** (`IceBolt`) — quạt mảnh băng xuyên quái, làm chậm.
| Lv | Nội dung |
|---|---|
| 1 | 3 mảnh quạt 30°, 60%, xuyên 1, chậm 30% 1.5s. CD 1.5 |
| 2 | 4 mảnh |
| 3 | 75%, xuyên 2 |
| 4 | 5 mảnh, quạt 45° |
| 5 | **[tiến hóa] Glacial Spear**: 5 giáo 110%, **xuyên tất cả, đóng băng 1s** |

**3. Poison Bolt → Venom Burst** (`PoisonBolt`) — trúng thì tách đạn nhỏ.
| Lv | Nội dung |
|---|---|
| 1 | 1 đạn 70%, độc 8%/s 3s, tách 2 mảnh (35%). CD 1.3 |
| 2 | **[đổi]** độc cộng dồn 3 lớp |
| 3 | 2 đạn, 85% |
| 4 | Tách 3 mảnh |
| 5 | **[tiến hóa] Venom Burst**: tách 4, **để lại mây độc 2s**, độc dồn 5 lớp |

**4. Holy Bolt → Divine Volley** (`HolyBolt`) — đạn đuổi, mạnh với boss (bonus riêng của skill).
| Lv | Nội dung |
|---|---|
| 1 | 1 đạn đuổi 90%, +20% lên boss/tinh anh. CD 1.5 |
| 2 | 2 đạn |
| 3 | 110% |
| 4 | 3 đạn, CD 1.2 |
| 5 | **[tiến hóa] Divine Volley**: 3 tia 140% **xuyên quái**, **+50% lên boss/tinh anh** |

**5. Throwing Knives → Thousand Blades** (`ThrowingKnives`) — dao bắn nối đuôi theo hướng chạy.
| Lv | Nội dung |
|---|---|
| 1 | 2 dao nối đuôi, 40%/dao, xuyên 1. CD 0.8 |
| 2 | 3 dao |
| 3 | 50% |
| 4 | 4 dao, xuyên 2 |
| 5 | **[tiến hóa] Thousand Blades**: **liên thanh 10 dao/giây**, 55%, xuyên 2 |

**6. Bouncing Ball → Wrecking Ball** (`BouncingBall`) — bóng xuyên quái, nảy ở mép màn hình.
| Lv | Nội dung |
|---|---|
| 1 | 1 bóng 70%, nảy 3 lần, 3s. CD 3 |
| 2 | Nảy 5 lần |
| 3 | 2 bóng, 85% |
| 4 | To hơn 30%, 4s |
| 5 | **[tiến hóa] Wrecking Ball**: bóng khổng lồ 120%, **nảy vô hạn 5s, đẩy lùi** |

**7. Throwing Axe → Twin Axes** (`ThrowingAxe`) — rìu xoay bay tới mép tầm chiến đấu rồi quay về, trúng cả lượt đi lẫn về.
| Lv | Nội dung |
|---|---|
| 1 | 1 rìu 80%. CD 2 |
| 2 | Rìu to hơn 20% |
| 3 | **[đổi]** 2 rìu xòe 30°, 95% |
| 4 | Nhanh hơn 30%, CD 1.6 |
| 5 | **[tiến hóa] Twin Axes**: 2 rìu 120%, **về tới tay thì tách 4 lưỡi nhỏ** (40%) |

### Xoay quanh người / hào quang

**8. Orbit Saw → Buzzsaw Storm** (`OrbitSaw`, họ OrbitSaw)
| Lv | Nội dung |
|---|---|
| 1 | 1 lưỡi, 100%/lần chạm, bán kính 0.4 |
| 2 | 2 lưỡi |
| 3 | 3 lưỡi, 115% |
| 4 | 4 lưỡi, quay nhanh hơn 25% |
| 5 | **[tiến hóa] Buzzsaw Storm**: 6 lưỡi 140%, **bán kính co giãn 0.3 ↔ 0.7** |

**9. Flame Ring → Sun Wheel** (`FlameRing`, họ OrbitSaw) — cầu lửa xoay quanh người.
| Lv | Nội dung |
|---|---|
| 1 | 2 cầu 60%, cháy 10%/s 2s, bán kính 0.5 |
| 2 | 3 cầu |
| 3 | 75%, bán kính 0.6 |
| 4 | 4 cầu, cháy 3s |
| 5 | **[tiến hóa] Sun Wheel**: **vòng 12 cầu liền**, 40%, cháy gấp đôi |

**10. Frost Aura → Absolute Zero** (`FrostAura`, họ Aura)
| Lv | Nội dung |
|---|---|
| 1 | Bán kính 0.5, chậm 20%, 10%/0.5s |
| 2 | Chậm 25% |
| 3 | Bán kính 0.6, 15%/0.5s |
| 4 | Chậm 30%, bán kính 0.7 |
| 5 | **[tiến hóa] Absolute Zero**: **mỗi 4s nổ băng 150%, đóng băng 1s** mọi quái trong vùng |

**11. Static Field → Tesla Dome** (`StaticField`, họ Aura) — giật quái ngẫu nhiên trong vùng.
| Lv | Nội dung |
|---|---|
| 1 | Bán kính 0.6, mỗi 1s giật 1 quái 90% |
| 2 | Giật 2 quái |
| 3 | 110%, bán kính 0.7 |
| 4 | Giật 3 quái, mỗi 0.8s |
| 5 | **[tiến hóa] Tesla Dome**: **giật mọi quái trong vòm** mỗi 1s, 80%, choáng 0.3s |

**12. Holy Aura → Sanctuary** (`HolyAura`, họ Aura) — gây sát thương và hồi máu.
| Lv | Nội dung |
|---|---|
| 1 | Bán kính 0.5, 15%/0.5s, hồi 0.5% HP/s |
| 2 | Hồi 0.75%/s |
| 3 | Bán kính 0.6, 20%/0.5s |
| 4 | Hồi 1%/s |
| 5 | **[tiến hóa] Sanctuary**: hồi 1.5%/s, **hồi thừa khi đầy máu thành giáp** (tối đa 20% HP), 25% |

### Vùng trên đất / từ trời

**13. Toxic Shroom → Plague Garden** (`ToxicShroom`, Strike + vùng) — mọc nấm độc cạnh quái ngẫu nhiên trong tầm.
| Lv | Nội dung |
|---|---|
| 1 | 1 nấm 3s, bào tử 0.35: 15%/0.5s + độc. CD 3 |
| 2 | 2 nấm |
| 3 | Bào tử 0.45, 20% |
| 4 | 3 nấm, 4s |
| 5 | **[tiến hóa] Plague Garden**: nấm lớn 0.6, **chậm 30%, tàn thì mọc 2 nấm con** (2s) |

**14. Spike Trap → Caltrops Field** (`SpikeTrap`, vùng bẫy) — rải bẫy quanh người.
| Lv | Nội dung |
|---|---|
| 1 | 3 bẫy, mỗi bẫy 120% khi quái giẫm, tồn tại 6s. CD 4 |
| 2 | 4 bẫy |
| 3 | 150% |
| 4 | 5 bẫy, **[đổi]** mỗi bẫy nổ 2 lần |
| 5 | **[tiến hóa] Caltrops Field**: 6 bẫy 180%, **mỗi lần nổ lan ra quái xung quanh** (0.35) |

**15. Meteor → Armageddon** (`Meteor`, Strike) — báo vùng 0.6s rồi rơi.
| Lv | Nội dung |
|---|---|
| 1 | 1 thiên thạch vào chỗ đông quái nhất, 200%, vùng 0.4, cháy. CD 4 |
| 2 | Vùng 0.5 |
| 3 | 2 thiên thạch (quái ngẫu nhiên), 240% |
| 4 | CD 3.2 |
| 5 | **[tiến hóa] Armageddon**: **mưa 6 thiên thạch trong 2s**, 260%, mỗi viên để lại vũng lửa |

**16. Thunder Strike → Thunder God** (`ThunderStrike`, Strike) — sét đánh quái ngẫu nhiên trong tầm chiến đấu.
| Lv | Nội dung |
|---|---|
| 1 | 1 tia 150%, vùng 0.2. CD 2 |
| 2 | 2 tia |
| 3 | 180%, **[đổi]** choáng 0.3s |
| 4 | 3 tia |
| 5 | **[tiến hóa] Thunder God**: **4 tia mỗi 1s**, 160%, choáng 0.5s |

### Nảy / tia / cận chiến

**17. Chain Lightning → Storm Chain** (`ChainLightning`, Chain)
| Lv | Nội dung |
|---|---|
| 1 | 100%, nảy 2 lần (mỗi lần −15%). CD 1.6 |
| 2 | Nảy 3 |
| 3 | 120% |
| 4 | Nảy 4, CD 1.3 |
| 5 | **[tiến hóa] Storm Chain**: nảy 6, **không giảm sát thương, choáng 0.3s** |

**18. Radiant Lance → Solar Ray** (`RadiantLance`, Beam) — tia dài bằng tầm chiến đấu theo hướng chạy.
| Lv | Nội dung |
|---|---|
| 1 | 50%/0.2s trong 1s. CD 3 |
| 2 | Tia rộng hơn |
| 3 | 65%/0.2s |
| 4 | Kéo dài 1.5s |
| 5 | **[tiến hóa] Solar Ray**: tia **xoay 360° quanh người** trong 3s, 70%/0.2s |

**19. Blade Slash → Blade Dance** (`BladeSlash`, Slash) — chém cung về phía quái gần nhất.
| Lv | Nội dung |
|---|---|
| 1 | Cung 120°, tầm 0.4, 150%. CD 1.2 |
| 2 | Tầm 0.5 |
| 3 | 180%, cung 150° |
| 4 | **[đổi]** chém trước + sau |
| 5 | **[tiến hóa] Blade Dance**: **chém 4 hướng cùng lúc**, 160% mỗi nhát |

### Pet (Summon — không có HP, không bị quái nhắm; tìm mục tiêu trong tầm chiến đấu của chủ)

**20. Battle Wolf → Alpha Pack** (`BattleWolf`)
| Lv | Nội dung |
|---|---|
| 1 | 1 sói, lao cắn 80% mỗi 0.8s |
| 2 | 95% |
| 3 | 2 sói |
| 4 | Cắn nhanh hơn (0.6s), 110% |
| 5 | **[tiến hóa] Alpha Pack**: 3 sói lớn 130%, **cắn gây chảy máu** 15%/s 3s |

**21. Gun Drone → Twin Drones** (`GunDrone`)
| Lv | Nội dung |
|---|---|
| 1 | 1 drone bay quanh, bắn 40% mỗi 0.5s |
| 2 | 50% |
| 3 | **[đổi]** 2 đạn mỗi lần |
| 4 | 2 drone |
| 5 | **[tiến hóa] Twin Drones**: **thêm tên lửa nổ vùng** 150% mỗi 2s |

**22. Sentry Turret → Tesla Tower** (`SentryTurret`) — đặt trụ tại chỗ đứng.
| Lv | Nội dung |
|---|---|
| 1 | 1 trụ 8s, bắn 60% mỗi 0.5s. CD 10 |
| 2 | Tồn tại 10s |
| 3 | 75% |
| 4 | **[đổi]** 2 trụ |
| 5 | **[tiến hóa] Tesla Tower**: trụ bắn **sét nảy 3 quái**, 90%, tồn tại 12s |

## 3. Skill trong trận — 9 hỗ trợ (Lv5 = thưởng mốc)

| # | Skill | Lv1 → Lv4 | Lv5 (mốc) |
|---|---|---|---|
| 23 | **Might** | +8 / 16 / 24 / 32% Damage | +40%, **+10% sát thương lên boss** |
| 24 | **Vitality** | +10 / 20 / 30 / 40% HP tối đa | +50%, **hồi đầy máu khi đạt** |
| 25 | **Toughness** | +4 / 8 / 12 / 16 DEF | +20, **không bị đánh 3s thì hồi 1 DEF/s** |
| 26 | **Keen Eye** | +4 / 8 / 12 / 16% Crit | +20%, **chí mạng gây choáng 0.2s** |
| 27 | **Executioner** | +15 / 30 / 45 / 60% Crit DMG | +75%, **kết liễu quái thường dưới 15% HP** |
| 28 | **Regeneration** | 1 / 2 / 3 / 4 HP mỗi 2s | 5 HP mỗi 1.5s |
| 29 | **Emergency Shield** | Dưới 20% HP: +30 khiên CD 20 → +40 → CD 16 → +55 | +70, **bất tử 1s khi kích hoạt** |
| 30 | **Magnet Stone** | +20 / 40 / 60 / 80% tầm hút | +100%, **mỗi 30s hút toàn bộ EXP trên màn** |
| 31 | **Eagle Eye** | +10 / 20 / 30 / 40% **tầm chiến đấu** | +50%, **+15% sát thương lên quái ở nửa ngoài tầm** |

DEF trong game là dung lượng giáp. Regeneration cộng thẳng HP (tăng cả HP tối đa như trước); hút máu, vùng hồi máu, hồi giáp chỉ hồi phần đã mất.

## 4. Skill tướng

Cấp theo sao: 0★ là bộ data đầu; mỗi sao 1–5 nâng một cấp (bước `Stars ≥ N` trong `Skill Progression`). 5★ = tiến hóa.
Nguyên tắc số: 0★ đủ gánh đầu trận (đánh đơn ≈ 150%/s, đánh vùng ≈ 80–120%/s mỗi quái); 5★ ≈ gấp 2–2.5 lần.

### Blessed Gladiator (Common) — Judgment Hammer → Divine Quake (Slash 360° dạng sóng lan)
| ★ | Nội dung |
|---|---|
| 0 | Đập búa: **vòng sóng lan từ người ra 0.6**, 100%, đẩy lùi nhẹ. CD 1.2 |
| 1 | 115% |
| 2 | Sóng 0.75 |
| 3 | **[đổi]** 2 vòng sóng mỗi lần đập |
| 4 | 130%, CD 1.0 |
| 5 | **[tiến hóa] Divine Quake**: sóng lan **hết tầm chiến đấu**, 150%, **để lại vùng thánh hồi 1% HP/s trong 3s** |

### Forest Ranger (Common) — Piercing Arrow → Hunter's Mark (Shooting)
| ★ | Nội dung |
|---|---|
| 0 | 1 mũi tên nhanh 110%, xuyên 2. CD 0.7 |
| 1 | Xuyên 3 |
| 2 | 125% |
| 3 | **[đổi]** 2 mũi song song |
| 4 | CD 0.6 |
| 5 | **[tiến hóa] Hunter's Mark**: 2 mũi 140% xuyên tất cả; **quái trúng bị đánh dấu 3s, nhận thêm 30% sát thương từ mọi nguồn** |

### Apprentice Mage (Common) — Arcane Orb → Arcane Nova (Shooting: cầu không va chạm, gây sát thương theo nhịp khi bay)
| ★ | Nội dung |
|---|---|
| 0 | Cầu bay chậm đuổi mục tiêu, sống 2s, **mỗi 0.25s gây 30% lên quái trong 0.2 quanh cầu**. CD 1.6 |
| 1 | Vùng 0.25 |
| 2 | 36%/0.25s |
| 3 | **[đổi]** 2 cầu |
| 4 | Sống 2.5s |
| 5 | **[tiến hóa] Arcane Nova**: 40%/0.25s; **hết giờ cầu vỡ 8 mảnh phép** (60%) |

### Iron Guard (Uncommon) — Shield Throw → Guardian Bulwark (Shooting, quỹ đạo nảy giữa quái + về tay)
| ★ | Nội dung |
|---|---|
| 0 | Ném khiên 130%, **nảy qua 3 quái rồi bay về tay**, đẩy lùi nhẹ. CD 1.4 |
| 1 | 145% |
| 2 | Nảy qua 4 quái |
| 3 | **[đổi]** choáng 0.3s mỗi quái trúng |
| 4 | CD 1.2 |
| 5 | **[tiến hóa] Guardian Bulwark**: 2 khiên 160%, **mỗi khiên về tay cho +20% DEF trong 3s** |

### Swamp Alchemist (Uncommon) — Volatile Flask → Grand Concoction (Shooting ném tới điểm)
| ★ | Nội dung |
|---|---|
| 0 | Ném bình vào quái ngẫu nhiên: nổ 0.3, 130%, **ngẫu nhiên 1 trong 3**: cháy / chậm 30% / độc. CD 1.0 |
| 1 | Nổ 0.35 |
| 2 | 150% |
| 3 | **[đổi]** 2 bình |
| 4 | **[đổi]** để lại vũng 1.5s **đúng nguyên tố** của bình |
| 5 | **[tiến hóa] Grand Concoction**: 180%, nổ 0.45, **mỗi bình gây cả 3 hiệu ứng** + vũng |

### Wild Huntress (Uncommon) — Impaling Javelin → Skewer Storm (Shooting xuyên tất cả, đẩy theo hướng bay)
| ★ | Nội dung |
|---|---|
| 0 | Lao 140%, xuyên tất cả, **kéo quái bị xuyên theo 0.3**, bay hết tầm chiến đấu. CD 1.0 |
| 1 | 160% |
| 2 | Kéo 0.45 |
| 3 | **[đổi]** cuối đường bay nổ 0.25 (100%) |
| 4 | CD 0.85 |
| 5 | **[tiến hóa] Skewer Storm**: 3 lao xòe 20°, 170%, nổ cuối đường **choáng 0.4s** |

### Frost Sentinel (Rare) — Glacier Spike → Frozen Domain (Strike dạng hàng) + bị động
| ★ | Nội dung |
|---|---|
| 0 | **4 gai băng mọc nối tiếp** từ chân về phía quái gần nhất, 90% + chậm 30% 1.5s. CD 1.0 |
| 1 | 5 gai |
| 2 | 105% |
| 3 | **[đổi]** gai đóng băng 0.5s |
| 4 | **[đổi]** 2 hàng gai xòe 30° |
| 5 | **[tiến hóa] Frozen Domain**: 3 hàng, 115%, **mỗi gai để lại băng 3s làm chậm 50%** |

**Glacial Armor (bị động):** +10% DEF; quái chạm vào mình bị chậm 30% (hào quang băng nhỏ quanh người).

### Ember Sorceress (Rare) — Flame Wall → Inferno March (Shooting đạn rộng xuyên tất cả) + bị động
| ★ | Nội dung |
|---|---|
| 0 | **Bức tường lửa rộng 0.4** chạy về phía quái gần nhất hết tầm chiến đấu, 120% + cháy 10%/s 2s. CD 1.1 |
| 1 | Rộng 0.5 |
| 2 | 140% |
| 3 | **[đổi]** để lại vệt lửa 1.5s |
| 4 | CD 1.0 |
| 5 | **[tiến hóa] Inferno March**: 3 tường xòe 40°, 160%, cháy gấp đôi |

**Kindling (bị động):** quái đang cháy nhận thêm 15% sát thương từ mình.

### Shadow Reaper (Epic) — Reaping Whirl → Death's Embrace (Slash 360°) + bị động
| ★ | Nội dung |
|---|---|
| 0 | Xoay lưỡi hái bán kính 0.45, 120%, **hút máu 5%** (riêng skill). CD 1.0 |
| 1 | 135% |
| 2 | Bán kính 0.55 |
| 3 | **[đổi]** **kéo quái trong 0.8 về phía mình** trước nhát chém |
| 4 | Hút 8%, CD 0.9 |
| 5 | **[tiến hóa] Death's Embrace**: **xoay 3 vòng liên tiếp** (100% mỗi vòng), kéo mạnh, hút 12% |

**Soul Harvest (bị động):** mỗi quái giết được +1% Damage trong 5s (tối đa 20 lớp).

### Celestial Paladin (Legendary) — Judgment Cross → Archangel (Strike chữ thập) + 2 bị động
| ★ | Nội dung |
|---|---|
| 0 | Báo 0.2s rồi **nổ ánh sáng hình chữ thập** tại quái gần nhất, 160%. CD 1.0 |
| 1 | Nhánh dài hơn |
| 2 | 185% |
| 3 | **[đổi]** 2 chữ thập vào 2 quái ngẫu nhiên |
| 4 | Choáng 0.4s |
| 5 | **[tiến hóa] Archangel**: 200%; **thêm thiên thần hộ vệ thường trực**, bắn tia sáng đuổi 80% mỗi 0.5s |

**Divine Rebirth (bị động):** một lần mỗi trận, HP dưới 15%: hồi 30% HP, bất tử 2s, nổ sáng 0.8 (150%) đẩy lùi + choáng 1s.
**Aura of Valor (bị động):** +3% Damage cho mỗi skill trong trận đang có.

## 5. Họ hành vi và cơ chế (đã làm)

| Họ (`SkillBehaviourType`) | Dùng cho |
|---|---|
| Shooting (mở rộng: xuyên, kích thước, tác động khi trúng / hết đường / theo nhịp, quãng bay theo tầm hoặc tới điểm, bắn nối đuôi, đẩy theo hướng bay, bonus riêng) | 1–7, Ranger, Mage, Iron Guard, Alchemist, Huntress, Ember |
| Quỹ đạo: Straight, Homing, Boomerang, BounceInCamera, Ricochet | 4, 6, 7, Mage, Iron Guard |
| OrbitSaw (bán kính co giãn) | 8, 9 |
| SlowAura (sát thương theo nhịp, giật N quái, hồi máu, hồi thừa thành giáp, nổ theo nhịp) | 10–12, Glacial Armor |
| Strike (tròn / hàng nối tiếp / chữ thập, báo vùng, mưa) | 13–16, Frost Sentinel, Paladin |
| Zone (theo nhịp / bẫy, hồi máu cho chủ, tác động khi nổ / hết giờ) — tạo qua tác động `SpawnZone` | vũng, nấm, bẫy, vùng thánh |
| Slash (quạt / 360°, sóng lan, nhiều đợt, kéo quái, hút máu riêng) | 19, Gladiator, Shadow Reaper |
| Chain, Beam | 17, 18 |
| Summon (Follow / Lunge / Stationary; Bite / Shoot / Chain; tên lửa) | 20–22, Archangel |
| StatBuff, Regeneration, EmergencyShield (hồi máu, bất tử, nổ, số lần), ShieldRegen, KillStack, SkillScaling | 23–31, các bị động |

Tác động dùng chung (`ISkillImpact`): Explode, Split, SpawnZone, RandomImpact, MultiImpact, BuffOwner.
Effect mới: Stun, Freeze, Bleed, Mark (nhận thêm sát thương); cháy/độc/chảy máu theo % Damage và cộng dồn lớp.
Luật trong `CombatHits`: thêm sát thương lên boss, kết liễu, dấu săn, mục tiêu đang cháy, mục tiêu ở xa, chí mạng gây choáng, đẩy/kéo, hút máu (hồi phần đã mất), sự kiện giết quái.

## 6. Việc còn lại

- Icon AI cho 7 skill đang dùng icon tạm từ pack: Orbit Saw, Flame Ring, Spike Trap, Battle Wolf, Gun Drone, Sentry Turret (và hình pet/thiên thần). Cần cấu hình key fal/OpenRouter cho MCP `generate_image`.
- Trọng số roll cho skill đã sở hữu.
- Skill boss (3 boss × 3 skill) và skill đồ.
- Cân bằng số sau khi chơi thử.
