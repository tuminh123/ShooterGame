# Chỉnh hitbox

Mở **Tools → Shooter Game → Hitbox Editor** ngoài Play Mode. Chọn dữ liệu ngay trong cửa sổ, không cần mở từng asset trong Project.

- **Nhân vật:** tìm tên, chọn nhân vật, dùng tab **Hitbox** của preview để kéo tâm và cạnh hộp. Có thể chỉnh visual và thanh máu trong cùng cửa sổ. **Khớp hitbox với hình ảnh** lấy bounds của sprite làm hộp ban đầu.
- **Đạn:** chọn Combat Assets đang dùng trong game, rồi chọn loại Holy/Fire/Ice/Poison. Mỗi loại lưu **Hit Offset** và **Hit Radius** riêng trong catalog. Kéo mũi tên để đổi tâm vòng tròn hoặc kéo tay nắm để đổi bán kính; cũng có thể nhập trực tiếp. Nút khớp hình ảnh đặt tâm tại tâm bounds sprite và bán kính bao quanh các góc.

Offset đạn dùng world unit như bán kính: X dọc hướng bay, Y bên trái hướng bay. Offset xoay theo đạn khi đổi hướng; scale hình ảnh không đổi offset hoặc bán kính. Sweep, gizmo runtime và vị trí spawn impact dùng tâm hitbox đã dịch. Skill chỉ chọn BulletType; mọi skill/cấp dùng cùng BulletType đều lấy hitbox của loại đó. Đổi lựa chọn loại đạn trong tool không đổi BulletType của skill.

Hitbox đạn vẫn là vòng tròn; phép khớp sprite chỉ là điểm bắt đầu để căn thủ công, không phải va chạm theo từng pixel. Thay đổi áp dụng cho đúng nhân vật hoặc đúng loại đạn đang chọn, hỗ trợ Undo/Redo. Bấm **Lưu thay đổi** để ghi asset. Preview tự dọn khi đổi tab, đóng cửa sổ hoặc vào Play Mode.
