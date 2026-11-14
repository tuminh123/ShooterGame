# Hình học Formation và cách spawn

## Hai cấu hình độc lập

- `EnemyFormationSO` giữ hình học: `fillMode`, `shape`, `level`, `size`,
  `rotation` và cấu hình Custom. Số quái tự tính từ Shape và Level.
- `EnemyPatternSO` giữ cách spawn: cả khối theo Path, từng thành viên theo
  Path, hoặc Area tại Player. Pattern tham chiếu một hình học, không tự
  quyết định hình đặc/rỗng hoặc giữ quota quái thứ hai.
- `Solid` giữ các hình và quy tắc Level cũ.
- `Hollow` chỉ hỗ trợ tròn/elip, vuông/chữ nhật, tam giác, thoi và lục giác.
  Hàng ngang/dọc và Custom không xuất hiện trong dropdown Rỗng.
- Số slot Rỗng = số điểm tối thiểu của Shape × `2^(level - 1)`.
  Tròn/tam giác có 3 điểm ở Level 1, vuông/thoi có 4, lục giác có 6.
  Không có trường số quái gốc để nhập tay. Mỗi cấp chỉ tăng mật độ
  trên cùng một viền; kích thước/bán kính và số lớp không thay đổi.

## Luồng runtime

```text
StageDataSO.spawnData
  -> EnemyPatternSO : IStageSpawnData
  -> FormationSpawnSystem : IStageSpawnSystem
  -> EnemyPatternSO.RuntimeData (snapshot)
  -> EnemyFormationRuntime
  -> EnemyController / EnemySpawnManager / MovementManager
```

`IStageSpawnSystem` vẫn giữ `Validate`, `Execute`, `Stop`. Các interface
`IUpdateStage`, `ILateUpdateStage`, `IStageSpawnStatus` tiếp tục phục vụ tick,
late tick và điều kiện hoàn tất Stage. Cả ba cách spawn dùng cùng một
`FormationSpawnSystem`, không tạo thêm hệ thống Stage riêng.

`Execute` chốt slot hình học, quota, loại spawn, roster, cấp, tốc độ,
knockback, nhịp spawn, nhịp thu Area và các điểm Path local. Khi đến mốc
spawn, runtime mới lấy vị trí camera/Player thực tế. Vì quota và slot lấy
từ cùng snapshot, thay đổi asset trong lúc chờ không làm lệch số thành viên.

## Ba cách spawn

| Mode | Sinh và chuyển động |
| --- | --- |
| Cả khối theo Path | Sinh mọi slot tại A + offset; cả nhóm dùng chung tiến trình Path. |
| Từng thành viên theo Path | Sinh một slot mỗi `spawnInterval`; mỗi thành viên có tuổi/quãng đường riêng trên Path + offset của slot. |
| Area tại Player | Sinh toàn bộ slot tại Player + offset; hình bám Player và thu theo Continuous/Burst. |

Area có thể dùng hình Solid hoặc Hollow. Thu liên tục/theo đợt là tham số
hành vi Area, không phải loại hình học. Cả hai mode Path vẫn giữ đường A/B,
camera-relative center, tốc độ, số lượt đi và xử lý knockback hiện có.

`EnemyAreaBuilder` được tái sử dụng làm helper dựng viền Hollow cho mọi cách
spawn; tên lớp cũ được giữ, nhưng runtime Area không trực tiếp gọi nó nữa.

## Editor và chuyển dữ liệu

- Inspector và Workbench dùng chung bộ lọc Shape theo Solid/Hollow.
- Preview dựng slot bằng `FormationBuilder` cho cả ba cách spawn.
- Preview tuần tự dùng cùng offset và thời gian sinh của từng slot như runtime.
- Đã bỏ trường `enemyCount` khỏi Pattern; quota lấy từ hình học.
- Các Area cũ được chuyển sang Hollow. Số quái hiện tại tuân theo Shape và
  Level, không giữ quota độc lập 20/18 trước đây. Thay đổi Level để chọn mật độ.
- Example_SequentialPath dùng hình học đang gán là Default_Waves_Wave_01_Formation:
  quota hiện là 19 slot của hình, thay cho quota độc lập 12 trước đây.
- Giá trị số của enum cách spawn và Shape được giữ để asset cũ không đổi ID.

## Script thay đổi trong lần sửa này

1. `EnemyFormationSO.cs`: Solid/Hollow, mật độ viền, validation hình đóng.
2. `FormationBuilder.cs`: chọn builder theo mode hình học, thống nhất slot count.
3. `EnemyAreaBuilder.cs`: làm rõ vai trò helper hình học viền.
4. `EnemyPatternSO.cs`: chỉ giữ cách spawn, dùng quota hình học và snapshot runtime.
5. `EnemyFormationRuntime.cs`: cùng bộ slot cho ba mode, giữ chuyển động Path/Area.
6. `FormationSpawnSystem.cs`: tạo snapshot trong Execute, spawn đúng quota snapshot.
7. `DataInspectorGUI.cs`: dropdown Shape lọc đúng mode, dùng chung giữa editor.
8. `ProjectDataSOEditor.cs`: Inspector hình học Solid/Hollow và Level mật độ.
9. `EnemyPatternEditor.cs`: tách hình học khỏi cách spawn trong Inspector.
10. `EnemyFormationWorkbench.cs`: preview đồng nhất runtime và bộ lọc Shape.

Không thêm script mới; không đổi contract `IStageSpawnSystem` hay registry Stage.
