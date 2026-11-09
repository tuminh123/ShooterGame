# Bảng chọn skill khi lên cấp

## Scene và trách nhiệm

```text
Canvas                               <- UIManager
├── GameplayUI                       <- GameplayController : UIPanelController
│   └── Play_UI_Action               <- panel HUD chứa RunUI và PauseUI
└── LevelUpUI                        <- SkillChoiceController : UIPanelController
    └── LevelUpPanel                 <- SkillChoiceUI, mặc định tắt
        └── ba thẻ lựa chọn          <- icon, tên, mô tả, sao và Button
```

`GameManager` là class thuần AsSingle trong GameSceneInstaller, sống cùng scene hiện tại.
Nó giữ GameState và điều phối Save sẵn sàng → prewarm → reset → spawn → Playing.
Player chết chuyển Lost; WinRun do điều kiện gameplay gọi. StartRun chơi lại,
ReturnHome dọn Player/Enemy/đạn cũ và reset tiến trình. Không có loading scene riêng.

`GameUIEventBus` static chỉ thông báo StateChanged cho UI, không giữ state và không
được manager gameplay dùng để thay thế thứ tự chuẩn bị. Static listener được reset
ở SubsystemRegistration và từng UI tự gỡ trong OnDisable.

`UIManager` giữ mảng panels chung và mapping statePanels từ GameState sang UI gốc.
Không còn defaultPanel hoặc ShowDefaultPanel. SetRootPanel chọn gốc lúc chạy,
OpenPanel mở tạm, ClosePanel/Back quay về UI trước và không đóng gốc hiện tại.
State không có route đóng UI cũ, không tự quay về Gameplay. SetVisible chỉ tạm
ẩn/hiện, không quyết định state game. Khi bật lại, UI đọc GameManager.State.

Scene hiện map Playing sang GameplayController; Faulted cũng hiện HUD để thao tác
reset dữ liệu/debug. Chưa có panel Home/Won/Lost nên các state đó chưa có màn riêng.
Auto Start Run trên installer đang bật để giữ hành vi tự vào trận. Tắt cờ này để
đợi lệnh StartRun từ Home UI sau này. Các menu Shooter Game > Game Flow cho phép
thử Start Run, Win Run, Lose Run và Return Home trong Play Mode.

`SkillChoiceController : UIPanelController` được Zenject inject LevelManager,
PlayerManager, SkillManager, GameplayPauseService và SkillDatabaseSO. Controller quản lý
queue, owner, pause, roll, retry và áp dụng lựa chọn; luôn hoạt động trên root LevelUpUI, là cha của panel con.
`GameplayController` nằm trên root GameplayUI và tự quản lý ẩn/hiện HUD cùng pause thủ công. UIManager không còn giữ GameObject HUD hay PauseUI trực tiếp.
`SkillChoiceUI` chỉ render, giới hạn input/focus và phát Selected/Hidden.
Panel tắt không làm mất listener lên cấp của controller.

`SkillDatabase.asset` chỉ chứa skill Gameplay; HolyBolt của nhân vật và skill enemy/boss nằm ngoài catalog. Chỉ entry có
Available In Run Choices được đưa vào catalog thưởng; thêm skill mới tại đây.

## Lựa chọn và vòng đời

- Tăng N cấp giữ N lượt. Mỗi lần roll tối đa ba skill khác nhau, chỉ offer cấp kế tiếp.
- Trần bảng thưởng là năm sao. Sao sáng biểu diễn cấp sẽ nhận sau khi chọn.
- Skill đơn cấp đã sở hữu không được offer lại. HolyBolt của nhân vật không xuất hiện trong bảng thưởng.
- Controller yêu cầu mở ngay khi lên cấp; UIManager cấp quyền hiển thị và controller giữ pause. Render ở LateUpdate sau callback gameplay.
- Chặn double click, offer cũ, input cùng frame và input trong 0.15 giây unscaled đầu.
- Chọn thành công mới tiêu một lượt; lượt tiếp theo roll từ runtime mới nhất.
- SetVisible(false) ẩn cả HUD và panel, trả token pause nhưng giữ queue.
- SetVisible(true) mở lại các lượt đang chờ. Panel bị tắt trực tiếp cũng trả pause;
  gọi RetrySkillChoices hoặc SetVisible(true) để mở lại.
