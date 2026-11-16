# Báo cáo thay đổi service contracts

## Phạm vi hoàn tất

- Tách các hợp đồng lớn và dùng lại tên hiện có làm interface tổng hợp.
- Bổ sung hợp đồng dịch vụ, chuyển các dependency gameplay/UI sang vai trò nhỏ.
- Giữ manager làm implementation singleton; bỏ self-binding khi không có consumer concrete.
- Tách installer thành cấu hình, binding và execution order; tránh đăng ký scheduler hai lần cho component scene.
- Tổ chức `Contracts/` theo hệ thống, giữ GUID khi di chuyển, bổ sung comment và region chức năng.
- Không thay đổi schema save, ID enum, dữ liệu Stage hoặc reference scene/prefab trong phạm vi refactor.

Chi tiết thiết kế: [Service contracts và Zenject](README.md).

## Thống kê file

| Loại | Số lượng |
| --- | ---: |
| File C# hiện có được sửa | 70 |
| File C# tạo mới, không tính di chuyển | 42 |
| File C# di chuyển/đổi tên cùng GUID | 11 |
| File implementation bị xóa hoàn toàn | 0 |
| File tài liệu/runner hiện có được sửa | 6 |

Các đường dẫn và metadata được liệt kê đầy đủ bên dưới. Git hiện hiển thị di chuyển dưới dạng xóa đường dẫn cũ và thêm đường dẫn mới vì chưa stage.

## Kiểm chứng

| Kiểm tra | Kết quả |
| --- | --- |
| Biên dịch runtime bằng compiler và reference response file của Unity | Đạt |
| Biên dịch Editor, gồm menu verification mới | Đạt |
| Rà cấu trúc interface hiện tại | Không có interface chức năng khai báo quá 3 hàm |
| Menu Check Service Contracts trong Editor | Đạt: 163 public interfaces |
| Smoke checks đầu tiên trên container thật | Đạt: singleton aliases, scheduler, movement, search, skill grant/upgrade, maintained effect, damage/death, stale handles, nested pause, restart và Home |
| Bổ sung kiểm tra projectile sweep → damage → trả pool và kiểm đếm initialize/dispose | Đã biên dịch; lượt chạy lại chưa xác nhận vì MCP mất kết nối với Editor |
| git diff --check | Đạt |
| Hai standalone runner cũ | Không đạt bước compile harness: còn tham chiếu timer/wave/feedback đã bị gỡ trước refactor; đường dẫn contract di chuyển đã được cập nhật |

Smoke checks không cấp thưởng kết quả và không nhặt loot ghi ví. Chưa kiểm chứng chơi trọn màn, luồng animation kết quả/UI tương tác hoặc build Android.

### Cảnh báo có sẵn ngoài phạm vi

Missing-script scan xác định ba prefab Layer Lab đã nạp có component thiếu script:

- `Assets/Layer Lab/GUI Pro-SuperCasual/Prefabs/Prefabs_DemoScene_Panels/Popup_EquipmentItemInfo01_Blue.prefab`
- `Assets/Layer Lab/GUI Pro-SuperCasual/Prefabs/Prefabs_DemoScene_Panels/Popup_Stage_Start_Sky.prefab`
- `Assets/Layer Lab/GUI Pro-SuperCasual/Prefabs/Prefabs_DemoScene_Panels/PopupDim_Play_Result_Victory.prefab`

Không sửa ba prefab này. Một thay đổi khác ở `Achievement.prefab` xuất hiện trong workspace khi làm việc; thay đổi đó được giữ nguyên và không thuộc danh sách refactor.

## File hiện có được sửa

