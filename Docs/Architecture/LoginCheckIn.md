# Login Check-in (quà đăng nhập)

> **Trạng thái:** ✅ Hiện hành · Kiểm tra với code: 2026-11-26. Kiểm tra tự động: **Shooter Game > Verification > Check Login Check-in**.

Người chơi vào game mỗi ngày thì nhận **một ô quà kế tiếp** của mỗi lịch. Bỏ ngày không mất gì và không quay về ô 1
(ví dụ: ngày 1 chơi → ô 1, ngày 2 nghỉ, ngày 3 chơi → ô 2). Không có Claim x2.

## 1. Data: `LoginCheckInCalendarSO`

Asset nằm ở `Assets/_Project/DataSO/LoginCheckIn/`, tạo bằng menu **Create > Shooter Game > Login Check-in > Calendar**.

| Field | Ý nghĩa |
| --- | --- |
| `id` | Lưu trong save; không trống, không trùng. Đổi ID thì tiến độ của lịch về 0. Popup ghép panel với lịch theo ID này. |
| `displayName` | Chữ trên ruy băng tiêu đề của popup. |
| `cycle` | `SevenDay` (7 ô, nhận đủ thì sang ngày sau bắt đầu vòng mới) · `Month` (theo tháng UTC, reset ngày 1, nhận tối đa bằng số ngày của tháng, tối đa 31 ô) · `Period` (chu kỳ `periodDays` ngày tính từ `periodStart`, chung cho mọi người chơi) · `Event` (mở trong `[eventStart, eventEnd)`, không reset). |
| `clock` | `Device` = giờ máy (chỉnh được; asset hiện tại để Device vì đang test) · `Server` = giờ server, chống chỉnh giờ tới/lùi; chưa có giờ (mất mạng) thì lịch bị ẩn. |
| `days` | Mỗi ô **một** phần thưởng (`IReward`: tiền, nhân vật, mảnh, trang bị), theo thứ tự ngày. |

Quy tắc kiểm tra (`Validate`): SevenDay đúng 7 ô; Month ≤ 31; Period ≤ `periodDays`; Event ≤ số ngày event; ngày dạng `yyyy-MM-dd`.

**Gia hạn event:** chỉ sửa `eventEnd`. Mã kỳ của Event là ngày mở, nên tiến độ giữ nguyên. Không đổi `eventStart` hay `id` khi event đang chạy.
Ví dụ 7 ô trong 10 ngày (`eventStart` 2026-12-01, `eventEnd` 2026-12-11): người chơi có 3 ngày dư để bù ngày bỏ lỡ.

Luật kỳ nằm ở một switch duy nhất: `LoginCheckInCalendarSO.TryGetPeriod` (giống `MissionDatabaseSO.TryGetCycle`); phép tính ngày/tháng/chu kỳ ở `Scripts/Utils/TimeCycles.cs`.

## 2. Miền `LoginCheckIn` (`Assets/_Project/Scripts/LoginCheckIn/`)

```text
LoginCheckInManager                     chỉ class này được bind (GameSceneInstaller > Login Check In)
└── LoginCheckInCalendar[]              runtime mỗi lịch: kỳ, ClaimedDays, ngày nhận gần nhất, trạng thái từng ô
⬆ ILoginCheckIn (LoginCheckInContracts.cs)   Calendars, HasClaimable, Refresh(), TryClaim(...), ⚡ Changed
Phụ thuộc: Save (ISaveStore) · Clock (IGameClock) · Progression (IRewardGrant)
```

- Nhận được khi: lịch mở, có giờ, còn ô trong kỳ, hôm nay chưa nhận.
- `TryClaim`: kiểm tra gói → đánh dấu đã nhận → `IRewardGrant.GrantRewards(..., SaveEntry)`: phần thưởng và cờ "đã nhận" nằm trong **một lần ghi**.
- Save: key `login_check_in` (`LoginCheckInSaveData`: `calendarId`, `cycle`, `claimedDays`, `lastClaimDay`). Chỉ ghi khi nhận quà; đổi kỳ chỉ tính lại trong RAM và tính ra y hệt ở lần mở sau. Save cũ không có key này thì đọc ra mặc định (không cần migration).
- Nghe `IGameClock.Changed`: server vừa trả giờ hoặc vừa mất giờ thì lịch dùng `Server` mở/đóng ngay.
- Lifecycle: Initialize sau Mission, trước Game; Dispose ngay sau Mission.

