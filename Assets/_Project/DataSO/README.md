# Hướng dẫn chung DataSO, level và save người chơi

> Trạng thái hiện tại: kết nối equipment trong GameSceneInstaller và PlayerManager đã được bật. Runtime nhận bonus từ database và ID trang bị đã lưu; database mẫu hiện rỗng, chưa mặc trang bị thì bonus bằng 0.

## 1. Phân chia dữ liệu

| Loại | Script | Trách nhiệm |
|---|---|---|
| Template nhân vật | CharacterDataSO | Type làm khóa, tên hiển thị, chỉ số gốc và skill mẫu |
| Level vĩnh viễn | PermanentLevelConfigSO | Bonus cố định mỗi cấp và công thức chi phí tài nguyên |
| Level trong trận | RunLevelConfigSO | Công thức EXP tuyến tính và một bộ bonus cố định mỗi cấp |
| Danh mục nhân vật | CharacterDatabaseSO | Tất cả template nhân vật, kiểm tra ID khi dựng dictionary |
| Danh mục level | LevelDatabaseSO | Danh sách config vĩnh viễn có type và config trận dùng chung |
| Đầu mối gameplay | LevelManager | Điều phối API và chuyển tiếp sự kiện từ cả hai service |
| Level nhân vật | CharacterLevelService | Tra config, đọc level vĩnh viễn, tăng cấp và gọi save |
| Level trận | RunLevelService | Nhận EXP, phát sự kiện tăng cấp/EXP và reset lượt chơi |
| Trạng thái level trận | RunLevelProgress | Chi tiết tính tiến trình nội bộ do RunLevelService sở hữu |
| Dữ liệu lưu lâu dài | PlayerSaveData | Nhân vật đang chọn và level vĩnh viễn của từng nhân vật |
| IO và quản lý hồ sơ | SaveManager | Load/save Easy Save 3, kiểm tra dữ liệu và quản lý thay đổi chưa lưu |
| Runtime nhân vật | CharacterRuntimeData | Bản chỉ số gốc, tổng chỉ số, level và HP riêng của Player |
| Danh mục trang bị | EquipmentDatabaseSO | Tra bonus cộng thẳng bằng ID vật phẩm |
| Trang bị đang mặc | PlayerEquipmentService | Đọc/thay loadout đã lưu, tính tổng bonus và phát sự kiện |

PlayerManager tạo CharacterRuntimeData từ template, bonus level và loadout đã load. Player sử dụng runtime làm nguồn chỉ số. Trang bị hiện có danh mục ID/bonus và danh sách đang mặc chung cho người chơi; chưa có inventory, slot, mở khóa hoặc nâng cấp vật phẩm. Skin, tiền tệ và EXP vĩnh viễn chưa được thêm. Hai config không tự thực thi skill.

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
3. Tạo PermanentLevelConfigSO cùng type, cấu hình bonus và chi phí.
4. Thêm template vào CharacterDatabaseSO và config vào LevelDatabaseSO.

Không cần thêm string ID hoặc sửa thuật toán ghép. Enum chỉ khai báo loại nhân vật; vẫn cần tạo và đăng ký dữ liệu của nhân vật mới.

CharacterDataSO có FormerlySerializedAs cho field characterId cũ để bảo toàn chuỗi thành displayName khi Unity đọc asset cũ. Đây chỉ là đổi field asset, không phải migration file save.

Khi init, một lượt duyệt config dựng type → config, rồi một lượt duyệt template ghép cùng type. Các dictionary runtime đều dùng CharacterType, độ phức tạp khởi tạo O(số config + số nhân vật), tra cứu trung bình O(1). Không tham chiếu character asset từ level config và không gán lại config vào template.

None là trạng thái chưa chọn, không dùng cho template, config hoặc bản ghi tiến trình. Mỗi template dùng type riêng; type trùng/không hợp lệ hoặc thiếu config khi ghép sẽ báo lỗi. Hai database là danh mục game, không phải danh sách sở hữu của người chơi.

