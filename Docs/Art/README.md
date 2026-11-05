# Art: Pickup / Mockup

Thư mục này chứa ảnh và bản vẽ minh họa do AI tạo bằng code (môi trường không có `image_gen`).

## Sprite nằm trong Unity (`Assets/_Project/Graphic/Pickups/`)

| File | Mô tả | Kích thước |
|---|---|---|
| `HealthPotion.png` | Bình máu: nút bần + thân thủy tinh đỏ | 96×96 (base 24×24 ×4) |
| `HolyWaterVial.png` | Lọ nước thánh: nắp vàng + thân xanh | 96×96 |
| `DungeonKey.png` | Chìa khóa vàng | 96×96 |

Tuân theo chuẩn trong `Docs/Art/Pickup-Art-Prompts.md`: nền trong suốt, palette hạn chế,
viền sẫm 1px, không gradient/glow/AA, upscale nearest-neighbor. Khi import vào Unity hãy đặt:
**Sprite Mode = Single, Filter Mode = Point, Compression = None, Max Texture Size ≥ 96.**

## Mockup (`Docs/Art/Mockups/`)

| File | Nội dung |
|---|---|
| `hud-layout.svg` | Bố cục HUD 1280×720: bảng player (HP/EXP), minimap + wave, joystick, thanh kỹ năng 4 slot, thông báo giữa màn hình |
| `level-layout.svg` | Sơ đồ bản đồ arena: tường, hồ nước, dung nham, crate, spawn player/quái, vật phẩm, cổng thoát + chú giải |
| `main-menu.svg` | Mockup màn hình chính: tiêu đề, 4 nút, sprite trang trí, "press any key" |
| `gothic-menu.svg` | Menu chính **gothic chibi**: tím/đen/đỏ máu, nến, dơi, nút khắc đá, linh vật chibi |
| `gothic-hud.svg` | HUD trong trận **gothic chibi**: thanh HP lọ máu, MP tím, minimap, kỹ năng, joystick |
| `gothic-character.svg` | Màn hình hồ sơ nhân vật **gothic chibi**: chân dung lớn, chỉ số, trang bị, túi đồ |

SVG xem được trực tiếp (kẻ ô vuông). Muốn xuất PNG: mở file trong trình duyệt rồi in/xuất,
hoặc chụp màn hình.

## Tái tạo

- Sprite: `node Docs/Art/Tools/gen-pixel-art.mjs`
- Kiểm tra sprite: `node Docs/Art/Tools/check-sprites.mjs` (ASCII preview), `node Docs/Art/Tools/check-png.mjs` (thông số PNG)
- Server xem trước cục bộ: `node Docs/Art/Tools/serve.mjs` (http://localhost:8765)
