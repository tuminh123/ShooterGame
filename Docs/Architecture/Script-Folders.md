# Cấu trúc folder script

Mã gameplay nằm trong `Assets/_Project/Scripts` và được nhóm theo hệ thống.

```text
Scripts/
├── Characters/
│   ├── CharacterFactory.cs       Tạo và thu hồi nhân vật
│   ├── CharacterView.cs          Vỏ Unity, nối runtime và controller
│   ├── CharacterSearch.cs        Truy vấn nhân vật
│   ├── CharacterTarget.cs        Tham chiếu theo vòng đời spawn
│   ├── ICharacterController.cs
│   ├── Data/                     Template, database, type và team
│   ├── Stats/                    Chỉ số, HP và modifier của nhân vật
│   ├── Player/                   Tạo Player và đọc input
│   └── Enemies/                  AI, spawn, wave và tăng trưởng quái
├── Combat/
│   ├── Skills/                   Handler, runtime, factory và hợp đồng skill
│   │   ├── Projectile/           Skill bắn đạn và cấu hình của skill
│   │   └── Passive/              Skill cộng chỉ số và cấu hình của skill
│   ├── Shooting/                 Pattern và điều phối một lượt bắn
│   ├── Projectiles/              Dữ liệu, spawn, bay, hit và VFX đạn
│   ├── Effects/                  Slow, damage theo thời gian và vòng đời hiệu ứng
│   └── Damage/                   DamageInfo, DamageResult và IDamageReceiver
├── Levels/
│   ├── Configs/                  Công thức level và database
│   ├── Runtime/                  Tiến trình level trong trận
│   └── Services/                 API level trận và level vĩnh viễn
├── Equipment/                    Database và loadout trang bị
├── Infrastructure/
│   ├── Zenject/                  Cấu hình dependency của scene
│   ├── Pooling/                  Tái sử dụng GameObject
│   ├── Movement/                 Áp dụng lệnh di chuyển
│   └── Persistence/              Đọc/ghi hồ sơ; Data/ chứa DTO save
├── World/                        Nền theo chunk; Data/ chứa database map
├── Camera/                       Camera bám Player
├── UI/                           Hành vi giao diện
└── Core/                         Hằng số dùng chung
```

## Cách tìm code

- Tạo nhân vật, input hoặc AI: `Characters`.
- Điều kiện dùng skill và cooldown: `Combat/Skills`.
- Số lượng và hướng các viên đạn trong một lượt bắn: `Combat/Shooting`.
- Đạn bay, trúng mục tiêu hoặc hết thời gian: `Combat/Projectiles`.
- Áp dụng và hết hạn slow, cháy, độc: `Combat/Effects`.
- Dữ liệu truyền sát thương: `Combat/Damage`.
- Công thức crit, defense và thay đổi HP hiện vẫn nằm trong `Characters/Stats/CharacterRuntimeData.cs`.
- Đánh chạm của Enemy hiện vẫn nằm trong `Characters/Enemies/EnemyController.cs`.

Đợt sắp xếp này chỉ di chuyển file và giữ nguyên `.meta`, không đổi class, namespace hoặc hành vi gameplay. Các tài liệu review/blueprint cũ có thể dùng đường dẫn trước khi sắp xếp; dùng cấu trúc này để tra vị trí hiện tại.