| File | Nội dung |
| --- | --- |
| [Assets/_Project/Scripts/Camera/CameraFolow.cs](../Camera/CameraFolow.cs) | Thay dependency concrete bằng hợp đồng theo vai trò; bổ sung/giữ comment và region chức năng. |
| [Assets/_Project/Scripts/Characters/CharacterFactory.cs](../Characters/CharacterFactory.cs) | Thay dependency concrete bằng hợp đồng theo vai trò; bổ sung/giữ comment và region chức năng. |
| [Assets/_Project/Scripts/Characters/CharacterManager.cs](../Characters/CharacterManager.cs) | Thay dependency concrete bằng hợp đồng theo vai trò; bổ sung/giữ comment và region chức năng. |
| [Assets/_Project/Scripts/Characters/CharacterSearch/CharacterSearch.cs](../Characters/CharacterSearch/CharacterSearch.cs) | Thay dependency concrete bằng hợp đồng theo vai trò; bổ sung/giữ comment và region chức năng. |
| [Assets/_Project/Scripts/Characters/CharacterSearch/CharacterSearchFactory.cs](../Characters/CharacterSearch/CharacterSearchFactory.cs) | Thay dependency concrete bằng hợp đồng theo vai trò; bổ sung/giữ comment và region chức năng. |
| [Assets/_Project/Scripts/Characters/CharacterSearch/CharacterSearchStrategies.cs](../Characters/CharacterSearch/CharacterSearchStrategies.cs) | Thay dependency concrete bằng hợp đồng theo vai trò; bổ sung/giữ comment và region chức năng. |
| [Assets/_Project/Scripts/Characters/CharacterView.cs](../Characters/CharacterView.cs) | Inject hợp đồng movement, skill, effect, pool và tìm kiếm nhỏ; giữ phối hợp vòng đời nhân vật. |
| [Assets/_Project/Scripts/Characters/Enemies/EnemyController.cs](../Characters/Enemies/EnemyController.cs) | Thay dependency concrete bằng hợp đồng theo vai trò; bổ sung/giữ comment và region chức năng. |
| [Assets/_Project/Scripts/Characters/Enemies/EnemyVisualManager.cs](../Characters/Enemies/EnemyVisualManager.cs) | Thay dependency concrete bằng hợp đồng theo vai trò; bổ sung/giữ comment và region chức năng. |
| [Assets/_Project/Scripts/Characters/Player/PlayerManager.cs](../Characters/Player/PlayerManager.cs) | Thay dependency concrete bằng hợp đồng theo vai trò; bổ sung/giữ comment và region chức năng. |
| [Assets/_Project/Scripts/Combat/CombatSpawner.cs](../Combat/CombatSpawner.cs) | Triển khai hoặc sử dụng hợp đồng combat/skill/effect; giữ luật combat và pool lifecycle. |
| [Assets/_Project/Scripts/Combat/Damage/DamageInfo.cs](../Combat/Damage/DamageInfo.cs) | Triển khai hoặc sử dụng hợp đồng combat/skill/effect; giữ luật combat và pool lifecycle. |
| [Assets/_Project/Scripts/Combat/Effects/Gameplay/EffectManager.cs](../Combat/Effects/Gameplay/EffectManager.cs) | Triển khai hoặc sử dụng hợp đồng combat/skill/effect; giữ luật combat và pool lifecycle. |
| [Assets/_Project/Scripts/Combat/Effects/Gameplay/EffectRuntime.cs](../Combat/Effects/Gameplay/EffectRuntime.cs) | Tham chiếu manager qua hợp đồng handle, giữ version và quyền sở hữu runtime. |
| [Assets/_Project/Scripts/Combat/Effects/Gameplay/EffectRuntimePool.cs](../Combat/Effects/Gameplay/EffectRuntimePool.cs) | Triển khai hoặc sử dụng hợp đồng combat/skill/effect; giữ luật combat và pool lifecycle. |
| [Assets/_Project/Scripts/Combat/Effects/Gameplay/GameplayEffects.cs](../Combat/Effects/Gameplay/GameplayEffects.cs) | Triển khai hoặc sử dụng hợp đồng combat/skill/effect; giữ luật combat và pool lifecycle. |
| [Assets/_Project/Scripts/Combat/Effects/Visuals/EffectView.cs](../Combat/Effects/Visuals/EffectView.cs) | Triển khai hoặc sử dụng hợp đồng combat/skill/effect; giữ luật combat và pool lifecycle. |
| [Assets/_Project/Scripts/Combat/Loot/LootManager.cs](../Combat/Loot/LootManager.cs) | Triển khai hoặc sử dụng hợp đồng combat/skill/effect; giữ luật combat và pool lifecycle. |
| [Assets/_Project/Scripts/Combat/ShootingSkill/Projectiles/ProjectileManager.cs](../Combat/ShootingSkill/Projectiles/ProjectileManager.cs) | Triển khai hoặc sử dụng hợp đồng combat/skill/effect; giữ luật combat và pool lifecycle. |
| [Assets/_Project/Scripts/Combat/ShootingSkill/Projectiles/ProjectileMoves.cs](../Combat/ShootingSkill/Projectiles/ProjectileMoves.cs) | Triển khai hoặc sử dụng hợp đồng combat/skill/effect; giữ luật combat và pool lifecycle. |
| [Assets/_Project/Scripts/Combat/ShootingSkill/Projectiles/ProjectileView.cs](../Combat/ShootingSkill/Projectiles/ProjectileView.cs) | Triển khai hoặc sử dụng hợp đồng combat/skill/effect; giữ luật combat và pool lifecycle. |
| [Assets/_Project/Scripts/Combat/ShootingSkill/Shooters.cs](../Combat/ShootingSkill/Shooters.cs) | Triển khai hoặc sử dụng hợp đồng combat/skill/effect; giữ luật combat và pool lifecycle. |
| [Assets/_Project/Scripts/Combat/Skills/Behaviours/FrostAuraSkill.cs](../Combat/Skills/Behaviours/FrostAuraSkill.cs) | Triển khai hoặc sử dụng hợp đồng combat/skill/effect; giữ luật combat và pool lifecycle. |
| [Assets/_Project/Scripts/Combat/Skills/Behaviours/NormalAttackSkill.cs](../Combat/Skills/Behaviours/NormalAttackSkill.cs) | Triển khai hoặc sử dụng hợp đồng combat/skill/effect; giữ luật combat và pool lifecycle. |
| [Assets/_Project/Scripts/Combat/Skills/Behaviours/OrbitSawSkill.cs](../Combat/Skills/Behaviours/OrbitSawSkill.cs) | Triển khai hoặc sử dụng hợp đồng combat/skill/effect; giữ luật combat và pool lifecycle. |
| [Assets/_Project/Scripts/Combat/Skills/Behaviours/ShootingSkill.cs](../Combat/Skills/Behaviours/ShootingSkill.cs) | Triển khai hoặc sử dụng hợp đồng combat/skill/effect; giữ luật combat và pool lifecycle. |
| [Assets/_Project/Scripts/Combat/Skills/CharacterSkills.cs](../Combat/Skills/CharacterSkills.cs) | Chỉ phụ thuộc ISkillRuntimeFactory; giữ ownership và cleanup skill gắn sẵn. |
| [Assets/_Project/Scripts/Combat/Skills/Skill.cs](../Combat/Skills/Skill.cs) | Hành vi nhận ISkillServices thay vì SkillFactory cụ thể. |
| [Assets/_Project/Scripts/Combat/Skills/SkillFactory.cs](../Combat/Skills/SkillFactory.cs) | Triển khai hợp đồng tạo runtime/behaviour và context dịch vụ skill qua interface. |
| [Assets/_Project/Scripts/Combat/Skills/SkillManager.cs](../Combat/Skills/SkillManager.cs) | Triển khai hoặc sử dụng hợp đồng combat/skill/effect; giữ luật combat và pool lifecycle. |
| [Assets/_Project/Scripts/Combat/Skills/SkillRuntime.cs](../Combat/Skills/SkillRuntime.cs) | Nhận ISkillBehaviourFactory để mở hành vi theo cấp. |
| [Assets/_Project/Scripts/Core/Currency/CurrencyService.cs](../Core/Currency/CurrencyService.cs) | Tách quyền đọc/cập nhật/lưu tiến trình và thay dependency bằng hợp đồng nhỏ. |
| [Assets/_Project/Scripts/Core/GameFlow/GameManager.cs](../Core/GameFlow/GameManager.cs) | Thay dependency concrete bằng hợp đồng theo vai trò; bổ sung/giữ comment và region chức năng. |
| [Assets/_Project/Scripts/Core/JobSystem/Movement/MovementManager.cs](../Core/JobSystem/Movement/MovementManager.cs) | Triển khai hợp đồng movement; handle giữ interface đăng ký; dependency spatial chỉ cần invalidation. |
| [Assets/_Project/Scripts/Core/JobSystem/Spatial/SpatialData.cs](../Core/JobSystem/Spatial/SpatialData.cs) | Handle giữ hợp đồng resolve thay vì toàn bộ spatial API; bảo toàn kiểm tra danh tính. |
| [Assets/_Project/Scripts/Core/JobSystem/Spatial/SpatialManager.cs](../Core/JobSystem/Spatial/SpatialManager.cs) | Thay dependency concrete bằng hợp đồng theo vai trò; bổ sung/giữ comment và region chức năng. |
| [Assets/_Project/Scripts/Core/JobSystem/Spatial/SpatialSearchStrategies.cs](../Core/JobSystem/Spatial/SpatialSearchStrategies.cs) | Thay dependency concrete bằng hợp đồng theo vai trò; bổ sung/giữ comment và region chức năng. |
| [Assets/_Project/Scripts/Core/Run/GameplayPauseService.cs](../Core/Run/GameplayPauseService.cs) | Thay dependency concrete bằng hợp đồng theo vai trò; bổ sung/giữ comment và region chức năng. |
| [Assets/_Project/Scripts/Core/Run/RunSessionManager.cs](../Core/Run/RunSessionManager.cs) | Thay dependency concrete bằng hợp đồng theo vai trò; bổ sung/giữ comment và region chức năng. |
| [Assets/_Project/Scripts/Equipment/PlayerEquipmentService.cs](../Equipment/PlayerEquipmentService.cs) | Tách quyền đọc/cập nhật/lưu tiến trình và thay dependency bằng hợp đồng nhỏ. |
| [Assets/_Project/Scripts/Infrastructure/Persistence/SaveManager.cs](../Infrastructure/Persistence/SaveManager.cs) | Tách đọc, ghi, flush, reset và lỗi nạp; giữ snapshot/retry hiện có. |
| [Assets/_Project/Scripts/Infrastructure/Persistence/SaveMigration.cs](../Infrastructure/Persistence/SaveMigration.cs) | Tách đọc, ghi, flush, reset và lỗi nạp; giữ snapshot/retry hiện có. |
| [Assets/_Project/Scripts/Infrastructure/Pooling/CharacterRuntimePool.cs](../Infrastructure/Pooling/CharacterRuntimePool.cs) | Thay dependency concrete bằng hợp đồng theo vai trò; bổ sung/giữ comment và region chức năng. |
| [Assets/_Project/Scripts/Infrastructure/Pooling/PoolManager.cs](../Infrastructure/Pooling/PoolManager.cs) | Thay dependency concrete bằng hợp đồng theo vai trò; bổ sung/giữ comment và region chức năng. |
| [Assets/_Project/Scripts/Infrastructure/Pooling/PoolPrewarmService.cs](../Infrastructure/Pooling/PoolPrewarmService.cs) | Chuẩn bị dung lượng qua hợp đồng riêng, bỏ downcast SpatialManager. |
| [Assets/_Project/Scripts/Infrastructure/Zenject/GameSceneInstaller.cs](../Infrastructure/Zenject/GameSceneInstaller.cs) | Giữ reference/validation ở file chính; điều phối partial bindings và flush save qua interface. |
| [Assets/_Project/Scripts/Levels/Services/CharacterLevelService.cs](../Levels/Services/CharacterLevelService.cs) | Tách quyền đọc/cập nhật/lưu tiến trình và thay dependency bằng hợp đồng nhỏ. |
| [Assets/_Project/Scripts/Levels/Services/LevelManager.cs](../Levels/Services/LevelManager.cs) | Tách quyền đọc/cập nhật/lưu tiến trình và thay dependency bằng hợp đồng nhỏ. |
| [Assets/_Project/Scripts/Levels/Services/RunLevelService.cs](../Levels/Services/RunLevelService.cs) | Tách quyền đọc/cập nhật/lưu tiến trình và thay dependency bằng hợp đồng nhỏ. |
| [Assets/_Project/Scripts/Spawning/Bosses/BossHandleSummon.cs](../Spawning/Bosses/BossHandleSummon.cs) | Tách quyền spawn, despawn, hình học, kết quả và tiến trình; chuyển dependency sang hợp đồng. |
| [Assets/_Project/Scripts/Spawning/Bosses/BossSpawnSystem.cs](../Spawning/Bosses/BossSpawnSystem.cs) | Tách quyền spawn, despawn, hình học, kết quả và tiến trình; chuyển dependency sang hợp đồng. |
| [Assets/_Project/Scripts/Spawning/Core/EnemySpawnManager.cs](../Spawning/Core/EnemySpawnManager.cs) | Tách quyền spawn, despawn, hình học, kết quả và tiến trình; chuyển dependency sang hợp đồng. |
| [Assets/_Project/Scripts/Spawning/Core/StageSpawnRoster.cs](../Spawning/Core/StageSpawnRoster.cs) | Tách quyền spawn, despawn, hình học, kết quả và tiến trình; chuyển dependency sang hợp đồng. |
| [Assets/_Project/Scripts/Spawning/Patterns/FormationSpawnSystem.cs](../Spawning/Patterns/FormationSpawnSystem.cs) | Tách quyền spawn, despawn, hình học, kết quả và tiến trình; chuyển dependency sang hợp đồng. |
| [Assets/_Project/Scripts/Spawning/Stages/StageManager.cs](../Spawning/Stages/StageManager.cs) | Tách quyền spawn, despawn, hình học, kết quả và tiến trình; chuyển dependency sang hợp đồng. |
| [Assets/_Project/Scripts/Spawning/Stages/StageProgressService.cs](../Spawning/Stages/StageProgressService.cs) | Tách quyền spawn, despawn, hình học, kết quả và tiến trình; chuyển dependency sang hợp đồng. |
| [Assets/_Project/Scripts/Spawning/Waves/WaveSpawnSystem.cs](../Spawning/Waves/WaveSpawnSystem.cs) | Tách quyền spawn, despawn, hình học, kết quả và tiến trình; chuyển dependency sang hợp đồng. |
| [Assets/_Project/Scripts/UI/Core/HomeController.cs](../UI/Core/HomeController.cs) | Consumer nhận hợp đồng đọc/lệnh đúng nhu cầu; giữ hành vi UI và lifecycle đăng ký. |
| [Assets/_Project/Scripts/UI/Core/LoadingController.cs](../UI/Core/LoadingController.cs) | Consumer nhận hợp đồng đọc/lệnh đúng nhu cầu; giữ hành vi UI và lifecycle đăng ký. |
| [Assets/_Project/Scripts/UI/Core/ScreenTransitionController.cs](../UI/Core/ScreenTransitionController.cs) | Consumer nhận hợp đồng đọc/lệnh đúng nhu cầu; giữ hành vi UI và lifecycle đăng ký. |
| [Assets/_Project/Scripts/UI/Core/UIManager.cs](../UI/Core/UIManager.cs) | Consumer nhận hợp đồng đọc/lệnh đúng nhu cầu; giữ hành vi UI và lifecycle đăng ký. |
| [Assets/_Project/Scripts/UI/Debug/ResetSaveButton.cs](../UI/Debug/ResetSaveButton.cs) | Consumer nhận hợp đồng đọc/lệnh đúng nhu cầu; giữ hành vi UI và lifecycle đăng ký. |
| [Assets/_Project/Scripts/UI/Gameplay/GameplayController.cs](../UI/Gameplay/GameplayController.cs) | Consumer nhận hợp đồng đọc/lệnh đúng nhu cầu; giữ hành vi UI và lifecycle đăng ký. |
| [Assets/_Project/Scripts/UI/Gameplay/PauseUI.cs](../UI/Gameplay/PauseUI.cs) | Consumer nhận hợp đồng đọc/lệnh đúng nhu cầu; giữ hành vi UI và lifecycle đăng ký. |
| [Assets/_Project/Scripts/UI/Gameplay/ResultController.cs](../UI/Gameplay/ResultController.cs) | Consumer nhận hợp đồng đọc/lệnh đúng nhu cầu; giữ hành vi UI và lifecycle đăng ký. |
| [Assets/_Project/Scripts/UI/Gameplay/RunUI.cs](../UI/Gameplay/RunUI.cs) | Consumer nhận hợp đồng đọc/lệnh đúng nhu cầu; giữ hành vi UI và lifecycle đăng ký. |
| [Assets/_Project/Scripts/UI/SkillChoice/SkillChoiceController.cs](../UI/SkillChoice/SkillChoiceController.cs) | Consumer nhận hợp đồng đọc/lệnh đúng nhu cầu; giữ hành vi UI và lifecycle đăng ký. |
| [Assets/_Project/Scripts/UI/SkillChoice/SkillChoicePool.cs](../UI/SkillChoice/SkillChoicePool.cs) | Consumer nhận hợp đồng đọc/lệnh đúng nhu cầu; giữ hành vi UI và lifecycle đăng ký. |
| [Assets/_Project/Scripts/UI/SkillChoice/SkillChoiceUI.cs](../UI/SkillChoice/SkillChoiceUI.cs) | Consumer nhận hợp đồng đọc/lệnh đúng nhu cầu; giữ hành vi UI và lifecycle đăng ký. |
| [Assets/_Project/Scripts/World/GroundChunkManager.cs](../World/GroundChunkManager.cs) | Thay dependency concrete bằng hợp đồng theo vai trò; bổ sung/giữ comment và region chức năng. |
| [Docs/Architecture/Spawn-Folders.md](../../../../Docs/Architecture/Spawn-Folders.md) | Cập nhật tài liệu kiến trúc, đường dẫn hoặc kết quả kiểm chứng. |
| [Docs/Architecture/Stage-Spawn-Refactor.md](../../../../Docs/Architecture/Stage-Spawn-Refactor.md) | Cập nhật tài liệu kiến trúc, đường dẫn hoặc kết quả kiểm chứng. |
| [Docs/Architecture/Systems-Overview.md](../../../../Docs/Architecture/Systems-Overview.md) | Cập nhật tài liệu kiến trúc, đường dẫn hoặc kết quả kiểm chứng. |
| [Docs/CombatCodeReview.md](../../../../Docs/CombatCodeReview.md) | Cập nhật tài liệu kiến trúc, đường dẫn hoặc kết quả kiểm chứng. |
| [Tests/EffectPlayback/Run.ps1](../../../../Tests/EffectPlayback/Run.ps1) | Cập nhật đường dẫn hợp đồng hoặc hướng dẫn chạy kiểm tra. |
| [Tests/SkillProgression/Run.ps1](../../../../Tests/SkillProgression/Run.ps1) | Cập nhật đường dẫn hợp đồng hoặc hướng dẫn chạy kiểm tra. |

