# Bố trí thư mục hệ thống spawn

Chỉ di chuyển file, đổi tên thư mục và cập nhật tài liệu. Không đổi tên class,
namespace, logic, cấu hình gameplay hoặc GUID của script/asset. Không chạy test,
build, yêu cầu compile hay thao tác Play trong Editor.

## Cấu trúc mới

```text
Assets/_Project/
  Scripts/Spawning/
    Core/                     # Interface, roster, EnemySpawnManager
    Stages/                   # StageDataSO, StageManager, StageProgressService
    Waves/                    # WaveConfigSO, WaveSpawnSystem
    Patterns/                 # Pattern data, runtime, spawn system, Area builder
      Formations/             # Hình học đội hình
      Paths/                  # Dữ liệu và thuật toán Path
    Bosses/                   # BossSpawnDataSO, BossSpawnSystem
  DataSO/Spawning/
    Stages/
    Waves/
    Patterns/
      Formations/
      Paths/
  Editor/Spawning/
    Common/
    Stages/
    Waves/
    Patterns/
```

EnemyController và EnemyVisualManager vẫn ở Scripts/Characters/Enemies vì chúng
xử lý hành vi/hiển thị nhân vật, không phải điều phối spawn. Không tạo thư mục data
Bosses rỗng khi chưa có asset boss spawn.

## Các file đã di chuyển

