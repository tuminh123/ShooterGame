# Hướng dẫn chung DataSO, level và save người chơi

> Trạng thái hiện tại: hệ thống trang bị (túi đồ, 6 ô mặc, độ hiếm, dòng phụ, set, skill đồ) đã chạy. Thiết kế và luật đầy đủ xem `Docs/Architecture/Equipment-Tasks.md`.

## 1. Phân chia dữ liệu

| Loại | Script | Trách nhiệm |
|---|---|---|
| Template nhân vật | CharacterDataSO | Type làm khóa, tên hiển thị, chỉ số gốc và skill mẫu |
| Level vĩnh viễn | CharacterLevelConfigSO | Bonus cố định mỗi cấp và thanh EXP nâng cấp bằng nguyên liệu |
| Level trong trận | RunLevelConfigSO | Công thức EXP tuyến tính và một bộ bonus cố định mỗi cấp |
| Danh mục nhân vật | CharacterDatabaseSO | Tất cả template nhân vật, kiểm tra ID khi dựng dictionary |
| Danh mục level | LevelDatabaseSO | Danh sách config vĩnh viễn có type và config trận dùng chung |
| Level nhân vật | CharacterLevels (miền Progression) | Tra config, đọc level vĩnh viễn, tăng cấp và gọi save |
| Level trận | RunLevel (miền Run, do RunManager tạo) | Nhận EXP, phát LevelUp/ExperienceChanged, giữ nguồn bonus level trận trên Player |
| Trạng thái level trận | RunLevelProgress | Chi tiết tính tiến trình nội bộ do RunLevel sở hữu |
| Dữ liệu lưu lâu dài | PlayerSaveData | Nhân vật đang chọn và level vĩnh viễn của từng nhân vật |
| IO hồ sơ | SaveManager | Đọc/ghi Easy Save 3 theo key, giữ snapshot chưa ghi được để retry |
| Runtime nhân vật | CharacterRuntimeData | Chỉ số gốc/cuối, HP và giáp hiện tại riêng của từng nhân vật; không lưu level/EXP |
| Danh mục trang bị | EquipmentDatabaseSO (+ EquipmentDataSO, EquipmentRulesSO) | Mẫu đồ, set, luật tăng trưởng dùng chung, đồ khởi đầu |
| Túi đồ / đồ đang mặc | EquipmentInventory, EquipmentLoadout (miền Progression) | Bản đồ đang sở hữu (key `equipment_items`), ô → bản đồ (key `equipment_slots`), tổng bonus, set đang bật |

PlayerSystem tạo CharacterRuntimeData từ template, bonus level và loadout đã load. Player sử dụng runtime làm nguồn chỉ số. Trang bị hiện có danh mục ID/bonus và danh sách đang mặc chung cho người chơi; chưa có inventory, slot, mở khóa hoặc nâng cấp vật phẩm. Skin, tiền tệ và EXP vĩnh viễn chưa được thêm. Hai config không tự thực thi skill.

## 2. CharacterType là khóa duy nhất

Không còn ID dạng string. CharacterType là enum làm khóa nhận diện chung cho template, config level, dictionary, sự kiện và dữ liệu save. DisplayName là chuỗi chỉ để hiển thị; đổi tên nhân vật hoặc asset không ảnh hưởng tiến trình.

Ví dụ hiện tại:

```csharp
public enum CharacterType
{
    None = 0,
    BlessedGladiator = 1
}
```

Khi thêm nhân vật mới:

1. Thêm giá trị vào CharacterType với số mới chưa dùng. Không đổi số hoặc tái sử dụng số của nhân vật đã có save.
2. Tạo CharacterDataSO, chọn type đó và nhập Display Name.
3. Tạo CharacterLevelConfigSO cùng type, cấu hình bonus và thanh EXP nâng cấp.
4. Thêm template vào CharacterDatabaseSO và config vào LevelDatabaseSO.

Không cần thêm string ID hoặc sửa thuật toán ghép. Enum chỉ khai báo loại nhân vật; vẫn cần tạo và đăng ký dữ liệu của nhân vật mới.

CharacterDataSO có FormerlySerializedAs cho field characterId cũ để bảo toàn chuỗi thành displayName khi Unity đọc asset cũ. Đây chỉ là đổi field asset, không phải migration file save.

Khi init, một lượt duyệt config dựng type → config, rồi một lượt duyệt template ghép cùng type. Các dictionary runtime đều dùng CharacterType, độ phức tạp khởi tạo O(số config + số nhân vật), tra cứu trung bình O(1). Không tham chiếu character asset từ level config và không gán lại config vào template.