## 3. StatBonus khác BaseStats

BaseStats là bộ chỉ số gốc đầy đủ. StatBonus chỉ là lượng cộng thêm vào từng field, mặc định tất cả bằng 0. Không dùng BaseStats.Default để biểu diễn bonus vì nó có sẵn 100 HP, 10 damage và hệ số crit 1.5.

Ví dụ bonus một lần lên cấp:

```text
maxHealth = 20
damage = 2
critChance = 0.05           → cộng 5 điểm phần trăm
critDamageMultiplier = 0.1 → hệ số 1.5 thành 1.6
Các field còn lại = 0
```

Bonus hiện chỉ cộng thẳng, không có phần trăm nhân chỉ số. Có thể nhập số âm nếu cần thiết kế đánh đổi; bên cấu hình chịu trách nhiệm nhập số hữu hạn, không NaN/infinity. ApplyTo giới hạn kết quả HP tối đa tối thiểu 1, crit multiplier tối thiểu 1, các chỉ số còn lại tối thiểu 0; không giới hạn crit chance ở 100%.

Khi có nhiều nguồn, cộng các StatBonus trước rồi ApplyTo một lần lên chỉ số gốc. Không ApplyTo lặp lại trên kết quả cũ khi tính lại chỉ số.

## 4. Level vĩnh viễn: bonus cố định, chi phí tăng theo công thức

Mỗi nhân vật có thể có PermanentLevelConfigSO riêng. Một config chứa:

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
- Không sửa config giữa trận. RunLevelService sở hữu một RunLevelProgress của người chơi trong lượt hiện tại; gameplay gọi qua LevelManager, không tạo thêm tiến trình song song.

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

Đang dùng schema phiên bản 2: khóa enum thay cho string ID. Easy Save trong project ghi enum bằng giá trị số (BlessedGladiator = 1). Save phiên bản 1 chưa có migration, sẽ bị từ chối và không ghi đè tự động; chưa chỉnh hoặc xóa file save thật. Dữ liệu trùng type, type không khai báo, type None trong bản ghi tiến trình hoặc level dưới 1 cũng bị từ chối.

equippedItemIds là field tùy chọn bổ sung cho schema 2: file cũ thiếu field này được đọc như loadout rỗng. ID trang bị phân biệt hoa/thường, không rỗng, không trùng và phải có trong EquipmentDatabase. Save không lưu bonus tính sẵn. Nếu database thiếu ID đã lưu, bước tạo runtime báo lỗi rõ ràng thay vì bỏ mất bonus âm thầm.

## 7. SaveManager, Easy Save và Zenject

SaveManager là class thuần, implements IInitializable và IDisposable. GameSceneInstaller bind một instance cho scene và settings tên file. Initialize có priority -200, trước PlayerManager (-100). Hai service level và LevelManager là class thuần được bind AsSingle; constructor chỉ dựng cấu hình, không đọc save trước Initialize. CharacterLevelService và RunLevelService được dispose trước SaveManager để hủy callback, cache.

Mặc định dùng file `PlayerSave.es3`, key `player_data`; hằng số nằm ở GameConstants.SaveKeys. Field Player Save File trên installer cho phép đổi tên file. Tên khác tương ứng file khác, không tự di chuyển save cũ.

Easy Save dùng Location.File và Directory.PersistentDataPath; đường dẫn dựa trên thư mục dữ liệu ứng dụng của Unity. Không ghi vào Assets. Trên nền tảng plugin chuyển File sang PlayerPrefs, hành vi tuân theo Easy Save.

Load chỉ đọc key này hoặc tạo hồ sơ mới trong RAM nếu key chưa tồn tại; chưa tự ghi file khi mới mở game. Nếu đọc lỗi, IsLoaded = false, LastError giữ lỗi và API thay đổi/ghi bị khóa. Không tự reset hồ sơ để ghi đè file đang lỗi. Có thể gọi Load lại sau khi nguyên nhân được xử lý; không có API xóa/reset file ở bước này.

