# Bảng thay đổi script — phiên làm việc 2026-11-20

> **Trạng thái:** chưa commit (nhánh `refactor/architecture`). Mọi thay đổi đều compile sạch, Check Service Contracts PASS 55,
> Zenject Validate OK, 0 Missing Script. Play test do người dùng thực hiện.
>
> **Lưu ý:** file này nằm trong thư mục `Resources`, nên Unity sẽ đóng gói nó vào bản build (dưới dạng TextAsset).
> Nếu không muốn vậy, chuyển file sang `Docs/`.

## 1. Tổng quan theo tính năng

| # | Tính năng | Miền | Script liên quan |
|---|---|---|---|
| F1 | Thanh level thành **thanh EXP nâng cấp** (mỗi lần bấm nạp nguyên liệu, đầy thanh thì lên cấp, phần dư chuyển sang cấp sau) | Progression, Save, UI | `CharacterLevelConfigSO`, `CharacterLevels`, `CharacterUpgradeTracks`, `ProgressionContracts`, `SaveSections`, `CharacterScreenModels`, `CharacterScreenController`, `CharacterDetailView`, `ProjectDataSOEditor` |
| F2 | **5 ô sao luôn hiện** (sao chưa có bị làm mờ) | UI | `CharacterDetailView`, `CharacterCardView` |
| F3 | **Mở nút nâng sao để test** (lưu số sao, tạm miễn phí bằng cờ test) | Progression, Save | `SaveSections`, `CharacterLevels`, `CharacterUpgradeTracks`, `ProgressionManager` |
| F4 | **Tool Character UI Exporter**: xuất ảnh chân dung + prefab nhân vật UI, tự gán vào CharacterData | Editor, UI, Data | `CharacterUIExporter`, `CharacterUIVisual`, `CharacterDataSO`, `ProjectDataSOEditor` |
| F5 | Màn Detail hiện **nhân vật UI đứng Idle**, **ăn mừng (Victory)** khi lên cấp hoặc lên sao | UI | `CharacterDetailView`, `CharacterScreenModels`, `CharacterScreenController` |
| F6 | **UX:** nút nảy khi bấm, trượt màn/tab (4 hướng), số nhảy khi tăng, Canvas lồng | UI, Editor | `ButtonClickTween`, `SlideTransition`, `TextPunch`, `UITweenSetup`, `CharacterDetailView`, `CharacterScreenController`, `StatRowView`, `CharacterScreenModels` |
| F7 | **Thanh bar Home dùng chung:** hiện cả ở Collection, nút màn đang mở nổi lên, ẩn ở Detail, nền chung, bỏ nút Back của Collection, chỉnh hướng trượt tab | UI | `HomeNavigationBar`, `HomeController`, `CharacterCollectionView`, `CharacterScreenController`, `CharacterDetailView` |

## 2. Script tạo mới (7 file, 1185 dòng)