| Đường dẫn cũ | Đường dẫn mới |
|---|---|
| Assets/_Project/Scripts/Core/Stages/StageDataSO.cs | Assets/_Project/Scripts/Spawning/Stages/StageDataSO.cs |
| Assets/_Project/Scripts/Core/Stages/StageManager.cs | Assets/_Project/Scripts/Spawning/Stages/StageManager.cs |
| Assets/_Project/Scripts/Core/Stages/StageProgressService.cs | Assets/_Project/Scripts/Spawning/Stages/StageProgressService.cs |
| Assets/_Project/Scripts/Core/Stages/Spawning/IStageSpawnData.cs | Assets/_Project/Scripts/Spawning/Core/IStageSpawnData.cs |
| Assets/_Project/Scripts/Core/Stages/Spawning/IStageSpawnSystem.cs | Assets/_Project/Scripts/Spawning/Core/IStageSpawnSystem.cs |
| Assets/_Project/Scripts/Core/Stages/Spawning/StageSpawnRoster.cs | Assets/_Project/Scripts/Spawning/Core/StageSpawnRoster.cs |
| Assets/_Project/Scripts/Characters/Enemies/EnemySpawnManager.cs | Assets/_Project/Scripts/Spawning/Core/EnemySpawnManager.cs |
| Assets/_Project/Scripts/Characters/Enemies/WaveConfigSO.cs | Assets/_Project/Scripts/Spawning/Waves/WaveConfigSO.cs |
| Assets/_Project/Scripts/Core/Stages/Spawning/WaveSpawnSystem.cs | Assets/_Project/Scripts/Spawning/Waves/WaveSpawnSystem.cs |
| Assets/_Project/Scripts/Core/Stages/Spawning/BossSpawnDataSO.cs | Assets/_Project/Scripts/Spawning/Bosses/BossSpawnDataSO.cs |
| Assets/_Project/Scripts/Core/Stages/Spawning/BossSpawnSystem.cs | Assets/_Project/Scripts/Spawning/Bosses/BossSpawnSystem.cs |
| Assets/_Project/Scripts/Core/Stages/Spawning/FormationSpawnSystem.cs | Assets/_Project/Scripts/Spawning/Patterns/FormationSpawnSystem.cs |
| Assets/_Project/Scripts/Characters/Enemies/Formations/EnemyPatternSO.cs | Assets/_Project/Scripts/Spawning/Patterns/EnemyPatternSO.cs |
| Assets/_Project/Scripts/Characters/Enemies/Formations/EnemyFormationRuntime.cs | Assets/_Project/Scripts/Spawning/Patterns/EnemyFormationRuntime.cs |
| Assets/_Project/Scripts/Characters/Enemies/Formations/EnemyAreaBuilder.cs | Assets/_Project/Scripts/Spawning/Patterns/EnemyAreaBuilder.cs |
| Assets/_Project/Scripts/Characters/Enemies/Formations/README.md | Assets/_Project/Scripts/Spawning/Patterns/README.md |
| Assets/_Project/Scripts/Characters/Enemies/Formations/EnemyFormationSO.cs | Assets/_Project/Scripts/Spawning/Patterns/Formations/EnemyFormationSO.cs |
| Assets/_Project/Scripts/Characters/Enemies/Formations/FormationBuilder.cs | Assets/_Project/Scripts/Spawning/Patterns/Formations/FormationBuilder.cs |
| Assets/_Project/Scripts/Characters/Enemies/Paths/EnemyPathSO.cs | Assets/_Project/Scripts/Spawning/Patterns/Paths/EnemyPathSO.cs |
| Assets/_Project/Scripts/Characters/Enemies/Paths/EnemyPathBuilder.cs | Assets/_Project/Scripts/Spawning/Patterns/Paths/EnemyPathBuilder.cs |
| Assets/_Project/Editor/StageDataEditor.cs | Assets/_Project/Editor/Spawning/Stages/StageDataEditor.cs |
| Assets/_Project/Editor/WaveConfigEditor.cs | Assets/_Project/Editor/Spawning/Waves/WaveConfigEditor.cs |
| Assets/_Project/Editor/Formations/EnemyFormationWorkbench.cs | Assets/_Project/Editor/Spawning/Patterns/EnemyFormationWorkbench.cs |
| Assets/_Project/Editor/Formations/EnemyPatternEditor.cs | Assets/_Project/Editor/Spawning/Patterns/EnemyPatternEditor.cs |
| Assets/_Project/Editor/Formations/EnemySpawnManagerEditor.cs | Assets/_Project/Editor/Spawning/Common/EnemySpawnManagerEditor.cs |
| Assets/_Project/DataSO/Stages/Stage_01.asset | Assets/_Project/DataSO/Spawning/Stages/Stage_01.asset |
| Assets/_Project/DataSO/Wave/Default_Waves.asset | Assets/_Project/DataSO/Spawning/Waves/Default_Waves.asset |
| Assets/_Project/DataSO/EnemyPatterns/Stage_01_Formation.asset | Assets/_Project/DataSO/Spawning/Patterns/Stage_01_Formation.asset |
| Assets/_Project/DataSO/EnemyPatterns/Example_Area.asset | Assets/_Project/DataSO/Spawning/Patterns/Example_Area.asset |
| Assets/_Project/DataSO/EnemyPatterns/Example_SequentialPath.asset | Assets/_Project/DataSO/Spawning/Patterns/Example_SequentialPath.asset |
| Assets/_Project/DataSO/EnemyPatterns/Example_Area_Shape.asset | Assets/_Project/DataSO/Spawning/Patterns/Formations/Example_Area_Shape.asset |
| Assets/_Project/DataSO/Wave/EnemyFormations/Default_Waves_Wave_01_Formation.asset | Assets/_Project/DataSO/Spawning/Patterns/Formations/Default_Waves_Wave_01_Formation.asset |
| Assets/_Project/DataSO/Wave/EnemyPaths/Default_Waves_Wave_01_Path.asset | Assets/_Project/DataSO/Spawning/Patterns/Paths/Default_Waves_Wave_01_Path.asset |

Mỗi file đi cùng .meta gốc. Metadata của các thư mục Stages, Patterns, Paths và
các thư mục data tương ứng cũng được giữ/chuyển khi phù hợp. Thư mục mới có .meta
mới. Chỉ xóa thư mục nguồn sau khi xác nhận rỗng.

## Tham chiếu và đối chiếu

- Script C# và asset giữ nguyên nội dung trong quá trình di chuyển; đã đối chiếu
  SHA-256 trước/sau. README/tài liệu có thể được cập nhật nội dung hướng dẫn sau đó.
- Không đổi assembly: code runtime vẫn ngoài Editor, code editor vẫn dưới Editor.
- Không tìm thấy đường dẫn cũ được ghi cố định trong code hoặc cấu hình Unity đã rà.
- Unity references theo GUID được giữ nguyên. Không cần sửa các GUID trong scene,
  prefab hay các DataSO đang tham chiếu nhau.
- Danh sách di chuyển và hash nguồn: Temp/SpawnFolderReorganization/moves.json.
