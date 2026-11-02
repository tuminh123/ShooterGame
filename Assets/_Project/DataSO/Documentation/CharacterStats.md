# Hệ thống chỉ số và công thức tính

Tài liệu mô tả các chỉ số trong `StatBonus`, ý nghĩa, đơn vị và công thức tính cuối cùng.

## 0. Quy ước chung

- **Đơn vị phần trăm**: mọi chỉ số tỉ lệ (`critChance`, `critDamageMultiplier`, `effectHit`, `effectResistance`) dùng thang **100 = 100%**. Ví dụ `critChance = 25` nghĩa là 25%.
- **Không clamp trong struct**: `StatBonus` chỉ cộng dồn. Việc kẹp giá trị (trần/sàn) làm ở bước tính kết quả cuối cùng.
- **Thứ tự tính chỉ số**: `chỉ số cuối = base + tổng StatBonus` (từ level, item, skill...), rồi mới đưa vào các công thức bên dưới.
- Phép `+` cộng từng field, phép `* count` nhân bonus với số lần lên cấp.

| Chỉ số | Đơn vị | Loại | Ghi chú |
|---|---|---|---|
| `maxHealth` | điểm | cộng phẳng | HP tối đa, không tự hồi HP hiện tại |
| `damage` | điểm | cộng phẳng | Sát thương cơ bản |
| `defense` | điểm | cộng phẳng | Chuyển thành **giáp ảo** |
| `moveSpeed` | Unity unit/giây | cộng phẳng | Có trần |
| `critChance` | % | cộng phẳng | Có thể vượt 100 |
| `critDamageMultiplier` | % | cộng phẳng | Cộng thêm vào sát thương khi crit |
| `effectHit` (cũ: `accuracy`) | % | cộng phẳng | Tăng tỉ lệ gây debuff |
| `effectResistance` (cũ: `dodge`) | % | cộng phẳng | Giảm tỉ lệ bị dính debuff |

> Nên đổi tên `accuracy` thành `effectHit` và `dodge` thành `effectResistance`, vì cả hai chỉ áp dụng cho debuff. Trong game hành động, `dodge` dễ bị hiểu là né đòn.

---

## 1. maxHealth

HP tối đa. Cộng phẳng, không hồi máu hiện tại khi tăng.

```csharp
float maxHP = baseMaxHealth + bonus.maxHealth;
```

Khi `maxHealth` tăng, giữ nguyên `current` (hoặc kẹp `current <= max` khi max giảm).

---

## 2. damage

Sát thương cơ bản trước khi tính crit.

```csharp
float baseDamage = baseDamageStat + bonus.damage;
```

---

## 3. critChance và critDamageMultiplier

### Ý nghĩa

- `critChance`: tỉ lệ ra đòn chí mạng (%).
- `critDamageMultiplier`: phần sát thương **cộng thêm** khi crit (%).
- Khi `critChance` vượt 100%: tỉ lệ crit thật chỉ là 100%, phần dư được **quy đổi thành crit multiplier**. Mỗi **20%** dư cho **+5%** multiplier.

### Công thức

```
overflow        = max(0, critChance - 100)
bonusMultiplier = overflow / 20 * 5            (= overflow * 0.25)
finalChance     = min(critChance, 100)
finalMultiplier = critDamageMultiplier + bonusMultiplier

Khi crit:   damage = damage + damage * finalMultiplier / 100
Không crit: damage = damage
```

### Code

```csharp
public static class CritCalculator
{
    const float OverflowStep = 20f;   // mỗi 20% crit dư...
    const float BonusPerStep = 5f;    // ...cộng 5% crit multiplier

    public struct CritResult
    {
        public float chance;       // tỉ lệ crit thật (0..100)
        public float multiplier;   // crit multiplier cuối (đơn vị %)
    }

    public static CritResult Resolve(float critChance, float critMultiplier)
    {
        float overflow = Mathf.Max(0f, critChance - 100f);
        float bonusMult = overflow / OverflowStep * BonusPerStep;

        return new CritResult
        {
            chance = Mathf.Min(critChance, 100f),
            multiplier = critMultiplier + bonusMult
        };
    }

    public static float RollDamage(float damage, float critChance, float critMultiplier)
    {
        var crit = Resolve(critChance, critMultiplier);
        bool isCrit = Random.value * 100f < crit.chance;
        return isCrit ? damage + damage * crit.multiplier / 100f : damage;
    }
}
```

### Ví dụ

| critChance | critMultiplier gốc | Kết quả |
|---|---|---|
| 25 | 50 | 25% crit, multiplier 50 |
| 100 | 50 | 100% crit, multiplier 50 |
| 160 | 50 | 100% crit, dư 60 nên +15, multiplier 65, damage 100 thành 165 |

### Lưu ý cân bằng

