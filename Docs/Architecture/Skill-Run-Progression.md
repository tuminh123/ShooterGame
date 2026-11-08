# Skill theo cấp trong trận

## Dữ liệu và vòng đời

- `SkillDataSO.Level` là cấp cấu hình hiện có (nền cho tiến trình vĩnh viễn); không phải cấp trong trận. Chưa có hệ thống save/nâng cấp skill vĩnh viễn riêng trong đợt này.
- Mỗi `SkillRuntime` bắt đầu `RunLevel = 1`, dù `SkillDataSO.Level = 5`. `MaxLevel` giới hạn cấp trận.
- `SkillManager.GrantOrUpgrade(definition, owner)` tìm cả skill riêng và skill trong trận. Nếu đã có thì nâng runtime đang chạy; không tạo bản bắn/bonus thứ hai. Không có thì tạo skill trong trận ở cấp 1.
- Skill riêng vẫn do `CharacterSkills` tick; skill nhận trong trận do `SkillManager` tick. Cả hai dùng `IRunLevelSkill` để cập nhật thông số/hành vi khi đổi cấp.
- `LevelManager.StartNewRun()` gỡ skill trong trận, đưa cấp trận của skill riêng về 1. Không sửa asset, cấp cấu hình hoặc bonus riêng/trang bị.
- Hết một lượt chơi cần gọi luồng reset trên khi bắt đầu lượt mới; game-over/scene transition hoàn chỉnh chưa thuộc thay đổi này.
- Nâng cấp không reset cooldown và không cộng lại `StatBonus`. Công thức tăng bonus theo level vĩnh viễn sẽ được thiết kế riêng.

## Shooter dùng chung, trạng thái chạy riêng

`ShootBehaviorFactory` là service chung biết các kiểu `StraightShooter`, `CircularShooter`, `SpiralShooter`. Mỗi shooting runtime nhận bản sao shooter của mình qua `CreateRuntime()`; góc Spiral không bị chia sẻ giữa nhân vật, giữa skill hoặc ghi vào asset.

`IShooter` chỉ tính hướng phát đạn; `IProjectileMove` chỉ tính hướng trong lúc bay. Movement hiện stateless nên factory dùng chung instance Straight/Homing. Khi thêm movement có trạng thái từng viên, trạng thái phải thuộc projectile, không đặt vào instance dùng chung.

`ShootingLevelTable` lưu hai danh sách để Unity serialize/chỉnh trong Inspector, sau đó dựng hai dictionary riêng cho từng runtime:

```csharp
Dictionary<int, (IShooter Shooter, IProjectileMove Movement, int Count)>
Dictionary<int, IReadOnlyList<IGameplayEffect>>
```

- Key là **level trận thật**, không phải index của list.
- Bảng shooting lấy mốc cao nhất <= level hiện tại. Mỗi entry chứa cả ba thông số; danh sách không bắt buộc nhập theo thứ tự.
- Bảng effect mở cộng dồn tất cả mốc <= level hiện tại, theo thứ tự level. Cùng một reference effect chỉ thêm một lần; hai cấu hình effect khác nhau vẫn là hai nguồn theo luật effect hiện có.
- Không có entry thì dùng cấu hình cơ bản. Level trùng hoặc vượt `MaxLevel`, strategy không được hỗ trợ và thông số không hợp lệ đều bị validation từ chối.
- Chỉ tạo lại shooter khi chuyển sang template khác; tăng số viên cùng template không mất góc Spiral. Reset về level 1 khởi tạo lại trạng thái shooter.
- Effect được tạo thành list snapshot mới khi đổi cấp, không sửa list đạn cũ đang giữ. Một volley cũng chốt count/shooter/effect trước callback spawn để không trộn hai cấu hình trong một lượt bắn.

HolyBolt là ví dụ đã cấu hình:

| Cấp trận | Số viên | Shooter | Movement | Effect bổ sung |
| --- | --- | --- | --- | --- |
| 1 | 1 | Straight | Straight | Không |
| 2 | 2 | Straight | Straight | Không |
| 3 | 3 | Straight | Straight | Không |
| 4 | 4 | Straight | Straight | Không |
| 5 | 5 | Straight | Homing | Không |
| 6 | 5 | Circular | Homing | Slow 30%, 2 giây |

