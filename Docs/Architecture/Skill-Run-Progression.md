# Skill theo cấp và nguồn

## Cấu trúc

SkillDataSO chứa display/source, cấp cấu hình ban đầu, Levels[] và danh sách mẫu ISkill[].
Mỗi cấp chứa mô tả và danh sách SkillBehaviourData đầy đủ. Số cấp là Levels.Length;
không còn MaxLevel riêng hoặc ShootingLevelTable.

SkillBehaviourType định danh loại: NormalAttack, Shooting, OrbitSaw, SlowAura,
Regeneration, EmergencyShield, StatBuff. Mỗi loại xuất hiện tối đa một lần trong
danh sách mẫu và một lần trong data của một cấp. Class tự trả Type cố định;
không nhập string hoặc enum thủ công.

ISkill là hợp đồng thực thi. Abstract Skill cung cấp Tick/TryExecute/SetPaused/
Dispose mặc định và clone context; hành vi con chỉ override phần cần dùng.
SkillBehaviourData chỉ giữ cấu hình, Validate và cooldown nếu loại đó cần.

## Cấu hình từng cấp

- ShootingData: cooldown/range, số đạn, IShooter, IProjectileMove, bullet type,
  tốc độ, hit radius, lifetime, targeting và effects.
- OrbitSawData: số lưỡi, bán kính quỹ đạo/lưỡi, tốc độ quay, nhịp hit, visual, effects.
- FrostAuraData: bán kính trực tiếp, slow, visual và effects lúc vào vùng.
- NormalAttackData: nhịp đánh, tầm tìm và effects khi tiếp xúc.
- RegenerationData: lượng hồi và cooldown.
- EmergencyShieldData: ngưỡng HP, lượng shield và cooldown.
- StatBonusData: chỉ số và giá trị đầy đủ tại cấp đó.

Mỗi cấp khai báo đầy đủ, không kế thừa ngầm cấu hình hoặc effect của cấp trước.
Trong ShootingData, StraightShooter luôn bắn theo hướng ngắm và không có tham số góc.
FanShooter triển khai IShooter riêng, mặc định spreadAngle = 60 độ; projectileCount
viên tự chia đều từ -spreadAngle/2 đến +spreadAngle/2 quanh hướng ngắm.
Một viên giữ hướng ngắm. Chọn FanShooter trong selector Shooter để dùng bắn quạt.
Ví dụ cấp 1 có ShootingData; cấp 2 thêm FrostAuraData thì runtime mở vùng băng ở
cấp 2. Danh sách mẫu bên dưới phải chứa cả ShootingSkill và FrostAuraSkill.

## Runtime

SkillManager chỉ giữ và tick runtime nguồn Gameplay. Reset trận chỉ dọn nhóm này.
CharacterSkills giữ context của spawn, tự tạo và sở hữu runtime Character/Equipment.
CharacterView.Update gọi CharacterSkills.Tick; disable/enable pause/resume skill,
chết/despawn/destroy dispose timer, visual và modifier. Skill nhân vật còn sống
không bị xóa hoặc dựng lại khi reset riêng tiến trình trận.
Slot trong LevelUpPanel chỉ hiển thị danh sách Gameplay từ SkillManager.
SkillRuntime chọn data theo cấp, ghép theo Type, gọi ApplyData cho hành vi đã có,
tạo instance cho loại mới và dispose loại không còn trong cấp mới.

Nâng cấp giữ cooldown đang chạy; cooldown mới dùng cho lần cast tiếp theo.
Cưa giữ góc/lịch hit của các lưỡi còn tồn tại, tăng/giảm visual theo bladeCount.
Aura cập nhật bán kính và gỡ token slow cũ trước khi áp dụng vùng mới.
Buff thay modifier cũ: +2 lên +5 cho kết quả +5, không cộng thành +7.

Nếu áp dụng cấp mới lỗi, runtime thử áp dụng lại data cấp cũ. Nếu rollback lỗi,
dispose skill đó; cleanup vẫn thử mọi hành vi. Callback reset/dispose không được
commit cấp mới lên runtime đã hết vòng đời. Rollback không cam kết phục hồi mọi
visual hoặc state nội bộ đã được thay đổi trước lỗi.

## Character và gameplay

HolyBolt đang gắn cho BlessedGladiator là nguồn Character, không nằm trong
SkillDatabase gameplay. EnemyNormalAttack và hai skill boss cũng là Character.
CharacterDataSO trực tiếp tham chiếu các definition vốn có; GUID được giữ khi
di chuyển asset.

- Gameplay: bắt đầu cấp 1, nhận/nâng bằng lượt thưởng trong màn.
- Character/Equipment: bắt đầu theo Level config, không được nâng bằng lượt thưởng.
- SkillDatabase chỉ chấp nhận nguồn Gameplay; bộ roll cũng lọc nguồn để phòng
  caller truyền nhầm catalog.
- Nếu sau này muốn một phiên bản HolyBolt nhận trong màn, tạo asset Gameplay riêng,
  không dùng chung definition của nhân vật.