- Chết, release, đổi Player, reset trận hoặc tắt controller hủy queue và dọn subscription/pause.
- Pause thủ công giữ token riêng, đóng bảng không tự bỏ pause thủ công.

## Phục hồi lỗi

Nếu nhận/nâng skill ném exception, SkillChoiceController ghi log, không tiêu lượt và chặn
skill lỗi khỏi các lần roll sau trong trận. Offer lỗi thời trả false chỉ được
roll lại, không tự đánh dấu skill hỏng.

SkillRuntime.TryUpgrade thử phục hồi behavior về RunLevel trước đó khi lỗi.
Nếu rollback cũng thất bại, runtime được dispose để không tick tiếp hoặc giữ
stat lease. Exception tổng hợp giữ thông tin lỗi nâng, rollback và cleanup.
Callback reset/dispose trong lúc nâng được ưu tiên, không rollback đè lên reset.

Nhận skill mới thất bại dọn behavior được tạo; lỗi cleanup được giữ cùng lỗi
gốc. Constructor runtime hủy cooldown và AddTemporaryBonus hoàn tác bonus
nếu callback chỉ số ném lỗi.

Nếu không còn lựa chọn vì skill lỗi, đóng bảng và trả pause nhưng giữ lượt.
Sau khi sửa nguyên nhân, gọi SkillChoiceController.RetrySkillChoices() để bỏ chặn và roll lại.
Không tự retry mỗi frame. Nếu catalog chỉ hết vì tất cả skill đã max, bỏ lượt
còn lại và tiếp tục game như chính sách trước đây.

## Kiểm tra

Chạy `powershell -NoProfile -ExecutionPolicy Bypass -File Tests/SkillProgression/Run.ps1`.
Harness chạy code UIManager, SkillChoiceController và skill production với panel/Unity native doubles;
kiểm tra queue, loại skill lỗi, retry, rollback, stat/timer cleanup, ẩn/hiện và reset.
Vẫn cần Play Mode để xác minh render, focus và click thực tế trong scene.
## Folder và quyền sở hữu

- `UI/Core`: UIManager và hợp đồng UIPanelController.
- `UI/Gameplay`: GameplayController, RunUI, PauseUI.
- `UI/SkillChoice`: SkillChoiceController, SkillChoiceUI, SkillChoicePool và queue/offer policy.
- `UI/Views`: các view dùng chung BarView, TextView, ShieldBorderGraphic.
- `UI/Character`: CharacterUI đi theo vòng đời nhân vật.
- `UI/Debug`: ResetSaveButton dành cho thao tác thử nghiệm/reset.

Chỉ ẩn panel con; root controller vẫn hoạt động để nhận sự kiện và giữ trạng thái.
Di chuyển script cùng file .meta để mọi reference trong scene/prefab giữ nguyên GUID.
## Slot skill đã sở hữu trong LevelUpPanel

SkillChoiceController lấy snapshot innate + run của đúng Player từ SkillManager,
SkillChoiceUI quản lý các instance đang dùng, còn OwnedSkillSlot chỉ hiển thị icon/sao.
Prefab: Assets/_Project/Prefabs/UI/OwnedSkillSlot.prefab. Kéo component prefab vào
Owned Slot Template của SkillChoiceUI; reference icon, Gems Off và Gems On nằm sẵn trên prefab.

Group_SkillFrmae02 giữ GridLayoutGroup gốc và bắt đầu không có slot con. Không có
initialOwnedSlots hoặc prewarm: chỉ gọi PoolManager.Spawn khi snapshot có skill và
số ô đang dùng chưa đủ. Pool tự tạo thêm clone nếu chưa có instance rảnh.

Nâng cấp cập nhật nội dung trên ô đang dùng. Khi số skill giảm, trả ô dư về pool;
Hide, OnDisable và OnDestroy cũng thu hồi toàn bộ ô. Slot.Clear xóa icon/sao trước
khi Despawn để lần dùng lại không giữ hình ảnh cũ. Pool chung giữ các clone rảnh.

Skill một cấp hiện một sao; sao nền theo MaxRunLevel, sao sáng theo RunLevel thực tế.
Không giới hạn 12 ô, không thêm ScrollRect/mask/content hoặc tạo một pool riêng.
