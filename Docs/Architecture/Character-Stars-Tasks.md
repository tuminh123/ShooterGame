# Hệ thống Sao nhân vật (nâng bằng mảnh) — kế hoạch task

> **Trạng thái:** 🚧 Đang làm · S1 ✅ code xong (2026-11-20) · S2a ✅ mở nút nâng sao để test (2026-11-20) · S2–S3 chưa làm · Ngày lập: 2026-11-20

## S2a — mở nút nâng sao để test (người dùng yêu cầu 2026-11-20)

Làm trước một phần của S2 để test UI: số sao lưu thật (`CharacterLevelsSaveData.Entry.stars`, ghi qua `CharacterLevels.TryAddStar`),
`StarTrack` mở thêm một sao mỗi lần bấm tới 5. **Chưa tốn mảnh**: cờ test tạm `StarTrack.FreeUpgradesForTesting = true`
(đặt false thì nút nâng sao bị khóa như cũ). Sao chưa cộng chỉ số vì chưa có config perk.
**Task S2 phải xóa cờ này** và thay bằng giá mảnh thật.

## Quyết định đã chốt (người dùng duyệt 2026-11-20)

1. Xây trên `StarTrack` có sẵn (`Progression/CharacterUpgradeTracks.cs`): biến track "luôn khóa" thành logic thật, không tạo track mới.
2. Nâng sao bằng **mảnh nhân vật**, và mảnh là **một loại currency**: chỉ thêm **một** giá trị `CurrencyType.Shard`.
   Khi giá là `Shard`, ví trừ mảnh của **chính nhân vật đang được nâng** (khóa là `CharacterType` có sẵn).
   - Lý do: dùng lại toàn bộ `IWallet` (đọc/cộng/trừ, event), `UpgradeQuote.Costs`, icon theo `CurrencyType` của Theme,
     và đường thưởng `IWallet.Add`/`AddBatch` (loot, `StageRewards`) cho nguồn mảnh sau này.
   - Không chọn "mỗi nhân vật một `CurrencyType`": mỗi nhân vật mới phải thêm enum và icon.
   - `IWallet` chỉ **thêm** hàm có tham số `owner`; hàm cũ giữ nguyên nên Run/Stage/CurrencyBar không bị ảnh hưởng.
3. Giá mảnh mỗi sao có 2 cách, chọn bằng enum + một switch (không làm strategy vì mỗi cách chỉ một dòng — Simple trước):
   - `Manual`: nhập tay số mảnh cho từng sao.
   - `Geometric` (cấp số nhân): mảnh sao n = `ceil(Base × Multiplier^(n − 1))`. Ví dụ Base 10, ×2 → 10, 20, 40, 80, 160.
   - Có cách thứ 3 cần tham số riêng thì mới chuyển sang strategy `[SerializeReference]` (giống `IShooter`).
4. Config sao chọn loại tiền (`costCurrency`, mặc định `Shard`); chọn Gold/Diamond thì trừ ví chung như thường.
5. Sao **không yêu cầu level**, chỉ cần đủ mảnh. Giai đoạn đầu chỉ có perk **Stat**; 5 perk của BlessedGladiator tạm điền theo mockup.
6. Trừ tiền và +1 sao ghi trong **một lần save** (`SaveBatch` key `currencies` + `character_levels`), theo mẫu `Wallet.AddBatch(..., stageProgress)` có sẵn.

## Task

| Task | Nội dung | File |
|---|---|---|
| **S1** ✅ | 5 ô sao luôn hiện: sao đã có màu vàng, sao chưa có làm mờ bằng màu `Image` (project không có sprite sao xám). Áp dụng header Detail và thẻ Collection. | Sửa: `UI/CharacterScreen/CharacterDetailView.cs`, `UI/CharacterScreen/CharacterCardView.cs` |
| **S2** | `CurrencyType.Shard`; mảnh theo nhân vật trong ví; `CharacterStarConfigSO`; `stars` trong save; `StarTrack` thật; bonus sao vào `IPermanentBonuses`; nút debug "+10 mảnh". | Mới: `Progression/Data/CharacterStarConfigSO.cs`, `UI/Debug/AddShardsButton.cs`. Sửa: `Progression/CurrencyType.cs`, `Progression/Wallet.cs`, `Progression/ProgressionContracts.cs`, `Progression/ProgressionManager.cs`, `Progression/CharacterLevels.cs`, `Progression/CharacterUpgradeTracks.cs`, `Progression/Data/LevelDatabaseSO.cs`, `Save/Data/SaveSections.cs`, `Editor/ProjectDataSOEditor.cs`. Asset: `BlessedGladiator_Stars.asset`, `LevelDatabase.asset`, icon mảnh trong `CharacterScreenTheme`. |
| **S3** | Tab Stars bind perk thật: `StarPerkRowView` trên `PerkRow_1..5` (icon, mô tả; trạng thái đã mở / kế tiếp / khóa); nút UPGRADE tab Stars hiện giá mảnh. | Mới: `UI/CharacterScreen/StarPerkRowView.cs`. Sửa: `CharacterScreenModels.cs`, `CharacterScreenController.cs`, `CharacterDetailView.cs`. Scene: gắn component lên `PerkRow_1..5`. |
| L1 (hỏi lại sau S2) | Level trừ vàng thật bằng cùng cơ chế ghi một lần, xóa TODO trong `LevelTrack`. | `CharacterUpgradeTracks.cs`, `Wallet.cs` |
| S4 (kế hoạch riêng) | Perk SkillLevel: cần contract đưa cấp skill vĩnh viễn từ Progression sang `CharacterSkills` (miền Character). | — |

Mỗi task: compile sạch, Check Service Contracts, Zenject Validate; báo người dùng trước khi sang task kế tiếp. Người dùng play test.