## File tạo mới

| File | Nội dung |
| --- | --- |
| [Assets/_Project/Editor/Verification/ServiceContractVerification.cs](../../Editor/Verification/ServiceContractVerification.cs) | Menu kiểm tra cấu trúc hợp đồng, alias singleton, lifecycle và integration trong Unity; chẩn đoán missing script. |
| [Assets/_Project/Scripts/Characters/Contracts/ICharacterFactory.cs](Characters/ICharacterFactory.cs) | Hợp đồng theo vai trò, tối đa 3 hàm; interface tổng hợp không khai báo thêm thành viên. |
| [Assets/_Project/Scripts/Characters/Contracts/ICharacterSystem.cs](Characters/ICharacterSystem.cs) | Hợp đồng theo vai trò, tối đa 3 hàm; interface tổng hợp không khai báo thêm thành viên. |
| [Assets/_Project/Scripts/Characters/Controllers/Contracts/ICharacterCapabilities.cs](Characters/Controllers/ICharacterCapabilities.cs) | Hợp đồng theo vai trò, tối đa 3 hàm; interface tổng hợp không khai báo thêm thành viên. |
| [Assets/_Project/Scripts/Characters/Player/Contracts/IPlayerSystem.cs](Characters/Player/IPlayerSystem.cs) | Hợp đồng theo vai trò, tối đa 3 hàm; interface tổng hợp không khai báo thêm thành viên. |
| [Assets/_Project/Scripts/Combat/Contracts/ICombatSpawnSystem.cs](Combat/ICombatSpawnSystem.cs) | Hợp đồng theo vai trò, tối đa 3 hàm; interface tổng hợp không khai báo thêm thành viên. |
| [Assets/_Project/Scripts/Combat/Damage/Contracts/IDamageReceiver.cs](Combat/Damage/IDamageReceiver.cs) | Hợp đồng theo vai trò, tối đa 3 hàm; interface tổng hợp không khai báo thêm thành viên. |
| [Assets/_Project/Scripts/Combat/Effects/Gameplay/Contracts/IEffectRuntimePool.cs](Combat/Effects/Gameplay/IEffectRuntimePool.cs) | Hợp đồng theo vai trò, tối đa 3 hàm; interface tổng hợp không khai báo thêm thành viên. |
| [Assets/_Project/Scripts/Combat/Effects/Gameplay/Contracts/IEffectSystem.cs](Combat/Effects/Gameplay/IEffectSystem.cs) | Hợp đồng theo vai trò, tối đa 3 hàm; interface tổng hợp không khai báo thêm thành viên. |
| [Assets/_Project/Scripts/Combat/Effects/Gameplay/Contracts/IGameplayEffect.cs](Combat/Effects/Gameplay/IGameplayEffect.cs) | Hợp đồng theo vai trò, tối đa 3 hàm; interface tổng hợp không khai báo thêm thành viên. |
| [Assets/_Project/Scripts/Combat/ShootingSkill/Contracts/IShootBehaviorFactory.cs](Combat/ShootingSkill/IShootBehaviorFactory.cs) | Hợp đồng theo vai trò, tối đa 3 hàm; interface tổng hợp không khai báo thêm thành viên. |
| [Assets/_Project/Scripts/Combat/ShootingSkill/Contracts/IShooter.cs](Combat/ShootingSkill/IShooter.cs) | Hợp đồng theo vai trò, tối đa 3 hàm; interface tổng hợp không khai báo thêm thành viên. |
| [Assets/_Project/Scripts/Combat/ShootingSkill/Projectiles/Contracts/IProjectileMove.cs](Combat/ShootingSkill/Projectiles/IProjectileMove.cs) | Hợp đồng theo vai trò, tối đa 3 hàm; interface tổng hợp không khai báo thêm thành viên. |
| [Assets/_Project/Scripts/Combat/ShootingSkill/Projectiles/Contracts/IProjectileMoveFactory.cs](Combat/ShootingSkill/Projectiles/IProjectileMoveFactory.cs) | Hợp đồng theo vai trò, tối đa 3 hàm; interface tổng hợp không khai báo thêm thành viên. |
| [Assets/_Project/Scripts/Combat/ShootingSkill/Projectiles/Contracts/IProjectileSystem.cs](Combat/ShootingSkill/Projectiles/IProjectileSystem.cs) | Hợp đồng theo vai trò, tối đa 3 hàm; interface tổng hợp không khai báo thêm thành viên. |
| [Assets/_Project/Scripts/Combat/Skills/Contracts/ISkill.cs](Combat/Skills/ISkill.cs) | Hợp đồng theo vai trò, tối đa 3 hàm; interface tổng hợp không khai báo thêm thành viên. |
| [Assets/_Project/Scripts/Combat/Skills/Contracts/ISkillFactory.cs](Combat/Skills/ISkillFactory.cs) | Hợp đồng theo vai trò, tối đa 3 hàm; interface tổng hợp không khai báo thêm thành viên. |
| [Assets/_Project/Scripts/Combat/Skills/Contracts/ISkillServices.cs](Combat/Skills/ISkillServices.cs) | Hợp đồng theo vai trò, tối đa 3 hàm; interface tổng hợp không khai báo thêm thành viên. |
| [Assets/_Project/Scripts/Combat/Skills/Contracts/ISkillSystem.cs](Combat/Skills/ISkillSystem.cs) | Hợp đồng theo vai trò, tối đa 3 hàm; interface tổng hợp không khai báo thêm thành viên. |
| [Assets/_Project/Scripts/Core/Currency/Contracts/ICurrencyService.cs](Core/Currency/ICurrencyService.cs) | Hợp đồng theo vai trò, tối đa 3 hàm; interface tổng hợp không khai báo thêm thành viên. |
| [Assets/_Project/Scripts/Core/GameFlow/Contracts/IGameFlow.cs](Core/GameFlow/IGameFlow.cs) | Hợp đồng theo vai trò, tối đa 3 hàm; interface tổng hợp không khai báo thêm thành viên. |
| [Assets/_Project/Scripts/Core/JobSystem/Movement/Contracts/IMovementSystem.cs](Core/JobSystem/Movement/IMovementSystem.cs) | Hợp đồng theo vai trò, tối đa 3 hàm; interface tổng hợp không khai báo thêm thành viên. |
| [Assets/_Project/Scripts/Core/JobSystem/Spatial/Contracts/ISpatialSearchStrategy.cs](Core/JobSystem/Spatial/ISpatialSearchStrategy.cs) | Hợp đồng theo vai trò, tối đa 3 hàm; interface tổng hợp không khai báo thêm thành viên. |
| [Assets/_Project/Scripts/Core/Run/Contracts/IGameplayPauseService.cs](Core/Run/IGameplayPauseService.cs) | Hợp đồng theo vai trò, tối đa 3 hàm; interface tổng hợp không khai báo thêm thành viên. |
| [Assets/_Project/Scripts/Core/Run/Contracts/IRunSession.cs](Core/Run/IRunSession.cs) | Hợp đồng theo vai trò, tối đa 3 hàm; interface tổng hợp không khai báo thêm thành viên. |
| [Assets/_Project/Scripts/Equipment/Contracts/IEquipmentService.cs](Equipment/IEquipmentService.cs) | Hợp đồng theo vai trò, tối đa 3 hàm; interface tổng hợp không khai báo thêm thành viên. |
| [Assets/_Project/Scripts/Infrastructure/Persistence/Contracts/ISaveService.cs](Infrastructure/Persistence/ISaveService.cs) | Hợp đồng theo vai trò, tối đa 3 hàm; interface tổng hợp không khai báo thêm thành viên. |
| [Assets/_Project/Scripts/Infrastructure/Pooling/Contracts/ICharacterRuntimePool.cs](Infrastructure/Pooling/ICharacterRuntimePool.cs) | Hợp đồng theo vai trò, tối đa 3 hàm; interface tổng hợp không khai báo thêm thành viên. |
| [Assets/_Project/Scripts/Infrastructure/Pooling/Contracts/IPoolPrewarmService.cs](Infrastructure/Pooling/IPoolPrewarmService.cs) | Hợp đồng theo vai trò, tối đa 3 hàm; interface tổng hợp không khai báo thêm thành viên. |
| [Assets/_Project/Scripts/Infrastructure/Pooling/Contracts/IPoolSystem.cs](Infrastructure/Pooling/IPoolSystem.cs) | Hợp đồng theo vai trò, tối đa 3 hàm; interface tổng hợp không khai báo thêm thành viên. |
| [Assets/_Project/Scripts/Infrastructure/Zenject/GameSceneInstaller.Bindings.cs](../Infrastructure/Zenject/GameSceneInstaller.Bindings.cs) | Tách đăng ký theo hệ thống; singleton qua interface và helper tránh tick trùng cho component scene. |
| [Assets/_Project/Scripts/Infrastructure/Zenject/GameSceneInstaller.ExecutionOrder.cs](../Infrastructure/Zenject/GameSceneInstaller.ExecutionOrder.cs) | Tách execution order, giữ giá trị và thứ tự lifecycle hiện có. |
| [Assets/_Project/Scripts/Levels/Contracts/ICharacterLevelService.cs](Levels/ICharacterLevelService.cs) | Hợp đồng theo vai trò, tối đa 3 hàm; interface tổng hợp không khai báo thêm thành viên. |
| [Assets/_Project/Scripts/Levels/Contracts/ILevelService.cs](Levels/ILevelService.cs) | Hợp đồng theo vai trò, tối đa 3 hàm; interface tổng hợp không khai báo thêm thành viên. |
| [Assets/_Project/Scripts/Levels/Contracts/IRunLevelService.cs](Levels/IRunLevelService.cs) | Hợp đồng theo vai trò, tối đa 3 hàm; interface tổng hợp không khai báo thêm thành viên. |
| [Assets/_Project/Scripts/Spawning/Bosses/Contracts/IBossSummonService.cs](Spawning/Bosses/IBossSummonService.cs) | Hợp đồng theo vai trò, tối đa 3 hàm; interface tổng hợp không khai báo thêm thành viên. |
| [Assets/_Project/Scripts/Spawning/Core/Contracts/IEnemySpawnService.cs](Spawning/Core/IEnemySpawnService.cs) | Hợp đồng theo vai trò, tối đa 3 hàm; interface tổng hợp không khai báo thêm thành viên. |
| [Assets/_Project/Scripts/Spawning/Stages/Contracts/IStageProgress.cs](Spawning/Stages/IStageProgress.cs) | Hợp đồng theo vai trò, tối đa 3 hàm; interface tổng hợp không khai báo thêm thành viên. |
| [Assets/_Project/Scripts/Spawning/Stages/Contracts/IStageService.cs](Spawning/Stages/IStageService.cs) | Hợp đồng theo vai trò, tối đa 3 hàm; interface tổng hợp không khai báo thêm thành viên. |
| [Assets/_Project/Scripts/UI/Core/Contracts/IScreenTransitionService.cs](UI/Core/IScreenTransitionService.cs) | Hợp đồng theo vai trò, tối đa 3 hàm; interface tổng hợp không khai báo thêm thành viên. |
| [Assets/_Project/Scripts/UI/Core/Contracts/IUINavigation.cs](UI/Core/IUINavigation.cs) | Hợp đồng theo vai trò, tối đa 3 hàm; interface tổng hợp không khai báo thêm thành viên. |
| [Assets/_Project/Scripts/World/Contracts/IWorldMapService.cs](World/IWorldMapService.cs) | Hợp đồng theo vai trò, tối đa 3 hàm; interface tổng hợp không khai báo thêm thành viên. |
| [Docs/Architecture/Service-Contracts-Change-Report.md](Service-Contracts-Change-Report.md) | Cập nhật tài liệu kiến trúc, đường dẫn hoặc kết quả kiểm chứng. |
| [Docs/Architecture/Service-Contracts.md](README.md) | Cập nhật tài liệu kiến trúc, đường dẫn hoặc kết quả kiểm chứng. |
| [Tests/ServiceContracts/README.md](../../../../Tests/ServiceContracts/README.md) | Cập nhật đường dẫn hợp đồng hoặc hướng dẫn chạy kiểm tra. |