None là trạng thái chưa chọn, không dùng cho template, config hoặc bản ghi tiến trình. Mỗi template dùng type riêng; type trùng/không hợp lệ hoặc thiếu config khi ghép sẽ báo lỗi. Hai database là danh mục game, không phải danh sách sở hữu của người chơi.

## 3. StatBonus khác BaseStats

BaseStats là bộ chỉ số gốc đầy đủ. StatBonus chỉ là lượng cộng thêm vào từng field, mặc định tất cả bằng 0. Không dùng BaseStats.Default để biểu diễn bonus vì nó có sẵn 100 HP, 10 damage, 5% crit chance và 50% sát thương cộng thêm khi crit. Công thức chuẩn xem [CharacterStats](Documentation/CharacterStats.md).

Ví dụ bonus một lần lên cấp:

```text
maxHealth = 20
damage = 2
critChance = 5            → cộng 5 điểm phần trăm
critDamageMultiplier = 10 → bonus sát thương crit từ 50% thành 60%
Các field còn lại = 0
```

Bonus hiện chỉ cộng thẳng. Có thể nhập số âm nếu cần thiết kế đánh đổi; dữ liệu phải hữu hạn. ApplyTo chỉ cộng giá trị; runtime kẹp kết quả cuối: maxHealth ít nhất 1, tốc độ tối đa 2 lần tốc độ gốc, effectResistance tối đa 80%, các chỉ số khác không âm. Crit chance dư 100% đổi sang bonus sát thương crit theo hệ số 0.25.

Khi có nhiều nguồn, cộng các StatBonus trước rồi ApplyTo một lần lên chỉ số gốc. Không ApplyTo lặp lại trên kết quả cũ khi tính lại chỉ số.

## 4. Level vĩnh viễn: bonus cố định, thanh EXP nâng cấp bằng nguyên liệu

Mỗi nhân vật có thể có CharacterLevelConfigSO riêng. Một config chứa:

| Field trong Inspector | Ý nghĩa | Giá trị mẫu |
|---|---|---:|
| Character Type | Nhân vật cụ thể dùng config này, khớp type trên template | BlessedGladiator |
| Max Level | Level vĩnh viễn tối đa | 10 |
| Bonus Per Level | Một bộ chỉ số cộng thêm cho mỗi lần lên cấp | +20 HP, +2 damage, ... |
| Clicks Per Level | Số lần bấm Upgrade để đầy thanh một mốc | 4 |

Gold cần cho mỗi mốc (độ dài thanh) **không** nằm trong config này mà nằm chung trong **LevelDatabaseSO → Permanent Level Cost**, theo độ hiếm của nhân vật (xem mục 4b). Enemy/Boss cũng dùng CharacterLevelConfigSO nhưng chỉ đọc Max Level và Bonus Per Level (bonus theo wave); Clicks Per Level bỏ qua với quái.

Không còn danh sách Element 0/1/2. Level 1 dùng chỉ số gốc, nên:

```text
Bonus tích lũy ở level L = Bonus Per Level × (L − 1)
Chỉ số nhân vật = chỉ số gốc + bonus tích lũy
```

Ví dụ HP gốc 100, bonus mỗi cấp 20: level 1 có 100 HP; level 2 có 120; level 5 có 180; level 10 có 280. Mỗi nhân vật có thể dùng bonus khác nhau, cùng công thức.

Nâng cấp vĩnh viễn dùng **cùng cơ chế với thanh EXP trong trận**, chỉ thay EXP bằng nguyên liệu:

```text
Gold cần để đầy thanh từ level L lên L+1 = Permanent Level Cost (công thức × hệ số độ hiếm của nhân vật)
Giá mỗi lần bấm = Gold cần cho mốc ÷ Clicks Per Level (làm tròn lên); lần cuối của mốc chỉ tốn phần còn thiếu
Mỗi lần bấm: tiêu giá đó → nạp đúng lượng đó vào thanh; đầy thanh → lên cấp (một lần bấm không tràn sang mốc sau)
```

Số lần bấm mỗi mốc luôn bằng nhau; **giá trên nút tăng theo level và độ hiếm** (Clicks Per Level = 4, Linear 100 / 50):