| File | Dòng | Loại | Vai trò | Tính năng |
|---|---:|---|---|---|
| `Scripts/UI/Core/HomeNavigationBar.cs` | 131 | MonoBehaviour | Thanh bar dưới dùng chung (trên object luôn bật `HomeNavigation`). Nút Battle → `CharacterScreenController.Close()`, nút Heroes → `Open()`. Nút của màn đang mở nổi lên (bật bản Focus). Khóa nút chưa có chức năng. Ẩn ở Detail (trượt xuống), bật/tắt nền chung theo trạng thái Home. Nghe `IGameState.StateChanged` và `CharacterScreenController.NavigationChanged` | F7 |
| `Scripts/UI/Views/CharacterUIVisual.cs` | 97 | MonoBehaviour | Nhân vật dạng UI gồm **Rig** (xương rỗng, cùng đường dẫn với prefab gameplay nên chạy được clip gốc) và **Layers** (Image xếp theo `sortingOrder`). Chép tư thế xương sang Image trong `LateUpdate`. API: `PlayIdle()`, `Celebrate()`, `SyncParts()`. Animator chạy unscaled time | F4, F5 |
| `Scripts/UI/Views/SlideTransition.cs` | 185 | MonoBehaviour + enum | Enum `SlideDirection { Left, Right, Up, Down }`. Trượt panel vào/ra bằng `anchoredPosition` (PrimeTween, unscaled). API: `PlayIn(...)`, `PlayOut(...)`, `SetVisibleInstant()`, `Opposite()`. Inspector: **Enter From**, **Exit To**, thời gian vào/ra. Khóa click khi đang trượt | F6 |
| `Scripts/UI/Views/ButtonClickTween.cs` | 71 | MonoBehaviour | Nút nảy khi bấm, cùng thông số với nút Play (0.9 → 1.08 → 1, 0.12s + 0.12s, unscaled). Chạy song song, không trì hoãn hành động của nút | F6 |
| `Scripts/UI/Views/TextPunch.cs` | 79 | Lớp thuần | Số phóng to, nảy về và nháy xanh khi giá trị **tăng**. View tạo và giữ lớp này, không cần gắn vào scene | F6 |
| `Editor/CharacterUIExporter.cs` | 554 | EditorWindow | Menu `Tools/Shooter Game/Character UI Exporter`. Đọc `CharacterDatabaseSO`, dựng Preview Scene, ép tư thế Idle, chụp PNG nền trong suốt (chụp nền đen + trắng để tách alpha), xuất prefab `CharacterUIVisual`, tạo `UI_CharacterController`. Tự gán `portrait` và `uiVisualPrefab`. Có nút xuất tất cả | F4 |
| `Editor/UITweenSetup.cs` | 68 | Editor menu | Menu `Tools/Shooter Game/UI/Add Click Tween To All Buttons`: gắn `ButtonClickTween` cho mọi nút trong scene và prefab của `_Project`. Bỏ qua nút đã có `PopupTweenBase` (nút Play) hoặc dùng transition Animation | F6 |

## 3. Script đã sửa (15 file)

### 3.1. UI — màn Character và Home

| File | +/− | Thay đổi chi tiết | Tính năng |
|---|---|---|---|
| `Scripts/UI/Core/HomeController.cs` | −12 | Bỏ `characterButton`, `characterScreen`, `OpenCharacterScreen()`; giờ chỉ khóa các nút thuộc MainUI ngoài nút Play. Thanh bar do `HomeNavigationBar` quản lý | F7 |
| `Scripts/UI/CharacterScreen/CharacterCollectionView.cs` | −15 | Bỏ `backButton`, event `BackClicked`, `OnBack()` và phần Lifecycle; về Home bằng thanh bar | F7 |
| `Scripts/UI/CharacterScreen/CharacterDetailView.cs` | +159 | **(1)** Thanh hiện `đã nạp/cần`, đầy thì hiện "MAX" (field `maxLevelText`). **(2)** `BindStars()`: 5 ô sao luôn hiện, màu `filledStarColor`/`emptyStarColor`; bỏ `SetActiveCount()`. **(3)** `BindCharacter()`, `GetOrCreateCharacter()`, `FitIntoPortrait()`: nhân vật UI vừa khung `Image_Portrait`, mỗi prefab chỉ tạo một lần; `Celebrate()` public. **(4)** `BindTabs()`: chỉ trượt khi thật sự đổi tab, `SetTabInstant()`. **(5)** `TextPunch` cho huy hiệu level và chữ thanh; nhận ra đổi nhân vật (`boundType`) để không nhảy số khi bấm ←/→. **(6)** Field `tabSlideForward` (Inspector: **Tab Slide Forward**) chọn hướng lướt khi đổi tab | F1, F2, F5, F6, F7 |
| `Scripts/UI/CharacterScreen/CharacterScreenController.cs` | +86 | **(1)** Truyền `Progress`/`ProgressRequired` vào model. **(2)** `OnUpgrade()`: so cấp/sao trước và sau, chỉ gọi `detail.Celebrate()` khi thật sự tăng. **(3)** Truyền `UIVisualPrefab`, `Type`, giá trị số của chỉ số. **(4)** Trượt màn: `SetVisible` trượt vào; `OnCardClicked` cho Detail trượt đè Collection; `OnDetailBack` cho Detail trượt ra; `OnCollectionBack` trượt ra rồi mới đóng màn. Thêm `transitionCompanion`, `closing`, `Busy` để khóa thao tác khi đang trượt. **(5)** Cho thanh bar: `Close()` public (thay `OnCollectionBack`), `IsShown`, `IsOnDetail`, event `NavigationChanged` (phát khi mở/đóng màn và đổi trang) | F1, F5, F6, F7 |
| `Scripts/UI/CharacterScreen/CharacterScreenModels.cs` | +20 | `CharacterDetailModel` thêm `Type`, `UIVisual`, `LevelProgress`, `LevelProgressRequired`. `StatRowModel` thêm `Amount` (giá trị số để biết tăng hay giảm) | F1, F5, F6 |
| `Scripts/UI/CharacterScreen/CharacterCardView.cs` | +19 | `BindStars()`: 5 ô sao luôn hiện với màu Filled/Empty | F2 |
| `Scripts/UI/CharacterScreen/StatRowView.cs` | +22 | `Bind(model, animate)` + `TextPunch`; `OnDisable` trả text về dáng gốc | F6 |