## Di chuyển / đổi tên và đường dẫn cũ bị xóa

| Đường dẫn cũ | Đường dẫn mới | GUID |
| --- | --- | --- |
| `Assets/_Project/Scripts/Characters/CharacterSearch/ICharacterSearch.cs` | [Assets/_Project/Scripts/Characters/CharacterSearch/Contracts/ICharacterSearch.cs](Characters/CharacterSearch/ICharacterSearch.cs) | Giữ nguyên; `.meta` đi cùng |
| `Assets/_Project/Scripts/Characters/CharacterSearch/ICharacterSearchFactory.cs` | [Assets/_Project/Scripts/Characters/CharacterSearch/Contracts/ICharacterSearchFactory.cs](Characters/CharacterSearch/ICharacterSearchFactory.cs) | Giữ nguyên; `.meta` đi cùng |
| `Assets/_Project/Scripts/Characters/Controllers/ICharacterController.cs` | [Assets/_Project/Scripts/Characters/Controllers/Contracts/ICharacterController.cs](Characters/Controllers/ICharacterController.cs) | Giữ nguyên; `.meta` đi cùng |
| `Assets/_Project/Scripts/Characters/Controllers/ICharacterFeedbackSource.cs` | [Assets/_Project/Scripts/Characters/Controllers/Contracts/ICharacterFeedbackSource.cs](Characters/Controllers/ICharacterFeedbackSource.cs) | Giữ nguyên; `.meta` đi cùng |
| `Assets/_Project/Scripts/Characters/Controllers/ICharacterVisualEventSource.cs` | [Assets/_Project/Scripts/Characters/Controllers/Contracts/ICharacterVisualEventSource.cs](Characters/Controllers/ICharacterVisualEventSource.cs) | Giữ nguyên; `.meta` đi cùng |
| `Assets/_Project/Scripts/Combat/Effects/Visuals/IEffectVisual.cs` | [Assets/_Project/Scripts/Combat/Effects/Visuals/Contracts/IEffectVisual.cs](Combat/Effects/Visuals/IEffectVisual.cs) | Giữ nguyên; `.meta` đi cùng |
| `Assets/_Project/Scripts/Combat/Skills/ISkill.cs` | [Assets/_Project/Scripts/Combat/Skills/Data/SkillBehaviourType.cs](../Combat/Skills/Data/SkillBehaviourType.cs) | Giữ nguyên; `.meta` đi cùng |
| `Assets/_Project/Scripts/Core/JobSystem/Spatial/ISpatialManager.cs` | [Assets/_Project/Scripts/Core/JobSystem/Spatial/Contracts/ISpatialManager.cs](Core/JobSystem/Spatial/ISpatialManager.cs) | Giữ nguyên; `.meta` đi cùng |
| `Assets/_Project/Scripts/Core/JobSystem/Spatial/ISpatialSearchFactory.cs` | [Assets/_Project/Scripts/Core/JobSystem/Spatial/Contracts/ISpatialSearchFactory.cs](Core/JobSystem/Spatial/ISpatialSearchFactory.cs) | Giữ nguyên; `.meta` đi cùng |
| `Assets/_Project/Scripts/Spawning/Core/IStageSpawnEntity.cs` | [Assets/_Project/Scripts/Spawning/Core/Contracts/IStageSpawnEntity.cs](Spawning/Core/IStageSpawnEntity.cs) | Giữ nguyên; `.meta` đi cùng |
| `Assets/_Project/Scripts/Spawning/Core/IStageSpawnSystem.cs` | [Assets/_Project/Scripts/Spawning/Core/Contracts/IStageSpawnSystem.cs](Spawning/Core/IStageSpawnSystem.cs) | Giữ nguyên; `.meta` đi cùng |

Không xóa implementation hoặc chức năng. `IDamageReceiver`, `IProjectileMove`, `ISpatialSearchStrategy`, `IShooter` và `IGameplayEffect` được tách từ file chứa dữ liệu/implementation sang file contract; file implementation gốc vẫn tồn tại. File `ISkill.cs` cũ chuyển thành `Data/SkillBehaviourType.cs` để giữ enum, còn hợp đồng `ISkill` nằm trong `Contracts/ISkill.cs`.

## Metadata mới

Mỗi script/thư mục mới trong Assets có `.meta`. Metadata của file di chuyển được giữ GUID và đã liệt kê ở bảng trên. Các metadata mới khác:

