# Bộ sưu tập nhân vật: sở hữu, mảnh riêng của từng nhân vật, ghép — kế hoạch task

> **Trạng thái:** ✅ Đã đổi sang mảnh riêng của từng nhân vật (2026-11-23) · compile sạch, Check Service Contracts PASS, Report Missing Scripts 0 · test Play Mode qua MCP đạt (gacha trùng, nâng sao, nút UPGRADE hiện "cần/có").
> Phiên bản trước (mảnh dùng chung theo độ hiếm + bản trùng + phân rã) đã bị thay thế; xem mục Lịch sử.

## Yêu cầu (người dùng)

- Collection hiện **đúng số nhân vật Player trong CharacterDatabase**, không lấp thẻ trống.
- **Đã sở hữu**: thẻ màu như cũ (level, sao). **Chưa sở hữu**: thẻ xám, ẩn level/sao, thay bằng **thanh mảnh**.
  Đủ mảnh → thanh và viền thẻ sáng lên, chạm vào là **ghép** ra nhân vật. Chưa đủ → chạm chỉ xem Detail, **không có nút nâng cấp**.
- Gacha ra nhân vật: chưa có thì **sở hữu luôn**; đã có thì thành **mảnh riêng của nhân vật đó** (số mảnh theo độ hiếm).
- Mảnh dùng để **ghép** (khi chưa có) và **nâng sao**. Level vẫn trả Gold.
- Số mảnh ghi trong database **theo độ hiếm** (hai nhân vật cùng độ hiếm cùng số mảnh).
- Giá nâng sao dùng **công thức** (`ILevelFormula`, như trang bị).
- Icon mảnh là **đầu nhân vật**, xuất từ Character UI Exporter.
- Mảnh thừa khi đủ 5 sao: cộng dồn, cơ chế tiêu thụ/ghép làm sau.

## Thiết kế

- **`CharacterShards`** (`ProgressionContracts.cs`): struct `Type` + `Amount`. Chỉ có hai trường vì độ hiếm suy ra được từ nhân vật
  (CharacterDataSO.Rarity), lưu thêm sẽ có hai nguồn sự thật cho cùng một thông tin.
- **Lưu trữ**: `long shards` trong `CharacterLevelsSaveData.Entry` (cùng Entry với level/sao), vì `CharacterLevels` là chủ duy nhất của
  key `character_levels`. Mảnh không đi qua ví nên mỗi thao tác chỉ ghi tiến trình một lần save.
- **Luật** trong `CharacterCollection`: `Grant`, `TryAssemble`, `GetNextStarCost`, `CanAffordNextStar`, `TryAddStar`.
  Luật số liệu trong `LevelDatabaseSO` (`unlockShards`, `shardsPerDuplicate`, `starCost`, nhân vật khởi đầu).
- **Giá nâng sao**: `StarTrack` trả `UpgradeQuote` bằng `ShardCost` + `ShardsOwned` (không dùng `Costs`, vì mảnh không phải tiền ví);
  lý do chặn mới `UpgradeBlock.NotEnoughShards` (thêm ở cuối enum). Level và trang bị không đổi.
- **`Grant` trả `out CharacterShards gained`**: nơi gọi (nút gacha, sau này gacha thật) báo người chơi mà không tự tính lại luật.
- **Icon mảnh**: `CharacterDataSO.ShardIcon` (field `shardIcon`, thiếu thì dùng `Portrait`).

## Task đã làm

| Task | Nội dung |
|---|---|
| T1 | Save `shards`, `CharacterShards`, `CharacterCollection` mới, bỏ `TryDismantle`/`Copies`/`StarShards`/`ShardOf`, đổi tên `shardsPerCopy` → `shardsPerDuplicate` (`FormerlySerializedAs`). |
| T2 | `UpgradeQuote.ShardCost`/`ShardsOwned`, `UpgradeBlock.NotEnoughShards`, `StarTrack`. |
| T3 | UI Character: nút UPGRADE hiện "cần/có" + icon, báo thiếu mảnh, thanh mảnh thẻ xám theo mảnh riêng, nút gacha debug báo mảnh nhận được. |
| T4 | Gỡ DISMANTLE: code (`CharacterDismantlePopupView`, `DismantleRowView`, model, nút) và scene (`Popup_Dismantle`, `Button_Dismantle`). |
| T5 | Character UI Exporter: nút "Xuất icon mảnh", khung zoom/lệch riêng, "Xuất tất cả". |
| T6 | Kiểm tra + tài liệu. |

## Lịch sử

Bản K1–K9 (2026-11-23, trước đó): mảnh là tiền ví theo độ hiếm (`CurrencyType.Shard*`), bản trùng lưu thành `copies`, phân rã bằng popup
DISMANTLE, nâng sao dùng bản trùng trước rồi trừ mảnh ví. Đã thay bằng thiết kế trên vì người chơi không thấy bản trùng ở đâu và có hai kho mảnh
cho cùng một việc nâng sao.
