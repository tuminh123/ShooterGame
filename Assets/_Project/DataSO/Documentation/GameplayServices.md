# Hệ thống nhân vật và dịch vụ gameplay

Thiết kế ưu tiên YAGNI/Simple → DRY → SOLID: tách dữ liệu, hành vi và vỏ Unity; không thêm lớp Character trung gian hoặc pool class thuần khi chưa cần.

## Các thành phần

| Thành phần | Trách nhiệm |
| --- | --- |
| CharacterDataSO | Mẫu theo loại: mã, phe, chỉ số gốc, visual, hitbox và tăng trưởng Enemy. |
| CharacterRuntimeData | Dữ liệu riêng từng cá thể: HP, chỉ số sau bonus, level và bonus chốt khi spawn. |
| CharacterView | Vỏ Unity chung: liên kết data/hành vi, giữ component và visual con, chuyển tiếp Tick/va chạm. |
| PlayerController | Class thuần đọc joystick/bàn phím và tính di chuyển analog; hết HP thì dừng. |
| EnemyController | Class thuần tự đuổi Player hiện tại; chết hoặc chạm Player thì về pool. |
| ICharacterController | Hợp đồng Tick/tiếp xúc để vỏ gọi hành vi hiện tại. |
| CharacterFactory | Ghép runtime, controller và visual vào vỏ khi vỏ còn tắt. |
| PlayerManager | Tạo/gỡ Player, áp dụng level/trang bị và phát PlayerChanged. |
| EnemySpawnManager | Chọn wave, loại và vị trí spawn; tính bonus lúc spawn, theo dõi quái đang sống. |

Controller không chứa bản sao HP/chỉ số. Runtime được gắn với view trong một vòng đời; controller nhận runtime khi xử lý. Hai controller có hành vi di chuyển độc lập, cùng gửi lệnh qua MovementManager.

## Prefab và cấu hình

- Prefabs/Character.prefab là prefab gameplay duy nhất: CharacterView, Rigidbody2D và BoxCollider2D.
- BlessedGladiator_Visual.prefab và ForagingMaggot_Visual.prefab chỉ chứa SpriteRenderer/Animator; lấy từ pool rồi gắn làm con khi spawn.
- BlessedGladiator_Character.asset thuộc Player; ForagingMaggot_Character.asset thuộc Enemy.
- Enemy mẫu có 20 HP, tốc độ 0.5; mỗi 50 giây cộng 5 HP cho cá thể mới của loại đó.
- Default_Waves.asset giữ nhịp mẫu 0.5 giây/con, tối đa 20 quái; thêm wave/loại quái trong Inspector.
- SampleScene đã gán Character Prefab ở GameSceneInstaller và Wave Config ở EnemySpawnManager.

CharacterType là khóa theo loại; CharacterTeam là phe. Boss thuộc Enemy. Database chung chứa cả hai phe, nhưng chỉ Player cần cấu hình level vĩnh viễn. Camera và nền nghe PlayerChanged(CharacterView).

## Vòng đời spawn/pool

```text
Tạo runtime và controller riêng cho lần spawn
→ lấy vỏ dưới Preparing đang tắt
→ inject clone mới một lần
→ beforeActivate: gán runtime/controller, hitbox, lấy visual con
→ chuyển vỏ sang holder hoạt động
→ OnEnable đăng ký movement và reset Animator
→ Update chuyển tiếp sang controller tương ứng
```

Khi thu hồi, gọi `CharacterFactory.Despawn(view)`: hủy movement, xóa runtime/controller, trả visual về pool, thông báo Released một lần rồi mới trả vỏ về pool. Chủ sở hữu bỏ tham chiếu để vỏ có thể đổi từ Enemy sang Player hoặc ngược lại. Khởi tạo lỗi cũng dọn phần trạng thái đang chuẩn bị dở.

Không gọi PoolManager.Despawn trực tiếp cho vỏ nhân vật. OnDisable chỉ hủy movement, không đổi parent hoặc thu hồi visual vì Unity đang tắt hierarchy. Tắt/bật tạm thời giữ nguyên runtime và hành vi; OnEnable đăng ký movement lại. Destroy trực tiếp để Unity hủy cả visual con, không thu hồi visual từ OnDestroy.

Vỏ/visual dùng Lean Pool. Runtime/controller được new khi spawn, không tạo trong Update. Chưa thêm pool class thuần; chỉ bổ sung khi Profiler cho thấy cấp phát khi spawn gây vấn đề. Các lệnh pool/movement hiện gọi trên main thread.

## Đồng hồ loại quái và wave

EnemySpawnProgress giữ mốc spawn thành công đầu tiên của từng CharacterType:

```text
bậc = floor((thời gian game hiện tại - mốc đầu của loại) / GrowthInterval)
bonus spawn = GrowthPerStep × bậc
chỉ số mới = chỉ số gốc + bonus spawn
HP ban đầu = maxHealth sau bonus
```

Spawn thêm cùng loại, hết quái sống, thu hồi vỏ và đổi wave không reset mốc. Quái đang sống giữ nguyên bonus đã nhận. Pause bằng timeScale = 0 dừng đồng hồ và không sinh thêm quái.

Wave nối tiếp theo thời lượng, chọn ngẫu nhiên đều giữa các loại được cấu hình. Mỗi wave có nhịp và trần riêng; quái còn từ wave trước vẫn tính vào trần. Wave cuối tiếp tục chạy đến hết lượt. LevelManager.StartNewRun() reset wave/đồng hồ và thu hồi Enemy của lượt trước.

## Kiểm tra và giới hạn

Editor/CharacterSystemTests.cs có kiểm tra HP độc lập, đồng hồ theo loại, bonus chốt khi spawn, chuyển wave, database Enemy không cần level vĩnh viễn và tái sử dụng vỏ giữa hai phe. Chạy Test Runner > EditMode; bài kiểm tra vỏ tự vào Play Mode để kiểm tra callback Unity thật.

Enemy chạm Player vẫn chỉ về pool như trước; chưa có sát thương va chạm, combat, game over hoặc điều kiện thắng wave. Skill trên SO vẫn là mẫu, chưa được khởi tạo/activate trong runtime. MovementManager tính bằng Jobs nhưng áp dụng Transform trên main thread, không thay thế bộ giải va chạm vật lý.

Khi kết thúc scene, CharacterFactory thu hồi nhân vật theo thứ tự visual → vỏ trước, sau đó PoolManager giải phóng pool tổng quát; buffer movement được giải phóng sau cùng. Không thu hồi toàn cục các pool ngoài scene.