SetPermanentLevel và SelectCharacter chỉ thay đổi RAM, đánh dấu IsDirty. Gọi Save sau các thay đổi quan trọng để ghi ngay xuống lưu trữ. Save trả false khi ghi thất bại, giữ thay đổi chưa lưu để có thể thử lại và ghi LastError. Không reload khi IsDirty vì có thể mất thay đổi.

Dispose cố lưu nếu còn dirty khi scene container bị giải phóng. Đây là điểm lưu bổ sung, không bảo đảm cứu dữ liệu nếu ứng dụng bị kill/crash. Chưa có hook tự lưu khi mobile pause; bên điều phối ứng dụng cần gọi Save tại mốc phù hợp sau này. Chưa có hệ thống backup/recovery riêng, cloud hoặc nhiều profile.

Service hiện có scope theo scene. Nếu sau này nhiều scene cùng cần một hồ sơ đang dùng, chuyển binding sang ProjectContext để có một instance chung; không bind lại đồng thời ở cả ProjectContext lẫn SceneContext.

## 8. LevelManager và hai service

```text
Gameplay/UI
    → LevelManager
        → CharacterLevelService
            → dictionary template/config
            → SaveManager → Easy Save 3
        → RunLevelService
            → RunLevelProgress
            → RunLevelConfigSO chung
```

Gameplay inject LevelManager làm đầu mối cho cả hai loại level. Các sự kiện trên manager chuyển tiếp trực tiếp sự kiện service; manager không giữ thêm bản sao level hoặc đăng ký callback lặp.

### API level vĩnh viễn

| API trên LevelManager | Tác dụng |
|---|---|
| GetCharacter(type) | Tra template theo type |
| GetPermanentConfig(type) | Tra config vĩnh viễn |
| GetPermanentLevel(type) | Đọc level hiện tại từ SaveManager |
| GetPermanentBonus(type) | Tính tổng bonus vĩnh viễn |
| TryGetUpgradeCost(type, out cost) | Giá lần nâng tiếp theo; false khi đạt trần |
| TryIncreasePermanentLevel(type, out saved) | Tăng một cấp, thử ghi save và phát sự kiện |
| SaveCharacterProgress() | Thử lưu lại hồ sơ, không tăng thêm cấp |

Chỉ gọi API đọc/thay đổi save sau SaveManager.Initialize thành công. Không đọc tiến trình trong constructor của class khác.

TryIncreasePermanentLevel chưa kiểm tra sở hữu, điều kiện mở khóa hoặc trừ tài nguyên vì chưa có ví. Bên gọi phải xác nhận điều kiện đó trước khi gọi.

```csharp
// levelManager được Zenject inject; ví dụ chỉ gọi sau khi save đã load thành công.
if (levelManager.TryIncreasePermanentLevel(CharacterType.BlessedGladiator, out bool saved))
{
    // true: level đã tăng trong RAM, sự kiện CharacterLevelChanged đã được phát.
    if (!saved)
    {
        // Không gọi tăng cấp lại! Xử lý lỗi và thử SaveCharacterProgress khi phù hợp.
    }
}
// false: đã đạt trần, không tăng level và không phát sự kiện.
```

Nếu IO lỗi, level mới còn trong RAM và SaveManager vẫn dirty. Service phát CharacterLevelChanged cho thay đổi thực tế, rồi CharacterLevelSaveFailed để bên ngoài biết chưa ghi được file. Không rollback level một cách âm thầm.

### API level trong trận

