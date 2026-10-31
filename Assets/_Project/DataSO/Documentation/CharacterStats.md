# Chỉ số nhân vật và công thức chiến đấu

## 1. Phạm vi triển khai

CharacterDataSO chứa BaseStats, hai mảng SkillDataSO[] (ActiveSkills, PassiveSkills) và mảng ShootingDataSO[] chạy bắn đạn riêng trong bước chuyển đổi hiện tại.

Mỗi SkillDataSO chứa SkillData struct (level, maxLevel, cooldown), SkillVisual class (tên, mô tả, icon) và một hành vi ISkill chỉ có Execute. SkillManager dùng chung quản lý data riêng của từng slot, cooldown và vòng đời. Character/Controller gửi yêu cầu trực tiếp; không có lớp Combat trung gian.

Đánh thường enemy lấy damage và crit từ CharacterRuntimeData.FinalStats: BaseStats trên CharacterDataSO cộng các bonus hiện tại. Skill đánh thường không có damage hoặc damageMultiplier riêng. Xem CombatAndSkills.md và Docs/Architecture/Skill-Shooting-Pipeline.md cho luồng hiện tại.

Các phần công thức dự kiến phía dưới cần được phân biệt với quy tắc đã triển khai; công thức effect hiện tại được ghi trong tài liệu pipeline.
## 2. Các field và đơn vị nhập

| Field | Ý nghĩa | Cách nhập | Mặc định |
|---|---|---|---:|
| `maxHealth` | Máu tối đa | Điểm máu, tối thiểu 1 | 100 |
| `damage` | Sát thương cơ bản | Điểm sát thương, không âm | 10 |
| `defense` | Giảm sát thương nhận | Điểm phòng thủ, không âm | 0 |
| `critChance` | Xác suất và bậc crit | 0.05 = 5%; 1 = 100%; 2.5 = 250% | 0.05 |
| `critDamageMultiplier` | Tổng hệ số crit bậc 1 | 1.5 = 150% tổng sát thương; tối thiểu 1 | 1.5 |
| `moveSpeed` | Tốc độ chạy | Đơn vị Unity mỗi giây, không âm | 3 |
| `range` | Phạm vi tìm mục tiêu | World unit, không âm | 2 |
| `dodge` | Kháng hiệu ứng bất lợi | Điểm kháng, không phải phần trăm | 0 |
| `accuracy` | Khả năng gây hiệu ứng bất lợi | Điểm chính xác, không phải phần trăm | 0 |

Giá trị mặc định chỉ là mốc ban đầu, chưa được cân bằng bằng chơi thử. `BaseStats.Default` trả về bộ giá trị trên; `new BaseStats()` hoặc `default(BaseStats)` vẫn có toàn bộ field bằng 0.

Các thuộc tính `Min` hỗ trợ nhập trong Inspector, không bảo vệ dữ liệu khi gán bằng code. Khi triển khai runtime cần kiểm tra giới hạn, từ chối NaN/infinity và không clamp critChance về 1.

## 3. HP: máu hiện tại và máu tối đa

```text
Máu hiện tại mới = max(0, máu hiện tại cũ − sát thương nhận)
```

Ví dụ đang có 100 HP, nhận 30 sát thương sau phòng thủ thì còn 70 HP. Về 0 thì chết.

`maxHealth` là sức chứa tối đa. `currentHealth` là máu hiện tại, sẽ nằm riêng trong runtime của từng nhân vật. Nhân vật mới khởi đầu với currentHealth bằng maxHealth.

Quy tắc dự kiến khi thay đổi maxHealth:

- Tăng maxHealth không tự hồi máu; nâng cấp muốn hồi phải thực hiện riêng.
- Giảm maxHealth thì giới hạn currentHealth xuống maxHealth mới nếu đang vượt quá.
- Hồi máu không vượt maxHealth.

## 4. Damage: sức mạnh đòn đánh

```text
Sát thương thường = damage của nhân vật × hệ số sát thương của skill
```

