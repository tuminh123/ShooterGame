# Màn Character (Collection + Detail) — kế hoạch task bản demo

> **Trạng thái:** ✅ Đã triển khai Task 1–5 (2026-11-19) + thanh EXP nâng cấp (2026-11-20), chờ người dùng play test theo checklist cuối file · Ngày lập: 2026-11-19

## Thay đổi 2026-11-20: thanh bar Home dùng chung (người dùng duyệt)

- `BottomBar_Menu` tách khỏi `MainUI`, nằm dưới object luôn bật `HomeNavigation` (ngay sau `CharacterScreen`), do
  `UI/Core/HomeNavigationBar.cs` quản lý. Lý do: UIManager tắt MainUI khi mở màn Character, mà thanh bar phải còn để bấm về Home.
- Nút Battle (`Button_03` / `Button_03_Focus`) về Home; nút Heroes (`Button_02` / `Button_02_Focus`, chữ "HEROES") mở màn Character.
  Nút của màn đang mở **nổi lên** (bật bản Focus). Các nút chưa có chức năng (`Button_01/04/05`) bị khóa.
- Vào **Detail** thì thanh bar trượt xuống ẩn (SlideTransition hướng Down), Back về Collection thì trượt lên.
- `MainUI/Background` tách thành nền dùng chung `HomeBackground` (hiện suốt trạng thái Home) để lúc trượt không lộ bản đồ.
- **Bỏ nút Back của Collection** (về Home bằng thanh bar). `CharacterScreenController` có `Close()`, `IsShown`, `IsOnDetail`, event `NavigationChanged`.
- Hướng trượt chỉnh trong Inspector: `SlideTransition` (Enter From / Exit To) cho màn và thanh bar; `CharacterDetailView.Tab Slide Forward` cho tab.

## Thay đổi 2026-11-20: UX — tween nút, trượt màn/tab, số nhảy, Canvas lồng (người dùng duyệt)

- **Nút:** `ButtonClickTween` (cùng thông số nút Play: 0.9 → 1.08 → 1, unscaled) trên mọi nút. Gắn hàng loạt bằng
  **Tools/Shooter Game/UI/Add Click Tween To All Buttons** (bỏ qua nút đã có `PopupTweenBase`, ví dụ nút Play). Thêm nút mới thì chạy lại menu.
- **Trượt:** `SlideTransition` (enum 4 hướng `SlideDirection`) trên `CollectionScreen`, `DetailScreen`, `LevelTab`, `StarTab`.
  Mở màn: Collection trượt vào; bấm thẻ: Detail trượt vào đè Collection; Back: trượt ra rồi mới đóng. Đổi tab: tab cũ ra một phía, tab mới vào từ phía đối diện.
  Trong lúc trượt, panel không nhận click và controller bỏ qua thao tác chuyển trang mới.
- **Số nhảy:** `TextPunch` (lớp thuần) cho 8 ô chỉ số, huy hiệu level, chữ thanh tiến độ: chỉ nhảy khi giá trị **tăng** và vẫn là nhân vật cũ.
- **Canvas lồng:** `LevelTab/Content/Stats`, `LevelTab/Slider_Level`, `Header/Portrait` (nhân vật UI chuyển động mỗi frame) và prefab `CurrencyBar`,
  để phần thay đổi không làm dựng lại Canvas chính. Không dùng `CanvasDynamic` (sorting 0 nằm dưới Canvas chính, tách khỏi panel cha).

## Thay đổi 2026-11-20: nhân vật UI cho thẻ và màn Detail (người dùng duyệt)

- Tool **Tools/Shooter Game/Character UI Exporter** (`Editor/CharacterUIExporter.cs`) đọc `CharacterDatabaseSO`, xuất
  (1) ảnh PNG nền trong suốt → `Graphic/Portraits/{Type}_Portrait.png`, gán vào `CharacterDataSO.portrait` (thẻ Collection);
  (2) prefab `CharacterUIVisual` → `Prefabs/UI/Characters/UI_{Type}.prefab`, gán vào `CharacterDataSO.uiVisualPrefab` (màn Detail).
  Không phụ thuộc pack CharacterMaker (chỉ đọc Transform/SpriteRenderer/Animator).
