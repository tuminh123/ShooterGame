# Hướng dẫn chung DataSO, level và save người chơi

> Trạng thái hiện tại: kết nối equipment trong GameSceneInstaller và PlayerSystem đã được bật. Runtime nhận bonus từ database và ID trang bị đã lưu; database mẫu hiện rỗng, chưa mặc trang bị thì bonus bằng 0.

## 1. Phân chia dữ liệu

| Loại | Script | Trách nhiệm |
|---|---|---|
| Template nhân vật | CharacterDataSO | Type làm khóa, tên hiển thị, chỉ số gốc và skill mẫu |
| Level vĩnh viễn | CharacterLevelConfigSO | Bonus cố định mỗi cấp và công thức chi phí tài nguyên |
| Level trong trận | RunLevelConfigSO | Công thức EXP tuyến tính và một bộ bonus cố định mỗi cấp |
| Danh mục nhân vật | CharacterDatabaseSO | Tất cả template nhân vật, kiểm tra ID khi dựng dictionary |
| Danh mục level | LevelDatabaseSO | Danh sách config vĩnh viễn có type và config trận dùng chung |
| Level nhân vật | CharacterLevels (miền Progression) | Tra config, đọc level vĩnh viễn, tăng cấp và gọi save |
| Level trận | RunLevel (miền Run, do RunManager tạo) | Nhận EXP, phát LevelUp/ExperienceChanged, giữ nguồn bonus level trận trên Player |
| Trạng thái level trận | RunLevelProgress | Chi tiết tính tiến trình nội bộ do RunLevel sở hữu |
| Dữ liệu lưu lâu dài | PlayerSaveData | Nhân vật đang chọn và level vĩnh viễn của từng nhân vật |
| IO hồ sơ | SaveManager | Đọc/ghi Easy Save 3 theo key, giữ snapshot chưa ghi được để retry |
| Runtime nhân vật | CharacterRuntimeData | Chỉ số gốc/cuối, HP và giáp hiện tại riêng của từng nhân vật; không lưu level/EXP |
| Danh mục trang bị | EquipmentDatabaseSO | Tra bonus cộng thẳng bằng ID vật phẩm |
| Trang bị đang mặc | Loadout (miền Progression) | Đọc/thay loadout đã lưu, tính tổng bonus và phát sự kiện |

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
3. Tạo CharacterLevelConfigSO cùng type, cấu hình bonus và chi phí.
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

## 4. Level vĩnh viễn: bonus cố định, chi phí tăng theo công thức

Mỗi nhân vật có thể có CharacterLevelConfigSO riêng. Một config chứa:

| Field trong Inspector | Ý nghĩa | Giá trị mẫu |
|---|---|---:|
| Character Type | Nhân vật cụ thể dùng config này, khớp type trên template | BlessedGladiator |
| Max Level | Level vĩnh viễn tối đa | 10 |
| Bonus Per Level | Một bộ chỉ số cộng thêm cho mỗi lần lên cấp | +20 HP, +2 damage, ... |
| Initial Upgrade Cost | Tài nguyên cần nâng từ level 1 lên 2 | 100 |
| Upgrade Cost Multiplier | Hệ số tăng chi phí mỗi cấp | 1.2 |

Không còn danh sách Element 0/1/2. Level 1 dùng chỉ số gốc, nên:

```text
Bonus tích lũy ở level L = Bonus Per Level × (L − 1)
Chỉ số nhân vật = chỉ số gốc + bonus tích lũy
```

Ví dụ HP gốc 100, bonus mỗi cấp 20: level 1 có 100 HP; level 2 có 120; level 5 có 180; level 10 có 280. Mỗi nhân vật có thể dùng bonus khác nhau, cùng công thức.

Chi phí nâng cấp vĩnh viễn:

```text
Chi phí từ level L lên L+1
    = ceil(Initial Upgrade Cost × Upgrade Cost Multiplier^(L − 1))
```

ceil là làm tròn lên, chỉ làm tròn kết quả cuối, không làm tròn sau mỗi lần nhân.

