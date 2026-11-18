# Cấu trúc thư mục script

Mã gameplay nằm trong `Assets/_Project/Scripts`, nhóm theo chức năng. Interface, factory và strategy của cùng hệ thống được đặt gần nhau.

```text
Scripts/
├── Characters/                   Miền Character (đã chuyển sang kiến trúc mới)
│   ├── CharacterManager.cs       Manager duy nhất được bind; tự tạo các system con bên dưới
│   ├── CharacterContracts.cs     TẤT CẢ interface mà miền khác được dùng
│   ├── CharacterContext.cs       Dịch vụ truyền cho CharacterView lúc Initialize (thay cho inject)
│   ├── CharacterFactory.cs, CharacterRegistry.cs
│   ├── View/                     CharacterView (MonoBehaviour vỏ) + CharacterPresentation, CharacterFeedback, visual handle/state
│   ├── CharacterSearch/          CharacterSearch, factory, ISingleCharacterSearch/IMultipleCharacterSearch và strategy
│   ├── Controllers/              ICharacterController (strategy) và CharacterControllerBase
│   ├── Data/                     Template, database, type và team
│   ├── Stats/                    CharacterRuntimeData (HP/giáp), StatSheet (nguồn bonus), công thức, pool runtime
│   ├── Player/                   PlayerController và PlayerManager
│   └── Enemies/                  EnemyController và ẩn/hiện quái theo camera
├── Simulation/                   Miền Simulation: SimulationManager, SimulationContracts.cs (xem README trong thư mục)
│   ├── Movement/                 Cập nhật di chuyển bằng job
│   └── Spatial/                  Hitbox, grid, truy vấn, ISpatialSearchStrategy và job không gian
├── Shared/                       CharacterHandle (danh tính lượt spawn), DamageInfo/DamageResult
├── Combat/                       Miền Combat (đã chuyển sang kiến trúc mới, xem Refactor-Architecture.md)
│   ├── CombatManager.cs          Manager duy nhất được bind; tự tạo các system con bên dưới
│   ├── CombatContracts.cs        TẤT CẢ interface mà miền khác được dùng
│   ├── CombatAssetsSO.cs         Catalog hình ảnh đạn và VFX
│   ├── Skills/                   SkillManager, SkillFactory, SkillServices, SkillRuntime, CharacterSkills, ISkill, Skill
│   │   ├── Behaviours/           Các hành vi (strategy): bắn, đánh thường, cưa, aura, hồi máu, khiên, buff
│   │   └── Data/                 SkillDataSO, SkillDatabaseSO, data theo cấp của từng hành vi
│   ├── Shooting/                 IShooter và các kiểu bắn (thẳng, quạt, tròn, xoắn)
│   ├── Projectiles/              ProjectileManager (trọn vòng đời đạn), ProjectileView, IProjectileMove và quỹ đạo
│   ├── Effects/                  EffectManager, EffectRuntime, pool, IGameplayEffect và cấu hình slow/cháy/độc
│   │   └── Data/                 ScriptableObject effect cũ (legacy), chỉ giữ để không mất asset
│   ├── Vfx/                      CombatVfx, EffectView, IEffectVisual và visual animation/particle/object
│   └── Loot/                     Vật phẩm rơi (sẽ chuyển sang miền Run ở Giai đoạn 4)
├── Core/
│   ├── GameConstants.cs
│   └── Run/                      RunSessionManager giữ vàng và thời gian của lượt chơi
├── Levels/
│   ├── Configs/                  Công thức level và database
│   ├── Runtime/                  Tiến trình level trong trận
│   └── Services/                 API level trận và level vĩnh viễn
├── Equipment/                    Database và trang bị Player
├── Infrastructure/
│   ├── Zenject/                  Cấu hình dependency của scene
│   ├── Pooling/                  Pool GameObject và pool generic cho class thuần
│   └── Persistence/              Đọc/ghi hồ sơ; Data/ chứa dữ liệu save
├── World/                        Miền World: WorldManager, WorldContracts.cs (IWorldMap), GroundChunks; Data/ chứa database map
├── Stage/                        Miền Stage (đã chuyển): StageManager, StageContracts.cs (4 vai trò), StageRewards
│   ├── Core/                     EnemySpawner, IStageSpawnData, IStageSpawnEntity, StageSpawnRoster
│   ├── Waves/                    WaveConfigSO, WaveSpawnSystem
│   ├── Patterns/                 Pattern/Formation/Path (xem README trong thư mục)
│   ├── Bosses/                   BossSpawnDataSO, BossSpawnSystem, BossArena
│   └── Data/                     StageDataSO, StageDatabaseSO
├── Camera/                       Camera bám Player
└── UI/                           UIManager điều phối; Views/ chứa view chung; Character/ chứa CharacterUI
```