| Chuyển cấp | Common ×1 (cần → mỗi lần) | Rare ×2 | Legendary ×5 |
|---|---:|---:|---:|
| 1 → 2 | 100 → 25 | 200 → 50 | 500 → 125 |
| 2 → 3 | 150 → 38 (lần cuối 36) | 300 → 75 | 750 → 188 (lần cuối 186) |
| 10 → 11 | 550 → 138 (lần cuối 136) | 1,100 → 275 | 2,750 → 688 (lần cuối 686) |

Thiếu tiền thì nút vẫn bấm được nhưng rung và báo "Not enough Gold" (`NotEnoughCurrency`). Tiền, level và điểm được ghi trong một lần save, nên lỗi IO không làm mất tiền mà không lên cấp. Tham số phải thỏa MaxLevel >= 1, Clicks Per Level >= 1; giá vượt int sẽ báo lỗi tràn số thay vì trả số âm.

GetTotalBonus(level) tính tổng bằng phép nhân struct, không duyệt bảng. Nếu save cao hơn MaxLevel mới, chỉ tính bonus tới trần hiện tại, không tự sửa level trong save. Level dưới 1 bị từ chối.

Save giữ `permanentLevel` và `upgradePoints` (Gold đã nạp vào thanh của cấp hiện tại) cho từng CharacterType. Save cũ không có `upgradePoints` được đọc là 0. Khi load, nếu công thức bị chỉnh khiến điểm đã lưu vượt thanh hiện tại thì điểm được ép về trong thanh (ở trần thì về 0) thay vì báo lỗi.

## 4b. Công thức "lượng cần cho mốc cấp" (`ILevelFormula`)

Mọi chỗ "mỗi mốc Lv → Lv+1 cần bao nhiêu" dùng chung một họ công thức (`Scripts/Progression/Formulas/`), chọn trong Inspector bằng danh sách:

| Công thức | Tham số | Lượng cần ở level L | Ví dụ |
|---|---|---|---|
| `LinearFormula` | Base, Step | `Base + Step × (L − 1)` | 100, 150, 200, ... |
| `GeometricFormula` | Base, Multiplier | `Base × Multiplier^(L − 1)`, làm tròn lên | 100, 115, 133, ... (×1.15) |

| Nơi dùng | Asset → field | Theo độ hiếm? |
|---|---|---|
| Gold nâng level vĩnh viễn nhân vật | LevelDatabase → Permanent Level Cost | Có, `CharacterRarity` của CharacterDataSO |
| Gold nâng cấp trang bị | EquipmentRules → Level Up Cost | Có, `EquipmentRarity` của món |
| EXP lên cấp trong trận | RunLevelConfig → Experience Formula | Không |

Có độ hiếm thì lượng cần = `công thức × hệ số của độ hiếm`, làm tròn lên (`RarityScaledFormula`). Ví dụ Linear Base 10, Step 5; Common ×1, Legendary ×5 → Lv1 → 2: Common cần 10, Legendary cần 50. Tỉ lệ giữa các độ hiếm giữ nguyên ở mọi mốc nên dễ cân bằng. **Hệ số phải tăng dần** theo độ hiếm (độ hiếm cao không được rẻ hơn), sai thì validate báo lỗi. Inspector có bảng **Preview** hiện lượng cần của các mốc cho từng độ hiếm.

Thêm cách tính mới để cân bằng: viết một class `[Serializable]` có constructor mặc định, implement `ILevelFormula` (đặt trong `LevelFormulas.cs`); nó tự xuất hiện trong danh sách chọn, code nơi dùng không phải sửa.

Asset cũ: các field A/B (`initialRequired`, `requiredIncreasePerLevel`, `initialExperience`, `experienceIncreasePerLevel`, `levelUpCostBase`, `levelUpCostPerLevel`) đã bỏ; các asset mẫu được điền lại bằng Linear với đúng số cũ, nên game chạy như trước với độ hiếm Common.

## 4c. Bộ sưu tập nhân vật: sở hữu, mảnh theo độ hiếm, ghép, bản trùng

Cấu hình ở **LevelDatabase → CHARACTER COLLECTION** (chung mọi nhân vật cùng độ hiếm; độ hiếm đọc từ CharacterDataSO.Rarity):

| Field | Ý nghĩa | Mẫu (Common / Uncommon / Rare / Epic / Legendary) |
|---|---|---|
| Starter Characters | Nhân vật sở hữu sẵn từ đầu, không cần ghép | BlessedGladiator |
| Collection Rules → Unlock Shards | Mảnh cùng độ hiếm để **ghép** nhân vật chưa có | 30 / 45 / 75 / 120 / 180 |
| Collection Rules → Shards Per Copy | Mảnh nhận khi **phân rã** một bản trùng; cũng là giá trị một bản trùng khi nâng sao | 10 / 15 / 25 / 40 / 60 |
| Star Cost | Mảnh để mở sao thứ n (`ILevelFormula` × hệ số độ hiếm) | Linear 10 / 10 × 1 · 1.5 · 2.5 · 4 · 6 → Common: 10, 20, 30, 40, 50 |