- `Assets/_Project/Editor/Verification.meta`
- `Assets/_Project/Editor/Verification/ServiceContractVerification.cs.meta`
- `Assets/_Project/Scripts/Characters/CharacterSearch/Contracts.meta`
- `Assets/_Project/Scripts/Characters/Contracts.meta`
- `Assets/_Project/Scripts/Characters/Contracts/ICharacterFactory.cs.meta`
- `Assets/_Project/Scripts/Characters/Contracts/ICharacterSystem.cs.meta`
- `Assets/_Project/Scripts/Characters/Controllers/Contracts.meta`
- `Assets/_Project/Scripts/Characters/Controllers/Contracts/ICharacterCapabilities.cs.meta`
- `Assets/_Project/Scripts/Characters/Player/Contracts.meta`
- `Assets/_Project/Scripts/Characters/Player/Contracts/IPlayerSystem.cs.meta`
- `Assets/_Project/Scripts/Combat/Contracts.meta`
- `Assets/_Project/Scripts/Combat/Contracts/ICombatSpawnSystem.cs.meta`
- `Assets/_Project/Scripts/Combat/Damage/Contracts.meta`
- `Assets/_Project/Scripts/Combat/Damage/Contracts/IDamageReceiver.cs.meta`
- `Assets/_Project/Scripts/Combat/Effects/Gameplay/Contracts.meta`
- `Assets/_Project/Scripts/Combat/Effects/Gameplay/Contracts/IEffectRuntimePool.cs.meta`
- `Assets/_Project/Scripts/Combat/Effects/Gameplay/Contracts/IEffectSystem.cs.meta`
- `Assets/_Project/Scripts/Combat/Effects/Gameplay/Contracts/IGameplayEffect.cs.meta`
- `Assets/_Project/Scripts/Combat/Effects/Visuals/Contracts.meta`
- `Assets/_Project/Scripts/Combat/ShootingSkill/Contracts.meta`
- `Assets/_Project/Scripts/Combat/ShootingSkill/Contracts/IShootBehaviorFactory.cs.meta`
- `Assets/_Project/Scripts/Combat/ShootingSkill/Contracts/IShooter.cs.meta`
- `Assets/_Project/Scripts/Combat/ShootingSkill/Projectiles/Contracts.meta`
- `Assets/_Project/Scripts/Combat/ShootingSkill/Projectiles/Contracts/IProjectileMove.cs.meta`
- `Assets/_Project/Scripts/Combat/ShootingSkill/Projectiles/Contracts/IProjectileMoveFactory.cs.meta`
- `Assets/_Project/Scripts/Combat/ShootingSkill/Projectiles/Contracts/IProjectileSystem.cs.meta`
- `Assets/_Project/Scripts/Combat/Skills/Contracts.meta`
- `Assets/_Project/Scripts/Combat/Skills/Contracts/ISkill.cs.meta`
- `Assets/_Project/Scripts/Combat/Skills/Contracts/ISkillFactory.cs.meta`
- `Assets/_Project/Scripts/Combat/Skills/Contracts/ISkillServices.cs.meta`
- `Assets/_Project/Scripts/Combat/Skills/Contracts/ISkillSystem.cs.meta`
- `Assets/_Project/Scripts/Core/Currency/Contracts.meta`
- `Assets/_Project/Scripts/Core/Currency/Contracts/ICurrencyService.cs.meta`
- `Assets/_Project/Scripts/Core/GameFlow/Contracts.meta`
- `Assets/_Project/Scripts/Core/GameFlow/Contracts/IGameFlow.cs.meta`
- `Assets/_Project/Scripts/Core/JobSystem/Movement/Contracts.meta`
- `Assets/_Project/Scripts/Core/JobSystem/Movement/Contracts/IMovementSystem.cs.meta`
- `Assets/_Project/Scripts/Core/JobSystem/Spatial/Contracts.meta`
- `Assets/_Project/Scripts/Core/JobSystem/Spatial/Contracts/ISpatialSearchStrategy.cs.meta`
- `Assets/_Project/Scripts/Core/Run/Contracts.meta`
- `Assets/_Project/Scripts/Core/Run/Contracts/IGameplayPauseService.cs.meta`
- `Assets/_Project/Scripts/Core/Run/Contracts/IRunSession.cs.meta`
- `Assets/_Project/Scripts/Equipment/Contracts.meta`
- `Assets/_Project/Scripts/Equipment/Contracts/IEquipmentService.cs.meta`
- `Assets/_Project/Scripts/Infrastructure/Persistence/Contracts.meta`
- `Assets/_Project/Scripts/Infrastructure/Persistence/Contracts/ISaveService.cs.meta`
- `Assets/_Project/Scripts/Infrastructure/Pooling/Contracts.meta`
- `Assets/_Project/Scripts/Infrastructure/Pooling/Contracts/ICharacterRuntimePool.cs.meta`
- `Assets/_Project/Scripts/Infrastructure/Pooling/Contracts/IPoolPrewarmService.cs.meta`
- `Assets/_Project/Scripts/Infrastructure/Pooling/Contracts/IPoolSystem.cs.meta`
- `Assets/_Project/Scripts/Infrastructure/Zenject/GameSceneInstaller.Bindings.cs.meta`
- `Assets/_Project/Scripts/Infrastructure/Zenject/GameSceneInstaller.ExecutionOrder.cs.meta`
- `Assets/_Project/Scripts/Levels/Contracts.meta`
- `Assets/_Project/Scripts/Levels/Contracts/ICharacterLevelService.cs.meta`
- `Assets/_Project/Scripts/Levels/Contracts/ILevelService.cs.meta`
- `Assets/_Project/Scripts/Levels/Contracts/IRunLevelService.cs.meta`
- `Assets/_Project/Scripts/Spawning/Bosses/Contracts.meta`
- `Assets/_Project/Scripts/Spawning/Bosses/Contracts/IBossSummonService.cs.meta`
- `Assets/_Project/Scripts/Spawning/Core/Contracts.meta`
- `Assets/_Project/Scripts/Spawning/Core/Contracts/IEnemySpawnService.cs.meta`
- `Assets/_Project/Scripts/Spawning/Stages/Contracts.meta`
- `Assets/_Project/Scripts/Spawning/Stages/Contracts/IStageProgress.cs.meta`
- `Assets/_Project/Scripts/Spawning/Stages/Contracts/IStageService.cs.meta`
- `Assets/_Project/Scripts/UI/Core/Contracts.meta`
- `Assets/_Project/Scripts/UI/Core/Contracts/IScreenTransitionService.cs.meta`
- `Assets/_Project/Scripts/UI/Core/Contracts/IUINavigation.cs.meta`
- `Assets/_Project/Scripts/World/Contracts.meta`
- `Assets/_Project/Scripts/World/Contracts/IWorldMapService.cs.meta`

## Centralized Interfaces Folder

The following 49 interface files were moved into `Assets/_Project/Scripts/Interfaces/` with their existing `.meta` files. Source contents, names, namespaces, APIs and Zenject bindings were preserved. Runtime and Editor compilation passed after the move. The architecture guide is now [README.md](README.md); this report was also moved into the same root. Private implementation-only nested interfaces remain private to their owning classes.

