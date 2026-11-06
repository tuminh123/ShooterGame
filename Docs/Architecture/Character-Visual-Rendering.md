# Visual cố định trên CharacterView và prefab visual riêng

Ngày ghi nhận: 2026-11-06.

> Đây là phương án thiết kế để cân nhắc chuyển đổi. Code hiện tại vẫn lấy prefab
> visual từ pool và gắn làm con CharacterView; tài liệu này không triển khai thay đổi đó.

## Hiện trạng

- `Character.prefab` là vỏ gameplay chung cho player, enemy và boss.
- `CharacterVisualSettings.Prefab` trỏ tới prefab visual của từng loại nhân vật.
- Khi spawn, `CharacterView` lấy visual từ `PoolManager`, gắn vào vỏ, áp dụng
  scale/offset và giữ danh sách SpriteRenderer/Animator của visual.
- Khi thu hồi, visual và vỏ được trả về các pool tương ứng.
- `PoolPrewarmSettings` hiện tạo sẵn visual theo từng loại nhân vật trong database.
  Nhiều loại dùng chung prefab thì dùng chung pool và cộng số lượng chuẩn bị.

Như vậy visual hiện đã là **con của CharacterView**. Điểm thay đổi được đề xuất
là giữ sẵn renderer/animator trên vỏ thay vì lấy một GameObject visual riêng mỗi lần spawn.

## Phương án cho nhân vật có cấu trúc đơn giản

```text
CharacterView                         ← vỏ gameplay được pool
├── VisualRoot                        ← giữ sẵn trên vỏ
│   ├── SpriteRenderer
│   └── Animator
└── Character Health Canvas           ← không nằm dưới nhánh scale visual
    └── Slider_Player + Shield Border
```

Khi spawn, data cung cấp sprite ban đầu, animation controller, scale và offset.
`CharacterView` nạp dữ liệu vào các component đã tồn tại. Mỗi nhân vật vẫn có
Animator riêng; chỉ asset sprite/controller được dùng chung.

Một animation kiểu đổi sprite vẫn chạy bình thường khi các clip điều khiển
`SpriteRenderer.sprite` trên đúng object/path. Không cần tạo GameObject mới chỉ
để đổi bộ animation.

## Cấu hình đề xuất

Tiếp tục gom thông số trong `CharacterVisualSettings`, có thể bổ sung:

| Thông số đề xuất | Vai trò |
| --- | --- |
| VisualMode | Chọn renderer cố định hoặc prefab visual riêng. |
| InitialSprite | Sprite tĩnh hoặc hình khởi đầu trước khi Animator cập nhật. |
| AnimationController | RuntimeAnimatorController cho Animator của cá thể. |
| Prefab | Chỉ dùng khi chọn chế độ prefab riêng; giữ để chuyển đổi dữ liệu cũ. |
| Scale / Offset | Transform của nhánh hiển thị, không scale toàn bộ vỏ. |
| Hitbox | Vùng trúng đòn riêng theo loại nhân vật. |
| HealthBarSize / Offset / Scale | Bố cục thanh máu riêng theo loại. |
| ShieldThickness | Độ dày và phần nở của shader giáp. |

Các field sprite/controller/mode ở bảng là **đề xuất**, chưa có trong struct hiện tại.
Asset không giữ HP, trạng thái Animator hay dữ liệu runtime của cá thể.

Nếu các nhân vật có cùng state machine và parameter, có thể dùng
`AnimatorOverrideController` để thay clip theo nhân vật. Nếu khác hành vi animation,
có thể dùng controller khác nhưng vẫn cần thống nhất contract với code: tên
parameter, layer và các thao tác reset. Không tự tạo override controller mới cho mỗi lần spawn.

## Trách nhiệm của các phần

- `CharacterFactory`: lấy vỏ và runtime, chọn controller gameplay, khởi tạo trước khi bật vỏ.
- `CharacterView`: cấu hình visual, reset trạng thái hiển thị và chuyển tiếp vòng đời.
- `CharacterUI`: áp dụng bố cục và cập nhật HP/giáp.
- `CharacterVisualSettings`: dữ liệu theo loại nhân vật để runtime và công cụ Editor cùng sử dụng.
- `PoolPrewarmService`: tạo sẵn đủ vỏ; chỉ chuẩn bị prefab visual riêng khi mode đó thực sự được dùng.

