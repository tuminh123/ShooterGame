# Kiểm tra kiến trúc và binding

Dự án không có bước build/lint bằng CLI. Sau mỗi thay đổi về contract, binding hoặc MonoBehaviour/SO, chạy các bước dưới đây
trong Unity Editor (có thể chạy qua Unity MCP, xem [Unity-MCP.md](Unity-MCP.md)).

| Bước | Menu | Chế độ | Kiểm tra gì |
| --- | --- | --- | --- |
| 1 | **Shooter Game > Verification > Check Service Contracts** | Edit Mode | Không có interface nào là facade tổng hợp (chỉ getter trả về interface khác); đếm số contract public. |
| 2 | **Edit > Zenject > Validate Current Scenes** | Edit Mode | Toàn bộ đồ thị dependency của `SampleScene` resolve được (thiếu binding, sai kiểu, vòng phụ thuộc). |
| 3 | **Shooter Game > Verification > Report Missing Scripts** | Edit/Play | Component mất script (thường do di chuyển file mà quên `.meta`). |
| 4 | **Shooter Game > Verification > Run Service Smoke Checks** | Play Mode, đợi tới Home | Mỗi miền một Manager: mọi vai trò resolve về cùng instance, không bind concrete, Initialize/Tick/Dispose đăng ký đúng một lần; smoke test di chuyển, tìm mục tiêu, đạn, skill, effect, sát thương chết, stale handle, pause lồng nhau, chơi lại (reset level/thống kê/skill) và về Home. |

Code nằm ở `Assets/_Project/Editor/Verification/ServiceContractVerification.cs`.
Smoke check không cấp thưởng kết quả, không ghi ví và không kiểm tra hình ảnh/animation/UI. Thoát Play Mode sau khi chạy.

Khi thêm một miền hoặc vai trò mới: thêm một dòng `CheckDomainManager(...)` trong `CheckBindings` với Manager và các vai trò của nó.

Các runner độc lập cũ (`Tests/SkillProgression`, `Tests/EffectPlayback`) đã bị xóa ở Giai đoạn 7: chúng biên dịch code production
cùng host double và tham chiếu tới các file đã không còn, nên kết quả không còn ý nghĩa.