| Previous location | Current location |
| --- | --- |
| `Assets/_Project/Scripts/Characters/CharacterSearch/Contracts/ICharacterSearch.cs` | [Assets/_Project/Scripts/Interfaces/Characters/CharacterSearch/ICharacterSearch.cs](Characters/CharacterSearch/ICharacterSearch.cs) |
| `Assets/_Project/Scripts/Characters/CharacterSearch/Contracts/ICharacterSearchFactory.cs` | [Assets/_Project/Scripts/Interfaces/Characters/CharacterSearch/ICharacterSearchFactory.cs](Characters/CharacterSearch/ICharacterSearchFactory.cs) |
| `Assets/_Project/Scripts/Characters/Contracts/ICharacterFactory.cs` | [Assets/_Project/Scripts/Interfaces/Characters/ICharacterFactory.cs](Characters/ICharacterFactory.cs) |
| `Assets/_Project/Scripts/Characters/Contracts/ICharacterSystem.cs` | [Assets/_Project/Scripts/Interfaces/Characters/ICharacterSystem.cs](Characters/ICharacterSystem.cs) |
| `Assets/_Project/Scripts/Characters/Controllers/Contracts/ICharacterCapabilities.cs` | [Assets/_Project/Scripts/Interfaces/Characters/Controllers/ICharacterCapabilities.cs](Characters/Controllers/ICharacterCapabilities.cs) |
| `Assets/_Project/Scripts/Characters/Controllers/Contracts/ICharacterController.cs` | [Assets/_Project/Scripts/Interfaces/Characters/Controllers/ICharacterController.cs](Characters/Controllers/ICharacterController.cs) |
| `Assets/_Project/Scripts/Characters/Controllers/Contracts/ICharacterFeedbackSource.cs` | [Assets/_Project/Scripts/Interfaces/Characters/Controllers/ICharacterFeedbackSource.cs](Characters/Controllers/ICharacterFeedbackSource.cs) |
| `Assets/_Project/Scripts/Characters/Controllers/Contracts/ICharacterVisualEventSource.cs` | [Assets/_Project/Scripts/Interfaces/Characters/Controllers/ICharacterVisualEventSource.cs](Characters/Controllers/ICharacterVisualEventSource.cs) |
| `Assets/_Project/Scripts/Characters/Player/Contracts/IPlayerSystem.cs` | [Assets/_Project/Scripts/Interfaces/Characters/Player/IPlayerSystem.cs](Characters/Player/IPlayerSystem.cs) |
| `Assets/_Project/Scripts/Combat/Contracts/ICombatSpawnSystem.cs` | [Assets/_Project/Scripts/Interfaces/Combat/ICombatSpawnSystem.cs](Combat/ICombatSpawnSystem.cs) |
| `Assets/_Project/Scripts/Combat/Damage/Contracts/IDamageReceiver.cs` | [Assets/_Project/Scripts/Interfaces/Combat/Damage/IDamageReceiver.cs](Combat/Damage/IDamageReceiver.cs) |
| `Assets/_Project/Scripts/Combat/Effects/Gameplay/Contracts/IEffectRuntimePool.cs` | [Assets/_Project/Scripts/Interfaces/Combat/Effects/Gameplay/IEffectRuntimePool.cs](Combat/Effects/Gameplay/IEffectRuntimePool.cs) |
| `Assets/_Project/Scripts/Combat/Effects/Gameplay/Contracts/IEffectSystem.cs` | [Assets/_Project/Scripts/Interfaces/Combat/Effects/Gameplay/IEffectSystem.cs](Combat/Effects/Gameplay/IEffectSystem.cs) |
| `Assets/_Project/Scripts/Combat/Effects/Gameplay/Contracts/IGameplayEffect.cs` | [Assets/_Project/Scripts/Interfaces/Combat/Effects/Gameplay/IGameplayEffect.cs](Combat/Effects/Gameplay/IGameplayEffect.cs) |
| `Assets/_Project/Scripts/Combat/Effects/Visuals/Contracts/IEffectVisual.cs` | [Assets/_Project/Scripts/Interfaces/Combat/Effects/Visuals/IEffectVisual.cs](Combat/Effects/Visuals/IEffectVisual.cs) |
| `Assets/_Project/Scripts/Combat/ShootingSkill/Contracts/IShootBehaviorFactory.cs` | [Assets/_Project/Scripts/Interfaces/Combat/ShootingSkill/IShootBehaviorFactory.cs](Combat/ShootingSkill/IShootBehaviorFactory.cs) |
| `Assets/_Project/Scripts/Combat/ShootingSkill/Contracts/IShooter.cs` | [Assets/_Project/Scripts/Interfaces/Combat/ShootingSkill/IShooter.cs](Combat/ShootingSkill/IShooter.cs) |
| `Assets/_Project/Scripts/Combat/ShootingSkill/Projectiles/Contracts/IProjectileMove.cs` | [Assets/_Project/Scripts/Interfaces/Combat/ShootingSkill/Projectiles/IProjectileMove.cs](Combat/ShootingSkill/Projectiles/IProjectileMove.cs) |
| `Assets/_Project/Scripts/Combat/ShootingSkill/Projectiles/Contracts/IProjectileMoveFactory.cs` | [Assets/_Project/Scripts/Interfaces/Combat/ShootingSkill/Projectiles/IProjectileMoveFactory.cs](Combat/ShootingSkill/Projectiles/IProjectileMoveFactory.cs) |
| `Assets/_Project/Scripts/Combat/ShootingSkill/Projectiles/Contracts/IProjectileSystem.cs` | [Assets/_Project/Scripts/Interfaces/Combat/ShootingSkill/Projectiles/IProjectileSystem.cs](Combat/ShootingSkill/Projectiles/IProjectileSystem.cs) |
| `Assets/_Project/Scripts/Combat/Skills/Contracts/ISkill.cs` | [Assets/_Project/Scripts/Interfaces/Combat/Skills/ISkill.cs](Combat/Skills/ISkill.cs) |
| `Assets/_Project/Scripts/Combat/Skills/Contracts/ISkillFactory.cs` | [Assets/_Project/Scripts/Interfaces/Combat/Skills/ISkillFactory.cs](Combat/Skills/ISkillFactory.cs) |
| `Assets/_Project/Scripts/Combat/Skills/Contracts/ISkillServices.cs` | [Assets/_Project/Scripts/Interfaces/Combat/Skills/ISkillServices.cs](Combat/Skills/ISkillServices.cs) |
| `Assets/_Project/Scripts/Combat/Skills/Contracts/ISkillSystem.cs` | [Assets/_Project/Scripts/Interfaces/Combat/Skills/ISkillSystem.cs](Combat/Skills/ISkillSystem.cs) |
| `Assets/_Project/Scripts/Core/Currency/Contracts/ICurrencyService.cs` | [Assets/_Project/Scripts/Interfaces/Core/Currency/ICurrencyService.cs](Core/Currency/ICurrencyService.cs) |
| `Assets/_Project/Scripts/Core/GameFlow/Contracts/IGameFlow.cs` | [Assets/_Project/Scripts/Interfaces/Core/GameFlow/IGameFlow.cs](Core/GameFlow/IGameFlow.cs) |
| `Assets/_Project/Scripts/Core/JobSystem/Movement/Contracts/IMovementSystem.cs` | [Assets/_Project/Scripts/Interfaces/Core/JobSystem/Movement/IMovementSystem.cs](Core/JobSystem/Movement/IMovementSystem.cs) |
| `Assets/_Project/Scripts/Core/JobSystem/Spatial/Contracts/ISpatialManager.cs` | [Assets/_Project/Scripts/Interfaces/Core/JobSystem/Spatial/ISpatialManager.cs](Core/JobSystem/Spatial/ISpatialManager.cs) |
| `Assets/_Project/Scripts/Core/JobSystem/Spatial/Contracts/ISpatialSearchFactory.cs` | [Assets/_Project/Scripts/Interfaces/Core/JobSystem/Spatial/ISpatialSearchFactory.cs](Core/JobSystem/Spatial/ISpatialSearchFactory.cs) |
| `Assets/_Project/Scripts/Core/JobSystem/Spatial/Contracts/ISpatialSearchStrategy.cs` | [Assets/_Project/Scripts/Interfaces/Core/JobSystem/Spatial/ISpatialSearchStrategy.cs](Core/JobSystem/Spatial/ISpatialSearchStrategy.cs) |
| `Assets/_Project/Scripts/Core/Run/Contracts/IGameplayPauseService.cs` | [Assets/_Project/Scripts/Interfaces/Core/Run/IGameplayPauseService.cs](Core/Run/IGameplayPauseService.cs) |
| `Assets/_Project/Scripts/Core/Run/Contracts/IRunSession.cs` | [Assets/_Project/Scripts/Interfaces/Core/Run/IRunSession.cs](Core/Run/IRunSession.cs) |
| `Assets/_Project/Scripts/Equipment/Contracts/IEquipmentService.cs` | [Assets/_Project/Scripts/Interfaces/Equipment/IEquipmentService.cs](Equipment/IEquipmentService.cs) |
| `Assets/_Project/Scripts/Infrastructure/Persistence/Contracts/ISaveService.cs` | [Assets/_Project/Scripts/Interfaces/Infrastructure/Persistence/ISaveService.cs](Infrastructure/Persistence/ISaveService.cs) |
| `Assets/_Project/Scripts/Infrastructure/Pooling/Contracts/ICharacterRuntimePool.cs` | [Assets/_Project/Scripts/Interfaces/Infrastructure/Pooling/ICharacterRuntimePool.cs](Infrastructure/Pooling/ICharacterRuntimePool.cs) |
| `Assets/_Project/Scripts/Infrastructure/Pooling/Contracts/IPoolPrewarmService.cs` | [Assets/_Project/Scripts/Interfaces/Infrastructure/Pooling/IPoolPrewarmService.cs](Infrastructure/Pooling/IPoolPrewarmService.cs) |
| `Assets/_Project/Scripts/Infrastructure/Pooling/Contracts/IPoolSystem.cs` | [Assets/_Project/Scripts/Interfaces/Infrastructure/Pooling/IPoolSystem.cs](Infrastructure/Pooling/IPoolSystem.cs) |
| `Assets/_Project/Scripts/Levels/Contracts/ICharacterLevelService.cs` | [Assets/_Project/Scripts/Interfaces/Levels/ICharacterLevelService.cs](Levels/ICharacterLevelService.cs) |
| `Assets/_Project/Scripts/Levels/Contracts/ILevelService.cs` | [Assets/_Project/Scripts/Interfaces/Levels/ILevelService.cs](Levels/ILevelService.cs) |
| `Assets/_Project/Scripts/Levels/Contracts/IRunLevelService.cs` | [Assets/_Project/Scripts/Interfaces/Levels/IRunLevelService.cs](Levels/IRunLevelService.cs) |
| `Assets/_Project/Scripts/Spawning/Bosses/Contracts/IBossSummonService.cs` | [Assets/_Project/Scripts/Interfaces/Spawning/Bosses/IBossSummonService.cs](Spawning/Bosses/IBossSummonService.cs) |
| `Assets/_Project/Scripts/Spawning/Core/Contracts/IEnemySpawnService.cs` | [Assets/_Project/Scripts/Interfaces/Spawning/Core/IEnemySpawnService.cs](Spawning/Core/IEnemySpawnService.cs) |
| `Assets/_Project/Scripts/Spawning/Core/Contracts/IStageSpawnEntity.cs` | [Assets/_Project/Scripts/Interfaces/Spawning/Core/IStageSpawnEntity.cs](Spawning/Core/IStageSpawnEntity.cs) |
| `Assets/_Project/Scripts/Spawning/Core/Contracts/IStageSpawnSystem.cs` | [Assets/_Project/Scripts/Interfaces/Spawning/Core/IStageSpawnSystem.cs](Spawning/Core/IStageSpawnSystem.cs) |
| `Assets/_Project/Scripts/Spawning/Stages/Contracts/IStageProgress.cs` | [Assets/_Project/Scripts/Interfaces/Spawning/Stages/IStageProgress.cs](Spawning/Stages/IStageProgress.cs) |
| `Assets/_Project/Scripts/Spawning/Stages/Contracts/IStageService.cs` | [Assets/_Project/Scripts/Interfaces/Spawning/Stages/IStageService.cs](Spawning/Stages/IStageService.cs) |
| `Assets/_Project/Scripts/UI/Core/Contracts/IScreenTransitionService.cs` | [Assets/_Project/Scripts/Interfaces/UI/Core/IScreenTransitionService.cs](UI/Core/IScreenTransitionService.cs) |
| `Assets/_Project/Scripts/UI/Core/Contracts/IUINavigation.cs` | [Assets/_Project/Scripts/Interfaces/UI/Core/IUINavigation.cs](UI/Core/IUINavigation.cs) |
| `Assets/_Project/Scripts/World/Contracts/IWorldMapService.cs` | [Assets/_Project/Scripts/Interfaces/World/IWorldMapService.cs](World/IWorldMapService.cs) |
| `Docs/Architecture/Service-Contracts-Change-Report.md` | [Assets/_Project/Scripts/Interfaces/Service-Contracts-Change-Report.md](Service-Contracts-Change-Report.md) |
| `Docs/Architecture/Service-Contracts.md` | [Assets/_Project/Scripts/Interfaces/README.md](README.md) |

## Getter Facades

39 aggregate interfaces now expose only parameterless getters returning their existing small interfaces. Aggregate inheritance has been removed. The 127 small interfaces retain their responsibilities and signatures. Implementations provide the facade and its roles separately; getters return the same instance. Zenject binds only the system facade(s), plus the lifecycle contracts required by its scheduler. Injected constructors/Construct methods obtain their small roles through the facade. Runtime objects created manually by factories may still receive an already obtained role.

The three spawn systems created via `DiContainer.Instantiate` also receive facades. `SkillFactory` exposes `ISkillFactory` and `ISkillServices` through one singleton binding. No new gameplay implementation or interface files were created or deleted for this step.

