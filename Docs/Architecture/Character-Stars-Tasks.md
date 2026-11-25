# Hệ thống Sao nhân vật (nâng bằng mảnh) — kế hoạch task

## Thiết kế hiện hành (2026-11-22) — đọc phần này trước

Mọi phần thưởng của sao nằm trong **`CharacterDataSO`** (`Characters/Data/`); `CharacterLevelConfigSO` không còn perk sao.

- **`skills`** = MỌI skill của nhân vật, mỗi skill một lần. Skill không có bước mở khóa là skill có sẵn.
- **`skillProgression`** = các bước "skill X đạt cấp Y khi thỏa điều kiện Z". Skill của bước **chọn bằng dropdown** từ `skills`
  (`Editor/SkillProgressStepDrawer.cs`), giá trị vẫn lưu tham chiếu asset nên đổi thứ tự `skills` không làm bước trỏ sai.
  Luật: Character Level chỉ để mở khóa (Skill Level = 1); Stars mở hoặc nâng cấp; sao trong 1..5; skill của bước phải nằm trong `skills`.
- **`starStats[5]`** (`StarStatReward`: chỉ số + giá trị cộng thẳng) = **chỉ số dự phòng**: sao n chỉ cộng chỉ số này khi
  **không có bước skill nào ở sao n**. Nút Inspector **"Random chỉ số sao"** điền cho các sao chưa có skill: chỉ số ngẫu nhiên
  trong các chỉ số gốc > 0, giá trị 5–10% chỉ số gốc. Kết quả nằm trong asset (không random lúc chơi, save không lưu thêm).
- Một chỗ tính duy nhất: `GetSkillGrants(level, stars)` (Combat lúc spawn + ô skill), `TryGetStarStat(star)` / `GetStarBonus(stars)`
  (bonus vĩnh viễn + tab Stars). `CharacterLevels.GetTotalBonus` = bonus level + `GetStarBonus`; nâng sao phát `BonusesChanged`.
- Tab Stars: sao có skill hiện "HolyBolt Lv.3"; sao chỉ có chỉ số hiện "+10 Defense" (tên/icon/định dạng từ `CharacterScreenThemeSO`);
  sao không có gì hiện "No reward yet". Inspector cảnh báo (vàng) nhân vật Player chưa có skill hoặc sao không có thưởng.

Các mục bên dưới là lịch sử kế hoạch; S2b (perk cộng chỉ số trong `CharacterLevelConfigSO.starPerks`) đã được thay bằng thiết kế trên.


> **Trạng thái:** 🚧 Đang làm · S1 ✅ · S2a ✅ · S3 ✅ · **S4 ✅ (2026-11-22: sao nâng skill + chỉ số dự phòng, dữ liệu nằm trong CharacterDataSO)** · **S2 thay bằng bộ sưu tập (2026-11-23): mảnh theo độ hiếm thay cho mảnh riêng từng nhân vật, xem [Character-Collection-Tasks](Character-Collection-Tasks.md)** · Ngày lập: 2026-11-20

## S4 — perk sao nối với skill + perk gắn với data nhân vật (người dùng yêu cầu 2026-11-21) — ✅ ĐÃ LÀM 2026-11-22, xem "Thiết kế hiện hành" ở đầu file

**Yêu cầu của người dùng:**
1. Nâng sao không chỉ cộng chỉ số mà còn **kết nối với skill của nhân vật** (ví dụ mở/nâng cấp một skill của chính nhân vật đó).
2. **Thiết kế lại chỗ đặt dữ liệu nâng cấp sao**: nâng cấp phải truy cập được data của nhân vật (`CharacterDataSO`: skill, chỉ số gốc...)
   hoặc được kéo thả vào từ data nhân vật, thay vì chỉ nằm rời trong `CharacterLevelConfigSO` như hiện tại.

**Hiện trạng (để so sánh khi thiết kế):**
- Perk nằm trong `CharacterLevelConfigSO.starPerks` (asset `DataSO/Levels/Permanent/<Tên>_PermanentLevels.asset`), mỗi perk chỉ có
  mô tả, icon và `StatBonus`. Config ghép với nhân vật bằng `CharacterType`, không tham chiếu `CharacterDataSO`.
- Bonus sao đi qua `CharacterLevels.GetTotalBonus` → `IPermanentBonuses.GetBonus` → `PlayerSystem` (StatSheet).
- Skill có sẵn của nhân vật nằm trong `CharacterDataSO.Skills` (`SkillDataSO`, dữ liệu theo từng cấp), do `CharacterSkills` (miền Character / Combat)
  quản lý; hiện không có khái niệm "cấp skill vĩnh viễn" từ Progression.

**Hướng người dùng đang cân nhắc (2026-11-21, CHƯA THỐNG NHẤT — thảo luận sau):**