### 3.2. Progression / Save (logic nâng cấp)

| File | +/− | Thay đổi chi tiết | Tính năng |
|---|---|---|---|
| `Scripts/Progression/Data/CharacterLevelConfigSO.cs` | ±57 | Bỏ `initialUpgradeCost`, `upgradeCostMultiplier`, `TryGetUpgradeCost`. Thêm `initialRequired` (A, nhận lại giá cũ qua `FormerlySerializedAs`), `requiredIncreasePerLevel` (B), `amountPerUpgrade`, `TryGetRequired(level)` = A + B × (L − 1). Quái chỉ đọc `maxLevel` + `bonusPerLevel`, nên không bị ảnh hưởng | F1 |
| `Scripts/Progression/CharacterLevels.cs` | +133 | Bỏ `TryGetUpgradeCost`, `TryIncreaseLevel`. Thêm `GetUpgradePoints`, `TryGetRequiredPoints`, `GetPointsToMaxLevel`, `TryAddUpgradePoints` (nạp điểm, lên nhiều cấp, ghi một lần save), `ClampUpgradePoints` lúc load, region **Stars** (`GetStars`, `TryAddStar`), `GetOrAddEntry` | F1, F3 |
| `Scripts/Progression/CharacterUpgradeTracks.cs` | +66 | `LevelTrack`: giá = `amountPerUpgrade` (lần chạm trần chỉ tốn phần còn thiếu), `TryUpgrade` nạp điểm. `StarTrack`: nhận `CharacterLevels`, đọc/ghi sao thật; **cờ test `FreeUpgradesForTesting = true`** (Task S2 sẽ xóa) | F1, F3 |
| `Scripts/Progression/ProgressionContracts.cs` | +11 | `UpgradeQuote` thêm `Progress`, `ProgressRequired` | F1 |
| `Scripts/Progression/ProgressionManager.cs` | +6 | `new StarTrack(levels)`; cập nhật sơ đồ comment | F3 |
| `Scripts/Save/Data/SaveSections.cs` | +17 | `CharacterLevelsSaveData.Entry` thêm `upgradePoints`, `stars`; cập nhật `Clone`, `Validate` (không âm). Save cũ thiếu field thì đọc ra 0 | F1, F3 |

### 3.3. Data / Editor

| File | +/− | Thay đổi chi tiết | Tính năng |
|---|---|---|---|
| `Scripts/Characters/Data/CharacterDataSO.cs` | +6 | Thêm `uiVisualPrefab` (`CharacterUIVisual`) + getter `UIVisualPrefab` | F4 |
| `Editor/ProjectDataSOEditor.cs` | +7 | Inspector `CharacterLevelConfigSO` hiện 3 field EXP mới. Inspector `CharacterDataSO` thêm mục **CHARACTER SCREEN (UI)** (rarity, portrait, uiVisualPrefab; trước đây portrait và rarity không hiện) + nút mở exporter | F1, F4 |

## 4. Asset và doc đã đổi

