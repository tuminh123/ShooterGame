# Play_UI_ChoiceSkill và lựa chọn khi lên cấp

## Scene đã khảo sát

```text
Canvas
├── Play_UI_Action                 ← SkillChoiceUI luôn hoạt động để nghe event
└── Play_UI_ChoiceSkill             ← modal, mặc định tắt
    ├── Dimed                      ← chặn click xuống gameplay
    ├── Group_SkillFrmae02          ← phần trang trí/slot có sẵn, không đổi trong đợt này
    └── PanelChooseSkill
        ├── Title_Line02/Text_Title
        └── Selects
            ├── BannerFrame04_Divided
            ├── BannerFrame04_Divided
            └── BannerFrame04_Divided
```

Mỗi banner có một icon `SkillFrame_*/Bg/SkillIcon`, `Text_Title`, `Text_Info` và `Group_GreadGems`. Group sao có hai hàng `Off` và `On`, mỗi hàng chứa năm Gem. Ban đầu scene chỉ là UI mẫu, root modal đang tắt và ba banner chưa có Button; nội dung mẫu chưa liên quan đến skill gameplay.

Đã thêm Button vào từng banner và gán trực tiếp các reference trong scene cho `SkillChoiceUI`. Script được gắn trên `Play_UI_Action`, vì nếu đặt listener trên modal đang tắt thì không nhận được sự kiện lên cấp. Không tìm object bằng tên trong mỗi frame.

## Luật lựa chọn

- Nghe `LevelManager.RunLevelChanged(previous, current)`. Tăng N cấp thì xếp N lượt chọn, không bỏ mất lượt khi nhặt nhiều EXP cùng lúc.
- Mỗi lượt lấy ngẫu nhiên ba skill khác nhau trong catalog serialized của UI, gồm các skill Player. EnemyNormalAttack và các skill boss không nằm trong catalog mặc định.
- Không sở hữu: chỉ offer **cấp 1**, một sao sáng.
- Đang cấp L: chỉ offer **L+1**, tuyệt đối không nhảy từ cấp 1 lên offer cấp 3.
- Chỉ chống trùng trong một bảng. Không có blacklist xuyên lượt: lần lên cấp sau có thể gặp lại cùng skill với cấp kế tiếp.
- Script chọn thông qua `SkillManager.GrantOrUpgrade`, nên skill riêng và skill nhận trong trận được nâng trên đúng runtime đang có, không nhân đôi hành vi hoặc bonus.
- Skill có `IRunLevelSkillData` hỗ trợ nâng cấp theo runtime. Các behavior đơn cấp như hồi máu, shield khẩn cấp và Toughness có `MaxRunLevel = 1` dù trường MaxLevel cũ trong asset là 5. Skill đơn cấp đã có không tạo lựa chọn không tác dụng; chưa có luật nhận lặp để cộng bonus.
- UI hiện chặn tối đa **5 cấp**, kể cả HolyBolt có cấu hình cấp 6. Tiến hóa cấp 6 chưa xuất hiện trong bảng này.
- Nếu catalog còn ít hơn ba skill hợp lệ thì hiển thị số lựa chọn còn lại; không nhân bản một skill trong cùng bảng. Nếu hết lựa chọn thì đóng bảng và tiếp tục game.

## Sao, icon và mô tả

- `Off` bật đúng số ô của `min(MaxRunLevel, 5)`.
- `On` bật đúng số sao của **cấp sẽ nhận sau khi chọn**. Offer cấp 2 có hai sao sáng; phần mô tả ghi rõ `Lv. 1 > 2`.
- Không clone cả `Group_GreadGems`; dùng năm Gem có sẵn ở mỗi hàng.
- Icon và tên lấy từ `SkillDataSO.Icon`/`DisplayName`. Đã gán icon từ bộ asset UI hiện có cho chín skill trong catalog.
- `SkillDataSO.runLevelDescriptions` cho phép nhập mô tả riêng từng cấp; index 0 là cấp 1. Nếu thiếu thì dùng `Description` chung.
- FireBolt/IceBolt/PoisonBolt trước đó chưa có bảng tăng cấp; đã thêm mốc 2–5 tăng số viên để lựa chọn nâng cấp có hiệu lực. Movement và effect gốc của từng skill giữ nguyên.
- Nội dung UI dùng tiếng Anh nhất quán với scene và font mẫu hiện tại.

## Pause và vòng đời

`GameplayPauseService` giữ token cho từng nguồn pause. Cả `PauseUI` và bảng skill cùng dùng service; đóng bảng không bỏ pause thủ công, và thả token cuối sẽ phục hồi timeScale trước khi pause.

UI xếp lượt ngay khi có event, nhưng render bảng trong LateUpdate. Chọn xong mới roll lại lượt tiếp theo từ runtime mới nhất. Offer cũ bị từ chối nếu skill đã đổi cấp qua Editor/callback. Chặn double click cùng frame và có khoảng đợi ngắn bằng thời gian unscaled.

Player chết, bị thay/thu hồi, reset trận hoặc UI bị tắt đều dọn bảng, hàng đợi và token pause. Các nút chỉ điều hướng keyboard/controller trong nhóm lựa chọn, không chọn nhầm nút Pause ở dưới modal.

## Thử trong Editor

1. Mở SampleScene, vào Play Mode.
2. Chọn menu **Shooter Game > Skills > Grant One Run Level**, hoặc nhặt đủ EXP.
3. Bảng mở ba lựa chọn; chọn một banner để nhận/nâng skill và tiếp tục game.
4. Lặp lại để kiểm tra skill cũ chỉ xuất hiện với cấp kế tiếp. Có thể cấp nhiều EXP để kiểm tra hàng đợi nhiều lượt.

Chạy `./Tests/SkillProgression/Run.ps1` để kiểm tra policy chọn skill, queue, offer cũ, trần sao và nhiều nguồn pause cùng các kiểm tra gameplay đã có. Harness dùng test doubles cho Unity native; không thay cho kiểm tra click/render trong Play Mode.
