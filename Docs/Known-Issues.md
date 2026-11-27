# Lỗi đã biết (chưa tái hiện được)

## [Nghiêm trọng] Đứng input khi bấm Claim trên thẻ Mission

**Trạng thái:** chưa tìm ra nguyên nhân, chưa tái hiện được bằng test tự động (902 bước bấm + 3 vòng Reset Data, 55 lần bấm Claim liên tục: không kẹt). Gặp một lần khi chơi tay.

**Triệu chứng (người chơi báo):**
- Ở màn Mission (Quest), bấm nút Claim trên một thẻ nhiệm vụ.
- Popup nhận thưởng màu đen (RewardReveal) **chưa kịp hiện**.
- Toàn bộ màn hình không nhận chạm nữa, kể cả nút Back. Game không crash: tween lặp (chữ Claim nhấp nháy) vẫn chạy.
- Thoát game vào lại: nhiệm vụ đã ở trạng thái đã nhận (phần thưởng đã lưu).

**Đã loại trừ:**
- Không phải đơ frame / vẽ lại Canvas nặng: tween vẫn chạy mượt.
- Không phải cờ `presenting` của `MissionScreenController`: cờ đó không chặn nút Back.
- Thứ tự vẽ đúng: `Popup` (index 11) nằm trên `Mission_List` (index 8).
- Log của lượt chơi đó không có exception.
- Overlay `ScreenTransition` và `ButtonClickTween` không giữ chặn raycast.

**Nghi vấn còn lại:**
1. `RewardRevealView` vừa bật với `group.alpha = 0` (đang fade 0 → 1): lớp `Dimmer` phủ toàn màn đã chặn chạm nhưng vô hình. Nếu fade không chạy tiếp (tween bị dừng/mất) thì màn hình bị khóa vô hình. Điểm chưa khớp: chạm vào Dimmer gọi Skip và đặt alpha = 1, lẽ ra popup phải hiện.
2. Một `SlideTransition` bị bỏ dở để `CanvasGroup.blocksRaycasts = false` trên màn hiện tại.

**Khi gặp lại:** đừng bấm Stop. Ghi lại thao tác ngay trước đó, rồi kiểm tra trong Hierarchy: `UI/Canvas/Popup/RewardReveal` có active không, `CanvasGroup.alpha` và `blocksRaycasts` của nó và của `Mission_List`, và EventSystem đang trỏ object nào (Inspector của EventSystem ở Play Mode).

**Hướng phòng vệ đã đề xuất (chưa làm):** tự gỡ khóa `presenting`, giữ raycast trong lúc slide, cho Dimmer chạm đâu cũng nhận; tách tween lặp sang Canvas con riêng.

Code liên quan: `UI/Mission/MissionScreenController.cs` (`OnClaim`, `OnRevealClosed`), `UI/Mission/RewardRevealView.cs` (`Show`, `Skip`, `FinishClose`), `UI/Views/SlideTransition.cs`.