Chỉ ví dụ shooting có cấp 6/hành vi đặc biệt. Các asset shooting khác chưa có bảng mới vẫn hoạt động theo cấu hình cơ bản. Tốc độ, bán kính hit và lifetime không đổi theo bảng này; damage lấy chỉ số tổng và roll crit riêng từng viên như trước.

## OrbitSaw

- Asset: `Assets/_Project/DataSO/Skills/OrbitSaw.asset`.
- Có đúng 5 cấp: số lưỡi bằng cấp trận, chia đều trên một quỹ đạo. Không có cấp 6.
- Mặc định: bán kính quỹ đạo 0.5, bán kính lưỡi 0.1 world unit, tốc độ 180 độ/giây.
- Mỗi lưỡi gây damage tổng của chủ nhân và roll crit, tối đa một hit trên cùng lượt spawn mục tiêu trong 0.5 giây. Lịch của từng lưỡi độc lập.
- Broadphase tìm đối thủ quanh quỹ đạo, sau đó swept circle kiểm tra hitbox; cung quay được chia đoạn <=15 độ (tối đa 128 đoạn/bước). Không dùng collider/Physics2D và không ngăn di chuyển qua tường.
- Lịch hit dùng `CharacterTarget` có identity/generation, được dọn khi hết hạn hoặc mục tiêu không còn hợp lệ; không giữ danh sách quái chết vô hạn.

## FrostAura

- Asset: `Assets/_Project/DataSO/Skills/FrostAura.asset`.
- Có đúng 5 cấp, bán kính = `initialRadius + (RunLevel - 1) * radiusPerLevel`. Mặc định 1, 2, 3, 4, 5 world unit; không có cấp 6.
- Một vùng luôn đi theo chủ nhân, không gây damage. Đối thủ có hitbox giao vùng chịu slow 30%; đây là aura duy trì chắc chắn, không roll kháng/xác suất như debuff đạn.
- Mỗi mục tiêu chỉ có một speed token cho mỗi nguồn aura. Ra khỏi vùng, chủ nhân disable/chết/despawn, hoặc skill bị gỡ thì token được gỡ. Không làm mới debuff có thời hạn, nên không để slow kéo dài ngoài vùng.
- Nhiều aura/slow cùng tồn tại vẫn dùng luật slow mạnh nhất của `CharacterEffectHandler`, không nhân chồng và không gỡ token của nguồn khác.

## Visual, cấu hình và kiểm tra

- Hai skill mới được gắn sẵn cho Blessed Gladiator để xem trong SampleScene. Có thể bỏ chúng khỏi `CharacterDataSO.Skills` và cấp bằng `GrantOrUpgrade` để dùng như skill nhận trong trận.
- Prefab `OrbitSawVisual` và `FrostAuraVisual` dùng `SkillAreaVisual` tạo mesh cơ bản; visual không chứa logic gây damage/slow. Có thể thay hình ảnh sau. Pool prewarm chuẩn bị 5 visual cưa và 1 visual băng.
- `IContinuousSkill` cho phép quỹ đạo/aura cập nhật trong cùng scheduler ngay cả khi cooldown chưa sẵn sàng. Pause chỉ ẩn visual và gỡ aura token; despawn/dispose trả visual về pool.
- Công cụ Editor: vào Play Mode, chọn asset skill trong Project, chọn **Shooter Game > Skills > Grant Or Upgrade Selected Skill**. Console báo cấp hiện tại; ở max level không nâng thêm. UI chọn skill khi lên cấp đã được nối, xem [Skill Choice UI](Skill-Choice-UI.md); UI tạm giới hạn 5 cấp.
- Comment trong `ShootingLevelTable`, `ShootingSkill`, `SkillRuntime` và hai runtime vùng/quỹ đạo giải thích ownership, reset và xử lý callback lồng nhau.

Chạy kiểm tra logic ngoài Unity từ root dự án:

```powershell
./Tests/SkillProgression/Run.ps1
```

Harness biên dịch trực tiếp các file production về progression, shooting, saw, aura và hình học; chỉ thay phần Unity lifecycle/render/pool/query bằng test doubles. Nó không xác minh native rendering, Inspector hay thứ tự Unity PlayerLoop; vẫn cần kiểm tra Play Mode.
