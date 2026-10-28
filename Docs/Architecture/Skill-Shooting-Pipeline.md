# Skill, shooting, projectile và hiệu ứng

## Phân chia trách nhiệm

- CharacterDataSO giữ một mảng SkillDataSO[].
- SkillDataSO phân loại bằng SkillCategory.Active/Passive. Field SerializeReference skill chỉ chọn Type của Strategy, không chứa cấu hình hoặc trạng thái riêng.
- SkillFactory đăng ký Dictionary<Type, ISkill>. Các Strategy không có trạng thái và được dùng chung.
- Mỗi lần gắn skill, factory tạo SkillRuntime riêng. Runtime giữ data, Strategy, cooldown, retry timer, góc pattern và quyền thu hồi passive.
- CharacterSkillHandler quản lý runtime, active tự động/thủ công, cooldown và việc gắn/gỡ passive. ISkill chỉ Execute và trả SkillExecution.
- ShootingService là đường bắn chung của Player, Enemy và boss.
- ShootingPatternFactory chọn IShootingPattern: Straight (một hướng/quạt), Circular hoặc Spiral.
- ProjectileService dùng một prefab vỏ duy nhất từ GameSceneInstaller. ProjectileDataSO chọn sprite, màu, damage multiplier, tốc độ, vùng trúng, lifetime, flight, hit và mảng hiệu ứng.
- ProjectileStrategyFactory chọn flight Straight/Homing và hit SingleTarget.
- CharacterEffectHandler sở hữu Slow/Burn/Poison trên mục tiêu; đạn despawn không xóa hiệu ứng đã áp dụng.

## Cách cấu hình

1. Chọn nhân vật trong DataSO/Characters, chỉnh mảng Skills.
2. Kéo asset trong DataSO/Skills vào mảng. Mỗi phần tử có cooldown riêng, kể cả khi dùng cùng Type Strategy.
3. Chọn skill để chỉnh Category, Cooldown, Auto Cast và Retry Interval.
4. ProjectileSkillDataSO chọn Projectile và Shooting Pattern. Inspector có dropdown để chọn Strategy.
5. Requires Target bật: tìm đối thủ gần nhất theo FinalStats.range. Tắt: phát đạn theo Default Direction, phù hợp pattern boss.
6. Auto Cast tắt: gameplay/UI gọi CharacterView.Skills.TryUse(runtime). Handler vẫn kiểm tra cooldown, owner và membership.
7. Gỡ skill bằng RemoveSkill(runtime) để thu hồi passive; không sửa asset trong lúc chơi.

Mảng mẫu của Blessed Gladiator vẫn chỉ có HolyBolt với cooldown 0,6 giây. Các skill FireBolt, IceBolt, PoisonBolt và Toughness là ví dụ để kéo thêm vào Inspector; không tự gắn vào nhân vật.

## Kiểu phát đạn và đường bay

Hai khái niệm độc lập:

| Cấu hình | Quyết định |
|---|---|
| Shooting pattern | Số viên và hướng ban đầu của từng viên trong một lần dùng skill |
| Projectile flight | Hướng di chuyển của từng viên sau khi đã sinh |
| Projectile hit | Cách xử lý khi viên đạn chạm mục tiêu |

Straight phát một viên hoặc quạt theo Spread Angle.
Circular chia đều 360 độ, không lặp viên ở đường nối 0/360.
Spiral phát các nhánh chia đều rồi tăng góc sau mỗi lần dùng skill. Đây là chuỗi phát xoắn ốc, không phải mỗi viên tự bay theo đường cong.

BossFireCircle: 12 viên mỗi 1,5 giây, không cần mục tiêu.
BossFireSpiral: 3 nhánh mỗi 0,12 giây, xoay 15 độ mỗi lần phát, không cần mục tiêu.
Hai nhân vật dùng cùng asset Spiral vẫn có Phase và CastCount riêng.

