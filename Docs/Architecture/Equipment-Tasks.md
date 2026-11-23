# Hệ thống Trang bị (Equipment) — thiết kế và kế hoạch task

> **Trạng thái:** ✅ E1–E5 code xong (2026-11-22) · compile sạch, Check Service Contracts PASS 57, Zenject Validate OK, 0 Missing Script, play test qua MCP OK · Chờ người dùng kiểm tra · **Trừ tiền thật ✅ (2026-11-23)** · Còn lại: Fuse, nguồn rơi đồ

## 0. Quyết định đã chốt

| # | Quyết định |
|---|---|
| 1 | Đồ **dùng chung** cho mọi nhân vật. 6 slot: Vũ khí, Mũ, Giáp, Găng, Giày, Nhẫn |
| 2 | Chỉ số chính cố định theo slot: Vũ khí → Damage · Mũ → MaxHealth · Giáp → Defense · Găng → CritChance · Giày → MoveSpeed · Nhẫn → CritDamageMultiplier |
| 3 | 5 độ hiếm nằm **trên từng bản đồ** (cùng một mẫu mũ có thể ra 5 độ hiếm). Số dòng phụ ban đầu: 0 / 1 / 2 / 3 / 4 |
| 4 | Tối đa Lv.20. Chỉ số chính tăng mỗi cấp. Ở mốc 5/10/15/20: mốc thứ k mà số dòng phụ < k thì **mở dòng mới**, đủ rồi thì **cộng vào một dòng phụ ngẫu nhiên** |
| 5 | Giá nâng cấp giống nâng anh hùng. Từ 2026-11-23: thiếu tiền thì quote trả `NotEnoughCurrency` (nút tắt, giá đỏ); đủ thì trừ tiền và lên cấp trong **một lần save** (`Wallet.SpendWith`, key `currencies` + `equipment_items`) |
| 6 | Mỗi món có **tối đa 1 skill, 1 cấp**, mở khi món đạt `skillUnlockLevel` |
| 7 | Mỗi món thuộc 0 hoặc 1 **set** (enum `EquipmentSet`). Mỗi set có **1 skill, 1 cấp**, bật khi mặc đủ `requiredPieces` món |
| 8 | Skill trùng là lỗi game design, không chặn trong data. Lúc spawn ưu tiên **Character > Equipment (món theo thứ tự slot, rồi set) > skill trong trận** |
| 9 | Skill của đồ/set là definition riêng nguồn `SkillSource.Equipment`, có đúng 1 cấp |
| 10 | Save có `itemId` không còn trong database thì **xóa món đó** (đổi ID cũng tính là xóa — không đổi ID đã phát hành) |
| 11 | Chỉ số chính theo bảng chung (slot + độ hiếm); khoảng roll dòng phụ dùng chung mọi độ hiếm |
| 12 | Có đồ để test bằng `starterItems` (save chưa nhận thì nhận một lần). Chưa làm Fuse |

| | Đầu | Lv5 | Lv10 | Lv15 | Lv20 |
|---|---|---|---|---|---|
| Common | 0 | mở | mở | mở | mở #4 |
| Uncommon | 1 | cộng | mở | mở | mở #4 |
| Rare | 2 | cộng | cộng | mở | mở #4 |
| Epic | 3 | cộng | cộng | cộng | mở #4 |
| Legendary | 4 | cộng | cộng | cộng | cộng |

## 1. Data (`Scripts/Progression/Equipment/Data/`)

- Enum trong `GameEnums.cs` (region Progression): `EquipmentSlot`, `EquipmentRarity`, `EquipmentSet`. Không dùng lại
  `CharacterRarity` vì enum đó không có `Uncommon` (chèn vào giữa sẽ lệch số đã lưu).
- `EquipmentDataSO` (mỗi mẫu một asset): `id`, tên, mô tả, icon, `slot`, `set`, `skill`, `skillUnlockLevel`.
  Luật skill ở một chỗ: `TryGetSkill(itemLevel, out skill)`.
- `EquipmentRulesSO` (một asset): chỉ số chính theo slot × độ hiếm (gốc + mỗi cấp), số dòng phụ ban đầu theo độ hiếm,
  `maxLevel`, `milestoneInterval`, `maxSubstats`, khoảng roll theo `StatType`, giá nâng (`levelUpCurrency` + `levelUpCost`: công thức `ILevelFormula` × hệ số theo `EquipmentRarity`, xem `DataSO/README.md` mục 4b).
- Luật mốc dòng phụ (`IsMilestone`, `WillUnlockSubstat`, `TryGetSubstatUnlockLevel`) nằm trên `EquipmentRulesSO`, để `EquipmentGrowth`
  và popup đọc chung một chỗ.