- Prefab UI = **Rig** (xương rỗng, cùng đường dẫn nên chạy clip gốc) + **Layers** (Image xếp theo sortingOrder);
  `CharacterUIVisual` chép tư thế xương sang Image trong `LateUpdate`. Lý do: UI vẽ theo hierarchy, gameplay vẽ theo sortingOrder.
- Animator UI dùng `Animations/UI/UI_CharacterController.controller` (Idle mặc định; trigger `Celebrate` → Victory một lần → Idle),
  dùng lại clip gốc, chạy unscaled time.
- Màn Detail: nhân vật UI đặt vừa khung `Image_Portrait` (ảnh tĩnh chỉ là dự phòng). Ăn mừng khi **level hoặc sao thật sự tăng**
  (bấm nạp điểm chưa đủ thanh thì không ăn mừng).

## Thay đổi 2026-11-20: thanh level thành thanh EXP nâng cấp (người dùng duyệt)

Trước đây mỗi lần bấm UPGRADE là +1 level ngay và thanh hiện `level/max`. Giờ dùng **cùng cơ chế với thanh EXP trong trận**:
mỗi lần bấm tiêu `AmountPerUpgrade` nguyên liệu và nạp đúng lượng đó vào thanh (1:1). Thanh của cấp L cần
`A + B × (L − 1)`; đầy thanh thì lên cấp và phần dư chuyển sang cấp sau. Lần bấm chạm trần chỉ tốn phần còn thiếu.

- Theo `Refactor-Architecture.md` §1 và §8 (Simple trước; "code lặp đến lần thứ 3 mới tách"): **không** tách struct dùng chung với `InBattleLevelProgress`. Hai nơi dùng là hai khái niệm khác miền; công thức nằm trong `CharacterLevelConfigSO`, vòng lặp nằm trong `CharacterLevels`. Có nơi dùng thứ 3 thì mới tách.
- File đã sửa (ngoài danh sách ban đầu, người dùng duyệt): `CharacterLevelConfigSO.cs`, `CharacterLevels.cs`, `CharacterUpgradeTracks.cs`, `ProgressionContracts.cs` (`UpgradeQuote.Progress/ProgressRequired`), `SaveSections.cs` (`upgradePoints`), `CharacterScreenModels.cs`, `CharacterScreenController.cs`, `CharacterDetailView.cs`, `Editor/ProjectDataSOEditor.cs`. Không đụng miền Battle.
- Save cũ không có `upgradePoints` nên đọc ra 0; không cần migration. Field `initialRequired` nhận lại giá trị `initialUpgradeCost` cũ qua `FormerlySerializedAs`.
- Vẫn **chưa trừ tiền** (TODO trong `LevelTrack`).

## Khác biệt so với kế hoạch (người dùng duyệt 2026-11-19)

Người dùng yêu cầu làm liền Task 2–5, gom mọi script UI của màn vào **một thư mục** và gom class/struct/enum thuần vào chung file:

- Mọi script UI nằm trong `Scripts/UI/CharacterScreen/`, kể cả `CurrencyBarView` (kế hoạch cũ đặt ở `UI/Views/`).
- Model và enum gom vào `CharacterScreenModels.cs`: `CharacterScreenPage`, `CharacterScreenTab`, `CharacterCardState`, `CharacterCardModel`, `StatRowModel`, `CostModel`, `UpgradeButtonModel`, `CharacterDetailModel`.
- `CharacterRarity` nằm luôn trong `CharacterDataSO.cs` (thay cho file riêng N1).
- MonoBehaviour và ScriptableObject vẫn mỗi class một file, vì Unity bắt buộc tên file phải trùng tên class.
- Sửa thêm 2 file ngoài danh sách:
  - `CharacterDatabaseSO.cs`: thêm getter `Characters`, để giữ thứ tự database (Dictionary không bảo đảm thứ tự).
  - `PlayerSystem.cs`: thêm `ActiveCharacterType` (save chưa chọn → loại mặc định), để UI đánh dấu đúng nhân vật đang dùng. `SpawnPlayer` dùng lại chính thuộc tính này.