*Nâng sao:*
- Nâng sao sẽ **nâng cấp skill** hoặc **mở khóa skill** của nhân vật (chưa chọn một trong hai, có thể cả hai).
- **Quy hết perk sao về skill**: hệ thống skill đã có loại tăng chỉ số (`SkillBehaviourType.StatBuff`, cộng qua `StatSheet.AddSource`),
  nên thay vì giữ `StatBonus` riêng trong `StarPerk`, tạo **skill tăng chỉ số riêng cho Player** (`SkillDataSO` kiểu StatBuff).
  Khi đó mọi perk sao đều là "skill": cộng chỉ số = một skill StatBuff, mở/nâng skill = skill thường. Lợi ích: một đường dữ liệu và
  một đường áp dụng duy nhất (DRY), tab Stars hiển thị bằng icon/mô tả của skill.
  Cần kiểm tra khi thiết kế: StatBuff hiện là skill trong trận (có thời gian, cooldown?) hay có thể là bonus vĩnh viễn ngay từ lúc spawn.
- Chọn loại perk bằng **enum** + một switch (người dùng đã chọn enum, không dùng strategy `[SerializeReference]`).

*Nâng level (hai phương án, chưa chọn):*
1. Lên level chỉ tăng chỉ số (như hiện tại: `bonusPerLevel`).
2. Lên level **vừa tăng chỉ số vừa mở khóa skill** ở một số mốc level.

*Tab Level — ô skill:*
- Số ô skill = **đúng số skill của nhân vật** (hiện cố định 3 ô `SkillSlots = 3` trong `CharacterScreenController`, ô thừa để trống).
- **Bấm vào ô skill thì hiện thông tin skill** (tên, mô tả, cấp...). Skill chưa mở khóa có thể hiện trạng thái khóa (chờ chốt hướng trên).

**Câu hỏi còn mở:**
- Nâng sao: nâng cấp skill, mở khóa skill, hay cả hai? Level có mở khóa skill không (phương án 1 hay 2)?
- Dữ liệu perk đặt ở đâu: ngay trong `CharacterDataSO` (kéo skill của chính nhân vật vào), hay SO riêng tham chiếu `CharacterDataSO`?
  Nếu chuyển chỗ thì migrate 5 perk hiện có của BlessedGladiator (thành 5 skill StatBuff) và giữ `.meta` GUID.
- **Contract Progression → skill (người dùng thấy chưa rõ, cần giải thích khi thảo luận):** số sao/level được **lưu** ở Progression
  (`CharacterLevels`), còn skill được **dựng** ở miền Character/Combat (`CharacterSkills`, `SkillFactory`) khi spawn Player.
  Hai miền chỉ nói chuyện qua interface trong `XxxContracts.cs`, nên Combat cần một vai trò đọc được kiểu
  "nhân vật X đã mở skill nào, mỗi skill cấp mấy" (ví dụ thêm hàm vào `IPermanentBonuses` hoặc một interface mới trong
  `ProgressionContracts.cs`). `PlayerSystem` hiện đã làm y hệt với chỉ số (đọc `IPermanentBonuses.GetBonus` lúc spawn), nên skill sẽ đi theo cùng mẫu.
  **Người dùng đồng ý (2026-11-21):** skill vĩnh viễn đi qua interface trong `ProgressionContracts.cs` như mọi giao tiếp giữa miền; tên hàm/vai trò chốt khi thiết kế.
- Tab Stars hiển thị perk skill thế nào (icon skill, mô tả cấp mới); popup thông tin skill dùng chung cho tab Level và tab Stars?

## S2b + S3 — perk sao (làm 2026-11-21)

Perk nằm ngay trong `CharacterLevelConfigSO.starPerks` (tối đa 5 `StarPerk`: mô tả, icon, `StatBonus`) thay vì tách `CharacterStarConfigSO`, vì mỗi nhân vật vẫn chỉ có một asset cấu hình tăng trưởng (Simple). `CharacterLevels.GetTotalBonus` = bonus level + `GetStarBonus(stars)`, nên Player và màn Character dùng cùng con số qua `IPermanentBonuses`. Nâng sao phát `BonusesChanged`. Tab Stars: `StarPerkRowView` trên `PerkRow_1..5` (đã mở / kế tiếp / khóa), dữ liệu qua `ICharacterUpgrades.GetStarPerks`. BlessedGladiator: +60 HP, +8 Damage, +10 Defense, +5% Crit, +25% Crit Damage. **Còn lại của S2:** giá mảnh (`CurrencyType.Shard`) và xóa `FreeUpgradesForTesting`.

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
   - `IWallet` chỉ **thêm** hàm có tham số `owner`; hàm cũ giữ nguyên nên Battle/Stage/CurrencyBar không bị ảnh hưởng.
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
| L1 ✅ (2026-11-23, làm trước S2) | Level trừ tiền thật (`CharacterLevelConfigSO.upgradeCurrency`, mặc định Gold): thiếu tiền → `NotEnoughCurrency`; đủ → trừ tiền + nạp điểm trong một lần save qua `Wallet.CanAfford` / `Wallet.SpendWith`. S2 dùng lại đúng hai hàm này cho mảnh. | `Wallet.cs`, `CharacterLevels.cs`, `CharacterUpgradeTracks.cs`, `CharacterLevelConfigSO.cs`, `ProgressionManager.cs`, `CharacterScreenController.cs` |
| S4 📝 (chưa thiết kế) | Perk sao nối với skill nhân vật + dữ liệu perk truy cập / kéo từ data nhân vật. Xem mục **S4** ở trên. | Chốt thiết kế trước |

Mỗi task: compile sạch, Check Service Contracts, Zenject Validate; báo người dùng trước khi sang task kế tiếp. Người dùng play test.