| Facade | Getters |
| --- | --- |
| `ICharacterSearch` | `GetCharacterSearchDispatch(): ICharacterSearchDispatch`; `GetCharacterRegionSearch(): ICharacterRegionSearch`; `GetCharacterTargetSearch(): ICharacterTargetSearch` |
| `ICharacterController` | `GetControl(): ICharacterControl`; `GetDamageReceiver(): ICharacterDamageReceiver`; `GetDamageDealer(): ICharacterDamageDealer`; `GetVitals(): ICharacterVitals`; `GetLifetime(): IDisposable` |
| `ICharacterFactory` | `GetCharacterSpawn(): ICharacterSpawn`; `GetCharacterDespawn(): ICharacterDespawn` |
| `ICharacterSystem` | `GetCharacterRegistration(): ICharacterRegistration`; `GetCharacterRegistry(): ICharacterRegistry`; `GetCharacterDeathEvents(): ICharacterDeathEvents` |
| `IPlayerSystem` | `GetPlayerReader(): IPlayerReader`; `GetPlayerSelection(): IPlayerSelection`; `GetPlayerLifecycle(): IPlayerLifecycle` |
| `IEffectRuntimePool` | `GetEffectRuntimeLease(): IEffectRuntimeLease`; `GetEffectRuntimeCapacity(): IEffectRuntimeCapacity` |
| `IEffectSystem` | `GetEffectApplication(): IEffectApplication`; `GetEffectHandleControl(): IEffectHandleControl`; `GetEffectOwnerLifecycle(): IEffectOwnerLifecycle`; `GetEffectCleanup(): IEffectCleanup` |
| `IGameplayEffect` | `GetSettings(): IGameplayEffectSettings`; `GetValidation(): IGameplayEffectValidation`; `GetRuntimeFactory(): IGameplayEffectFactory` |
| `ICombatSpawnSystem` | `GetProjectileSpawner(): IProjectileSpawner`; `GetEffectVisualSpawner(): IEffectVisualSpawner`; `GetCombatObjectRelease(): ICombatObjectRelease`; `GetEffectVisualCleanup(): IEffectVisualCleanup` |
| `IShootBehaviorFactory` | `GetFactory(): IShooterFactory`; `GetDirectionProvider(): IShotDirection` |
| `IShooter` | `GetDefinition(): IShooterDefinition`; `GetExecution(): IShooterExecution` |
| `IProjectileMoveFactory` | `GetProjectileMoveSelection(): IProjectileMoveSelection` |
| `IProjectileSystem` | `GetProjectileRegistration(): IProjectileRegistration`; `GetProjectileCleanup(): IProjectileCleanup`; `GetProjectileCapacity(): IProjectileCapacity` |
| `ISkill` | `GetConfiguration(): ISkillConfiguration`; `GetExecution(): ISkillExecution`; `GetLifecycle(): ISkillLifecycle` |
| `ISkillFactory` | `GetRuntimeFactory(): ISkillRuntimeFactory`; `GetBehaviourFactory(): ISkillBehaviourFactory` |
| `ISkillServices` | `GetShooting(): ISkillShootingServices`; `GetEffects(): ISkillEffectServices` |
| `ISkillSystem` | `GetOwnerLifecycle(): ISkillOwnerLifecycle`; `GetGrant(): ISkillGrant`; `GetReader(): IOwnedSkillReader`; `GetRunCleanup(): IRunSkillCleanup` |
| `ICurrencyService` | `GetReader(): ICurrencyReader`; `GetWallet(): ICurrencyWallet`; `GetPersistence(): ICurrencyPersistence`; `GetStageRewardTransaction(): IStageRewardTransaction` |
| `IGameFlow` | `GetGameStateReader(): IGameStateReader`; `GetRunCommands(): IRunCommands`; `GetRunResultCommands(): IRunResultCommands` |
| `IMovementSystem` | `GetRegistration(): IMovementRegistration`; `GetCommands(): IMovementCommand`; `GetArena(): IMovementArena`; `GetCapacity(): IMovementCapacity` |
| `ISpatialManager` | `GetRegistration(): ISpatialRegistration`; `GetInvalidation(): ISpatialInvalidation`; `GetResolution(): ISpatialResolution`; `GetQueries(): ISpatialQuery`; `GetCapacity(): ISpatialCapacity` |
| `ISpatialSearchFactory` | `GetQueryBuilder(): ISpatialQueryBuilder`; `GetStrategyProvider(): ISpatialStrategyProvider`; `GetQueryValidator(): ISpatialQueryValidator` |
| `IGameplayPauseService` | `GetGameplayPause(): IGameplayPause` |
| `IRunSession` | `GetRunStatistics(): IRunStatistics`; `GetRunGoldRecorder(): IRunGoldRecorder` |
| `IEquipmentService` | `GetEquipmentReader(): IEquipmentReader`; `GetEquipmentCommands(): IEquipmentCommands` |
| `ISaveService` | `GetSaveReader(): ISaveReader`; `GetSaveWriter(): ISaveWriter`; `GetSaveFlush(): ISaveFlush`; `GetSaveReset(): ISaveReset`; `GetSaveLoadFailure(): ISaveLoadFailure` |
| `ICharacterRuntimePool` | `GetCharacterRuntimeLease(): ICharacterRuntimeLease`; `GetCharacterRuntimeCapacity(): ICharacterRuntimeCapacity` |
| `IPoolPrewarmService` | `GetPoolPrewarm(): IPoolPrewarm`; `GetPoolPrewarmProgress(): IPoolPrewarmProgress` |
| `IPoolSystem` | `GetGameObjectPool(): IGameObjectPool`; `GetPoolPreparation(): IPoolPreparation`; `GetPoolHierarchy(): IPoolHierarchy` |
| `ICharacterLevelService` | `GetCharacterCatalog(): ICharacterCatalog`; `GetCharacterLevelReader(): ICharacterLevelReader`; `GetCharacterLevelCommands(): ICharacterLevelCommands` |
| `ILevelService` | `GetCharacterLevelCatalog(): ICharacterLevelCatalog`; `GetPermanentLevelReader(): IPermanentLevelReader`; `GetPermanentLevelCommands(): IPermanentLevelCommands`; `GetRunProgress(): IRunProgress`; `GetRunProgressCommands(): IRunProgressCommands` |
| `IRunLevelService` | `GetRunLevelReader(): IRunLevelReader`; `GetRunLevelCommands(): IRunLevelCommands` |
| `IBossSummonService` | `GetBossSummon(): IBossSummon` |
| `IEnemySpawnService` | `GetEnemySpawnValidation(): IEnemySpawnValidation`; `GetEnemySpawn(): IEnemySpawn`; `GetEnemyDespawn(): IEnemyDespawn`; `GetEnemySpawnGeometry(): IEnemySpawnGeometry`; `GetEnemySpawnLateEvents(): IEnemySpawnLateEvents` |
| `IStageProgress` | `GetStageProgressReader(): IStageProgressReader`; `GetStageVictoryRewards(): IStageVictoryRewards` |
| `IStageService` | `GetStageSelection(): IStageSelection`; `GetStageRunLifecycle(): IStageRunLifecycle`; `GetStageResults(): IStageResults`; `GetStageSchedule(): IStageSchedule` |
| `IScreenTransitionService` | `GetScreenTransition(): IScreenTransition` |
| `IUINavigation` | `GetUIPanelNavigation(): IUIPanelNavigation`; `GetUIRootNavigation(): IUIRootNavigation` |
| `IWorldMapService` | `GetWorldMap(): IWorldMap` |

### Updated source files