- Hệ nâng cấp/lưu skill ở Home chưa được triển khai. Level config là giá trị
  khởi tạo hiện tại, không phải một hệ save mới.

GameManager dọn skill gameplay và giữ pause khi kết thúc trận. Skill nhân vật
còn sống giữ nguyên runtime; chỉ dọn khi owner chết/despawn/destroy. Khi bắt đầu
trận mới hoặc về Home, GameManager thu hồi nhân vật cũ trước khi reset tiến trình.
Reset riêng tiến trình không dựng lại skill nhân vật, không sửa asset hoặc dữ liệu lưu.
Owner chết/despawn gỡ timer, visual và token của chính owner đó.

## Damage và effect

Damage bắn/cưa/tiếp xúc tiếp tục dùng final stats của owner và pipeline hiện có.
Đạn snapshot damage/effect khi bắn; nâng cấp không thay đổi viên đã bay.
Mỗi shooter có state riêng. Đổi config shooter tạo instance mới; cùng config giữ
góc Spiral. IProjectileMove quyết định hướng bay, MovementManager cập nhật vị trí.

Cưa dùng spatial sweep và lịch hit riêng theo lưỡi/mục tiêu. Aura không gây damage,
slow duy trì bị gỡ khi mục tiêu ra ngoài hoặc nguồn pause/dispose.
Effect bổ sung của aura áp dụng lúc vào vùng và giữ thời hạn riêng của nó.

## Kiểm tra

Chỉ kiểm tra biên dịch trong đợt thay đổi này. Không chạy harness hoặc Play Mode.
Harness ngoài Assets được cập nhật theo model mới; dùng Run.ps1 khi muốn kiểm tra
logic bằng host doubles. Native rendering, serialization/import và scene lifecycle
vẫn cần kiểm tra trong Unity.


## Thư mục

Scripts/Combat/Skills/Data chứa cấu hình từng loại và SkillLevelData.
Scripts/Combat/Skills/Behaviours chứa phần thực thi.
Các class điều phối, ISkill và abstract Skill nằm ở gốc Scripts/Combat/Skills.

DataSO/Skills/SkillDataGameplay chứa 8 skill nhận trong màn.
DataSO/Skills/SkillDataCharacter/BlessedGladiator chứa HolyBolt.
DataSO/Skills/SkillDataCharacter/ForagingMaggot chứa EnemyNormalAttack.
DataSO/Skills/SkillDataCharacter/Boss chứa hai skill boss.

## HP/giáp và cập nhật UI

CharacterRuntimeData lưu lostHealth/lostShield; CurrentHealth/CurrentShield được
tính từ max trừ lượng đã mất. MaxHealth lấy từ FinalStats.maxHealth, MaxShield
lấy từ FinalStats.defense, không còn grantedShieldCapacity.

Bonus thay max sẽ giữ damage đã nhận: 30/100 nhận +10 thành 40/110.
Phần HP/giáp được cấp trực tiếp cũng tăng current và max cùng lượng, kể cả
khi đang đầy; phần cấp được giữ khi tính lại stats. Regeneration không chờ
nhân vật bị thương mới kích hoạt. Nhân vật đã chết không tự hồi sinh khi đổi bonus.

StatModifierLease.Update thay giá trị buff tại chỗ, tránh gỡ buff cũ làm current
tụt tạm thời trước khi áp dụng cấp mới.

Changed được phát sau thao tác damage, cấp HP/giáp, thay bonus level/trang bị,
thêm/cập nhật/gỡ modifier hoặc đổi slow. CharacterUI đăng ký khi bind, đọc cả
current/max mới rồi cập nhật BarView; gỡ listener khi disable/despawn.
Một listener lỗi được log, không ngăn các listener khác cập nhật.

Đợt chỉnh HP/giáp đã qua biên dịch và 21 kiểm tra tập trung về phép cộng, giữ damage,
thông báo và không tự hồi sinh. Không chạy toàn bộ harness hoặc Unity Play Mode.
## Modifier stat chung

StatModifier xác định StatType, StatOperation (Add/Percent) và value.
StatCalculator tính (Base + external + tổng Add) * (1 + tổng Percent / 100).
CharacterRuntimeData dùng AddModifier/AddModifiers và StatModifierLease.Update/Dispose.
Không còn movementScale hoặc AddTemporaryBonus. lostHealth/lostShield vẫn là trạng
thái damage đã nhận để tính current từ max, không phải modifier.

StatBonusData dùng enum StatType và phép toán operation. Asset cũ giữ cùng thứ tự
enum; thiếu operation mặc định Add nên không đổi ý nghĩa buff hiện có.
Slow/aura chuyển thành MoveSpeed Percent; EffectHandler giữ luật slow mạnh nhất.
Visual theo thời hạn effect chưa được nối thêm trong đợt sửa stat này.

Đã qua biên dịch và 35 kiểm tra tập trung stat/HP/giáp. Không chạy toàn bộ harness
hoặc Unity Play Mode.
Phạm vi modifier chỉ tăng/giảm các stat hiện có. Hệ effect, pool, mục tiêu và công thức nhận damage giữ nguyên; slow hiện có chỉ nối sang modifier MoveSpeed.