| API/property trên LevelManager | Tác dụng |
|---|---|
| StartNewRun() | Reset level 1, EXP 0 và thông báo lượt mới |
| AddRunExperience(amount) | Nhận EXP; trả số cấp tăng |
| RunLevel | Level hiện tại trong trận |
| RunExperience | EXP dư |
| RunExperienceToNextLevel | EXP cần riêng cho cấp hiện tại, 0 khi đạt trần |
| IsRunMaxLevel | Đã đạt trần |
| RunBonus | Tổng bonus cố định của các level trong trận |

Constructor tạo trạng thái ban đầu level 1 nhưng không phát sự kiện. Bên điều phối lượt chơi gọi StartNewRun sau khi các hệ thống đã đăng ký nghe. Chưa tự gọi từ PlayerManager.

```csharp
levelManager.StartNewRun();
int gainedLevels = levelManager.AddRunExperience(35);

// Không cộng trực tiếp lên kết quả cũ khi tính lại chỉ số.
CharacterDataSO template = levelManager.GetCharacter(CharacterType.BlessedGladiator);
StatBonus total = levelManager.GetPermanentBonus(template.CharacterType)
    + levelManager.RunBonus;
BaseStats currentStats = total.ApplyTo(template.BaseStats);
```

Đây là ví dụ công thức. PlayerManager hiện tự tính cùng công thức này, cộng thêm bonus trang bị vào CharacterRuntimeData; chưa clone skill.

### Sự kiện

| Event trên LevelManager | Tham số |
|---|---|
| CharacterLevelChanged | characterType, level cũ, level mới |
| CharacterLevelSaveFailed | characterType, level mới đã có trong RAM, exception IO |
| RunLevelChanged | level cũ, level mới |
| RunExperienceChanged | EXP dư, EXP cần cấp tiếp |
| RunReset | Không tham số |

Một lần nhận EXP lên nhiều cấp phát một RunLevelChanged chứa khoảng level. Ví dụ từ 1 lên 3: sự kiện (1, 3), UI có thể mở 2 lượt chọn thưởng. Tính toán xong trước khi phát, nên đọc manager trong callback sẽ thấy trạng thái cuối.

Reset không phát sự kiện tăng cấp; phát RunReset và RunExperienceChanged. Nhận EXP khi đã đạt trần hoặc không thay đổi tiến trình không phát lại thông báo EXP.

Bên nghe tự hủy đăng ký khi không dùng nữa. Không gọi tăng cấp/reset tiếp bên trong callback của chính chuỗi xử lý đó; service chặn thay đổi lồng nhau, có thể đưa yêu cầu sang lần xử lý sau.

Level trận phát sự kiện nhưng không gọi save lâu dài. Muốn tiếp tục một lượt đã thoát sẽ cần loại save lượt chơi riêng, chưa triển khai.

### Save snapshot và dictionary

GetSnapshot của SaveManager trả bản sao độc lập. Sửa snapshot không đánh dấu dirty hoặc thay hồ sơ đang quản lý.

SaveManager có dictionary CharacterType → bản ghi tiến trình, dựng lại sau load và cập nhật khi thêm bản ghi. CharacterLevelService có dictionary CharacterType → template và CharacterType → config. Các cache dùng enum làm khóa, tra cứu trung bình O(1), không ghi dictionary lặp vào file save.

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
| Crit multiplier | 1.5 | +0.05 | 1.7 |
| Dodge | 0 | +1 | 4 |
| Accuracy | 0 | +1 | 4 |

Level vĩnh viễn mẫu dùng giá đầu 100, hệ số 1.2, trần 10. Level trong trận mẫu dùng A = 10, B = 5, trần 10, mỗi cấp +5 HP và +1 damage. Nhân vật vĩnh viễn level 5, trong trận level 3 có maxHealth = 100 + 4×20 + 2×5 = 190; damage = 10 + 4×2 + 2×1 = 20.

Các giá trị là ví dụ để chỉnh, chưa được cân bằng bằng chơi thử.