- [Assets/_Project/Editor/Verification/ServiceContractVerification.cs](../../Editor/Verification/ServiceContractVerification.cs)
- [Assets/_Project/Scripts/Camera/CameraFolow.cs](../Camera/CameraFolow.cs)
- [Assets/_Project/Scripts/Characters/CharacterFactory.cs](../Characters/CharacterFactory.cs)
- [Assets/_Project/Scripts/Characters/CharacterManager.cs](../Characters/CharacterManager.cs)
- [Assets/_Project/Scripts/Characters/CharacterSearch/CharacterSearch.cs](../Characters/CharacterSearch/CharacterSearch.cs)
- [Assets/_Project/Scripts/Characters/CharacterSearch/CharacterSearchFactory.cs](../Characters/CharacterSearch/CharacterSearchFactory.cs)
- [Assets/_Project/Scripts/Characters/CharacterView.cs](../Characters/CharacterView.cs)
- [Assets/_Project/Scripts/Characters/CharacterVisualHandle.cs](../Characters/CharacterVisualHandle.cs)
- [Assets/_Project/Scripts/Characters/Controllers/CharacterControllerBase.cs](../Characters/Controllers/CharacterControllerBase.cs)
- [Assets/_Project/Scripts/Characters/Enemies/EnemyVisualManager.cs](../Characters/Enemies/EnemyVisualManager.cs)
- [Assets/_Project/Scripts/Characters/Player/PlayerManager.cs](../Characters/Player/PlayerManager.cs)
- [Assets/_Project/Scripts/Combat/CombatSpawner.cs](../Combat/CombatSpawner.cs)
- [Assets/_Project/Scripts/Combat/Effects/Gameplay/EffectManager.cs](../Combat/Effects/Gameplay/EffectManager.cs)
- [Assets/_Project/Scripts/Combat/Effects/Gameplay/EffectRuntimePool.cs](../Combat/Effects/Gameplay/EffectRuntimePool.cs)
- [Assets/_Project/Scripts/Combat/Effects/Gameplay/GameplayEffects.cs](../Combat/Effects/Gameplay/GameplayEffects.cs)
- [Assets/_Project/Scripts/Combat/Effects/Visuals/AnimationEffectVisual.cs](../Combat/Effects/Visuals/AnimationEffectVisual.cs)
- [Assets/_Project/Scripts/Combat/Effects/Visuals/EffectView.cs](../Combat/Effects/Visuals/EffectView.cs)
- [Assets/_Project/Scripts/Combat/Effects/Visuals/ObjectEffectVisual.cs](../Combat/Effects/Visuals/ObjectEffectVisual.cs)
- [Assets/_Project/Scripts/Combat/Effects/Visuals/ParticleEffectVisual.cs](../Combat/Effects/Visuals/ParticleEffectVisual.cs)
- [Assets/_Project/Scripts/Combat/Loot/LootManager.cs](../Combat/Loot/LootManager.cs)
- [Assets/_Project/Scripts/Combat/ShootingSkill/Projectiles/ProjectileManager.cs](../Combat/ShootingSkill/Projectiles/ProjectileManager.cs)
- [Assets/_Project/Scripts/Combat/ShootingSkill/Projectiles/ProjectileMoves.cs](../Combat/ShootingSkill/Projectiles/ProjectileMoves.cs)
- [Assets/_Project/Scripts/Combat/ShootingSkill/Projectiles/ProjectileView.cs](../Combat/ShootingSkill/Projectiles/ProjectileView.cs)
- [Assets/_Project/Scripts/Combat/ShootingSkill/Shooters.cs](../Combat/ShootingSkill/Shooters.cs)
- [Assets/_Project/Scripts/Combat/Skills/Behaviours/FrostAuraSkill.cs](../Combat/Skills/Behaviours/FrostAuraSkill.cs)
- [Assets/_Project/Scripts/Combat/Skills/Behaviours/NormalAttackSkill.cs](../Combat/Skills/Behaviours/NormalAttackSkill.cs)
- [Assets/_Project/Scripts/Combat/Skills/Behaviours/OrbitSawSkill.cs](../Combat/Skills/Behaviours/OrbitSawSkill.cs)
- [Assets/_Project/Scripts/Combat/Skills/Behaviours/ShootingSkill.cs](../Combat/Skills/Behaviours/ShootingSkill.cs)
- [Assets/_Project/Scripts/Combat/Skills/Behaviours/SkillAreaVisual.cs](../Combat/Skills/Behaviours/SkillAreaVisual.cs)
- [Assets/_Project/Scripts/Combat/Skills/Data/ShootingData.cs](../Combat/Skills/Data/ShootingData.cs)
- [Assets/_Project/Scripts/Combat/Skills/Data/SkillBehaviourData.cs](../Combat/Skills/Data/SkillBehaviourData.cs)
- [Assets/_Project/Scripts/Combat/Skills/Skill.cs](../Combat/Skills/Skill.cs)
- [Assets/_Project/Scripts/Combat/Skills/SkillDataSO.cs](../Combat/Skills/SkillDataSO.cs)
- [Assets/_Project/Scripts/Combat/Skills/SkillFactory.cs](../Combat/Skills/SkillFactory.cs)
- [Assets/_Project/Scripts/Combat/Skills/SkillManager.cs](../Combat/Skills/SkillManager.cs)
- [Assets/_Project/Scripts/Combat/Skills/SkillRuntime.cs](../Combat/Skills/SkillRuntime.cs)
- [Assets/_Project/Scripts/Core/Currency/CurrencyService.cs](../Core/Currency/CurrencyService.cs)
- [Assets/_Project/Scripts/Core/GameFlow/GameManager.cs](../Core/GameFlow/GameManager.cs)
- [Assets/_Project/Scripts/Core/JobSystem/Movement/MovementManager.cs](../Core/JobSystem/Movement/MovementManager.cs)
- [Assets/_Project/Scripts/Core/JobSystem/Spatial/SpatialManager.cs](../Core/JobSystem/Spatial/SpatialManager.cs)
- [Assets/_Project/Scripts/Core/JobSystem/Spatial/SpatialSearchFactory.cs](../Core/JobSystem/Spatial/SpatialSearchFactory.cs)
- [Assets/_Project/Scripts/Core/Run/GameplayPauseService.cs](../Core/Run/GameplayPauseService.cs)
- [Assets/_Project/Scripts/Core/Run/RunSessionManager.cs](../Core/Run/RunSessionManager.cs)
- [Assets/_Project/Scripts/Equipment/PlayerEquipmentService.cs](../Equipment/PlayerEquipmentService.cs)
- [Assets/_Project/Scripts/Infrastructure/Persistence/SaveManager.cs](../Infrastructure/Persistence/SaveManager.cs)
- [Assets/_Project/Scripts/Infrastructure/Persistence/SaveMigration.cs](../Infrastructure/Persistence/SaveMigration.cs)
- [Assets/_Project/Scripts/Infrastructure/Pooling/CharacterRuntimePool.cs](../Infrastructure/Pooling/CharacterRuntimePool.cs)
- [Assets/_Project/Scripts/Infrastructure/Pooling/PoolManager.cs](../Infrastructure/Pooling/PoolManager.cs)
- [Assets/_Project/Scripts/Infrastructure/Pooling/PoolPrewarmService.cs](../Infrastructure/Pooling/PoolPrewarmService.cs)
- [Assets/_Project/Scripts/Infrastructure/Zenject/GameSceneInstaller.Bindings.cs](../Infrastructure/Zenject/GameSceneInstaller.Bindings.cs)
- [Assets/_Project/Scripts/Infrastructure/Zenject/GameSceneInstaller.cs](../Infrastructure/Zenject/GameSceneInstaller.cs)
- [Assets/_Project/Scripts/Interfaces/Characters/CharacterSearch/ICharacterSearch.cs](Characters/CharacterSearch/ICharacterSearch.cs)
- [Assets/_Project/Scripts/Interfaces/Characters/Controllers/ICharacterController.cs](Characters/Controllers/ICharacterController.cs)
- [Assets/_Project/Scripts/Interfaces/Characters/ICharacterFactory.cs](Characters/ICharacterFactory.cs)
- [Assets/_Project/Scripts/Interfaces/Characters/ICharacterSystem.cs](Characters/ICharacterSystem.cs)
- [Assets/_Project/Scripts/Interfaces/Characters/Player/IPlayerSystem.cs](Characters/Player/IPlayerSystem.cs)
- [Assets/_Project/Scripts/Interfaces/Combat/Effects/Gameplay/IEffectRuntimePool.cs](Combat/Effects/Gameplay/IEffectRuntimePool.cs)
- [Assets/_Project/Scripts/Interfaces/Combat/Effects/Gameplay/IEffectSystem.cs](Combat/Effects/Gameplay/IEffectSystem.cs)
- [Assets/_Project/Scripts/Interfaces/Combat/Effects/Gameplay/IGameplayEffect.cs](Combat/Effects/Gameplay/IGameplayEffect.cs)
- [Assets/_Project/Scripts/Interfaces/Combat/ICombatSpawnSystem.cs](Combat/ICombatSpawnSystem.cs)
- [Assets/_Project/Scripts/Interfaces/Combat/ShootingSkill/IShootBehaviorFactory.cs](Combat/ShootingSkill/IShootBehaviorFactory.cs)
- [Assets/_Project/Scripts/Interfaces/Combat/ShootingSkill/IShooter.cs](Combat/ShootingSkill/IShooter.cs)
- [Assets/_Project/Scripts/Interfaces/Combat/ShootingSkill/Projectiles/IProjectileMoveFactory.cs](Combat/ShootingSkill/Projectiles/IProjectileMoveFactory.cs)
- [Assets/_Project/Scripts/Interfaces/Combat/ShootingSkill/Projectiles/IProjectileSystem.cs](Combat/ShootingSkill/Projectiles/IProjectileSystem.cs)
- [Assets/_Project/Scripts/Interfaces/Combat/Skills/ISkill.cs](Combat/Skills/ISkill.cs)
- [Assets/_Project/Scripts/Interfaces/Combat/Skills/ISkillFactory.cs](Combat/Skills/ISkillFactory.cs)
- [Assets/_Project/Scripts/Interfaces/Combat/Skills/ISkillServices.cs](Combat/Skills/ISkillServices.cs)
- [Assets/_Project/Scripts/Interfaces/Combat/Skills/ISkillSystem.cs](Combat/Skills/ISkillSystem.cs)
- [Assets/_Project/Scripts/Interfaces/Core/Currency/ICurrencyService.cs](Core/Currency/ICurrencyService.cs)
- [Assets/_Project/Scripts/Interfaces/Core/GameFlow/IGameFlow.cs](Core/GameFlow/IGameFlow.cs)
- [Assets/_Project/Scripts/Interfaces/Core/JobSystem/Movement/IMovementSystem.cs](Core/JobSystem/Movement/IMovementSystem.cs)
- [Assets/_Project/Scripts/Interfaces/Core/JobSystem/Spatial/ISpatialManager.cs](Core/JobSystem/Spatial/ISpatialManager.cs)
- [Assets/_Project/Scripts/Interfaces/Core/JobSystem/Spatial/ISpatialSearchFactory.cs](Core/JobSystem/Spatial/ISpatialSearchFactory.cs)
- [Assets/_Project/Scripts/Interfaces/Core/Run/IGameplayPauseService.cs](Core/Run/IGameplayPauseService.cs)
- [Assets/_Project/Scripts/Interfaces/Core/Run/IRunSession.cs](Core/Run/IRunSession.cs)
- [Assets/_Project/Scripts/Interfaces/Equipment/IEquipmentService.cs](Equipment/IEquipmentService.cs)
- [Assets/_Project/Scripts/Interfaces/Infrastructure/Persistence/ISaveService.cs](Infrastructure/Persistence/ISaveService.cs)
- [Assets/_Project/Scripts/Interfaces/Infrastructure/Pooling/ICharacterRuntimePool.cs](Infrastructure/Pooling/ICharacterRuntimePool.cs)
- [Assets/_Project/Scripts/Interfaces/Infrastructure/Pooling/IPoolPrewarmService.cs](Infrastructure/Pooling/IPoolPrewarmService.cs)
- [Assets/_Project/Scripts/Interfaces/Infrastructure/Pooling/IPoolSystem.cs](Infrastructure/Pooling/IPoolSystem.cs)
- [Assets/_Project/Scripts/Interfaces/Levels/ICharacterLevelService.cs](Levels/ICharacterLevelService.cs)
- [Assets/_Project/Scripts/Interfaces/Levels/ILevelService.cs](Levels/ILevelService.cs)
- [Assets/_Project/Scripts/Interfaces/Levels/IRunLevelService.cs](Levels/IRunLevelService.cs)
- [Assets/_Project/Scripts/Interfaces/Spawning/Bosses/IBossSummonService.cs](Spawning/Bosses/IBossSummonService.cs)
- [Assets/_Project/Scripts/Interfaces/Spawning/Core/IEnemySpawnService.cs](Spawning/Core/IEnemySpawnService.cs)
- [Assets/_Project/Scripts/Interfaces/Spawning/Stages/IStageProgress.cs](Spawning/Stages/IStageProgress.cs)
- [Assets/_Project/Scripts/Interfaces/Spawning/Stages/IStageService.cs](Spawning/Stages/IStageService.cs)
- [Assets/_Project/Scripts/Interfaces/UI/Core/IScreenTransitionService.cs](UI/Core/IScreenTransitionService.cs)
- [Assets/_Project/Scripts/Interfaces/UI/Core/IUINavigation.cs](UI/Core/IUINavigation.cs)
- [Assets/_Project/Scripts/Interfaces/World/IWorldMapService.cs](World/IWorldMapService.cs)
- [Assets/_Project/Scripts/Levels/Services/CharacterLevelService.cs](../Levels/Services/CharacterLevelService.cs)
- [Assets/_Project/Scripts/Levels/Services/LevelManager.cs](../Levels/Services/LevelManager.cs)
- [Assets/_Project/Scripts/Levels/Services/RunLevelService.cs](../Levels/Services/RunLevelService.cs)
- [Assets/_Project/Scripts/Spawning/Bosses/BossHandleSummon.cs](../Spawning/Bosses/BossHandleSummon.cs)
- [Assets/_Project/Scripts/Spawning/Bosses/BossSpawnSystem.cs](../Spawning/Bosses/BossSpawnSystem.cs)
- [Assets/_Project/Scripts/Spawning/Core/EnemySpawnManager.cs](../Spawning/Core/EnemySpawnManager.cs)
- [Assets/_Project/Scripts/Spawning/Patterns/FormationSpawnSystem.cs](../Spawning/Patterns/FormationSpawnSystem.cs)
- [Assets/_Project/Scripts/Spawning/Stages/StageManager.cs](../Spawning/Stages/StageManager.cs)
- [Assets/_Project/Scripts/Spawning/Stages/StageProgressService.cs](../Spawning/Stages/StageProgressService.cs)
- [Assets/_Project/Scripts/Spawning/Waves/WaveSpawnSystem.cs](../Spawning/Waves/WaveSpawnSystem.cs)
- [Assets/_Project/Scripts/UI/Core/HomeController.cs](../UI/Core/HomeController.cs)
- [Assets/_Project/Scripts/UI/Core/LoadingController.cs](../UI/Core/LoadingController.cs)
- [Assets/_Project/Scripts/UI/Core/ScreenTransitionController.cs](../UI/Core/ScreenTransitionController.cs)
- [Assets/_Project/Scripts/UI/Core/UIManager.cs](../UI/Core/UIManager.cs)
- [Assets/_Project/Scripts/UI/Debug/ResetSaveButton.cs](../UI/Debug/ResetSaveButton.cs)
- [Assets/_Project/Scripts/UI/Gameplay/GameplayController.cs](../UI/Gameplay/GameplayController.cs)
- [Assets/_Project/Scripts/UI/Gameplay/PauseUI.cs](../UI/Gameplay/PauseUI.cs)
- [Assets/_Project/Scripts/UI/Gameplay/ResultController.cs](../UI/Gameplay/ResultController.cs)
- [Assets/_Project/Scripts/UI/Gameplay/RunUI.cs](../UI/Gameplay/RunUI.cs)
- [Assets/_Project/Scripts/UI/SkillChoice/SkillChoiceController.cs](../UI/SkillChoice/SkillChoiceController.cs)
- [Assets/_Project/Scripts/UI/SkillChoice/SkillChoiceUI.cs](../UI/SkillChoice/SkillChoiceUI.cs)
- [Assets/_Project/Scripts/World/GroundChunkManager.cs](../World/GroundChunkManager.cs)

Runtime and Editor compilation passed. Unity Editor checks passed for 166 public contracts, getter-only facades, unbound leaf roles, stable singleton instances, initialize/dispose/tick registrations, movement, search, projectile sweep/damage/release, skills, effects, stale handles, pause, restart and Home. Console reported zero errors. The temporary background-update setting used during prewarm was restored and Play Mode was stopped.