- **Mảnh là tiền tệ** (`CurrencyType.ShardCommon..ShardLegendary`), nằm trong ví. Icon + tên hiển thị ở CharacterScreenTheme → Currencies.
- **Gacha** (`ICharacterCollection.Grant`): chưa có → sở hữu luôn; đã có → +1 bản trùng.
- **Ghép**: thẻ chưa sở hữu đủ mảnh thì sáng lên, chạm vào là trừ Unlock Shards và sở hữu.
- **Phân rã**: nút DISMANTLE ở màn Collection → popup, mỗi nhân vật có bản trùng một dòng, bấm để đổi hết bản trùng thành mảnh.
- **Nâng sao**: dùng bản trùng của chính nhân vật trước (mỗi bản = Shards Per Copy mảnh, phần dư vào ví), thiếu thì trừ mảnh. Level vẫn trả Gold.
- Nhân vật chưa sở hữu: thẻ xám (material `Shaders/UIGrayscale.mat`), thanh mảnh thay level/sao; Detail chỉ xem (ẩn nút chọn và nâng cấp).
- Save: `owned`, `copies` trong `character_levels` (cùng Entry với level/sao). Save cũ đọc ra chưa sở hữu, 0 bản trùng; nhân vật khởi đầu luôn tính là đã có.
- Thêm nhân vật Player mới: thêm template vào CharacterDatabase **và** một CharacterLevelConfigSO vào LevelDatabase (bắt buộc, thiếu thì báo lỗi khi vào game).

## 5. Level trong trận: bonus cố định và công thức EXP

RunLevelConfigSO được gán một lần vào LevelDatabaseSO và dùng chung cho level người chơi trong trận:

| Field trong Inspector | Ý nghĩa | Giá trị mẫu |
|---|---|---:|
| Max Level | Level tối đa của lượt chơi | 10 |
| Bonus Per Level | Bộ chỉ số cộng y nguyên mỗi lần lên cấp | +5 HP, +1 damage |
| Experience Formula | Công thức EXP cho mỗi mốc (xem mục 4b) | Linear 10 / 5 |

```text
EXP từ level L lên L+1 = Experience Formula (mẫu: Linear Base 10, Step 5 → 10 + 5 × (L − 1))
Bonus tích lũy = Bonus Per Level × (L − 1)
```

| Chuyển cấp | EXP cần riêng (Linear Base 10, Step 5) |
|---|---:|
| 1 → 2 | 10 |
| 2 → 3 | 15 |
| 3 → 4 | 20 |
| 4 → 5 | 25 |
| 9 → 10 | 50 |

Không phải mốc EXP cộng dồn. Từ level 1 nhận 35 EXP: tiêu 10 để lên level 2, tiêu 15 để lên level 3, còn 10 EXP. Cần thêm 10 EXP nữa để lên level 4.

RunLevelProgress.AddExperience trả số cấp đã tăng, giúp UI sau này mở đúng số lượt lựa chọn nâng cấp. Hiện chưa có UI chọn thưởng hoặc tự thực thi skill. Bonus cố định của mỗi lần lên cấp là cùng một bộ, không tăng theo công thức chi phí.

Nếu muốn chỉ nhận chỉ số qua lựa chọn, đặt Bonus Per Level về 0. Không cấu hình cùng phần thưởng ở cả bonus cố định và lựa chọn nếu không muốn nhận hai lần.

Quy tắc runtime:

- Bắt đầu level 1, EXP 0; Reset trả về trạng thái đó.
- EXP dư được giữ để lên cấp tiếp theo, có thể lên nhiều cấp trong một lần nhận.
- Đạt MaxLevel thì bỏ EXP dư và ngừng nhận thêm EXP.
- EXP nhận âm bị từ chối; cấu hình yêu cầu A >= 1 và B >= 0.
- Công thức dùng số nguyên; nếu yêu cầu EXP vượt int thì báo overflow.
- Level/EXP trong trận không ghi vào PlayerSaveData.
- Không sửa config giữa trận. RunLevel sở hữu một RunLevelProgress của người chơi trong lượt hiện tại; miền khác chỉ đọc qua `IRunReader` của RunManager, không tạo thêm tiến trình song song.

