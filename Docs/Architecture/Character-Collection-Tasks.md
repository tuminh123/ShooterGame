# Bộ sưu tập nhân vật: sở hữu, mảnh theo độ hiếm, ghép, bản trùng — kế hoạch task

> **Trạng thái:** ✅ K1–K9 code xong (2026-11-23) · compile sạch, Check Service Contracts PASS · Chờ người dùng play-test. Người dùng yêu cầu làm lần lượt mọi task, không cần duyệt từng bước.

## Yêu cầu (người dùng, 2026-11-23)

- Collection hiện **đúng số nhân vật Player trong CharacterDatabase**, không lấp thẻ trống.
- **Đã sở hữu**: thẻ màu như cũ (level, sao). **Chưa sở hữu**: thẻ xám, ẩn level/sao, thay bằng **thanh mảnh**.
  Đủ mảnh → thanh và viền thẻ sáng lên, chạm vào là **ghép** ra nhân vật. Chưa đủ → chạm chỉ xem Detail, **không có nút nâng cấp**.
- Gacha ra nhân vật: chưa có thì **sở hữu luôn**; đã có thì vẫn cộng vào bộ sưu tập (**bản trùng**).
- **Mảnh theo độ hiếm** (dùng chung mọi nhân vật cùng độ hiếm). Nhân vật Common nâng cấp bằng mảnh Common.
- Bản trùng: **phân rã thành mảnh ở một chỗ riêng**; ra trùng thì **nâng cấp được luôn**.
- Số mảnh ghi trong database **theo độ hiếm, chung mọi nhân vật** (phù thủy Common và kỵ sĩ Common cùng là 10 mảnh).
- Giá nâng cấp bằng mảnh dùng **công thức** (`ILevelFormula`, như trang bị).

## Giả định đã chọn (người dùng không trả lời câu hỏi, chọn hướng hợp lý nhất — đổi được trong data)

1. **"10 mảnh" = giá trị một bản trùng khi phân rã** (Common 10). Ghép và nâng sao có số riêng trong database.
2. **Mảnh dùng để nâng sao.** Level vẫn trả Gold như cũ.
3. **Bản trùng được giữ lại.** Khi nâng sao: dùng bản trùng của chính nhân vật đó trước (mỗi bản = giá trị phân rã), thiếu thì trừ mảnh;
   giá trị dư của bản trùng vào ví mảnh, không mất.
4. **Chỗ phân rã**: popup riêng mở từ nút trên đầu màn Collection; mỗi nhân vật có bản trùng một dòng, nút phân rã hết.
5. **Nhân vật khởi đầu** (sở hữu sẵn, không cần lưu): danh sách trong LevelDatabaseSO, mặc định BlessedGladiator.

## Thiết kế

- **Mảnh là tiền tệ**: `CurrencyType.ShardCommon..ShardLegendary` (thêm ở cuối enum). Dùng lại Wallet: `CanAfford`, trừ/cộng và
  ghi cùng một lần save với tiến trình nhân vật.
- **Sở hữu + bản trùng** lưu trong `character_levels` (cùng Entry với level/sao), vì `CharacterLevels` là chủ duy nhất của key này
  (hai chủ cùng ghi một key sẽ đè nhau). Save cũ không có field → chưa sở hữu, 0 bản trùng; nhân vật khởi đầu luôn được tính là đã có.
- **Luật bộ sưu tập** trong `LevelDatabaseSO`: mỗi độ hiếm có `unlockShards` (ghép), `shardsPerCopy` (phân rã);
  `starCost` = `RarityScaledFormula<CharacterRarity>` (mảnh cho sao kế tiếp); danh sách nhân vật khởi đầu.
- Logic nằm trong `CharacterCollection` (Progression, do ProgressionManager tạo); miền khác dùng `ICharacterCollection`.

## Task

| Task | Nội dung |
|---|---|
| K1 | 5 loại mảnh trong `CurrencyType`; icon + tên hiển thị tiền trong CharacterScreenTheme; thông báo "Not enough ..." dùng tên hiển thị. |
| K2 | Luật bộ sưu tập trong `LevelDatabaseSO` + Inspector. |
| K3 | Save (`owned`, `copies`), `Wallet.ApplyWith` (cộng/trừ có dấu, ghi cùng tiến trình), `CharacterCollection`, `ICharacterCollection`. |
| K4 | `StarTrack` thật (mảnh + bản trùng), xóa `FreeUpgradesForTesting`; `LevelTrack`/`StarTrack` chặn khi chưa sở hữu. |
| K5 | UI Collection: đủ số thẻ, thẻ xám + thanh mảnh, sáng khi đủ, chạm để ghép, đếm "đã có/tổng". |
| K6 | UI Detail: chưa sở hữu thì ẩn nút nâng cấp và nút chọn. |
| K7 | Popup phân rã bản trùng. |
| K8 | Nút debug: gacha nhân vật ngẫu nhiên, +mảnh mọi độ hiếm. |
| K9 | Docs. |