- Cơ chế dư giúp `critChance` **không bao giờ vô ích** sau mốc 100%, nên không cần trần cứng.
- Dùng cách quy đổi **liên tục** (30% dư thì +7.5). Nếu muốn theo bậc thì dùng `Mathf.Floor(overflow / 20f) * 5f`, dễ hiển thị nhưng phí điểm lẻ.
- Sát thương kỳ vọng khi chance <= 100: `damage * (1 + chance/100 * mult/100)`.

---

## 4. defense (giáp ảo)

### Ý nghĩa

`defense` **không giảm sát thương**. Nó chuyển thành **giáp ảo** (shield): một lớp máu phụ hấp thụ sát thương trước, **mất hết là hết** (không hồi).

### Công thức

```
maxShield = defense * ShieldPerDefense     (khởi điểm: ShieldPerDefense = 1)

Khi nhận sát thương X:
  absorbed = min(shield, X)
  shield  -= absorbed
  health  -= (X - absorbed)
```

### Code

```csharp
public class Health
{
    public float current, max;
    public float shield, maxShield;

    public void TakeDamage(float amount)
    {
        float absorbed = Mathf.Min(shield, amount);
        shield -= absorbed;
        current -= amount - absorbed;   // phần dư mới trừ vào máu
    }
}
```

### Lưu ý cân bằng

- Giáp không hồi nên giá trị gần bằng HP: bắt đầu với **1 giáp ≈ 1 HP** (hoặc thấp hơn một chút).
- Vì là số phẳng nên cân bằng giống `maxHealth`, không cần công thức giảm sát thương.
- Nếu sau này muốn giáp thú vị hơn, có thể chọn một trong các hướng: hồi đầy đầu mỗi màn/wave, hồi sau X giây không bị đánh, hoặc chỉ nhặt thêm. Nếu giáp có thể hồi thì tăng giá trị của nó lên (khoảng 1 giáp = 1.5 HP).

---

## 5. moveSpeed

```csharp
float speed = Mathf.Min(baseSpeed + bonus.moveSpeed, baseSpeed * MaxSpeedRatio); // MaxSpeedRatio ~ 2
```

Luôn có **trần cứng** (khoảng 2 lần tốc độ gốc), vì tốc độ quá cao làm hỏng camera, va chạm và cảm giác điều khiển.

---

## 6. effectHit và effectResistance (chỉ áp dụng cho debuff)

### Ý nghĩa

- Mỗi debuff/skill có `baseChance` riêng (ví dụ choáng 50%, làm chậm 70%, độc 100%).
- `effectHit` của người gây: tăng tỉ lệ dính debuff.
- `effectResistance` của người nhận: giảm tỉ lệ dính debuff.

### Công thức (khuyên dùng: nhân)

```
resCapped = min(effectResistance, 80)
chance    = baseChance * (1 + effectHit / 100) * (1 - resCapped / 100)
chance    = clamp(chance, 0, 100)
```

```csharp
public static float EffectChance(float baseChance, float effectHit, float effectRes)
{
    float resCapped = Mathf.Min(effectRes, 80f);   // kháng tối đa 80%
    float chance = baseChance * (1f + effectHit / 100f) * (1f - resCapped / 100f);
    return Mathf.Clamp(chance, 0f, 100f);
}

public static bool Roll(float baseChance, float effectHit, float effectRes)
{
    return Random.value * 100f < EffectChance(baseChance, effectHit, effectRes);
}
```

### Ví dụ

| baseChance | effectHit | effectResistance | Kết quả |
|---|---|---|---|
| 50 | 20 | 30 | 50 x 1.2 x 0.7 = **42%** |
| 50 | 0 | 80 | 50 x 1 x 0.2 = **10%** |
| 80 | 50 | 0 | 80 x 1.5 = 120, kẹp còn **100%** |

### Vì sao dùng nhân thay vì cộng/trừ

Cách cộng trừ (`base + hit - res`) dễ tính nhẩm nhưng: debuff gốc 50% mà kháng 50 thì về 0%, kháng cao làm mọi debuff yếu vô dụng. Cách nhân giảm **theo tỉ lệ**, không bao giờ trừ về 0 đột ngột.

### Lưu ý cân bằng

- Đặt trần kháng khoảng **75-80%**.
- Giữ `baseChance` của debuff mạnh ở mức **40-70%**. Nếu gốc quá cao (80-100%) thì `effectHit` bị phí vì chạm trần 100%.
- **Boss** nên có kháng/miễn nhiễm khống chế riêng, không dựa vào chỉ số chung.
- **Chống khống chế liên tục**: sau khi bị choáng thì miễn choáng 3-5 giây, hoặc mỗi lần bị khống chế liên tiếp giảm thời gian.
- Debuff gây sát thương (độc, cháy) có thể dùng kháng để giảm thời gian/sát thương thay vì quyết định trúng hay không.

