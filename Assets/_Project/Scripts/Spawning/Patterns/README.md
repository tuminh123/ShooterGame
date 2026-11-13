# Pattern, Formation và Path

Thư mục này chứa cấu hình, xử lý spawn và chuyển động của Pattern:

- EnemyPatternSO.cs: danh sách quái riêng và ba kiểu Formation, SequentialPath, Area.
- FormationSpawnSystem.cs: tạo nhóm, quản lý nhịp sinh và vòng đời các lượt Pattern.
- EnemyFormationRuntime.cs: thực thể nhóm sở hữu thành viên, slot và chuyển động chung.
- EnemyAreaBuilder.cs: sinh điểm trên đường viền rỗng cho Area.
- Formations/: hình học đội hình và thuật toán sinh slot.
- Paths/: dữ liệu waypoint và thuật toán lấy vị trí dọc đường.

Formation/SequentialPath sử dụng Path. Area không dùng Path, lấy player làm tâm
và thu nhỏ dần. Chỉ con chạm player nhận knockback qua EnemyController, không choáng.
Con chạy Path tách khỏi slot rồi tự đi nốt đường với tốc độ nhóm, không quay về hình.
Con Area vẫn thuộc viền và tiếp tục thu nhỏ sau knockback.
Hết Path hoặc thời gian Area thì thu hồi, không chuyển sang chase.

StageSpawnRoster sở hữu cả nhóm qua IStageSpawnEntity, giống cách nó sở hữu enemy/boss
đơn qua adapter. Nhóm tự đếm và thu hồi thành viên; roster không đăng ký lại từng con.
Formation dùng chung một tiến trình tâm và speed; SequentialPath có tiến trình riêng
theo thời điểm sinh từng con, không dùng slot hình. Nhóm vẫn cập nhật qua nhịp Stage.

Pattern dùng quái riêng, không trừ ngân sách Wave. Mọi nhân vật vẫn được tạo qua
EnemySpawnManager trong ../Core/. Bị giết vẫn dùng hệ thống EXP/vàng hiện có;
tự biến mất không tính kill và không sinh thưởng.

Editor nằm tại Assets/_Project/Editor/Spawning/Patterns/.
Asset nằm tại Assets/_Project/DataSO/Spawning/Patterns/, với các thư mục con
Formations/ và Paths/ cho hình và đường dùng lại.

Xem [kiến trúc spawn](../../../../../Docs/Architecture/Wave-Progression.md)
và [bố trí thư mục](../../../../../Docs/Architecture/Spawn-Folders.md).
