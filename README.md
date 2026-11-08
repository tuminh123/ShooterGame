# ShooterGame

Dự án game hành động 2D đang phát triển bằng **Unity 2022.3.62f3**.

## Mở dự án

1. Clone repository về máy.
2. Thêm thư mục dự án vào Unity Hub và mở bằng Unity 2022.3.62f3.
3. Đợi Unity import asset và khôi phục package.
4. Mở `Assets/_Project/Scenes/SampleScene.unity` và chạy Play.

Player hỗ trợ joystick và bàn phím WASD/phím mũi tên trong Editor hoặc bản PC.

## Cấu trúc

- `Assets/_Project`: code gameplay, scene, prefab, đồ họa và cấu hình ScriptableObject.
- `Assets/Plugins`: các thư viện bên thứ ba được dùng trong dự án.
- `Packages`: khai báo package Unity.
- `ProjectSettings`: cấu hình dự án.
- `Docs/Architecture`: tài liệu thiết kế; một số phần là blueprint chưa triển khai.

Luồng hiện có gồm di chuyển Player/quái, pooling và prewarm trước khi tạo Player, nền theo chunk, level vĩnh viễn/trong trận, loot EXP/vàng, lưu tiến trình và trang bị. Combat dùng SkillDataSO, có bắn thẳng/quạt/vòng tròn/xoắn ốc, đạn dùng chung vỏ và hiệu ứng làm chậm/cháy/độc. Nhân vật có cấu hình visual/hitbox/thanh máu và shader giáp. Luồng loading scene, thắng/thua và trở về chưa được hoàn thiện.

## Tài liệu hệ thống

- [Tổng quan các hệ thống hiện tại](Docs/Architecture/Systems-Overview.md).
- [Thiết kế visual cố định và prefab visual riêng](Docs/Architecture/Character-Visual-Rendering.md) — đề xuất, chưa chuyển code.
- [Pool prewarm](Docs/Architecture/Pool-Prewarm.md).

Xem `Assets/_Project/DataSO/README.md` để biết cách cấu hình dữ liệu gameplay.

Các thư viện và asset bên thứ ba tuân theo giấy phép của tác giả tương ứng. Repository chưa cấp giấy phép riêng cho mã nguồn của dự án.

Xem Docs/Architecture/Skill-Shooting-Pipeline.md để cấu hình skill, kiểu bắn, đạn và hiệu ứng.

Skill cưa/vùng băng, cấp trong trận và bảng nâng cấp shooting được mô tả trong [Skill Run Progression](Docs/Architecture/Skill-Run-Progression.md). Trong Play Mode, chọn một asset skill rồi dùng menu `Shooter Game > Skills > Grant Or Upgrade Selected Skill` để kiểm tra nâng cấp.

Khi lên cấp, [Skill Choice UI](Docs/Architecture/Skill-Choice-UI.md) mở ba lựa chọn với tên, icon, mô tả và tối đa năm sao. Có thể thử bằng menu `Shooter Game > Skills > Grant One Run Level` trong Play Mode.