---

## 7. Gợi ý quy trình cân bằng

1. **Chọn mốc TTK** (thời gian giết): ví dụ người chơi giết quái thường trong 2-3 giây, quái giết người chơi trong 10-15 giây. Từ đó suy ra `damage` và `maxHealth` theo level.
2. **Quy về hai đại lượng**: DPS (sát thương mỗi giây, gồm crit) và EHP (máu + giáp ảo). Mọi chỉ số nên được đánh giá bằng đóng góp vào hai thứ này.
3. **Lập bảng tính** (Google Sheets): cột level, tổng `StatBonus`, DPS, EHP. Vẽ biểu đồ để thấy chỉ số nào tăng quá nhanh.
4. **Ngân sách điểm**: mỗi level/item có số điểm cố định để chia cho các chỉ số. Chỉ số dễ vỡ cân bằng (crit, tốc độ, kháng) cho giá cao hơn.
5. **Mô phỏng**: viết script giả lập hàng nghìn trận giữa build mạnh nhất và yếu nhất ở cùng level để kiểm tra chênh lệch.
6. **Đưa hệ số vào ScriptableObject**: `OverflowStep`, `BonusPerStep`, `ShieldPerDefense`, trần kháng, trần tốc độ... để chỉnh mà không phải sửa code.

---

## 8. Bảng tổng hợp hệ số cần chỉnh

| Hệ số | Giá trị khởi điểm | Dùng ở |
|---|---|---|
| `OverflowStep` | 20 (%) | Crit dư |
| `BonusPerStep` | 5 (%) | Crit dư |
| `ShieldPerDefense` | 1 | Defense thành giáp |
| `MaxEffectResistance` | 80 (%) | Trần kháng debuff |
| `MaxSpeedRatio` | 2 | Trần tốc độ |
| `baseChance` debuff | 40-70 (%) | Từng skill/debuff |

## 9. Ranh giới triển khai trong project

- `CharacterDataSO.BaseStats` giữ chỉ số gốc; `range` vẫn là bán kính tìm mục tiêu, không có bonus range trong đợt này.
- `CharacterRuntimeData` giữ danh tính, base/final stats, HP và giáp hiện tại; chỉ tính damage đầu ra, không xử lý nhận đòn hoặc tiến trình lên cấp.
- `CharacterControllerBase` xử lý gây/nhận damage, hồi/trừ HP và chết. `PlayerController` và `EnemyController` kế thừa; `CharacterView` chuyển tiếp lời gọi và hiển thị.
- `CharacterLevelService` và `RunLevelService` giữ level/EXP. `PlayerManager` cộng bonus bên ngoài rồi truyền tổng vào runtime; không sao chép level trong màn vào runtime.
- `CharacterEffectHandler` giữ danh sách slow, token và luật slow mạnh nhất. Runtime chỉ áp dụng hệ số di chuyển đã được Effect tính.
- Spawn mới khởi tạo đầy HP và giáp; bonus skill được áp dụng qua scheduler; defense được cấp thêm chỉ bổ sung lượng giáp mới tương ứng. Tắt/bật tạm thời không hồi giáp. Tăng maxHealth hoặc defense trong khi chơi không tự hồi HP hoặc giáp; giảm giới hạn thì kẹp giá trị hiện tại.
- Đòn chỉ làm mất giáp vẫn là đòn đánh được chấp nhận và phát thông báo damage. `DamageResult` có `HealthLost`, `ShieldLost`, `TotalLost`; `DamageInfo.IsCritical` thay cho bậc crit cũ.
- Các asset hiện có đã chuyển crit chance từ 0..1 sang phần trăm, crit multiplier từ tổng hệ số sang phần trăm cộng thêm, và application chance của effect từ 0..1 sang 0..100. `slowFraction` vẫn là phần tốc độ bị giảm trong 0..1.
- Hệ số đang triển khai: shield/defense = 1; speed cap/base = 2; resistance cap = 80%; crit overflow = 0.25. Gợi ý đưa chúng vào SO ở phần trên là hướng mở rộng, chưa thêm SO cấu hình mới trong đợt này.
- Mỗi CharacterView sở hữu CharacterSkills để cập nhật cooldown skill vốn có. SkillManager chung độc lập với nhân vật, chỉ giữ và cập nhật skill do nguồn bên ngoài truyền vào; chưa nối nguồn thẻ bài, vị trí hoặc chỉ số của skill trong trận. SkillFactory chỉ tạo behavior ISkill riêng; từng behavior giữ chủ nhân và tự thực hiện logic của nó. Cấu hình skill vốn có nằm trong danh sách Skills trên CharacterDataSO; không có bridge; hai phạm vi được cập nhật riêng.
