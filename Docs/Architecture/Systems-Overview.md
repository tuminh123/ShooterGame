# Tổng quan các hệ thống ShooterGame

Ngày ghi nhận theo code hiện tại: 2026-11-06.

Tài liệu này là điểm bắt đầu để tìm code và tài liệu chi tiết. Một số tài liệu
thiết kế cũ mô tả blueprint hoặc phiên bản trước; không mặc định mọi nội dung trong đó đã được triển khai.

## Ranh giới dữ liệu và hành vi

```text
ScriptableObject: cấu hình dùng chung
        ↓
Service / factory: tạo, kết nối và quản lý vòng đời
        ↓
Runtime: trạng thái riêng của cá thể/lượt chơi
        ↓
View / UI: hiển thị và tương tác với Unity
```

Không ghi HP, cooldown hay bonus tạm của cá thể vào asset cấu hình dùng chung.

## Bản đồ hệ thống

| Hệ thống | Thành phần chính | Trách nhiệm hiện tại |
| --- | --- | --- |
| Khởi tạo | GameSceneInstaller, Zenject | Bind dependency, thứ tự khởi tạo và dọn dịch vụ. |
| Save | SaveManager | Load/save tiến trình và xử lý hồ sơ. |
| Character | CharacterFactory, CharacterView, CharacterRuntimeData | Ghép vỏ, runtime và hành vi; quản lý HP/giáp, spawn/despawn. |
| Player | PlayerManager, PlayerController | Nhân vật đang chơi, input và áp dụng bonus level/trang bị. |
| Enemy/boss | EnemySpawnManager, EnemyController, WaveConfigSO | Chọn đợt/quái, sinh theo vùng, đuổi player, tách đàn và thu hồi. |
| Visual | CharacterVisualSettings, EnemyVisualManager | Prefab visual theo loại, layout, hitbox và bật/tắt visual enemy theo camera. |
| Pool | PoolManager, PoolPrewarmService, CharacterRuntimePool | Tạo sẵn/tái sử dụng GameObject và runtime nhân vật. |
| Skill | CharacterSkills, SkillRuntime, SkillManager, SkillFactory | Skill gắn theo nhân vật, skill trong trận, cooldown và vòng đời behavior. |
| Shooting | ShootingSkill, ShootBehaviorFactory, ProjectileManager | Kiểu bắn, số viên mỗi lượt, spawn, di chuyển và xử lý đạn. |
| Effect gameplay | EffectManager, EffectRuntimePool | Áp dụng/lập lịch/reset runtime làm chậm và sát thương theo thời gian. |
| Effect hình ảnh | CombatAssetsSO, CombatSpawner, EffectView | Tra và phát prefab VFX; tách khỏi trạng thái effect gameplay. |
| Level | LevelManager, CharacterLevelService, RunLevelService | Level vĩnh viễn và EXP/level của lượt chơi. |
| Trang bị | PlayerEquipmentService, EquipmentDatabaseSO | Loadout và tổng bonus theo dữ liệu trang bị. |
| Loot/lượt chơi | LootManager, PickupView, RunSessionManager | Vật phẩm rơi, nhận EXP/vàng, đồng hồ và số enemy bị hạ. |
| UI | UIManager, RunUI, CharacterUI, PauseUI | Trạm UI, HUD trận, máu/giáp theo nhân vật và pause. |
| Job System | MovementManager, TimerManager, SpatialManager | Buffer dùng lại, cập nhật movement/timer và truy vấn không gian. |
| World | GroundChunkManager | Nền/chunk theo vị trí player. |

Boss thuộc phe Enemy; `IsBoss` đánh dấu loại nhân vật để hiển thị thanh máu.
Không suy ra boss đã có hệ phase/behavior riêng chỉ từ flag này.

## Luồng khởi động

```text
Bind dịch vụ và inject scene
→ SaveManager.Initialize (-200)
→ PoolPrewarmService.Initialize (-150)
→ PlayerManager.Initialize (-100)
→ RunSessionManager.Initialize (-50)
→ Gameplay/spawner hoạt động
```