Nhân vật có 20 damage:

| Hệ số skill | Sát thương thường |
|---:|---:|
| 0.5 | 10 |
| 1 | 20 |
| 2 | 40 |

Đây là sát thương trước crit và defense. Hệ số skill sẽ thuộc cấu hình skill, chưa nằm trong BaseStats. Skill nhiều đạn cần quy định rõ hệ số tính cho từng viên hay cả loạt.

## 5. Defense: giảm sát thương ổn định cho mỗi đòn trúng

Defense không tiêu hao khi bị đánh, không phải thanh shield và không roll xác suất block.

```text
Hệ số sát thương nhận = 100 / (100 + defense)
Tỷ lệ giảm sát thương = defense / (100 + defense)
Sát thương nhận = sát thương sau crit × hệ số sát thương nhận
```

Nếu đòn đánh gây 100 sát thương trước phòng thủ:

| Defense | Giảm sát thương | Sát thương nhận |
|---:|---:|---:|
| 0 | 0% | 100 |
| 50 | 33.33% | 66.67 |
| 100 | 50% | 50 |
| 200 | 66.67% | 33.33 |
| 400 | 80% | 20 |

Có 100 defense, nhận ba đòn 100 damage thì mỗi đòn mất 50 HP, tổng mất 150 HP. Defense vẫn là 100 sau đó.

Số 100 trong công thức là mốc cân bằng chung. Khi triển khai nên đưa vào cấu hình toàn game, không lưu lặp trong từng nhân vật. Tăng mốc này làm cùng lượng defense giảm ít sát thương hơn.

Phần trăm giảm sát thương tăng chậm dần. Tuy nhiên, khi chưa xét hồi máu, khả năng chịu sát thương gốc được tính bằng:

```text
Máu hiệu dụng = maxHealth × (1 + defense / 100)
```

Ví dụ 1,000 HP và 100 defense chịu được khoảng 2,000 sát thương gốc. HP và defense hỗ trợ nhau; phần trăm giảm sát thương tăng chậm không có nghĩa mỗi điểm defense giảm giá trị máu hiệu dụng.

Shield, block, xuyên giáp và sát thương bỏ qua phòng thủ chưa thuộc bước này.

## 6. Crit chance: xác suất chí mạng và bậc chí mạng

Cho phép vượt 100%, dùng cách cộng thêm phần thưởng theo bậc.

```text
Bậc chắc chắn = floor(critChance)
Xác suất thêm một bậc = critChance − bậc chắc chắn
Bậc cuối = bậc chắc chắn + (roll thành công ? 1 : 0)
```

`floor` là lấy phần nguyên xuống dưới. Roll một số ngẫu nhiên r trong [0, 1); thành công nếu r nhỏ hơn xác suất thêm bậc. Chỉ cần một lần roll cho mỗi hit hợp lệ, không cần lặp qua từng bậc. Bậc 0 nghĩa là đòn thường.

| Crit chance | Kết quả |
|---:|---|
| 0% | Luôn bậc 0 |
| 50% | 50% bậc 0, 50% bậc 1 |
| 100% | Luôn bậc 1 |
| 150% | 50% bậc 1, 50% bậc 2 |
| 200% | Luôn bậc 2 |
| 250% | 50% bậc 2, 50% bậc 3 |
| 300% | Luôn bậc 3 |

Tỷ lệ là xác suất trên từng hit, không bảo đảm cứ hai hit ở mức 50% sẽ có đúng một crit.

## 7. Crit damage: sát thương tăng theo bậc

```text
Hệ số sát thương cuối = 1 + bậc crit × (critDamageMultiplier − 1)
Sát thương sau crit = sát thương thường × hệ số sát thương cuối
```

Ví dụ sát thương thường 100, critDamageMultiplier = 1.5:

| Bậc crit | Hệ số cuối | Sát thương sau crit |
|---:|---:|---:|
| 0 | 1 | 100 |
| 1 | 1.5 | 150 |
| 2 | 2 | 200 |
| 3 | 2.5 | 250 |