## 6. PlayerSaveData lưu những gì

```text
version: 2
selectedCharacterType: 1
equippedItemIds: []
characters:
    characterType: 1, permanentLevel: 5
```

File chỉ chứa hồ sơ người chơi. Không lưu chỉ số tổng, currentHealth, cooldown, skill instance, template SO, level/EXP trong trận, enemy hoặc boss đang tồn tại.

Không có bản ghi cho một nhân vật thì GetPermanentLevel trả 1. Việc đọc không tự thêm bản ghi. Danh sách tiến trình không phải danh sách mở khóa nhân vật; cơ chế mở khóa chưa được triển khai. SelectCharacter lưu type đang chọn, không tự tạo tiến trình hoặc xác nhận template đã được đăng ký trong database.

Ví dụ trên là schema 2 cũ. Hiện dùng schema 4 tách theo key; SaveMigration chuyển schema 2/3 sang cấu trúc mới và giữ key cũ. Enum vẫn lưu bằng giá trị số, không đổi các ID đã phát hành. Schema 1 không được hỗ trợ, bị chặn ghi đè. Service sở hữu từng phần kiểm tra dữ liệu trước khi dùng.

equippedItemIds là field tùy chọn bổ sung cho schema 2: file cũ thiếu field này được đọc như loadout rỗng. ID trang bị phân biệt hoa/thường, không rỗng, không trùng và phải có trong EquipmentDatabase. Save không lưu bonus tính sẵn. Nếu database thiếu ID đã lưu, bước tạo runtime báo lỗi rõ ràng thay vì bỏ mất bonus âm thầm.

## 7. SaveManager, Easy Save và Zenject

SaveManager (miền Save) chỉ xử lý lưu trữ: `ISaveStore` (Load<T>(key, defaultData, validate), Save<T>, SaveBatch) và `ISaveMaintenance` (SaveIfDirty, ResetData). Wallet, CharacterLevels, EquipmentLoadout, StageProgress (bên trong ProgressionManager) và PlayerSystem sở hữu dữ liệu và quyết định thời điểm lưu; mỗi section tự kiểm tra bằng `XxxSaveData.Validate`. Thứ tự khởi tạo: SaveManager (-200, mở file rồi chạy SaveMigration ngay bên trong) → ProgressionManager (-160) → CharacterManager (-150) → gameplay.

Mặc định dùng chung file `PlayerSave.es3`, các key `player_selection`, `character_levels`, `equipment_items`, `equipment_slots`, `currencies`, và marker `save_format` phiên bản 4. Hằng số ở GameConstants.SaveKeys. `player_data` chỉ còn phục vụ migration. Field Player Save File trên installer cho phép đổi tên file; tên khác không tự di chuyển save cũ.

Easy Save dùng Location.File và Directory.PersistentDataPath; đường dẫn dựa trên thư mục dữ liệu ứng dụng của Unity. Không ghi vào Assets. Trên nền tảng plugin chuyển File sang PlayerPrefs, hành vi tuân theo Easy Save.

SaveMigration ghi các section mặc định cho hồ sơ mới, hoặc chuyển hồ sơ cũ; marker phiên bản ghi sau cùng. Nếu đọc hoặc migration lỗi, IsLoaded = false và ghi bị khóa để bảo vệ file. ResetSaveButton xóa save rồi reload scene nếu người dùng chủ động reset.

Các service cập nhật RAM rồi gửi bản sao độc lập để lưu ngay. Save trả false khi IO lỗi; SaveManager giữ snapshot mới nhất theo key để retry. Retry ghi snapshot, không phát lại thao tác cộng tiền/tăng cấp. Không reload key còn pending.

Installer gọi SaveIfDirty khi pause, mất focus và thoát; Dispose cũng thử ghi phần pending. Không bảo đảm ghi được nếu ứng dụng bị kill/crash hoặc thiết bị còn lỗi IO. Chưa có backup/cloud hay nhiều profile.

LootSystem nhặt vàng → RunManager gọi IWallet.Add(CurrencyType.Gold, amount) → lưu key currencies ngay. `IRunReader.Gold` (RunStatistics) chỉ thống kê vàng trong trận; kết thúc trận không cộng lại. Kim cương dùng CurrencyType.Diamond trong cùng ví.

Service hiện có scope theo scene. Nếu sau này nhiều scene cùng cần một hồ sơ đang dùng, chuyển binding sang ProjectContext để có một instance chung; không bind lại đồng thời ở cả ProjectContext lẫn SceneContext.

