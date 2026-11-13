# Báo cáo cấu trúc spawn cuối cùng

## Cấu trúc đã chốt

```text
StageDataSO
  List<ScriptableObject> spawnData
    WaveConfigSO     : IStageSpawnData
    EnemyPatternSO   : IStageSpawnData
    BossSpawnDataSO  : IStageSpawnData

StageManager
  Dictionary<Type, IStageSpawnSystem>
    WaveSpawnSystem
    FormationSpawnSystem
    BossSpawnSystem
```

Stage giữ chính asset, không bọc qua entry, không có danh sách wave/Timing/Delay
hoặc Duration spawn riêng. WaveConfigSO quản lý lại toàn bộ chuỗi wave như trước.
Các hệ thống dùng chung EnemySpawnManager để tạo/thu hồi nhân vật.

## File đã sửa trong lần hoàn thiện này

- Assets/_Project/Scripts/Spawning/Stages/StageDataSO.cs: một danh sách asset trực tiếp;
  cung cấp dữ liệu qua IStageSpawnData và kiểm tra interface.
- Assets/_Project/Scripts/Spawning/Stages/StageManager.cs: registry duyệt danh sách phẳng;
  bỏ context và mọi xử lý wave cụ thể; giữ điều phối vòng đời/kết quả/thưởng.
- Assets/_Project/Scripts/Spawning/Core/IStageSpawnData.cs:
  factory nhận DiContainer hiện có để tạo hệ thống bằng constructor injection.
- Assets/_Project/Scripts/Spawning/Core/IStageSpawnSystem.cs:
  bỏ Initialize(context); giữ Validate/Execute/Tick/LateTick/Stop và trạng thái.
- Assets/_Project/Scripts/Spawning/Waves/WaveSpawnSystem.cs:
  tự quản lý toàn bộ chuỗi wave, quota, nhịp và điều kiện chuyển của quái thường.
- Assets/_Project/Scripts/Spawning/Patterns/FormationSpawnSystem.cs:
  nhận trực tiếp spawner/player/spatial qua constructor.
- Assets/_Project/Scripts/Spawning/Bosses/BossSpawnSystem.cs:
  nhận spawner qua constructor, dùng cấp trong BossSpawnDataSO.
- Assets/_Project/Scripts/Spawning/Bosses/BossSpawnDataSO.cs:
  factory dùng constructor injection; bỏ cấp phụ thuộc context.
- Assets/_Project/Scripts/Spawning/Patterns/EnemyPatternSO.cs:
  factory dùng constructor injection, giữ dữ liệu ba kiểu Pattern.
- Assets/_Project/Editor/Spawning/Stages/StageDataEditor.cs:
  hiển thị danh sách asset trực tiếp, tự kiểm tra interface, không dùng attribute/drawer.
- Assets/_Project/DataSO/Spawning/Stages/Stage_01.asset:
  trực tiếp chứa Default_Waves và Stage_01_Formation; giữ ID/map/rewards.
- Assets/_Project/DataSO/Spawning/Waves/Default_Waves.asset:
  khôi phục danh sách wave đầy đủ thay cho cấu hình một lượt đơn.
- Docs/Architecture/Wave-Progression.md: cập nhật theo cấu trúc cuối cùng.

## File được khôi phục

- Assets/_Project/Scripts/Spawning/Waves/WaveConfigSO.cs:
  bộ wave đầy đủ, triển khai IStageSpawnData.
- Assets/_Project/Editor/Spawning/Waves/WaveConfigEditor.cs:
  Inspector danh sách wave và cách chuyển wave.

GUID gốc của hai script được giữ lại. Không thêm script mới ngoài việc khôi phục
hai file này.

## File đã xóa trong lần hoàn thiện này

Cùng các .meta đi kèm:

- Assets/_Project/Scripts/Core/Stages/Spawning/StageSpawnContext.cs
- Assets/_Project/Scripts/Core/Stages/Spawning/StageSpawnEntry.cs
- Assets/_Project/Scripts/Core/Stages/Spawning/StageSpawnAssetAttribute.cs
- Assets/_Project/Editor/StageSpawnAssetDrawer.cs
- Assets/_Project/Scripts/Core/Stages/Spawning/WaveSpawnDataSO.cs
- Assets/_Project/Editor/WaveSpawnDataEditor.cs

Không tạo class thay thế cho context/entry/attribute. Dependency được nhận trực
tiếp qua constructor; interface được kiểm tra ngay trong StageDataSO/StageDataEditor.

## Các file đã bỏ ở các bước trước

- EnemyPatternManager.cs: thay bằng FormationSpawnSystem.
- WaveProgress.cs: tiến trình thuộc từng run trong WaveSpawnSystem.
- EnemyFormationSpawn.cs: Formation không còn dùng ngân sách Wave.
- EnemyPatternRegressionChecks.cs: phụ thuộc API cũ, đã bỏ theo phạm vi không chạy test.

## Các phần trước đó được giữ

EnemySpawnManager, EnemyFormationRuntime, EnemyController, CharacterView,
EnemyFormationSO, EnemyAreaBuilder, EnemyPathBuilder và các editor Pattern/Workbench
giữ các thay đổi liên quan đến spawn độc lập và va chạm đã thống nhất trước đó.

StageSpawnRoster vẫn cần để theo dõi đúng nhân vật do từng hệ thống tạo, tránh đếm
hoặc thu hồi nhầm lượt pool. Nó không chứa dependency hoặc thay thế context.

## Bàn giao

Không chạy test/build, không yêu cầu compile, không Play hoặc điều khiển Editor.
Chỉ rà code và tham chiếu. Người dùng tự kiểm thử.

Backup cấu hình trước lần sửa này: Temp/StageSpawnFlatListMigration/.