- Portrait của BlessedGladiator tạm dùng `CharacterList_Sample01` (ảnh trong mockup), vì chưa có ảnh chân dung riêng.
- `Icon_Front` trong `Button_Prev`/`Button_Next` đã tắt raycast và tắt Button con, để click đi tới nút cha.
- `CollectionScreen` phủ kín toàn màn hình và có `CurrencyBar` riêng. Lúc đầu nó nằm lọt giữa Topbar/BottomBar của `MainUI`, nhưng UIManager tắt `MainUI` khi mở màn Character nên bị hở trắng trên/dưới. Giờ màn Character không phụ thuộc vào bất kỳ phần nào của Home.

## Quy tắc phạm vi (bắt buộc)

- Chỉ được **tạo** các script trong mục [Script tạo mới](#script-tạo-mới) và chỉ được **sửa** các script trong mục [Script sửa](#script-sửa).
- Cần thêm, bớt, đổi tên script hoặc sửa script ngoài danh sách thì **dừng lại và báo người dùng duyệt** trước khi làm. Việc này cũng áp dụng khi phát hiện lỗi cũ ở file khác.
- Làm từng task theo thứ tự. Sau mỗi task game phải compile được và phải báo người dùng trước khi sang task tiếp theo.
- Không dùng MVVM. Mô hình chọn là **Controller + View**:
  - Controller (`UIPanelController`) giữ trạng thái màn hình, gọi service và đẩy dữ liệu xuống View.
  - View chỉ hiển thị dữ liệu và phát event khi người dùng bấm.
- Tuân thủ quy tắc trong `CLAUDE.md`: field SO là `[SerializeField] private` có getter, comment tiếng Việt, `/// <summary>`, `#region`, giữ GUID `.meta`.

## Phạm vi demo

**Có trong demo:**
- Mở màn Character từ Home.
- Collection: lưới thẻ nhân vật Player, đánh dấu nhân vật đang dùng.
- Detail: độ hiếm, tên, ảnh, ←/→, sao, thanh level, 8 chỉ số cuối, 3 skill.
- Chọn nhân vật dùng cho trận sau.
- Nút UPGRADE hiện trạng thái theo `UpgradeQuote.CanUpgrade`. Bấm nút gọi `TryUpgrade`: Level tăng 1 cấp, **chưa trừ tiền** (TODO).
- Tab Stars hiển thị 5 dòng perk dạng mockup tĩnh (`PerkList/Perks/PerkRow_1..5`, chữ giữ nguyên từ mockup, chưa có data); `TryUpgrade(Star)` trả `false`.

**Quyết định đã chốt (2026-11-19):**
1. `CanUpgrade` và `TryUpgrade` đi thành cặp trong `ICharacterUpgrades`. `TryUpgrade` luôn kiểm tra lại điều kiện trước khi làm.
2. Tiền: demo **không kiểm tra và không trừ tiền**. Để `// TODO` trong phần điều kiện của `CanUpgrade` và trong `TryUpgrade`. Giá trả về dạng **danh sách** `Costs` để sau này hỗ trợ một lần nâng tốn 2 loại tiền; demo tạm điền 1 mục Gold.
3. Nút mở màn: `MainUI/BottomBar_Menu/Button_02` (icon sách).

**Chưa làm, mỗi mục là một kế hoạch riêng sau này:**
- Tiền khi nâng level: field loại tiền và giá trong config, kiểm tra số dư, trừ tiền cùng một lần save với việc tăng level.
- Hệ thống sao, đã chọn **hướng C**: mỗi sao mở 1 perk, `StarPerkType { Stat, SkillLevel }` chọn bằng một switch. Perk `Stat` đi vào `IPermanentBonuses`, perk `SkillLevel` đi vào `CharacterSkills`. Làm perk Stat trước, SkillLevel sau. UI dùng `StarTab/PerkList/Perks/PerkRow_1..5` (đã có trong scene, đang tắt).
- Mở khóa nhân vật.
- Sắp xếp theo "Property".
- Popup `PopupFull_CharacterLevelUp`.

## Kiến trúc tóm tắt

```
UIManager ─► CharacterScreenController (UIPanelController, inject Zenject)
               ├─ IPlayerSelection     ← CharacterManager    (mới)
               ├─ ICharacterUpgrades   ← ProgressionManager  (mới, CanUpgrade dùng chung Level/Star)
               ├─ IPermanentBonuses    ← ProgressionManager  (đã có)
               ├─ IGameState           ← GameManager         (đã có)
               ├─ CharacterScreenThemeSO (kéo vào Inspector)
               ├─► CharacterCollectionView ─► CharacterCardView × N
               └─► CharacterDetailView     ─► StatRowView × 8, Image skill × 3
CurrencyBarView ─► IWallet (đã có), tự inject
```

- Chỉ số cuối = `StatCalculator.Calculate(data.BaseStats, new[] { bonus.ToModifiers() })`. Không viết công thức thứ hai.
- Dữ liệu được làm mới khi có event, không polling:
  - `IPlayerSelection.SelectionChanged`
  - `IPermanentBonuses.BonusesChanged`
  - `IWallet.BalanceChanged`
- Đăng ký event trong `OnEnable`, gỡ trong `OnDisable`.

---

## Script tạo mới

| # | Đường dẫn (`Assets/_Project/Scripts/…`) | Chức năng | Không làm |
|---|---|---|---|
| N1 | `Characters/Data/CharacterRarity.cs` | `enum CharacterRarity { Common = 0, Rare = 1, Epic = 2, Legendary = 3 }`. ID được lưu trong asset; loại mới thêm vào cuối. | Không chứa logic |
| N2 | `Progression/CharacterUpgradeTracks.cs` | Họ strategy **nội bộ** của Progression: `internal interface IUpgradeTrack { UpgradeQuote GetQuote(CharacterType type); bool TryUpgrade(CharacterType type, out bool saved); }` cùng 2 class:<br>• `LevelTrack.GetQuote`: đọc `CharacterLevels` (`GetLevel`, `GetConfig().MaxLevel`, `TryGetUpgradeCost`). `Block = MaxReached` khi hết cấp, còn lại `None`. `Costs` = 1 mục Gold với giá từ config. `// TODO`: kiểm tra số dư → `NotEnoughCurrency`.<br>• `LevelTrack.TryUpgrade`: gọi lại `GetQuote`; nếu `!CanUpgrade` thì trả `false`, ngược lại gọi `CharacterLevels.TryIncreaseLevel` (đã có). `// TODO`: trừ tiền trong cùng lần save.<br>• `StarTrack`: `GetQuote` luôn trả `Block = NotAvailable`, `Current = 0`, `Max = 5`; `TryUpgrade` luôn trả `false`. | Không bind Zenject, không trừ tiền trong demo |
| N3 | `UI/CharacterScreen/CharacterScreenThemeSO.cs` | SO cấu hình hiển thị của màn:<br>• Mỗi `StatType` có icon, tên, định dạng (số thường / %).<br>• Mỗi `CharacterRarity` có sprite nhãn, chữ và màu.<br>• Mỗi `CurrencyType` có icon, dùng cho giá trên nút UPGRADE.<br>Có `Validate()` để kiểm tra đủ 8 StatType, đủ mọi Rarity và đủ mọi CurrencyType. | Không chứa trạng thái runtime |
| N4 | `UI/CharacterScreen/CharacterScreenController.cs` | `UIPanelController` của màn:<br>• Inject các interface ở sơ đồ trên.<br>• Giữ trạng thái: màn đang mở (Collection/Detail), chỉ số nhân vật đang xem, tab đang mở (Level/Stars).<br>• Dựng model rồi gọi `Bind` của View.<br>• Xử lý event của View: mở Detail, ←/→ vòng tròn, Chọn, Back, đổi tab, UPGRADE (gọi `TryUpgrade` theo tab đang mở rồi vẽ lại khi nhận `Upgraded`).<br>• Có `Open()` công khai cho Home gọi, bên trong gọi `RequestVisibility(true)`. Back ở Collection gọi `RequestVisibility(false)`.<br>• Chỉ cho mở khi `GameState.Home`. | Không tự vẽ UI, không tự tính công thức chỉ số |
| N5 | `UI/CharacterScreen/CharacterCollectionView.cs` | View màn Collection:<br>• Sinh và tái dùng `CharacterCardView` từ prefab trong `Content`. Thêm thẻ trống cho đủ hàng 3 cột.<br>• Hiện chữ "đã có/tổng".<br>• Phát `event Action<int> CardClicked` và `event Action BackClicked`. | Không gọi service |
| N6 | `UI/CharacterScreen/CharacterCardView.cs` | View một thẻ:<br>• `Bind(CharacterCardModel)` hiện ảnh, tên, badge level, sao và trạng thái Normal / Selected / Empty.<br>• Phát `Clicked`.<br>• Struct `CharacterCardModel` đặt trong file này. | Không gọi service |
| N7 | `UI/CharacterScreen/CharacterDetailView.cs` | View màn Detail:<br>• Header: độ hiếm, tên, ảnh, sao.<br>• Tab Level: badge level, slider level/max, 8 `StatRowView`, 3 `Image` skill.<br>• Tab Stars: chỉ bật/tắt `StarTab`; 5 `PerkRow` là mockup tĩnh, View không bind data trong demo.<br>• Footer: Back, Chọn/Đang dùng, UPGRADE (danh sách giá với icon tiền, tối đa 2 mục; lý do bị chặn).<br>• `Bind(CharacterDetailModel)`. Struct `CharacterDetailModel` đặt trong file này.<br>• Phát `PrevClicked`, `NextClicked`, `BackClicked`, `SelectClicked`, `UpgradeClicked`, `TabChanged`. | Không gọi service |
| N8 | `UI/CharacterScreen/StatRowView.cs` | View một ô chỉ số: `Bind(Sprite icon, string value)`. Dùng cho prefab `StatRow`. | Không tự định dạng số; Controller định dạng theo Theme |
| N9 | `UI/Views/CurrencyBarView.cs` | Thanh tiền dùng chung (Gold, Diamond):<br>• Tự inject `IWallet`.<br>• Nghe `BalanceChanged` và đọc lại số dư mỗi khi bật.<br>• Chỉ đọc ví khi `IGameState.State == Home`, giống `HomeController`.<br>Nút [+] chưa có chức năng. | Không trừ hay cộng tiền |

Tổng cộng có **9 script mới**. Thư mục `UI/CharacterScreen/` đặt tên khác `UI/Character/`, vì thư mục kia chứa `CharacterUI.cs`, là thanh máu trên đầu nhân vật trong world.

## Script sửa

| # | Đường dẫn (`Assets/_Project/…`) | Nội dung sửa |
|---|---|---|
| S1 | `Scripts/Characters/Data/CharacterDataSO.cs` | Thêm `[SerializeField] private Sprite portrait` và `[SerializeField] private CharacterRarity rarity`, kèm getter `Portrait` và `Rarity`, có Tooltip. Không đổi field cũ. |
| S2 | `Scripts/Characters/CharacterContracts.cs` | Thêm `IPlayerSelection`:<br>• `IReadOnlyList<CharacterDataSO> PlayableCharacters`: Team = Player, giữ thứ tự database.<br>• `CharacterType SelectedCharacterType`<br>• `bool SelectCharacter(CharacterType)`: false nghĩa là lỗi IO.<br>• `event Action<CharacterType> SelectionChanged` |
| S3 | `Scripts/Characters/CharacterManager.cs` | Implement `IPlayerSelection`:<br>• Chuyển lời gọi sang `PlayerSystem.SelectedCharacterType` và `PlayerSystem.SelectCharacter`, đều đã có sẵn.<br>• Dựng danh sách Player một lần từ `CharacterDatabaseSO`.<br>• Phát `SelectionChanged` sau khi chọn thành công. |
| S4 | `Scripts/Progression/ProgressionContracts.cs` | Thêm:<br>• `enum CharacterUpgradeKind { Level, Star }`<br>• `enum UpgradeBlock { None, MaxReached, NotEnoughCurrency, NotAvailable }`<br>• `readonly struct CurrencyCost { CurrencyType Currency; long Amount; }`<br>• `readonly struct UpgradeQuote { Current, Max, IReadOnlyList<CurrencyCost> Costs, Block, CanUpgrade }`<br>• `interface ICharacterUpgrades { UpgradeQuote GetQuote(CharacterType, CharacterUpgradeKind); bool TryUpgrade(CharacterType, CharacterUpgradeKind, out bool saved); event Action<CharacterType, CharacterUpgradeKind> Upgraded; }` |
| S5 | `Scripts/Progression/ProgressionManager.cs` | Implement `ICharacterUpgrades`: tạo `LevelTrack` và `StarTrack`, rồi chọn track bằng `switch (kind)`. Phát `Upgraded` khi `TryUpgrade` thành công. Cập nhật sơ đồ comment đầu file. |
| S6 | `Scripts/UI/Core/HomeController.cs` | Thêm `[SerializeField] Button characterButton` và `[SerializeField] CharacterScreenController characterScreen`. Bấm nút thì gọi `characterScreen.Open()`. Sửa `SetVisible` để nút này không bị tắt. |
| S7 | `Editor/Verification/ServiceContractVerification.cs` | Thêm `typeof(IPlayerSelection)` vào dòng `CheckDomainManager` của CharacterManager, và `typeof(ICharacterUpgrades)` vào dòng của ProgressionManager. |

Tổng cộng có **7 script sửa**. Các file `GameSceneInstaller.cs`, `CharacterLevels.cs`, `PlayerSystem.cs`, `UIManager.cs` và `StatCalculator.cs` **không sửa**:
- `BindInterfacesTo<…Manager>()` tự bind interface mới.
- `CharacterLevels` và `PlayerSystem` đã có đủ API cần dùng.

## Asset, prefab và scene (không phải script)

- **Prefab mới** trong `Assets/_Project/Prefabs/UI/`:
  - `CharacterCard.prefab`, tách từ `CardFrame01_Gray`.
  - `StatRow.prefab`, tách từ `BasicFrame_Round12_Gray`.
  - `CurrencyBar.prefab`, tách từ `Group_ResourceBar`.
- **Asset mới**: `Assets/_Project/DataSO/Databases/CharacterScreenTheme.asset`.
- **Scene `SampleScene`**:
  - Đổi tên `UI/Canvas/CharcaterUI` thành `CharacterScreen`, để không trùng tên class `CharacterUI` dùng cho thanh máu.
  - Gộp `Upgrade_Stats` và `Upgrade_Starts` thành `DetailScreen` có 2 tab; xóa phần Header bị lặp.
  - Đổi tên `Collection_List` thành `CollectionScreen`.
  - Thay 3 bản `Group_ResourceBar` bằng một `CurrencyBar`.
  - Xóa các thẻ đặt tay trong `Content`.
  - Thêm `CharacterScreenController` vào mảng `panels` của `UIManager`.
  - Gán nút Character của Home là `MainUI/BottomBar_Menu/Button_02`.
- **Data**: điền `portrait` và `rarity` cho các nhân vật Player trong `DataSO/Characters/`.
- **Docs**: cập nhật `Docs/Architecture/Systems-Overview.md` (thêm màn Character) và `ProgressionContracts.cs` (comment vai trò).

---

## Task

### Task 1 — Dọn scene và tách prefab (không code)
- **Làm**: toàn bộ mục *Scene* ở trên, trừ việc gắn script. Tách 3 prefab. Giữ nguyên sprite và bố cục mockup.
- **Script**: không có.
- **Kiểm tra**:
  - Report Missing Scripts = 0.
  - Chụp màn hình Collection và Detail để người dùng duyệt bố cục.

**Kết quả (2026-11-19, xong):** Missing Scripts = 0, Check Service Contracts PASS 53, scene đã lưu. Cấu trúc thực tế:

```
UI/Canvas/CharacterScreen
├── CollectionScreen
│   ├── Background, Top/Topbar (Text_Title, Text_Property, Bg_Oblique/Text "8/20")
│   ├── Scroll View/Viewport/Content   ← rỗng, Controller sinh CharacterCard
│   └── Button_Back                    ← mới, về Home (nằm ở dải dưới màn)
├── DetailScreen
│   ├── PanelEffect
│   ├── Header: Label_Rarity, Text_Name, Portrait/Image_Portrait, Button_Prev, Button_Next, Bar/Stars
│   ├── Tabs: Tab_Level, Tab_Stars     ← mới, có Button
│   ├── LevelTab: Slider_Level, Content/Stats (StatRow_1..8 = prefab), Content/Skill (SkillSlot_1..3/Icon)
│   ├── StarTab [off]: PerkList/Perks/PerkRow_1..5 (mockup tĩnh 5 perk, hiện khi mở tab Stars)
│   └── Footer: Button_Back, Button_Select (Text_Select), Button_Upgrade (Text_Title, Area/Cost_1, Cost_2 [off])
└── CurrencyBar                        ← prefab, thay 3 bản copy
```

- Prefab `Prefabs/UI/CharacterCard.prefab`: Bg/Image_Portrait, Border, Group_GradeStars, Badge_Level/Text_Level, Text_Name, Label_InUse (Selected), Icon_Empty (Empty), ButtonGroup [off] giữ nguyên từ mockup.
- Ghi chú cho Task 4: ở trạng thái Empty, `CharacterCardView` cũng tắt Bg/Gradient, Glow, Bottom để thẻ tối như mockup thẻ trống.
- Chữ trên UI viết tiếng Anh (IN USE, SELECT, LEVEL, STARS) cho đồng bộ với mockup và font hiện có.

### Task 2 — Data nhân vật và Theme
- **Script**: N1, N3, S1.
- **Làm**:
  - Tạo `CharacterScreenTheme.asset` và điền icon cho 8 chỉ số. Dùng các icon đang có trong mockup, gồm 4 icon trong `Graphic/Icons/`.
  - Điền `portrait` và `rarity` cho nhân vật Player.
- **Kiểm tra**:
  - Compile sạch.
  - `Theme.Validate()` qua.
  - Asset nhân vật cũ không mất dữ liệu (đọc lại `BaseStats` và `Skills`).

### Task 3 — Service: chọn nhân vật và CanUpgrade
- **Script**: N2, S2, S3, S4, S5, S7.
- **Kiểm tra**:
  - Check Service Contracts PASS.
  - Edit > Zenject > Validate Current Scenes OK.
  - Chạy thử trong Edit Mode (đọc, không ghi save): `GetQuote(Level)` trả level/max/giá đúng theo `CharacterLevelConfigSO`; `GetQuote(Star)` trả `NotAvailable`.

### Task 4 — View
- **Script**: N5, N6, N7, N8, N9.
- **Làm**: gắn component vào scene và prefab, rồi gán tham chiếu trong Inspector.
- **Kiểm tra**:
  - Compile sạch.
  - Missing Scripts = 0.
  - Không còn tham chiếu nào bị null trong Inspector.

### Task 5 — Controller và nối Home
- **Script**: N4, S6.
- **Làm**:
  - Gắn `CharacterScreenController` vào `CharacterScreen`.
  - Thêm controller vào `UIManager.panels`.
  - Gán nút ở Home.
  - Cập nhật Docs.
- **Kiểm tra**:
  - Compile sạch.
  - Check Service Contracts.
  - Zenject Validate.
  - Người dùng play test theo checklist bên dưới.

## Checklist play test (người dùng thực hiện)

- [ ] Từ Home mở được Collection. Số thẻ bằng số nhân vật Player. Nhân vật đang dùng có nhãn "ĐANG DÙNG".
- [ ] Chạm thẻ thì vào Detail đúng nhân vật. ←/→ đổi nhân vật vòng tròn, giữ nguyên tab đang mở.
- [ ] 8 chỉ số khớp chỉ số Player khi vào trận.
- [ ] 3 ô skill hiện đúng icon skill của nhân vật.
- [ ] Bấm Chọn rồi vào trận thì spawn đúng nhân vật. Tắt và mở lại game vẫn nhớ lựa chọn.
- [ ] Nút UPGRADE: còn cấp thì sáng và hiện giá 25. Mỗi lần bấm thanh tăng 25 (chữ `25/100`, `50/100`…); bấm lần thứ 4 thì lên level 2, huy hiệu đổi, chỉ số tăng, thanh về `0/150`. **Tiền chưa bị trừ** (đúng với demo). Tắt rồi mở lại game vẫn giữ đúng level và vị trí thanh. Hết cấp thì thanh đầy, hiện "MAX". Tab Stars hiện 5 dòng perk mockup; nút UPGRADE ở tab này bị tắt (`NotAvailable`).
- [ ] Vào trận sau khi nâng: chỉ số Player tăng đúng theo level mới.
- [ ] CurrencyBar hiện đúng số vàng và kim cương.
- [ ] Back: Detail về Collection, rồi Collection về Home. Không có lỗi trong Console.
