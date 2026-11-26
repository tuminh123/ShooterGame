# Damage Text (số sát thương bay lên)

> **Trạng thái:** ✅ Hiện hành · Kiểm tra với code: 2026-11-26. Đo Profiler: 0 byte cấp phát mỗi frame, 1 draw call, ~0,04 ms/frame.

## 1. Nằm ở đâu

| Thứ | Vị trí |
| --- | --- |
| Hệ thống | `Assets/_Project/Scripts/Characters/DamageText/DamageTextSystem.cs` |
| Data (font, màu, cỡ, nhịp) | `Assets/_Project/Scripts/Characters/DamageText/DamageTextStyleSO.cs` |
| Asset cấu hình | `Assets/_Project/DataSO/Characters/DamageTextStyle.asset` |
| Gán vào game | **GameSceneInstaller > Damage Text Style** (trống = tắt số) |
| Nơi báo đòn trúng | `CharacterView.NotifyDamageReceived` (mọi loại sát thương đều đi qua đây) |

## 2. Luồng

```text
Đạn / chém / skill / cháy-độc → CharacterView.ReceiveDamage → controller trừ giáp, HP
  → CharacterView.NotifyDamageReceived → DamageTextSystem.Show(view, DamageInfo, DamageResult)   (bỏ qua nếu quái ngoài màn hình)
CharacterManager.LateTick → DamageTextSystem.LateTick: tính vị trí/cỡ/độ mờ, dựng lại 1 mesh
CharacterManager.DespawnAll (về Home, chơi lại) → DamageTextSystem.Clear
```

`CharacterManager` tạo hệ thống bằng `new` và truyền vào vỏ qua `CharacterContext` (như Search, Runtimes): không event riêng cho từng
quái, không closure, không binding Zenject mới.

## 3. Vì sao tối ưu

- **Một mesh cho mọi số**: 1 draw call, không GameObject cho từng số, không Canvas phải rebuild.
- **Mảng struct cố định** (`Max Count` ô) và buffer đỉnh tạo một lần: sau khởi tạo không cấp phát. Đầy thì thay số cũ nhất.
- **Không tween**: vị trí, cỡ, độ mờ tính thẳng từ thời gian (thời gian game, nên pause thì đứng yên).
- **Hình chữ lấy từ atlas của font SDF có sẵn** (`Cairo_Line_Black SDF`): nét sắc, viền giống chữ UI. Số được tự định dạng vào mảng char, không tạo string.
- **Gộp số**: cùng mục tiêu trúng liên tiếp trong `Merge Window` thì cộng dồn và nảy lại (OrbitSaw, cháy, độc không làm rác màn hình).

## 4. Hiển thị

| Trường hợp | Màu (field) |
| --- | --- |
| Quái trúng đòn | `Enemy Hit` (trắng) |
| Chí mạng | `Critical` (vàng), to hơn `Critical Scale`, nảy mạnh hơn, thêm "!" |
| Player bị đánh | `Player Hit` (đỏ) |
| Chỉ trừ giáp | `Shield Hit` (xanh nhạt) |

Số lớn rút gọn: 1.250 → `1.2K`, 3.400.000 → `3.4M`. Font phải có `0-9 . K M !` (style tự kiểm tra lúc khởi động).