| Chuyển cấp | Chi phí với giá đầu 100, hệ số 1.2 |
|---|---:|
| 1 → 2 | 100 |
| 2 → 3 | 120 |
| 3 → 4 | 144 |
| 4 → 5 | 173 |
| 5 → 6 | 208 |
| 9 → 10 | 430 |

Hệ số 1 giữ nguyên chi phí, lớn hơn 1 khiến cấp cao đắt hơn. Đây là chi phí tài nguyên, không phải EXP trong trận. Chưa chọn loại tiền/vật phẩm và chưa có ví hoặc logic trừ tài nguyên. TryGetUpgradeCost(currentLevel, out long cost) chỉ trả giá, false khi đã đạt trần hoặc level không hợp lệ.

Code dùng phép nhân decimal để hạn chế sai số trước khi làm tròn, trả long; cấu hình khiến chi phí vượt phạm vi số sẽ báo overflow, không âm thầm trả giá âm hoặc miễn phí. Các tham số phải hữu hạn, MaxLevel >= 1, giá đầu >= 1, hệ số >= 1.

GetTotalBonus(level) tính tổng bằng phép nhân struct, không duyệt bảng. Nếu save cao hơn MaxLevel mới, chỉ tính bonus tới trần hiện tại, không tự sửa level trong save. Level dưới 1 bị từ chối.

Save vẫn chỉ giữ permanentLevel cho từng CharacterType. Không thêm EXP vĩnh viễn vì cơ chế này dùng chi phí tài nguyên. Sau này ví tài nguyên của người chơi có thể được bổ sung vào PlayerSaveData độc lập.

## 5. Level trong trận: bonus cố định và EXP tuyến tính

RunLevelConfigSO được gán một lần vào LevelDatabaseSO và dùng chung cho level người chơi trong trận:

| Field trong Inspector | Ý nghĩa | Giá trị mẫu |
|---|---|---:|
| Max Level | Level tối đa của lượt chơi | 10 |
| Bonus Per Level | Bộ chỉ số cộng y nguyên mỗi lần lên cấp | +5 HP, +1 damage |
| Initial Experience | EXP từ level 1 lên 2, hệ số A | 10 |
| Experience Increase Per Level | Lượng EXP yêu cầu tăng thêm mỗi cấp, hệ số B | 5 |

```text
EXP từ level L lên L+1 = A + B × (L − 1)
Bonus tích lũy = Bonus Per Level × (L − 1)
```

| Chuyển cấp | EXP cần riêng với A = 10, B = 5 |
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

SaveManager (miền Save) chỉ xử lý lưu trữ: `ISaveStore` (Load<T>(key, defaultData, validate), Save<T>, SaveBatch) và `ISaveMaintenance` (SaveIfDirty, ResetData). Wallet, CharacterLevels, Loadout, StageProgress (bên trong ProgressionManager) và PlayerSystem sở hữu dữ liệu và quyết định thời điểm lưu; mỗi section tự kiểm tra bằng `XxxSaveData.Validate`. Thứ tự khởi tạo: SaveManager (-200, mở file rồi chạy SaveMigration ngay bên trong) → ProgressionManager (-160) → CharacterManager (-150) → gameplay.

Mặc định dùng chung file `PlayerSave.es3`, các key `player_selection`, `character_levels`, `equipment`, `currencies`, và marker `save_format` phiên bản 4. Hằng số ở GameConstants.SaveKeys. `player_data` chỉ còn phục vụ migration. Field Player Save File trên installer cho phép đổi tên file; tên khác không tự di chuyển save cũ.

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
        → Loadout, Wallet, StageProgress
    → RunManager (IRunReader, IRunLifecycle)   level trong trận (miền Run, GĐ 4)
        → RunLevel
            → RunLevelProgress
            → RunLevelConfigSO chung (LevelDatabaseSO.RunLevelConfig)
