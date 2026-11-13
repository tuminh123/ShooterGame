using System;
using UnityEngine;

/// <summary>Ba cách hoạt động dùng chung hệ thống Pattern.</summary>
public enum EnemyPatternType { Formation, SequentialPath, Area }

/// <summary>Cấu hình quái và cách hoạt động của Formation, SequentialPath hoặc Area.</summary>
[CreateAssetMenu(fileName = "EnemyPattern", menuName = "Shooter Game/Enemy/Pattern")]
public sealed class EnemyPatternSO : ScriptableObject, IStageSpawnData
{
    #region Định tuyến hệ thống

    public Type SystemType => typeof(FormationSpawnSystem);
    /// <summary>Tạo FormationSpawnSystem và inject trực tiếp các dependency cần dùng.</summary>
    public IStageSpawnSystem CreateSystem(Zenject.DiContainer container) => container.Instantiate<FormationSpawnSystem>();

    #endregion

    #region Cấu hình quái và chuyển động

    [Tooltip("Formation: cả khối theo đường. SequentialPath: từng con xuất hiện tại A. Area: viền rỗng bám player và thu nhỏ.")]
    public EnemyPatternType type;
    [Tooltip("Mốc bắt đầu tính bằng giây từ đầu Stage, độc lập với Wave.")]
    [Min(0f)] public float delay;
    [Tooltip("Danh sách quái riêng, không lấy từ Wave và không trừ spawnCount của Wave.")]
    public CharacterDataSO[] enemies = Array.Empty<CharacterDataSO>();
    [Min(1)] public int enemyLevel = 1;
    [Tooltip("Chỉ con chạm player nhận knockback qua EnemyController rồi đi tiếp độc lập, không về slot đội hình.")]
    [Min(0f)] public float knockbackDistance = 0.35f;
    [Min(0.01f)] public float knockbackDuration = 0.2f;
    [Tooltip("Hình dùng cho Formation hoặc đường viền rỗng của Area.")]
    public EnemyFormationSO formation;
    [Tooltip("Chỉ dùng cho Formation và SequentialPath. Area không sử dụng Path.")]
    public EnemyPathSO path;
    [Tooltip("Tốc độ chung của nhóm trên Path. Con tách đội hình giữ tốc độ này để chạy nốt đường.")]
    [Min(0.01f)] public float speed = 2.5f;
    [Tooltip("1: A → B. 2: A → B → A. Hết hành trình thu hồi, không chuyển sang chase.")]
    [Range(1, 100)] public int passes = 1;
    [Tooltip("SequentialPath: tổng quái. Area: số quái trên đường viền. Formation lấy số slot từ hình.")]
    [Min(1)] public int enemyCount = 20;
    [Tooltip("Khoảng cách thời gian giữa hai con xuất hiện tại A, chỉ dùng cho SequentialPath.")]
    [Min(0.01f)] public float spawnInterval = 0.3f;
    [Tooltip("Thời gian gameplay để Area thu về 0. Tâm luôn theo player; hết thời gian thu hồi mọi con còn lại.")]
    [Min(0.01f)] public float shrinkDuration = 5f;

    #endregion

    #region Số lượng và kiểm tra

    /// <summary>Formation lấy số slot hình; hai kiểu còn lại dùng enemyCount.</summary>
    public int Count => type == EnemyPatternType.Formation ? formation.SlotCount : enemyCount;

    /// <summary>Kiểm tra cấu hình chung và các trường riêng cần cho kiểu Pattern đã chọn.</summary>
    public void Validate()
    {
        if (float.IsNaN(delay) || float.IsInfinity(delay) || delay < 0f ||
            !Enum.IsDefined(typeof(EnemyPatternType), type) || enemyLevel < 1 ||
            !Positive(knockbackDuration) || float.IsNaN(knockbackDistance) ||
            float.IsInfinity(knockbackDistance) || knockbackDistance < 0f || enemies == null || enemies.Length == 0)
            throw new InvalidOperationException("Pattern cần delay, loại, cấp, knockback và danh sách quái riêng hợp lệ.");
        foreach (var enemy in enemies)
            if (enemy == null || enemy.Team != CharacterTeam.Enemy || enemy.IsBoss || enemy.VisualPrefab == null)
                throw new InvalidOperationException("Pattern chỉ dùng enemy thường có visual.");
        if (type != EnemyPatternType.Area)
        {
            if (path == null || !Positive(speed) || passes < 1 || passes > 100)
                throw new InvalidOperationException("Formation/SequentialPath cần Path, tốc độ và số lượt hợp lệ.");
            path.Validate();
        }
        if (type != EnemyPatternType.SequentialPath)
        {
            if (formation == null) throw new InvalidOperationException("Cần gán hình cho Formation/Area.");
            formation.Validate();
        }
        if (type == EnemyPatternType.SequentialPath && (enemyCount < 1 || enemyCount > 4096 || !Positive(spawnInterval)))
            throw new InvalidOperationException("SequentialPath cần 1–4096 quái và nhịp spawn dương.");
        if (type == EnemyPatternType.Area)
        {
            if (enemyCount < 3 || enemyCount > 4096 || !Positive(shrinkDuration))
                throw new InvalidOperationException("Area cần 3–4096 quái và thời gian thu nhỏ dương.");
            if (formation.Shape == FormationType.Custom || formation.Shape == FormationType.HorizontalLine || formation.Shape == FormationType.VerticalLine)
                throw new InvalidOperationException("Area cần hình kín: tròn, vuông, tam giác, thoi hoặc lục giác.");
        }
    }

    /// <summary>Kiểm tra giá trị dương, không NaN hoặc vô cực.</summary>
    private static bool Positive(float value) => value > 0f && !float.IsInfinity(value);
    #endregion
}
