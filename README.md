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

Luồng hiện có gồm di chuyển Player/quái, pooling, nền theo chunk, level vĩnh viễn và level trong trận, lưu tiến trình và chỉ số runtime của Player. Database trang bị hiện rỗng. Combat và skill chưa hoàn thiện.

Xem `Assets/_Project/DataSO/README.md` để biết cách cấu hình dữ liệu gameplay.

Các thư viện và asset bên thứ ba tuân theo giấy phép của tác giả tương ứng. Repository chưa cấp giấy phép riêng cho mã nguồn của dự án.