Mỗi bậc cộng thêm 50% sát thương thường. Không tính theo 1.5 mũ bậc và không nhân chồng 150 → 225 → 337.5.

Nếu multiplier tăng lên 2 thì bậc 0/1/2/3 gây 100/200/300/400. Crit chance vượt 100% không sửa field multiplier; nó tăng bậc và từ đó tăng hệ số thực tế của đòn.

### Sát thương trung bình để cân bằng nâng cấp

```text
Sát thương trung bình trước defense
    = sát thương thường × [1 + critChance × (critDamageMultiplier − 1)]
```

Dùng critChance dạng số thực: 150% = 1.5. Với sát thương thường 100 và multiplier 1.5, critChance 0%/100%/200%/300% cho sát thương trung bình 100/150/200/250.

Đây là trung bình qua nhiều hit. Crit chance và crit damage cùng tăng sẽ hỗ trợ nhau; cần cân bằng chi phí và độ hiếm của nâng cấp. Chưa đặt trần gameplay cho critChance ở bước này.

Quy tắc ban đầu: sát thương trực tiếp được crit nếu skill cho phép. Độc/đốt theo thời gian và hiệu ứng phát sinh không mặc định crit; sẽ có luật riêng khi xây các hệ thống đó.

## 8. Move speed: di chuyển được bao xa

```text
Quãng đường = moveSpeed × thời gian
```

moveSpeed = 3 nghĩa là đi 3 đơn vị Unity mỗi giây. Đi 2 giây được 6 đơn vị nếu không bị cản.

Đây không phải tốc độ đánh, tốc độ đạn hoặc hồi skill. Trong bullet hell, chạy nhanh giúp người chơi chủ động né đạn nên chỉ số này cũng có giá trị phòng thủ.

## 9. Accuracy và dodge: hiệu ứng có áp dụng thành công không

Accuracy thuộc bên gây hiệu ứng, dodge thuộc bên nhận. Cả hai là điểm. Dodge chỉ chống hiệu ứng bất lợi, không né va chạm và không tự giảm sát thương.

Mỗi hiệu ứng có xác suất gốc p, trong khoảng 0..1, do cấu hình skill/hiệu ứng quyết định sau này.

```text
Chênh lệch = accuracy bên gây − dodge bên nhận
Hệ số = 1 + chênh lệch / (100 + abs(chênh lệch))
Xác suất cuối = clamp01(p × hệ số)
```

abs là giá trị tuyệt đối: abs(-100) = 100. clamp01 giới hạn kết quả từ 0 đến 1. Roll riêng một số r trong [0, 1); hiệu ứng thành công nếu r nhỏ hơn xác suất cuối.

Với skill có xác suất gốc 50%:

| Accuracy | Dodge | Hệ số | Xác suất cuối |
|---:|---:|---:|---:|
| 0 | 0 | 1 | 50% |
| 100 | 100 | 1 | 50% |
| 100 | 0 | 1.5 | 75% |
| 0 | 100 | 0.5 | 25% |
| 200 | 0 | khoảng 1.667 | khoảng 83.33% |

Ví dụ accuracy 0, dodge 100: chênh lệch = -100; hệ số = 1 - 100/200 = 0.5; xác suất cuối = 50% × 0.5 = 25%.

Hệ quả của công thức:

- Điểm bằng nhau giữ nguyên xác suất gốc của skill.
- Xác suất gốc 0% vẫn là 0% dù accuracy cao.
- Xác suất gốc 100% vẫn có thể giảm nếu dodge cao hơn accuracy.
- Hệ số tăng tiến gần 2; skill có xác suất gốc thấp không tự đạt chắc chắn chỉ nhờ accuracy.
- Accuracy không tăng thời lượng hoặc sức mạnh hiệu ứng.
- Miễn nhiễm và hiệu ứng bắt buộc áp dụng cần luật riêng.

Mốc 100 ở đây là mốc cân bằng hiệu ứng, độc lập với mốc 100 của defense. Có thể điều chỉnh hai mốc riêng.