Các asset pattern nằm trong DataSO/Shooting. Có thể ghép bất kỳ pattern với loại đạn nào thông qua skill. Prefab HolyBolt hiện là vỏ dùng chung; prefab HolyBolt_Homing cũ cũng chỉ là vỏ và không quyết định flight.

## Hiệu ứng và chỉ số

- Ice mang Slow: giảm tốc 30% trong 2 giây.
- Fire mang Burn: damage mỗi giây bằng 20% damage nguồn, trong 3 giây.
- Poison mang Poison: damage mỗi giây bằng 25% damage nguồn, trong 4 giây.
- Damage trực tiếp chốt crit khi bắn. Damage theo thời gian dùng damage gốc của nguồn lúc bắn, không roll crit lại, và đi qua defense một lần mỗi tick.
- Khi damage trực tiếp giết mục tiêu, không thêm hiệu ứng lên vỏ đã bị thu hồi.
- Range giữ từ BaseStats, không có bonus nâng range.
- Slow là modifier tạm thời trên tốc độ sau bonus. Nhiều Slow dùng mức mạnh nhất; mỗi hiệu ứng có thời hạn riêng.
- Cùng asset hiệu ứng được áp dụng lại sẽ làm mới thời gian và dữ liệu nguồn, không cộng tầng. Lịch tick DOT được giữ để bắn liên tục không trì hoãn damage.
- Burn và Poison dùng hai asset riêng nên có thể cùng tồn tại.

Mỗi hiệu ứng có Application Chance và Use Accuracy And Dodge. Khi bật, quy tắc hiện tại là:

    chance = baseChance * (100 + max(0, accuracy))
                        / (100 + max(0, accuracy) + max(0, dodge))

Kết quả giới hạn 0–1. Đây là quy tắc cân bằng mặc định, có thể thay tập trung tại CharacterEffectHandler.ApplicationChance. Tắt Use Accuracy And Dodge để chỉ dùng xác suất trên asset. Dodge không né damage trực tiếp.

## Passive và vòng đời

Toughness dùng StatBonusSkill, cộng defense một lần khi handler được tick và gỡ khi mất skill, chết hoặc despawn. Recalculate do level/trang bị thay đổi vẫn giữ modifier đang gắn.

ISkill có thể trả IDisposable trong SkillExecution.Lifetime để handler thu hồi tác dụng/subscription. Implementation passive mẫu là cộng chỉ số liên tục. API TryTriggerPassive(runtime, effect) cho phép Strategy đăng ký sự kiện rồi yêu cầu handler kiểm tra cooldown nội bộ trước khi kích hoạt; chưa có asset passive sự kiện mẫu hoặc UI chọn skill.

Khi chết hoặc despawn: handler skill dispose, handler hiệu ứng dispose, modifier được thu hồi. Khi spawn lại: tạo runtime và handler mới. Tắt GameObject tạm thời giữ cùng runtime, các bộ đếm dùng deltaTime chỉ tiếp tục khi object hoạt động trở lại.

## Mở rộng

- Hành vi skill mới: triển khai ISkill, tạo data phù hợp, đăng ký ở SkillFactory và thêm lựa chọn Inspector.
- Pattern mới: triển khai IShootingPattern, đăng ký ShootingPatternFactory; không sửa ProjectileService hoặc từng nguyên tố.
- Đường bay mới: triển khai IProjectileFlight và đăng ký ProjectileStrategyFactory.
- Hit mới: triển khai IProjectileHit và đăng ký ProjectileStrategyFactory.
- Hiệu ứng mới: tạo data/Strategy, đăng ký StatusEffectFactory.

Không lưu owner, cooldown, góc xoắn ốc hay thời hạn hiệu ứng trên ScriptableObject/Strategy dùng chung.

## Kiểm tra

SkillPipelineTests kiểm tra tham chiếu asset, runtime độc lập, hình học vòng/quạt, modifier/range, accuracy/dodge và pipeline Play Mode qua pooling.
CharacterSystemTests tiếp tục kiểm tra vỏ dùng chung, dữ liệu runtime và tăng trưởng Enemy.