Controller gameplay và animation controller là hai khái niệm khác nhau:
`PlayerController`/`EnemyController` xử lý hành vi; `RuntimeAnimatorController` là asset điều khiển Animator.

## Reset khi lấy vỏ từ pool

Một vỏ có thể đổi từ enemy A sang enemy B hoặc player. Trước khi hiển thị cần:

1. Chọn đúng mode và tắt nhánh visual không sử dụng để tránh vẽ hai hình cùng lúc.
2. Gán sprite/controller và đưa màu, flip, sorting, enabled về giá trị đã cấu hình.
3. Áp dụng lại local position, rotation và scale; không giữ transform của loại trước.
4. Reset trạng thái animation sau khi Animator có controller hợp lệ và object đã hoạt động.
   Không gọi cập nhật Animator trên nhánh đang tắt chỉ để ép lấy frame đầu.
5. Dọn visual/VFX riêng của lượt trước và các tham chiếu cá thể cũ.

Không đổi animation controller hoặc gọi Rebind mỗi frame. Nếu dùng lại cùng
controller, vẫn cần đảm bảo state/parameter phù hợp cho lượt spawn mới.

## Boss và trường hợp dùng prefab riêng

Boss chỉ dùng một SpriteRenderer/Animator có thể sử dụng renderer cố định như enemy.
Kích thước lớn không phải lý do bắt buộc tạo prefab visual riêng: scale, hitbox
và thanh máu đã có cấu hình riêng.

Giữ prefab riêng khi visual có nhiều renderer, nhiều phần cơ thể, skeleton,
vũ khí con, điểm phát VFX hoặc cấu trúc khác hẳn. Khi dùng chế độ này, lấy prefab
đó từ pool như hiện tại và tắt renderer cố định trên vỏ.

Nếu toàn bộ nhân vật trong game cùng một cấu trúc, có thể chỉ triển khai mode
renderer cố định trước. Không bắt buộc thêm hai mode khi chưa có nhân vật cần ngoại lệ.

## Lợi ích và đánh đổi

- Giảm GameObject visual phải chuẩn bị theo từng loại enemy.
- Giảm thao tác lấy/trả visual và đổi parent khi spawn/despawn.
- Giữ sẵn tham chiếu renderer/animator trên vỏ thay vì tìm component lại theo visual.
- Không đảm bảo giảm draw call: số renderer đang hiển thị vẫn phụ thuộc số nhân vật.
- Đổi controller, khởi tạo Animator, tải texture và shader vẫn có chi phí riêng.
- Một pool vỏ dùng chung không giữ sẵn mọi bộ animation; cần đo lần đổi controller đầu.
- Số GameObject giảm không đồng nghĩa chắc chắn tăng FPS. Cần đo startup, bộ nhớ,
  GC và thời gian spawn trên thiết bị mục tiêu khi thực sự triển khai.

## Kế hoạch chuyển đổi khi thực hiện

1. Thêm VisualRoot/SpriteRenderer/Animator vào vỏ và cấu hình dữ liệu mới.
2. Chuyển dữ liệu từ từng prefab cũ: sprite, controller và transform; không bỏ asset cũ trước khi chuyển xong.
3. Sửa luồng Initialize/ClearSpawn để quản lý mode và reset đúng trạng thái.
4. Sửa prewarm: mode cố định dùng component có sẵn trong vỏ, mode prefab mới chuẩn bị pool visual riêng.
5. Cập nhật preview Editor để xem đúng mode, hitbox và thanh máu.
6. Kiểm tra tái sử dụng vỏ giữa các loại, pause, chết, thu hồi và animation frame đầu.

Đọc thêm: [tổng quan hệ thống](Systems-Overview.md), [pool prewarm](Pool-Prewarm.md).