## 10. Buff và nâng cấp chỉ số

Quy tắc dự kiến cho maxHealth, damage, defense, moveSpeed, dodge và accuracy:

```text
Chỉ số cuối = (chỉ số gốc + tổng cộng thẳng) × (1 + tổng phần trăm thay đổi)
```

Ví dụ speed gốc 3, cộng 1, buff +20%, slow -30%:

```text
(3 + 1) × (1 + 0.2 − 0.3) = 3.6
```

Giới hạn kết quả: maxHealth tối thiểu 1; các điểm còn lại tối thiểu 0. Các buff phần trăm cộng với nhau, không nhân chồng từng buff. Chưa thiết kế luật riêng cho stun hoặc kháng slow.

Crit chance cộng theo điểm phần trăm: 20% + 5 điểm phần trăm = 25%; trong code là 0.2 + 0.05 = 0.25. Khác với tăng tương đối 5% của 20%, chỉ thành 21%.

Crit damage cộng trực tiếp vào hệ số: 1.5 + 0.25 = 1.75, tức từ tổng sát thương crit 150% lên 175%. Nội dung nâng cấp cần ghi rõ đơn vị.

Runtime nên giữ chỉ số gốc và modifier riêng, tính lại khi modifier thay đổi. Không liên tục cộng/trừ vào template khi bật/tắt buff. Các quy tắc buff này mới là thiết kế, chưa có code thực thi.

## 11. Ví dụ tính một đòn hoàn chỉnh

Bên đánh: damage 40, critChance 150%, critDamageMultiplier 1.5, accuracy 100. Skill: hệ số sát thương 2, cơ hội gây độc gốc 50%. Mục tiêu: defense 100, dodge 0, currentHealth 200.

```text
Sát thương thường = 40 × 2 = 80
150% crit → 50% bậc 1 hoặc 50% bậc 2

Bậc 1: 80 × 1.5 = 120 trước defense → mất 60 HP → còn 140 HP
Bậc 2: 80 × 2 = 160 trước defense → mất 80 HP → còn 120 HP

Gây độc roll riêng:
Chênh lệch = 100 − 0 = 100
Xác suất độc = 50% × (1 + 100 / 200) = 75%
```

Thứ tự xử lý: damage × hệ số skill → crit → defense → trừ HP. Sau đó xử lý hiệu ứng theo điều kiện của skill; ví dụ trên giả sử mục tiêu còn sống và không miễn nhiễm.

Crit không tự tăng xác suất độc. Độc mạnh bao nhiêu, kéo dài bao lâu và có cộng dồn không thuộc cấu hình hiệu ứng riêng.

Giữ phép tính sát thương bằng float, chỉ làm tròn để hiển thị. Chưa có luật tối thiểu 1 sát thương, hồi máu từ sát thương âm hoặc các cơ chế chưa thống nhất.

## 12. Template và runtime

- BaseStats và SkillData được sao chép theo giá trị, không sửa asset trong lúc chơi.
- SkillManager giữ level, cooldown và IDisposable riêng cho từng slot của từng nhân vật; cùng asset vẫn có trạng thái độc lập.
- ISkill trên asset chỉ chứa hành vi/cấu hình cố định, không giữ owner, target hoặc timer.
- Tắt tạm thời giữ trạng thái và ngừng đếm giờ; chết/despawn hủy đăng ký và thu hồi passive.
- Pool spawn lại tạo đăng ký mới. Handle cũ không điều khiển được lượt spawn mới.
- Nâng level skill hiện chỉ cập nhật level tới maxLevel; chưa tự tăng damage hoặc giảm cooldown.
## 13. Config level và save người chơi

PermanentLevelConfigSO, RunLevelConfigSO, StatBonus, RunLevelProgress và SaveManager đã được nối vào luồng spawn. Chi tiết EXP, nâng cấp vĩnh viễn và cộng bonus nằm trong [hướng dẫn chung DataSO](../README.md).
