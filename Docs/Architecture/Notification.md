# Notification (chấm báo trên UI)

> **Trạng thái:** ✅ Hiện hành · Kiểm tra với code: 2026-11-26. Kiểm tra tự động: **Shooter Game > Verification > Check Notifications**.

Một nơi duy nhất quyết định **khi nào hiện chấm báo** cho mọi hệ thống. UI chỉ hiển thị, nên mọi chấm cùng khóa luôn khớp nhau
(thanh dưới, nút ở Home, tab trong màn).

## 1. Vị trí: tầng trên cùng

```text
            UI  (NotificationBadgeView, MissionScreenController)
             ↑  đọc INotifications
     NotificationManager              ← tầng TRÊN CÙNG (trên cả Mission)
             │  đọc interface của tầng dưới
  IMissions · ILoginCheckIn · IShop · ICharacterUpgrades · ICharacterCollection · IPlayerSelection · IEquipment · IWallet
```

Notification chỉ gọi xuống qua interface; chiều ngược là các C# `event` có sẵn của tầng dưới. **Không miền nào bên dưới biết
Notification**, nên thêm thông báo không phải sửa Mission/Shop/Progression.

## 2. Cấu trúc (`Assets/_Project/Scripts/Notification/`)

```text
NotificationManager            chỉ class này được bind; Initialize + Tick + Dispose (Dispose ĐẦU TIÊN)
└── NotificationCounter        đánh dấu khóa khi nguồn đổi, báo một lần mỗi khóa mỗi frame, cộng khóa gộp
    └── INotificationSource    (Strategy, NotificationSources.cs) — nơi DUY NHẤT chứa luật của từng loại
        ├── QuestNotificationSource    ← IMissions
        ├── LoginNotificationSource    ← ILoginCheckIn
        ├── ShopNotificationSource     ← IShop
        ├── HeroNotificationSource     ← ICharacterUpgrades, ICharacterCollection, IPlayerSelection, IWallet
        └── GearNotificationSource     ← IEquipment, IWallet
⬆ INotifications (NotificationContracts.cs)   Get(key, item) → NotificationState { Count, Active }, ⚡ Changed(key)
```

## 3. Khóa và luật (`NotificationKey` trong `GameEnums.cs`)

| Khóa | Đếm | Mục (`item`) |
| --- | --- | --- |
| `Quest` | Mission đã xong chưa nhận, ở các nhóm đang mở | ID nhóm (`daily`, `weekly`, …) |
| `Login` | Lịch đăng nhập còn quà hôm nay | ID lịch (`7day`, `period30`) |
| `Shop` | Gói **miễn phí** còn lượt (gói trả tiền không báo, nếu không chấm sáng mãi) | ID gói |
| `Heroes` | Nhân vật có việc: **đủ vàng cho cả level tiếp theo** (không phải một lần bấm), hoặc lên sao được, hoặc ghép được | Tên `CharacterType` |
| `Gear` | Đồ **đang mặc** nâng cấp được (túi đồ không tính) | Tên `EquipmentSlot` |
| `Home` | Khóa gộp: `Quest + Login` (nút Battle khi đang ở tab khác) | — |

**Nhịp:** nguồn đổi → chỉ đánh dấu; `Tick` báo UI một lần mỗi khóa (nhiều event cùng frame chỉ báo một lần). Mỗi giây
(unscaled time, Home pause vẫn chạy) Manager gọi `Sync()` của các nguồn → `Refresh()` của Mission, LoginCheckIn, Shop, để sang
ngày/kỳ mới chấm bật/tắt ngay. Số đếm không cache: `Get` hỏi thẳng nguồn nên không lệch dữ liệu thật.
**Trước `Initialize`** (view trong scene bật sớm hơn Initialize của mọi Manager, lúc Progression chưa đọc save) `Get` luôn trả 0
và `Tick` không làm gì; Tick đầu tiên sau `Initialize` báo mọi khóa để các chấm vẽ lại đúng.

## 4. UI: `NotificationBadgeView` (`Scripts/UI/Views/`)

Gắn lên **object luôn bật** (thường là nút), trỏ `Badge` tới chấm cần bật/tắt; `Key`, `Items` (cộng các mục; trống = tổng), `Mode`
(`Dot` / `Count`, lớn hơn 9 hiện "9+"). View tắt (nút bị ẩn, bản thường của tab đang Focus) thì chấm tắt theo.

| Chỗ | Khóa |
| --- | --- |
| Thanh dưới: `Button_01` Shop / `Button_02` Heroes / `Button_03` Battle / `Button_04` Gear | `Shop` / `Heroes` / `Home` / `Gear` (đều Dot đỏ "!") |
| Home: `Button_Quest`, `Button_Gift_Divided`, `Button_Login30` | `Quest` / `Login` + `7day` / `Login` + `period30` |
| Shop: `Tab_Offers`, `Tab_Daily`, `Tab_Resources` | `Shop` + ID các gói của tab |
| Tab màn Mission (tạo bằng code) | `MissionScreenController` đọc `INotifications.Get(Quest, ID nhóm)` |

Quy ước: mọi chấm báo dùng **một kiểu duy nhất — chấm đỏ "!"** (Mode = Dot). Mode Count (hiện số) vẫn có sẵn nếu sau này cần.

## 5. Thêm một loại thông báo

1. Thêm giá trị `NotificationKey` (ở cuối, đánh số rõ ràng).
2. Thêm một class `XxxNotificationSource` trong `NotificationSources.cs` (bọc interface của miền cần báo).
3. Thêm một dòng `counter.Add(...)` trong constructor của `NotificationManager`.
4. Gắn `NotificationBadgeView` với khóa mới lên UI; chạy **Check Notifications** và **Check Service Contracts**.