- `EquipmentDatabaseSO` (giữ GUID): `rules`, `items`, `sets` (`EquipmentSetData`: tên, icon, mô tả, `requiredPieces`, `skill`;
  luật bật ở một chỗ `IsActive(pieces)`), `starterItems`.

## 2. Runtime và Save

- `EquipmentItem` (class runtime, chỉ getter; chỉ `EquipmentInventory`/`EquipmentGrowth` sửa): `Uid`, `Definition`, `SetData`,
  `Rarity`, `Level`, `MainStat`, `MainValue`, `Substats` (`StatModifier` Add).
- Save **không đổi schema** (vẫn 4): dùng hai key mới `equipment_items` và `equipment_slots`. Key cũ `equipment` (danh sách ID loại,
  không có slot/độ hiếm, database lúc đó rỗng) bị bỏ, không đọc lại. Lý do: key mới thiếu thì đọc ra mặc định, nên không cần migration
  và không có rủi ro Easy Save đọc dữ liệu kiểu cũ vào kiểu mới.
- Load: món có `itemId` mất → xóa + warning; dòng phụ sai so với luật (trùng chỉ số chính, thiếu/thừa) → sửa + warning; slot trỏ tới
  món không còn hoặc sai loại → gỡ + warning. Có sửa thì ghi lại save.

## 3. System trong Progression

```
ProgressionManager
├── EquipmentGrowth      luật thuần: chỉ số chính, roll dòng phụ, mốc 5/10/15/20, sửa dòng phụ khi load
├── EquipmentInventory   các món, Grant (nội bộ), quote + TryLevelUp, starter items  → key equipment_items
└── EquipmentLoadout              slot → uid, Equip/Unequip/UnequipAll, CountEquipped, set đang bật, TotalBonus → key equipment_slots
```

## 4. Contract (`ProgressionContracts.cs`)

- `IPermanentBonuses`: không đổi (GetBonus đã gồm `EquipmentLoadout.TotalBonus`).
- `ICharacterProgress` (+2 hàm, cùng nơi dùng là spawn Player): `GetEquippedItems`, `GetActiveSets`.
- `IEquipment` (màn Equipment): `Items`, `GetEquipped`, `CountEquipped`, `Equip`, `Unequip`, `UnequipAll`, `GetQuote`, `TryUpgrade`, `Changed`.
- `IEquipmentGrant` (`Grant(item, rarity)`): thêm 2026-11-22 khi có nơi dùng đầu tiên là nút Gacha của panel test (`UI/Debug/GachaEquipmentButton`). Thưởng Stage/loot sau này dùng cùng vai trò.

## 5. Combat

`CharacterSkillSystem.CollectSkillGrants` (nhánh Player): skill nhân vật → skill của từng món (thứ tự slot) → skill set; skill đã có thì bỏ bản sau.
Skill trong trận là asset nguồn Gameplay khác hẳn nên luôn đứng sau.

## 6. UI (`Scripts/UI/Equipment/`)

- `EquipmentScreenController` (panel UIManager, mở từ nút Bag trên thanh Home), `EquipmentLoadoutView` (6 ô, chỉ số, set, nút),
  `EquipmentInventoryView` (6 tab + lưới đồ chưa mặc), `EquipmentCardView` (thẻ dùng chung), `EquipmentInfoPopupView` +
  `EquipmentStatRowView` (popup chi tiết), `EquipmentStatsPopupView` (Property: 8 chỉ số), `EquipmentThemeSO`.
- Popup: dòng chính (+X xem trước), 4 dòng phụ (khóa: "Mở ở Lv.X"), khối Skill, khối Set (x/y), gợi ý mốc, Equip/Unequip, Level Up/MAX.
- Fuse và sao độ hiếm (`Group_GradeStars`) bị ẩn.

## 7. Task

| Task | Nội dung | Trạng thái |
|---|---|---|
| E1 Data | enum, `EquipmentDataSO`, `EquipmentRulesSO`, `EquipmentDatabaseSO`, inspector, asset mẫu |✅ |
| E2 Logic + Save | `EquipmentItem`, `EquipmentGrowth`, `EquipmentInventory`, `EquipmentLoadout`, save section, contract, nối `ProgressionManager` |✅ |
| E3 Skill | Combat cấp skill đồ + set theo thứ tự ưu tiên |✅ |
| E4 UI màn chính | theme, thẻ, ô, tab, lưới, controller, tab Home |✅ |
| E5 UI popup | popup chi tiết, popup Property |✅ |
| Sau | Fuse, nguồn rơi đồ, bán/phân rã | |
