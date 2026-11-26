# Gacha (quay tướng / trang bị)

> **Trạng thái:** ✅ Hiện hành · Kiểm tra với code: 2026-11-26. Kiểm tra tự động: **Shooter Game > Verification > Check Gacha**.

Hai banner: **tướng** và **trang bị**. Chúng chỉ khác nhau ở **pool** (cái quay ra); giá, tỉ lệ, pity và cách quay dùng chung.

## 1. Config tỉ lệ nằm ở đâu

Mọi con số cân bằng nằm trong asset `GachaBannerSO` (`Assets/_Project/DataSO/Gacha/`): `Gacha_Heroes`, `Gacha_Gear`.
Inspector hiện luôn tỉ lệ từng bậc và **trung bình bao nhiêu lượt ra Legendary** (tính bằng đúng công thức dùng khi quay).

| Nhóm | Field | Ý nghĩa |
| --- | --- | --- |
| Giá | `currency`, `singleCost`, `tenCost` | Giá x1 / x10 |
| Tỉ lệ | `rates` (bậc + trọng số) | Xác suất = trọng số / tổng trọng số các bậc pool có |
| Pity | `pityTier`, `softPityStart`, `softPityStep`, `hardPity` | Từ lượt S tỉ lệ cộng Δ mỗi lượt; lượt N chắc chắn ra |
| Bảo đảm | `guaranteeTier`, `guaranteeEvery` | Cứ G lượt có ít nhất một món ≥ bậc này |
| Pool | `pool` (`CharacterGachaPool` / `EquipmentGachaPool`) | Bậc → phần thưởng |
| Rương | `chestClosed`, `chestOpen` | Ảnh rương của màn quay (trống = rương mặc định của `GachaChestView`). Ảnh nằm ở `Graphic/Gacha/` |

Số mặc định: tướng 1 / 5 / 15 / 30 / 49 (soft 46 +5%, hard 60); trang bị 1,5 / 8 / 20 / 30 / 40,5 (soft 36 +6%, hard 45);
Epic bảo đảm mỗi 10 lượt. Tướng 150 / 1.350 kim cương, trang bị 100 / 900.

Công thức (`GachaRoller`):

```text
P(bậc i)                = w_i / Σw
Bậc pity ở lượt thứ k   = p            (k < S)
                        = p + Δ(k−S+1) (S ≤ k < N)
                        = 1            (k = N)
Lượt trung bình         E = Σ k · p_k · Π_{j<k}(1 − p_j);  tỉ lệ thực = 1 / E
```

## 2. Code (`Assets/_Project/Scripts/Gacha/`)

```text
GachaManager                 chỉ class này được bind; Initialize đọc pity (key gacha)
├── GachaBannerSO[]          data (Data/GachaBannerSO.cs)
│   └── IGachaPool           Strategy: CharacterGachaPool / EquipmentGachaPool (Data/GachaPools.cs)
└── GachaRoller              công thức quay (static, thuần)
⬆ IGacha (GachaContracts.cs)   Banners, GetBlock, PullsUntilPity, Pull, ⚡ Changed
Phụ thuộc: Progression (IWallet, IRewardGrant) · Save
```

- Quay trên **bản sao** bộ đếm pity, rồi `IRewardGrant.PurchaseRewards` trừ tiền + trao quà + lưu pity trong **một lần ghi**.
  Thiếu tiền hoặc cấu hình sai thì không đổi gì; thoát game giữa chừng không quay lại được kết quả.
- Tướng trùng thành mảnh, trang bị roll dòng phụ: luật có sẵn của Progression, gacha không tự làm.
- Bậc pool không có (ví dụ chưa có tướng Legendary) bị bỏ khỏi bảng; khi đó thẻ không hiện dòng pity.

## 3. UI

| Thành phần | Nằm ở | Vai trò |
| --- | --- | --- |
| Tab **Summon** (tab thứ 4 của Shop) | `Canvas/Shop/Bottom/Tab_Summon` | `ShopScreenController` bật panel như mọi tab |
| `GachaPanelView` | `Canvas/Shop/Panel_Summon` | Hai thẻ banner (ghép với asset theo ID): tên, ảnh, tỉ lệ, "Legendary guaranteed in N pulls", nút x1 / x10 (giá đỏ khi thiếu tiền) |
| `GachaChestView` | `Canvas/Popup/GachaChest` | Màn tối, rương nảy ra, rung 3 lần mạnh dần, bật mở; ánh sáng sau rương theo màu bậc cao nhất. Chạm để bỏ qua |
| `RewardRevealView` (dùng chung với Mission/Shop) | `Canvas/Popup/RewardReveal` | Bong bóng hiện lần lượt; Claim thì tướng/mảnh bay vào Heroes, trang bị vào Bag |

## 4. Thêm banner

1. Tạo asset **Create > Shooter Game > Gacha > Banner**, điền ID, giá, tỉ lệ, pity, pool.
2. Thêm vào **GameSceneInstaller > Gacha > Banners**.
3. Nhân bản một thẻ trong `Canvas/Shop/Panel_Summon/Viewport/Content`, thêm phần tử vào `GachaPanelView > Cards` với `bannerId` = ID asset.
4. Chạy **Check Gacha** và **Check Service Contracts**.