| File | Loại | Thay đổi |
|---|---|---|
| `Scenes/SampleScene.unity` | Scene | `SlideTransition` trên CollectionScreen, DetailScreen, LevelTab, StarTab, BottomBar_Menu (hướng Down). Canvas lồng trên `Stats`, `Slider_Level`, `Portrait`. `ButtonClickTween` trên 47 nút. **Thanh bar:** `MainUI/Background` → `HomeBackground` (nền chung, trước MainUI); object mới `HomeNavigation` (sau CharacterScreen) chứa `BottomBar_Menu` + `HomeNavigationBar`; nút mới `Button_02_Focus` ("HEROES"); xóa `CollectionScreen/Button_Back` |
| `Prefabs/UI/CharacterCard.prefab` | Prefab | `ButtonClickTween` |
| `Prefabs/UI/CurrencyBar.prefab` | Prefab | Canvas lồng + GraphicRaycaster, `ButtonClickTween` |
| `Prefabs/UI/Characters/UI_BlessedGladiator.prefab` | Prefab mới | Nhân vật UI (8 bộ phận) |
| `Animations/UI/UI_CharacterController.controller` | Mới | Idle mặc định, trigger `Celebrate` → Victory một lần → Idle (dùng lại clip gốc) |
| `Graphic/Portraits/BlessedGladiator_Portrait.png` | Ảnh mới | Chân dung 512×512 nền trong suốt |
| `DataSO/Characters/BlessedGladiator_Character.asset` | Data | Gán portrait + uiVisualPrefab |
| `DataSO/Levels/Permanent/BlessedGladiator_PermanentLevels.asset` | Data | A = 100, B = 50, mỗi lần nạp 25 |
| `DataSO/README.md` | Doc | Mục 4 (thanh EXP nâng cấp), bảng API CharacterLevels |
| `Docs/Architecture/Character-Screen-Tasks.md` | Doc | Các mục thay đổi 2026-11-20, checklist |
| `Docs/Architecture/Character-Stars-Tasks.md` | Doc mới | Kế hoạch hệ thống sao (S1–S4) |

## 5. Vấn đề phát hiện (không do phiên này gây ra)

| Vấn đề | Chi tiết |
|---|---|
| `Prefabs/CharacterType/Flower_Berry.prefab` bị cụt | Dòng cuối `loseState:` thiếu giá trị, có sẵn từ commit `93733702` (13/11). Unity báo không đọc được file. Chưa sửa vì nằm ngoài phạm vi |
| `Layer Lab/.../PresetData.asset` bị sửa | Đổi lúc 15:57, là các slot preset của pack CharacterMaker (có thể do người dùng lưu preset). Tool không đụng tới |
| DLL cũ / reference null khi mở dự án | Nguyên nhân lỗi LootDatabase và nút Book lúc đầu phiên. Đã compile lại, reimport và tải lại scene. Nếu gặp lại: Reimport `Scripts` và **không Save scene** khi thấy reference bị null |

### Lỗi đã sửa: không bấm được thẻ để vào Detail (do `SlideTransition`)

`PlayOut` tắt panel (`SetActive(false)`) **trước** khi gọi callback "trượt xong". `OnDisable` → `Stop()` xóa callback, nên
`CharacterScreenController` kẹt ở trạng thái đang trượt (`Busy`) và bỏ qua mọi lần bấm thẻ (sau lần Back đầu tiên từ Detail hoặc bấm Battle).
Sửa: `Finish(deactivate)` lấy callback ra trước rồi mới tắt panel.

**Sau đó bỏ hẳn cờ khóa** (người dùng duyệt, nguyên tắc Simple): `CharacterScreenController` không còn `Busy`, `closing`,
`transitionCompanion`, `BeginPageTransition`, `EndPageTransition`. Collection **luôn bật** làm nền khi màn mở; Detail tự bật/tắt bằng
`SlideTransition.PlayIn/PlayOut`. Chỉ còn một callback bắt buộc: nút Battle chờ Collection trượt ra xong mới báo UIManager đóng màn.
Bấm liên tục khi đang trượt chỉ làm tween mới thay tween cũ (giật nhẹ), không thể kẹt thao tác.

### Lỗi đã sửa: nút Heroes trên thanh bar không bấm được

Scene lưu các nút mockup ở trạng thái `interactable = false`; trước đây `HomeController` mở khóa nút Heroes lúc chạy vì nó nằm trong MainUI.
Khi tách thanh bar khỏi MainUI, không còn ai mở khóa. Sửa: `HomeNavigationBar.OnEnable` gọi `NavTab.SetInteractable()` cho Battle và Heroes
(cả bản nổi, để không bị tô màu xám).

