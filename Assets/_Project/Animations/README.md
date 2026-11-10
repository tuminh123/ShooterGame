# Animation theo nhân vật / đối tượng

Mỗi thư mục chứa các clip `.anim` và Animator Controller `.controller` riêng của một nhân vật hoặc đối tượng.

Ví dụ:

```text
Animations/
├── BlessedGladiator/
│   ├── BlessedGladiator.controller
│   └── BlessedGladiator_Clip.anim
├── ForagingMaggot/
│   ├── ForagingMaggot.controller
│   └── ForagingMaggot_Clip.anim
├── Ice VFX 1/
└── Ice VFX 2/
```

Đã tách 36 clip và 32 controller từ 32 file Aseprite hiện có.
Hai prefab nhân vật đang sử dụng đã được đổi sang controller trong thư mục này.
Khi tạo prefab nhân vật / đối tượng khác, gán controller tương ứng vào component Animator của prefab visual.

Các nhân vật hiện đều có prefab `TênNhânVật_Visual.prefab` trong `Prefabs/VisualCharacter`,
được gán sprite ban đầu và controller tương ứng.
Hai hiệu ứng băng có `IceVFX1_Visual.prefab` và `IceVFX2_Visual.prefab` trong `Prefabs/VisualEffect`.
Chúng có `EffectVisual` trên root để đáp ứng `IEffectVisual`. Animator tự phát state mặc định khi GameObject được bật; giữ `Keep Animator State On Disable` tắt để chạy lại từ đầu sau mỗi lần trả về pool.

Clip giữ các sprite keyframe, thời gian và cấu hình loop của clip nhập từ Aseprite.
Controller chứa state machine/state riêng và tham chiếu các clip mới.
Sprite vẫn tham chiếu file Aseprite gốc trong `Graphic`; không xóa các file nguồn đó.

Các asset ở đây là bản tách có thể chỉnh sửa độc lập. Thay đổi animation trong Aseprite sẽ không tự ghi đè clip/controller đã tách.