## 8. Level vĩnh viễn (Progression) và level trận (Run)

```text
Gameplay/UI
    → ProgressionManager (IWallet, IPermanentBonuses, IStageProgress)   dữ liệu giữa các lượt (GĐ 5)
        → CharacterLevels
            → dictionary template/config
            → ISaveStore (SaveManager → Easy Save 3)
        → EquipmentLoadout, Wallet, StageProgress
    → RunManager (IRunReader, IRunLifecycle)   level trong trận (miền Run, GĐ 4)
        → RunLevel
            → RunLevelProgress
            → RunLevelConfigSO chung (LevelDatabaseSO.RunLevelConfig)
```

Miền khác chỉ thấy level vĩnh viễn qua `IPermanentBonuses.GetBonus(type)` (level vĩnh viễn + trang bị, dùng bởi PlayerSystem)
và `BonusesChanged`. Màn Character nâng cấp qua `ICharacterUpgrades` (`GetQuote`/`TryUpgrade`, trong `ProgressionContracts.cs`);
`UpgradeQuote` mang cả tiến độ thanh (`Progress`/`ProgressRequired`). Các API dưới đây nằm trên `CharacterLevels` bên trong ProgressionManager.

### API level vĩnh viễn (trên CharacterLevels)

| API | Tác dụng |
|---|---|
| GetConfig(type) | Tra config vĩnh viễn |
| GetLevel(type) | Đọc level vĩnh viễn |
| GetTotalBonus(type) | Tính tổng bonus vĩnh viễn |
| GetUpgradePoints(type) | Gold đã nạp vào thanh của cấp hiện tại |
| TryGetRequiredPoints(type, out required) | Gold cần để đầy thanh cấp hiện tại (công thức × hệ số độ hiếm); false khi đạt trần |
| TryAddUpgradePoints(type, amount, costs, out saved) | Trả `costs`, nạp điểm, lên cấp khi đầy thanh; tiền + level + điểm ghi trong một lần save; phát LevelChanged chỉ khi lên cấp |
| SaveProgress() | Thử lưu lại hồ sơ, không nạp thêm điểm |

Chỉ gọi API đọc/thay đổi save sau ProgressionManager.Initialize. Không đọc tiến trình trong constructor của class khác.

TryIncreaseLevel chưa tự kiểm tra sở hữu, mở khóa hoặc trừ tài nguyên. Bên gọi xử lý điều kiện/thanh toán bằng Wallet trước khi tăng cấp.

Nếu IO lỗi, level mới còn trong RAM và snapshot vẫn chờ ghi. CharacterLevels phát LevelChanged cho thay đổi thực tế, rồi SaveFailed để bên ngoài biết chưa ghi được file. Không rollback level một cách âm thầm.

### API level trong trận (miền Run)

| API trên RunManager | Tác dụng |
|---|---|
| `IRunLifecycle.ResetRun()` | GameManager gọi: dọn skill gameplay, loot, về level 1/EXP 0, đồng hồ và thống kê về 0, rồi phát RunReset |
| `IRunReader.Level` | Level hiện tại trong trận |
| `IRunReader.Experience` | EXP dư |
| `IRunReader.ExperienceToNextLevel` | EXP cần riêng cho cấp hiện tại, 0 khi đạt trần |

EXP chỉ vào qua loot (LootSystem → RunManager → RunLevel.AddExperience); không có API cộng EXP công khai cho miền khác.
Bonus level trận (`RunLevelConfigSO.GetTotalBonus(level)`) là **một nguồn riêng** trong `StatSheet` của Player:
RunLevel thêm nguồn này khi nhận `PlayerChanged` và cập nhật lease tại chỗ khi lên cấp/reset.
PlayerSystem chỉ cộng level vĩnh viễn + trang bị. Tổng cuối vẫn do `StatCalculator` tính một chỗ.

### Sự kiện

| Event | Nguồn | Tham số |
|---|---|---|
| BonusesChanged | IPermanentBonuses | Không tham số (level vĩnh viễn của một nhân vật hoặc loadout vừa đổi) |
| BalanceChanged | IWallet | loại tiền, số dư mới |
| LevelUp | IRunReader | level cũ, level mới |
| ExperienceChanged | IRunReader | EXP dư, EXP cần cấp tiếp |
| RunReset | IRunReader | Không tham số |

Một lần nhận EXP lên nhiều cấp phát một LevelUp chứa khoảng level. Ví dụ từ 1 lên 3: sự kiện (1, 3), UI có thể mở 2 lượt chọn thưởng. Tính toán và bonus được cập nhật trước khi phát, nên đọc RunManager trong callback sẽ thấy trạng thái cuối.