```

Miền khác chỉ thấy level vĩnh viễn qua `IPermanentBonuses.GetBonus(type)` (level vĩnh viễn + trang bị, dùng bởi PlayerSystem)
và `BonusesChanged`. Các API dưới đây nằm trên `CharacterLevels` bên trong ProgressionManager; chưa có UI nâng cấp nên
chưa có vai trò (interface) công khai — sẽ thêm vào `ProgressionContracts.cs` khi UI đó ra đời.

### API level vĩnh viễn (trên CharacterLevels)

| API | Tác dụng |
|---|---|
| GetConfig(type) | Tra config vĩnh viễn |
| GetLevel(type) | Đọc level vĩnh viễn |
| GetTotalBonus(type) | Tính tổng bonus vĩnh viễn |
| TryGetUpgradeCost(type, out cost) | Giá lần nâng tiếp theo; false khi đạt trần |
| TryIncreaseLevel(type, out saved) | Tăng một cấp, thử ghi save và phát LevelChanged |
| SaveProgress() | Thử lưu lại hồ sơ, không tăng thêm cấp |

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

Level vĩnh viễn mẫu dùng giá đầu 100, hệ số 1.2, trần 10. Level trong trận mẫu dùng A = 10, B = 5, trần 10, mỗi cấp +5 HP và +1 damage. Nhân vật vĩnh viễn level 5, trong trận level 3 có maxHealth = 100 + 4×20 + 2×5 = 190; damage = 10 + 4×2 + 2×1 = 20.

Các giá trị là ví dụ để chỉnh, chưa được cân bằng bằng chơi thử.

PlayerSave.example.json chỉ minh họa hồ sơ phiên bản 2, chọn type BlessedGladiator (1) ở level vĩnh viễn 5. Không phải file ES3 có thể chép trực tiếp vào thư mục save, SaveManager không tự đọc nó. Chưa ghi đè hoặc tạo hồ sơ save thật.

CharacterDataSO stores one Skills list of SkillDataSO assets. Each CharacterView owns CharacterSkills and advances its innate cooldowns. The scene SkillSystem independently schedules run instances supplied by a future external card/reward source. It does not receive a character or use player stats; that integration is deferred. SkillFactory creates fresh behaviors through an explicit switch; each behavior owns its gameplay logic. CharacterView initializes and disposes innate skills; CharacterControllerBase updates them; CharacterManager only tracks characters. Character cleanup never removes run skills. HolyBolt and enemy normal attack execute automatically. See Docs/Architecture/Skill-Shooting-Pipeline.md.

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

Thêm vật phẩm vào danh sách Items của EquipmentDatabase: ID ổn định, tên hiển thị và bonus. `Loadout.SetEquippedItems` (bên trong ProgressionManager; vai trò công khai sẽ thêm khi có UI trang bị) nhận toàn bộ ID đang mặc; danh sách rỗng để tháo hết. Ví dụ, sau khi đã khai báo ID boots trong database:

```csharp
bool saved = loadout.SetEquippedItems(new[] { "boots" });
// Runtime đã cập nhật dù saved=false. Khi IO lỗi, thử loadout.SaveEquipment() hoặc chờ ISaveMaintenance.SaveIfDirty.
loadout.SetEquippedItems(System.Array.Empty<string>());
```

Thay loadout kiểm tra tất cả ID trước khi sửa RAM, không cho trùng ID hoặc đổi loadout lồng nhau trong callback. Loadout sở hữu danh sách, gửi snapshot vào key equipment ngay rồi phát EquipmentChanged; ProgressionManager chuyển thành BonusesChanged để PlayerSystem tính lại. Chưa có UI trang bị, kiểm tra sở hữu, slot hoặc item instance; ID ở đây là ID loại vật phẩm, mỗi loại được cộng tối đa một lần.

Skill, shooting, đạn và hiệu ứng đã được nối vào Character runtime. Enemy dùng chung vỏ/runtime và chết qua pipeline damage/despawn. Chưa nối EXP nhặt được, UI chọn thưởng, ví tài nguyên hoặc game over/chơi lại hoàn chỉnh. Các blueprint kiến trúc cũ cần được đối chiếu với mã hiện tại; tài liệu Skill-Shooting-Pipeline.md mô tả phần đã triển khai.

Do schema config đã đổi từ danh sách sang công thức, các config do người dùng tự tạo trước đó cần chuyển giá trị sang Bonus Per Level và các tham số mới. Hai asset mẫu trong project đã được cập nhật. Save nhân vật đã chuyển sang schema 2, lưu level theo CharacterType thay cho string ID.
