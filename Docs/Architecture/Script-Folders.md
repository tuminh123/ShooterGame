# Cấu trúc thư mục script

Mã gameplay nằm trong `Assets/_Project/Scripts`, nhóm theo chức năng. Interface, factory và strategy của cùng hệ thống được đặt gần nhau.

```text
Scripts/
├── Characters/
│   ├── CharacterFactory.cs, CharacterManager.cs, CharacterView.cs
│   ├── CharacterSearch/          Search, factory, interface, strategy, request và target
│   ├── Controllers/              Interface và controller cơ sở dùng chung
│   ├── Data/                     Template, database, type và team
│   ├── Stats/                    Chỉ số, HP, giáp và modifier
│   ├── Player/                   Controller và quản lý Player
│   └── Enemies/                  Controller, spawn, wave và tăng trưởng quái
├── Combat/
│   ├── CombatAssetsSO.cs         Tài nguyên hình ảnh đạn và prefab VFX
│   ├── CombatSpawner.cs          Spawn và thu hồi đạn/VFX
│   ├── ShootingSkill/
│   │   ├── ShootingSkill.cs      Data và thực thi lượt bắn
│   │   ├── Shooters.cs           Interface, factory và các kiểu bắn
│   │   └── Projectiles/          View, manager, factory và kiểu di chuyển đạn
│   ├── Skills/                   Hợp đồng, runtime, factory, manager và skill nhân vật
│   │   ├── SkillType/            Đánh thường, hồi máu và tạo giáp
│   │   └── Passive/              Skill cộng chỉ số
│   ├── Effects/
│   │   ├── Gameplay/             Cấu hình, manager, runtime, pool và handler hiệu ứng
│   │   │   └── Data/             Các ScriptableObject hiệu ứng cũ được giữ lại
│   │   └── Visuals/              EffectView, IEffectVisual, animation/particle/sprite visual
│   ├── Loot/                     Vật phẩm rơi và nhặt EXP/vàng; Data/ chứa LootDatabaseSO
│   └── Damage/                   Dữ liệu và hợp đồng nhận sát thương
├── Core/
│   ├── GameConstants.cs
│   ├── Run/                      RunSessionManager giữ vàng và thời gian của lượt chơi
│   └── JobSystem/
│       ├── Movement/             Cập nhật di chuyển bằng job
│       └── Spatial/              Hitbox, grid, truy vấn, strategy và job không gian
├── Levels/
│   ├── Configs/                  Công thức level và database
│   ├── Runtime/                  Tiến trình level trong trận
│   └── Services/                 API level trận và level vĩnh viễn
├── Equipment/                    Database và trang bị Player
├── Infrastructure/
│   ├── Zenject/                  Cấu hình dependency của scene
│   ├── Pooling/                  Pool GameObject và pool generic cho class thuần
│   └── Persistence/              Đọc/ghi hồ sơ; Data/ chứa dữ liệu save
├── World/                        Nền theo chunk; Data/ chứa database map
├── Camera/                       Camera bám Player
└── UI/                           UIManager điều phối; Views/ chứa view chung; Character/ chứa CharacterUI
```

## Cách tìm code

- Tìm nhân vật: `Characters/CharacterSearch`. `CharacterSearch` chọn strategy qua `ICharacterSearchFactory`; strategy truy cập Spatial qua interface.
- Ẩn/hiện enemy theo camera: `Characters/Enemies/EnemyVisualManager.cs`. Enemy bắt đầu ẩn visual; trước khi MainCamera render, manager kiểm tra bounds sprite và chỉ bật visual con khi giao khung hình. AI, movement, hitbox và gameplay effect vẫn hoạt động ngoài camera. Bounds được tính từ local bounds và Transform để vẫn theo đúng vị trí khi visual đang tắt. Animation được reset khi visual hiện lại. Player không bị áp dụng quy tắc này.
- Kiểm tra hình học và chạy job tìm kiếm: `Core/JobSystem/Spatial`.
- Data và thực thi bắn, kiểu bắn, vòng đời đạn: `Combat/ShootingSkill`.
- Quản lý skill và cooldown: `Combat/Skills`; mỗi runtime hành vi giữ cooldown riêng. Duration/nhịp effect thuộc `EffectManager` và `EffectRuntime`.
- Slow, cháy, độc và vòng đời hiệu ứng: `Combat/Effects/Gameplay`.
- Hiển thị VFX: `Combat/Effects/Visuals`; tài nguyên prefab vẫn nằm trong `CombatAssetsSO`.
- HP/giáp dưới chân: `UI/Character/CharacterUI.cs` nằm trên vỏ nhân vật, điều khiển Canvas World Space con từ `Prefabs/UI/CharacterVitals.prefab` cho Player/boss. Vị trí/scale chỉnh tay trên prefab. Prefab asset DynamicUI đã xóa; nhóm DynamicUI độc lập trong scene và UIManager vẫn được giữ để hiển thị EXP/vàng/thời gian/level.
- Áp dụng sát thương và quyết định chết: `Characters/Controllers/CharacterControllerBase.cs`.
- HP/giáp và chỉ số runtime: `Characters/Stats/CharacterRuntimeData.cs`.
- Level/EXP trong trận: `Levels/Services/RunLevelService.cs`.

Di chuyển script luôn kèm `.meta` để giữ GUID và tham chiếu Unity. Đợt sắp xếp này không đổi class, namespace hoặc hành vi gameplay. Các đoạn diff trong tài liệu review cũ giữ nguyên đường dẫn lịch sử; liên kết nguồn được cập nhật theo vị trí hiện tại.