Reset không phát LevelUp; phát ExperienceChanged (cùng Gold/Time/EnemiesDefeated changed) rồi RunReset sau cùng. Nhận EXP khi đã đạt trần hoặc không thay đổi tiến trình không phát lại thông báo EXP.

Bên nghe tự hủy đăng ký khi không dùng nữa. Không cộng EXP/reset tiếp bên trong callback của chính chuỗi xử lý đó; RunLevel và RunManager chặn thay đổi lồng nhau.

Level trận không gọi save lâu dài. Muốn tiếp tục một lượt đã thoát sẽ cần loại save lượt chơi riêng, chưa triển khai.

### Save snapshot và dictionary

Service giữ dữ liệu đang dùng và gửi Clone() vào SaveManager.Save; snapshot pending không thay đổi theo dữ liệu runtime.

CharacterLevels giữ dictionary CharacterType → tiến trình/template/config; Wallet giữ dictionary CurrencyType → số dư. SaveManager chỉ giữ dictionary key → snapshot còn lỗi ghi. Cache không được lưu lặp vào file.

## 9. Thư mục và bộ dữ liệu mẫu

```text
DataSO/
    README.md
    Documentation/
        CharacterStats.md
        GameplayServices.md
    Databases/
        CharacterDatabase.asset
        LevelDatabase.asset
        EquipmentDatabase.asset
    Characters/
        BlessedGladiator_Character.asset
    Levels/
        Permanent/
            BlessedGladiator_PermanentLevels.asset
        Run/
            Shared_RunLevels.asset
    Maps/
        MapDatabase.asset
    SaveExamples/
        PlayerSave.example.json
```

Đây là một file hướng dẫn chung. Khi thêm DataSO mới, không tạo Markdown riêng cho từng asset.

CharacterDatabase mẫu chứa BlessedGladiator_Character. LevelDatabase mẫu chứa BlessedGladiator_PermanentLevels trong danh sách và tham chiếu Shared_RunLevels dùng chung. Cả template, prefab và config vĩnh viễn đều chọn type BlessedGladiator (giá trị enum 1). DisplayName mẫu là Blessed Gladiator. Template và config không tham chiếu trực tiếp lẫn nhau. EquipmentDatabase mẫu để rỗng, đã gán vào SampleScene; chưa mặc trang bị thì bonus bằng 0.

Khi tự setup, gán Character Database, Level Database và Equipment Database trên GameSceneInstaller. Cả ba field đều bắt buộc. Không phải gán config trận lên từng nhân vật. Prefab Player khai báo CharacterType để kiểm tra khớp với nhân vật trong save; installer hiện chỉ hỗ trợ một prefab. Save chưa chọn nhân vật (None) dùng type của prefab.

| Chỉ số | Gốc level 1 | Bonus mỗi level vĩnh viễn | Tổng ở level vĩnh viễn 5 |
|---|---:|---:|---:|
| HP tối đa | 100 | +20 | 180 |
| Damage | 10 | +2 | 18 |
| Defense | 0 | +2 | 8 |
| Move speed | 3 | +0.02 | 3.08 |
| Crit chance | 5% | +1 điểm phần trăm | 9% |
| Crit damage cộng thêm | 50% | +5 điểm phần trăm | 70% |
| Effect resistance | 0% | +1 điểm phần trăm | 4% |
| Effect hit | 0% | +1 điểm phần trăm | 4% |

Level vĩnh viễn mẫu dùng Permanent Level Cost Linear 100 / 50 (Common ×1), trần 20. Level trong trận mẫu dùng Experience Formula Linear 10 / 5, trần 20, mỗi cấp +5 HP và +1 damage. Nhân vật vĩnh viễn level 5, trong trận level 3 có maxHealth = 100 + 4×20 + 2×5 = 190; damage = 10 + 4×2 + 2×1 = 20.

Các giá trị là ví dụ để chỉnh, chưa được cân bằng bằng chơi thử.

PlayerSave.example.json chỉ minh họa hồ sơ phiên bản 2, chọn type BlessedGladiator (1) ở level vĩnh viễn 5. Không phải file ES3 có thể chép trực tiếp vào thư mục save, SaveManager không tự đọc nó. Chưa ghi đè hoặc tạo hồ sơ save thật.