## 5b. Đợt 8 task (2026-11-20)

| # | Task | Script | Scene/Asset |
|---|---|---|---|
| 8 | Sửa `Flower_Berry.prefab` bị cụt (thiếu ký tự xuống dòng cuối sau `loseState:`) | — | Prefab |
| 3 | Topbar Home hiện ví thật (vàng **và** kim cương) | `HomeController` bỏ code vàng trùng; dùng lại `CurrencyBarView` | `CurrencyBarView` trên `MainUI/Topbar/Group_ResourceBar` |
| 4 | Giữa Home hiện nhân vật đang chọn (prefab UI Idle, tên, level, thanh EXP), cập nhật khi chọn/nâng cấp | `HomeController`; **mới** `UI/Views/CharacterPreviewSlot.cs` (lớp thuần dùng chung với Detail, tách từ `CharacterDetailView`) | Nối `Middle/Character`, `Middle/Text (TMP)`, `Middle/Slider_Level`; Canvas lồng trên `Middle/Character` |
| 1 | Giữ nút UPGRADE để nâng liên tục (nhanh dần), thả tay không tính dư một lần | **mới** `UI/Views/ButtonHoldRepeat.cs` | Trên `Button_Upgrade` |
| 2 | Thanh EXP chạy mượt; lên cấp thì chạy đầy → về 0 → chạy tới phần dư | `CharacterDetailView.AnimateBar` (field `barFillDuration`) | — |
| 5 | Vuốt trái/phải ở Detail để đổi nhân vật | `CharacterDetailView` (`IBeginDragHandler`/`IDragHandler`/`IEndDragHandler`, field `swipeThreshold`) | — |
| 6 | Nút bị khóa (MAX…) rung ngang khi bấm | **mới** `UI/Views/ButtonDeniedShake.cs` | Trên `Button_Upgrade` |
| 7 | Home và màn Character trượt đẩy nhau (Home ra trái, Character vào từ phải và ngược lại) | `HomeController.SetVisible`, `CharacterScreenController.SetVisible` (trượt cả màn), `Close()` chỉ còn `RequestVisibility(false)`, `IsShown => WantsVisible` | `SlideTransition` trên `MainUI` (Left) và `CharacterScreen` (Right); bỏ SlideTransition của `CollectionScreen` |

### Điều chỉnh: bấm Play vào trận không trượt Home

`HomeController.SetVisible` chỉ trượt khi đổi qua lại với màn Character (field `hiddenByMenu`: Home bị ẩn lúc state vẫn là Home).
Vào trận / từ trận về Home thì bật/tắt ngay như cũ, vì `ScreenTransition` đã làm màn tối dần.
`ButtonClickTween.Play` bỏ qua khi nút đã bị tắt (hết cảnh báo PrimeTween khi bấm tab thanh bar).

**Bấm Play (bản cuối, người dùng chốt):** như cũ — nút Play nảy → màn tối dần → vào trận. Thanh bar **không trượt** khi rời/về Home:
`HomeNavigationBar.Refresh` nhận ra state đổi giữa Home và không-Home (`wasHome`) nên bật/tắt ngay cùng Home lúc màn đã tối,
tránh hở đáy màn. Chỉ trượt khi vẫn ở menu (vào/ra Detail). Đã bỏ thử nghiệm "thanh bar trượt xuống trước khi tối" (`HideThen`, `Restore`, field `navigationBar`).

## 6. Việc còn mở

| Task | Nội dung |
|---|---|
| S2 | Mảnh nhân vật: `CurrencyType.Shard` (theo nhân vật trong ví), `CharacterStarConfigSO` (Manual / Geometric), perk Stat cộng chỉ số, **xóa cờ test** |
| S3 | Tab Stars bind perk thật (`StarPerkRowView`) |
| L1 | Level trừ vàng thật (dùng chung cơ chế ghi một lần với S2) |
| UX tiếp | Khi đóng/mở màn Character, phần nội dung của Home (Topbar, nút Play) tắt/bật tức thời vì UIManager chỉ cho một panel hiện; nền chung che phần trống. Muốn Home trượt cùng thì cần sửa UIManager (bước riêng) |