Asset của miền Stage vẫn nằm ở `Assets/_Project/DataSO/Spawning/` (giữ tên cũ để không phải dời
asset), còn Editor đã dời sang `Assets/_Project/Editor/Stage/`.

```text
Assets/_Project/Editor/Stage/
├── Data/                         StageDataEditor
├── Waves/                        WaveConfigEditor
└── Patterns/                     EnemyPatternEditor, EnemyFormationWorkbench
```

## Cách tìm code

- Miền Character nhìn từ bên ngoài: chỉ cần đọc `Characters/CharacterContracts.cs`. Cách các system bên trong được nối với nhau: constructor của `Characters/CharacterManager.cs`.
- Tìm nhân vật: `Characters/CharacterSearch`. `CharacterSearch` chọn strategy theo `CharacterSearchType` qua `CharacterSearchFactory`; strategy truy cập Spatial qua các vai trò trong `Simulation/SimulationContracts.cs`.
- Ẩn/hiện enemy theo camera: `Characters/Enemies/EnemyVisualManager.cs`. Enemy bắt đầu ẩn visual; trước khi MainCamera render, manager kiểm tra bounds sprite và chỉ bật visual con khi giao khung hình. AI, movement, hitbox và gameplay effect vẫn hoạt động ngoài camera. Bounds được tính từ local bounds và Transform để vẫn theo đúng vị trí khi visual đang tắt. Animation được reset khi visual hiện lại. Player không bị áp dụng quy tắc này.
- Kiểm tra hình học và chạy job tìm kiếm: `Simulation/Spatial`.
- Hướng quay, animation, bật/tắt visual: `Characters/View/CharacterPresentation.cs`. Nháy trắng và màu trạng thái: `Characters/View/CharacterFeedback.cs`.
- Miền Combat nhìn từ bên ngoài: chỉ cần đọc `Combat/CombatContracts.cs`. Cách các system bên trong được nối với nhau: constructor của `Combat/CombatManager.cs`.
- Miền Stage nhìn từ bên ngoài: chỉ cần đọc `Stage/StageContracts.cs` (`IStageSelection`, `IStageRunLifecycle`, `IStageResults`, `IStageSchedule`). `StageManager` tự tạo `EnemySpawner`, ba spawn system, `BossArena` và `StageRewards`; thưởng kết quả nằm trong `Stage/StageRewards.cs`.
- Hành vi bắn: `Combat/Skills/Behaviours/ShootingSkill.cs` + `Data/ShootingData.cs`. Kiểu bắn: `Combat/Shooting`. Vòng đời đạn: `Combat/Projectiles`.
- Quản lý skill và cooldown: `Combat/Skills`; mỗi runtime hành vi giữ cooldown riêng. Duration/nhịp effect thuộc `EffectManager` và `EffectRuntime`.
- Slow, cháy, độc và vòng đời hiệu ứng: `Combat/Effects`.
- Hiển thị VFX: `Combat/Vfx`; tài nguyên prefab vẫn nằm trong `CombatAssetsSO`.
- HP/giáp dưới chân: `UI/Character/CharacterUI.cs` nằm trên vỏ nhân vật, điều khiển Canvas World Space con từ `Prefabs/UI/CharacterVitals.prefab` cho Player/boss. Vị trí/scale chỉnh tay trên prefab. Prefab asset DynamicUI đã xóa; nhóm DynamicUI độc lập trong scene và UIManager vẫn được giữ để hiển thị EXP/vàng/thời gian/level.
- Áp dụng sát thương và quyết định chết: `Characters/Controllers/CharacterControllerBase.cs`.
- HP/giáp: `Characters/Stats/CharacterRuntimeData.cs`. Thêm/gỡ bonus chỉ số (level, trang bị, buff, slow, wave): `runtime.Stats.AddSource` trong `Characters/Stats/StatSheet.cs`. Công thức: `Characters/Stats/StatCalculator.cs`.
- Level/EXP trong trận: `Levels/Services/RunLevelService.cs`.

Di chuyển script luôn kèm `.meta` để giữ GUID và tham chiếu Unity. Đợt sắp xếp này không đổi class, namespace hoặc hành vi gameplay. Các đoạn diff trong tài liệu review cũ giữ nguyên đường dẫn lịch sử; liên kết nguồn được cập nhật theo vị trí hiện tại.