CharacterDataSO.Skills chứa MỌI skill của nhân vật; Skill Progression chọn skill từ danh sách này (dropdown) để mở/nâng theo level hoặc sao, và Star Stats là chỉ số dự phòng cho sao không có bước skill (xem Docs/Architecture/Character-Stars-Tasks.md). Each CharacterView owns CharacterSkills and advances its innate cooldowns. The scene SkillSystem independently schedules run instances supplied by a future external card/reward source. It does not receive a character or use player stats; that integration is deferred. SkillFactory creates fresh behaviors through an explicit switch; each behavior owns its gameplay logic. CharacterView initializes and disposes innate skills; CharacterControllerBase updates them; CharacterManager only tracks characters. Character cleanup never removes run skills. HolyBolt and enemy normal attack execute automatically. See Docs/Architecture/Skill-Shooting-Pipeline.md.

## 10. Phạm vi kết nối gameplay

Luồng khởi động hiện tại:

```text
SaveManager.Initialize (-200, mở file + SaveMigration)
    -> ProgressionManager.Initialize (-160) -> CharacterManager.Initialize (-150)
    -> GameManager.Initialize (100) -> PlayerSystem.SpawnPlayer
    -> lấy nhân vật được chọn và template
    -> đọc level vĩnh viễn, level trận và ID trang bị
    -> tạo CharacterRuntimeData riêng, đầy HP
    -> Player.InitializeRuntime
    -> PlayerChanged thông báo nhân vật đã sẵn sàng
```

PlayerSystem thêm một nguồn bonus (level vĩnh viễn + trang bị) và RunLevel thêm một nguồn riêng cho level trận vào `StatSheet` của Player. Runtime không giữ level; CharacterLevels và RunLevel sở hữu tiến trình. Khi các nguồn đổi, runtime tính lại từ base và modifier đang có. Tăng maxHealth/defense không tự hồi HP/giáp; khi giới hạn giảm thì kẹp lượng hiện tại. Spawn mới đầy HP và giáp, bonus skill được áp dụng bởi scheduler sau khi đăng ký; defense mới chỉ cấp thêm lượng giáp mới tương ứng. Bật lại sau disable không hồi giáp. ResetRun reset tiến trình, không tự hồi HP hoặc tạo lại nhân vật.

Đọc runtime qua CharacterView.RuntimeData hoặc PlayerSystem.RuntimeData. FinalStats và BaseStats trả bản sao struct. Gọi CharacterView.ReceiveDamage hoặc ChangeHealth (âm trừ HP trực tiếp, dương hồi HP); view chuyển tiếp tới CharacterControllerBase. Controller chung xử lý giáp hấp thụ trước HP, thông báo và chết; PlayerController/EnemyController kế thừa và định nghĩa hành vi chết riêng. Nghe Changed để cập nhật UI. Effect sở hữu thời hạn, danh sách slow và luật slow mạnh nhất; runtime chỉ nhận hệ số di chuyển đã tổng hợp.

Thêm mẫu đồ: tạo asset `Shooter Game/Data/Equipment Item` (ID ổn định, tên, icon, ô, set, skill + level mở) rồi kéo vào Items của EquipmentDatabase. Chỉ số không nằm trên mẫu đồ: chỉ số chính đọc từ `EquipmentRules` theo ô × độ hiếm, dòng phụ được roll khi nhận đồ và lưu vào save. Set khai báo trong Sets của database (mỗi `EquipmentSet` một mục: số món cần mặc + một skill). Skill của đồ/set là definition nguồn Equipment, đúng 1 cấp. Màn Equipment dùng `IEquipment`; Combat đọc đồ/set đang mặc qua `ICharacterProgress`. Lưu ý: đổi hoặc xóa ID của mẫu đồ thì các bản đã lưu với ID đó bị xóa khỏi túi khi load.

Skill, shooting, đạn và hiệu ứng đã được nối vào Character runtime. Enemy dùng chung vỏ/runtime và chết qua pipeline damage/despawn. Chưa nối EXP nhặt được, UI chọn thưởng, ví tài nguyên hoặc game over/chơi lại hoàn chỉnh. Các blueprint kiến trúc cũ cần được đối chiếu với mã hiện tại; tài liệu Skill-Shooting-Pipeline.md mô tả phần đã triển khai.

Do schema config đã đổi từ danh sách sang công thức, các config do người dùng tự tạo trước đó cần chuyển giá trị sang Bonus Per Level và các tham số mới. Hai asset mẫu trong project đã được cập nhật. Save nhân vật đã chuyển sang schema 2, lưu level theo CharacterType thay cho string ID.
