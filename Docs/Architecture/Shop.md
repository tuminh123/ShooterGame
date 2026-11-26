# Shop (gói mua bằng tài nguyên / tiền thật)

> **Trạng thái:** ✅ Hiện hành · Kiểm tra với code: 2026-11-26. Kiểm tra tự động: **Shooter Game > Verification > Check Shop**.
> Gacha làm riêng sau, không nằm trong tài liệu này.

Mọi thứ trong shop là **gói**: một danh sách phần thưởng (`IReward`) và một giá. Giá có thể là tài nguyên trong game, miễn phí
hoặc tiền thật (IAP). Ví dụ gói 100.000đ = 25 mảnh nhân vật + 30 vàng + 200 kim cương; gói 100 vàng = 5 mảnh nhân vật.

## 1. Data: `ShopPackSO` (`Assets/_Project/DataSO/Shop/`)

| Field | Ý nghĩa |
| --- | --- |
| `id` | Lưu trong save; UI ghép thẻ với gói theo ID này. |
| `displayName`, `icon` | Tên trên thẻ; icon trống thì dùng icon của phần thưởng đầu. |
| `price` (`[SerializeReference] IShopPrice`) | `CurrencyPrice` (vàng/kim cương) · `FreePrice` · `StorePrice` (`productId` + chữ giá, ví dụ "100.000đ"). Giá chỉ **mô tả**, ghi vào `PurchaseRequest` (giống `IReward` ghi vào `RewardBatch`). |
| `limit`, `limitCount` | `None` / `Daily` (0h UTC) / `Weekly` (thứ Hai 0h UTC) / `Lifetime`. Luật kỳ: một switch trong `ShopPackSO.GetCycle`. |
| `clock` | `Device` khi test, `Server` khi chạy thật (chống chỉnh giờ cho giới hạn Daily/Weekly). |
| `rewards` | `List<IReward>` — dùng lại nguyên các loại thưởng có sẵn (tiền, nhân vật, mảnh nhân vật, trang bị). |

## 2. Miền `Shop` (`Assets/_Project/Scripts/Shop/`)

```text
ShopManager                         chỉ class này được bind (GameSceneInstaller > Shop)
├── ShopPackRuntime[]               số lần đã mua trong kỳ (key shop)
└── IPurchaseCheck                  (Strategy, PurchaseChecks.cs; chọn bằng PaymentKind trong một switch)
    ├── CurrencyPurchaseCheck       đủ vàng/kim cương không (IWallet.CanAfford)
    └── StorePurchaseCheck          IAP — hiện GIẢ LẬP: store luôn sẵn sàng, mua là thành công
⬆ IShop (ShopContracts.cs)   Packs, Find, GetBlock, HasFreePack, Refresh, BuyAsync, ⚡ Changed
Phụ thuộc: Progression (IWallet, IRewardGrant.PurchaseRewards) · Clock · Save
```

**Mua (`BuyAsync`)**:
1. Điều kiện chung: gói mở (nguồn giờ có giờ), còn lượt → nếu không: `Closed` / `LimitReached`.
2. Điều kiện riêng của cách trả (`IPurchaseCheck.Check`): thiếu tiền → `NotEnoughCurrency`; store chưa sẵn sàng → `StoreUnavailable`.
3. Kiểm tra gói thưởng trước khi trả tiền (cấu hình sai thì không mất gì).
4. `PayAsync`: tài nguyên không trừ ở đây; store chờ thanh toán (bất đồng bộ).
5. `IRewardGrant.PurchaseRewards(costs, rewards, shopSnapshot)`: **trừ tiền + trao quà + số lần đã mua trong MỘT lần ghi**
   — không bao giờ mất tiền mà không nhận quà. Giá và thưởng cùng loại tiền được tính gộp (trừ rồi cộng).

**Khi cài Unity IAP:** chỉ sửa thân `StorePurchaseCheck` (khởi tạo store, gọi mua, chờ kết quả). Data, `ShopManager` và UI không đổi.

## 3. UI (`Assets/_Project/Scripts/UI/Shop/`, scene `Canvas/Shop`)

| Thành phần | Vai trò |
| --- | --- |
| `ShopScreenController` (`UIPanelController`) | Tab menu như Heroes/Gear (`IsMenuTab`): thanh tiền (trên) và thanh điều hướng (dưới) của Home vẫn hiện, về Home bằng nút Battle; Shop trượt vào bằng `SlideTransition`. Mở từ nút Shop (`HomeNavigation/BottomBar_Menu/Button_01`). **Mỗi tab một panel riêng**: `Panel_Offers` (gói tiền thật), `Panel_Daily` (Daily Deals + đồng hồ reset), `Panel_Resources` (Gem Pack + Gold Pack). Đổi tab: panel cũ trượt ra, panel mới trượt vào từ phía đối diện (`SlideTransition` của từng panel). Chấm than trên tab do `NotificationBadgeView` (khóa Shop + ID gói của tab) lo — xem Notification.md. |
| `ShopPackView` | Một thẻ (thẻ nhỏ hoặc banner), ghép gói qua `packId`. Vẽ tên, icon (field trống = giữ hình vẽ sẵn), số lượng, giá (FREE / icon tiền + số / chữ giá store). Thiếu tiền: giá đỏ. Hết lượt: thẻ mờ + "Sold out". |
| `ShopConfirmPopup` (`UIPanelController`, **overlay**) | Popup xác nhận, do `UIManager` quản lý như popup đăng nhập: đè lên màn Shop, Back đóng popup trước, rời màn Shop thì tự đóng, scene lưu bật hay tắt đều chạy. Popup xác nhận (`Canvas/Popup/Popup_ShopConfirm`, dựng từ `Popup02_Basic_Divided_White`): tên gói, icon + số lượng, "Buy X for Y?", nút Buy / đóng / bấm nền tối. Gói miễn phí nhận ngay, không hỏi. |
| Sau khi mua | `RewardRevealView` dùng chung hiện quà; không mua được thì `NoticeToast` báo lý do. |

## 4. Thêm một gói

1. Tạo asset **Create > Shooter Game > Shop > Pack**, điền ID, giá, giới hạn, phần thưởng.
2. Thêm vào **GameSceneInstaller > Shop > Packs**.
3. Trên scene: gắn `ShopPackView` (+ `Button`, `CanvasGroup`) cho thẻ, điền `packId`, thêm thẻ vào `Packs` của tab tương ứng trong `ShopScreenController`.
4. Chạy **Check Shop** và **Check Service Contracts**.