## 3. UI

| Thành phần | Nằm ở | Vai trò |
| --- | --- | --- |
| `LoginCheckInPopup` (`UIPanelController`, **overlay**) | `Canvas/Login` | Popup đè lên Home. Mỗi lịch một panel dựng sẵn (`Daily_Bonus_7Day` ↔ `7day`, `Daily_Bonus_30Day` ↔ `period30`). Mở bằng `StaggeredPopupTween`. Bấm ô "hôm nay" để nhận → `RewardRevealView` dùng chung. Mở từ nút: chỉ lịch đó. Tự mở (một lần mỗi phiên, khi Home đã hiện xong và còn quà): Close chuyển sang lịch kế tiếp còn quà, hết thì đóng. |
| `LoginCheckInDayView` | Từng thẻ ngày | Chỉ hiển thị. Mỗi trạng thái bật một nhóm object và tô màu nền / phần nhấn / nhãn ngày (gán trên Inspector): **hôm nay** (7 ngày: `Focus`, hiệu ứng, dải trắng `FocusTop` + chữ xanh lá; 30 ô: góc `Corner` trắng + số xanh lá; nhấp nháy), **chưa tới** (`Glow`, màu gốc; ô tím giữ màu tím), **đã nhận** (`Icon_Check`, nền xám, icon mờ). Icon giữ tỉ lệ; icon/số lượng lấy từ `RewardSlotFactory.Preview`. |
| `AttentionPulse` | `Scripts/UI/Views/` | Tween phóng to/thu nhỏ lặp lại (PrimeTween, unscaled time) để thu hút; dùng chung cho các panel nhận quà sau này. Không gắn chung object với `ButtonClickTween` (giành scale). |
| Nút ở Home (mỗi lịch một nút) | `MainUI/Middle/Button_Gift_Divided` (7 ngày), `Button_Login30` (30 ngày, icon lịch) | `HomeController > Login Entries`: mỗi nút mở đúng lịch của nó, chấm than đỏ riêng (`Alert_Login`, `Alert_Login30`, đặt cạnh nút vì nút có Mask) do `NotificationBadgeView` (khóa Login + ID lịch) lo — xem Notification.md, đồng hồ hiện thời gian còn lại của kỳ (lịch 7 ngày không có hạn thì ẩn). Lịch đang đóng thì ẩn nút. NotificationManager kiểm tra sang ngày mới mỗi giây; Home chạy đồng hồ và gọi `TryAutoOpen`. |

**Popup là overlay của `UIManager`** (`UIPanelController.IsOverlay = true`): `UIManager` chỉ hiện một *màn* tại một thời điểm, còn overlay
đè lên màn đang hiện mà không ẩn nó, không vào stack điều hướng, và bị đóng khi màn dưới đổi (mở màn khác, đổi state, Back). Như mọi panel,
`UIManager` tắt nó lúc khởi động và tự bật object khi mở, nên scene lưu `Login` bật hay tắt đều chạy như nhau. Việc tự mở nằm ở
`HomeController.Update` (không ở popup) vì object popup có thể đang tắt.

## 4. Thêm một lịch mới

1. Tạo asset `LoginCheckInCalendarSO`, điền `id`, `cycle`, `clock`, các ô.
2. Thêm asset vào **GameSceneInstaller > Login Check In > Calendars** (thứ tự = thứ tự popup tự mở).
3. Dựng một panel trong `Canvas/Login` (hoặc dùng lại bố cục có sẵn), gắn `LoginCheckInDayView` cho từng thẻ, thêm một phần tử vào
   `LoginCheckInPopup > Panels` với `calendarId` = ID của asset.
4. Chạy **Check Login Check-in** và **Check Service Contracts**.