Prewarm lấy prefab từ character database, combat assets, loot database và danh
sách bổ sung. Cấu hình nằm ở `Assets/_Project/DataSO/Databases/PoolPrewarmSettings.asset`.
Đây là chuẩn bị đồng bộ trong gameplay scene, chưa có loading scene riêng.

## Luồng nhân vật

```text
CharacterDataSO + bonus
→ thuê CharacterRuntimeData
→ lấy CharacterView từ pool dưới nhánh Preparing đang tắt
→ gắn controller, visual, hitbox và layout UI
→ bật vỏ và đăng ký movement/spatial/skill
→ chơi
→ dọn skill/effect/controller, gỡ đăng ký và thông báo release
→ trả runtime, visual và vỏ về pool
```

`CharacterRuntimeData.Generation` phân biệt các lượt sử dụng cùng runtime.
Mục tiêu/callback cũ không được nhận diện cá thể chỉ bằng reference tới vỏ hoặc runtime.
Controller và một số behavior/collection vẫn có chi phí khởi tạo khi spawn; prewarm
hiện không phải cam kết không cấp phát bộ nhớ trong gameplay.

## Skill và shooting

- `CharacterDataSO.Skills` là danh sách skill được trang bị sẵn cho loại nhân vật.
- `SkillDataSO` giữ cấu hình; behavior runtime thuộc cá thể/lượt chơi.
- `CharacterSkills` quản lý skill gắn sẵn; `SkillManager` quản lý lịch skill của lượt chơi.
- Shooting hiện bắn theo cooldown và `ProjectileCount`.
- Cơ chế băng đạn/nạp đạn cùng UI số đạn/thanh nạp đã được gỡ.

## UI và visual hiện tại

- Thanh máu dùng `Slider_Player` trên Canvas world space của character.
- Player và boss hiển thị máu/giáp; enemy thường không bật thanh này.
- Giáp là `ShieldBorderGraphic` với shader viền bên ngoài, giảm theo tỉ lệ giáp.
- Layout/visual/hitbox gom trong `CharacterVisualSettings`, có công cụ căn trong Editor.
- Visual vẫn là prefab con lấy từ pool. VisualRoot cố định là đề xuất trong
  [Character-Visual-Rendering.md](Character-Visual-Rendering.md), chưa chuyển code.

## Chưa thuộc phạm vi hiện tại

- Scene chuẩn bị/loading và chuyển scene sau thắng/thua.
- Giữ pool xuyên scene hoặc chuyển quyền sở hữu pool sang gameplay scene khác.
- Chuyển sang renderer/animator cố định trên vỏ nhân vật.
- Bảo đảm không cấp phát hoặc không khựng frame: cần đo bằng Profiler trên thiết bị mục tiêu.

## Tài liệu liên quan

| Tài liệu | Dùng để đọc gì |
| --- | --- |
| [Visual rendering](Character-Visual-Rendering.md) | So sánh visual cố định và prefab riêng; kế hoạch chuyển đổi. |
| [Pool prewarm](Pool-Prewarm.md) | Cấu hình, thứ tự chuẩn bị và giới hạn pooling. |
| [Skill/shooting pipeline](Skill-Shooting-Pipeline.md) | Cấu hình skill, kiểu bắn, đạn và effect. |
| [Loot/HUD](Loot-and-Hud.md) | Luồng vật phẩm rơi và HUD; phần UI cũ cần đối chiếu với code mới. |
| [Script folders](Script-Folders.md) | Bản đồ thư mục source. |
| [DataSO](../../Assets/_Project/DataSO/README.md) | Cấu hình level, save và trang bị. |
| [Character stats](../../Assets/_Project/DataSO/Documentation/CharacterStats.md) | Chỉ số và công thức bonus. |
| [Combat and skills](../../Assets/_Project/DataSO/Documentation/CombatAndSkills.md) | Tài liệu combat/skill bổ sung. |
| [Job System](../../Assets/_Project/Scripts/Core/JobSystem/README.md) | Movement, timer, spatial và batch truy vấn. |
| [Character blueprint](Character-EventDriven-OOP.md) | Tham khảo thiết kế ban đầu, không phải mô tả đầy đủ code hiện tại. |
| [Character demo](Character-Architecture-Demo.md) | Demo/blueprint nhân vật, có phần chưa triển khai. |