PlayerSave.example.json chỉ minh họa hồ sơ phiên bản 2, chọn type BlessedGladiator (1) ở level vĩnh viễn 5. Không phải file ES3 có thể chép trực tiếp vào thư mục save, SaveManager không tự đọc nó. Chưa ghi đè hoặc tạo hồ sơ save thật.

Hai danh sách ISkill mẫu để rỗng vì chưa có class skill cụ thể và menu chọn skill. Không dùng skill DataSO.

## 10. Phạm vi kết nối gameplay

Luồng khởi động hiện tại:

```text
SaveManager.Load (priority -200)
    -> PlayerManager.SpawnPlayer (priority -100)
    -> lấy nhân vật được chọn và template
    -> đọc level vĩnh viễn, level trận và ID trang bị
    -> tạo CharacterRuntimeData riêng, đầy HP
    -> Player.InitializeRuntime
    -> PlayerChanged thông báo nhân vật đã sẵn sàng
```

Chỉ số tổng = BaseStats + PermanentBonus + RunBonus + EquipmentBonus. Runtime sao chép struct từ template, không Instantiate hoặc chỉnh sửa asset SO. Khi level vĩnh viễn của nhân vật, level trận, reset trận hoặc trang bị đổi, PlayerManager tính lại từ bản gốc. HP hiện tại giữ nguyên và được giới hạn về MaxHealth mới; tăng MaxHealth không tự hồi HP. Spawn Player mới tạo runtime mới đầy HP. Spawn lại trong cùng lượt vẫn dùng level trận hiện tại; StartNewRun reset level, không tự hồi HP hoặc tạo lại nhân vật.

Đọc runtime qua Player.RuntimeData hoặc PlayerManager.RuntimeData. Stats và BaseStats trả về bản sao struct; thay đổi HP qua ApplyHealthChange (âm để trừ, dương để hồi), nghe Changed để cập nhật UI. Player gửi Stats.moveSpeed sang MovementManager, giữ mức nghiêng joystick và dừng di chuyển khi HP bằng 0. Với template mẫu level 1, tốc độ bây giờ là 3 thay cho 2 từng khai báo riêng trong prefab.

Thêm vật phẩm vào danh sách Items của EquipmentDatabase: ID ổn định, tên hiển thị và bonus. Inject PlayerEquipmentService vào bên quản lý trang bị rồi gọi SetEquippedItems với toàn bộ ID đang mặc; danh sách rỗng để tháo hết. Ví dụ, sau khi đã khai báo ID boots trong database:

```csharp
bool saved = equipmentService.SetEquippedItems(new[] { "boots" });
// Runtime đã cập nhật dù saved=false. Khi IO lỗi, thử saveManager.Save() để lưu lại.
equipmentService.SetEquippedItems(System.Array.Empty<string>());
```

Thay loadout kiểm tra tất cả ID trước khi sửa RAM, không cho trùng ID hoặc đổi loadout lồng nhau trong callback. SaveManager lưu danh sách và đánh dấu dirty; service thử save ngay rồi phát EquipmentChanged để runtime tính lại. Chưa có UI trang bị, kiểm tra sở hữu, slot hoặc item instance; ID ở đây là ID loại vật phẩm, mỗi loại được cộng tối đa một lần.

Chưa clone skill, chưa nối EXP nhặt được, chưa có UI chọn thưởng hoặc ví tài nguyên để mua level vĩnh viễn. Runtime có HP nhưng pipeline sát thương và hành vi chết/despawn chưa được nối. Quái chưa dùng CharacterRuntimeData. Các tài liệu trong Docs/Architecture là blueprint cho phần combat và skill tiếp theo.

Do schema config đã đổi từ danh sách sang công thức, các config do người dùng tự tạo trước đó cần chuyển giá trị sang Bonus Per Level và các tham số mới. Hai asset mẫu trong project đã được cập nhật. Save nhân vật đã chuyển sang schema 2, lưu level theo CharacterType thay cho string ID.
