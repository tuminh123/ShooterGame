using System;
using System.Collections.Generic;
using UnityEngine;

#region Area Shrink Timeline

/// <summary>Snapshot nhịp thu, dùng chung cho runtime và Editor; không giữ đồng hồ riêng.</summary>
public readonly struct AreaShrinkTiming
{
    public readonly AreaShrinkMode Mode;
    public readonly float Duration, BurstDuration, PauseDuration;

    public AreaShrinkTiming(AreaShrinkMode mode, float duration, float burstDuration, float pauseDuration)
    {
        Mode = mode;
        Duration = duration;
        BurstDuration = burstDuration;
        PauseDuration = pauseDuration;
    }

    /// <summary>
    /// Tổng thời gian Area tồn tại = thời gian thu + số lần nghỉ × thời gian nghỉ.
    /// Ví dụ thu 5 giây, mỗi đợt 1 giây, nghỉ 1 giây: 5 đợt, 4 lần nghỉ, tổng 9 giây.
    /// </summary>
    public float TotalDuration
    {
        get
        {
            if (Mode == AreaShrinkMode.Continuous) return Duration;

            // Trừ 1e-6 để sai số làm tròn (5 / 1 = 5.0000001) không bị Ceiling đẩy lên thành 6 đợt.
            int burstCount = (int)Math.Ceiling((double)Duration / BurstDuration - 1e-6d);
            int pauseCount = Math.Max(0, burstCount - 1); // Không nghỉ sau đợt cuối.
            return Duration + pauseCount * PauseDuration;
        }
    }

    /// <summary>Tổng giây thực sự thu đến mốc elapsed, bỏ qua các khoảng nghỉ.</summary>
    public float GetShrinkTime(float elapsed)
    {
        if (elapsed <= 0f) return 0f;
        if (elapsed >= TotalDuration) return Duration;
        if (Mode == AreaShrinkMode.Continuous) return Mathf.Min(elapsed, Duration);
        double cycle = (double)BurstDuration + PauseDuration;
        double count = Math.Floor(elapsed / cycle);
        double active = count * BurstDuration + Math.Min(BurstDuration, elapsed - count * cycle);
        return Mathf.Clamp((float)active, 0f, Duration);
    }

    public float GetScale(float elapsed) => Mathf.Clamp01(1f - GetShrinkTime(elapsed) / Duration);

    public bool IsResting(float elapsed)
    {
        if (Mode != AreaShrinkMode.Burst || PauseDuration <= 0f || elapsed < 0f || elapsed >= TotalDuration) return false;
        double cycle = (double)BurstDuration + PauseDuration;
        return elapsed - Math.Floor(elapsed / cycle) * cycle >= BurstDuration;
    }
}

#endregion

/// <summary>Cấu hình quái và cách hoạt động của Formation, SequentialPath hoặc Area.</summary>
[CreateAssetMenu(fileName = "EnemyPattern", menuName = "Shooter Game/Enemy/Pattern")]
public sealed class EnemyPatternSO : ScriptableObject, IStageSpawnData
{
    #region Enemy & Movement Configuration

    [Tooltip("Chỉ chọn cách spawn/chuyển động: cả khối theo Path, từng thành viên theo Path, hoặc Area quanh Player lúc spawn. Hình Đặc/Rỗng lấy từ Formation asset.")]
    [SerializeField] private EnemyPatternType type;
    [Tooltip("Mốc bắt đầu tính bằng giây từ đầu Stage, độc lập với Wave.")]
    [SerializeField][Min(0f)] private float delay;
    [Tooltip("Danh sách quái riêng, không lấy từ Wave và không trừ spawnCount của Wave.")]
    [SerializeField] private CharacterDataSO[] enemies = Array.Empty<CharacterDataSO>();
    [Tooltip("Cấp của mọi thành viên trong lượt Pattern, từ 1 trở lên; độc lập số thứ tự wave vì Pattern chạy theo mốc giờ Stage.")]
    [SerializeField][Min(1)] private int enemyLevel = 1;
    [Tooltip("Chỉ con chạm player nhận knockback qua EnemyController rồi đi tiếp độc lập, không về slot đội hình.")]
    [SerializeField][Min(0f)] private float knockbackDistance = 0.35f;
    [Tooltip("Thời gian đẩy lùi thành viên sau khi chạm Player, tính bằng giây gameplay; phải lớn hơn 0.")]
    [SerializeField][Min(0.01f)] private float knockbackDuration = 0.2f;
    [Tooltip("Hình học bắt buộc cho cả ba cách spawn: Đặc/Rỗng, Shape, Level, Size và Rotation. Số slot là số quái cần sinh.")]
    [SerializeField] private EnemyFormationSO formation;
    [Tooltip("Chỉ dùng cho Formation và SequentialPath. Area không sử dụng Path.")]
    [SerializeField] private EnemyPathSO path;
    [Tooltip("Tốc độ chung của nhóm trên Path. Con tách đội hình giữ tốc độ này để chạy nốt đường.")]
    [SerializeField][Min(0.01f)] private float speed = 2.5f;
    [Tooltip("1: A → B. 2: A → B → A. Hết hành trình thu hồi, không chuyển sang chase.")]
    [SerializeField][Range(1, 100)] private int passes = 1;
    [Tooltip("Khoảng cách thời gian giữa hai con xuất hiện tại A, chỉ dùng cho SequentialPath.")]
    [SerializeField][Min(0.01f)] private float spawnInterval = 0.3f;
    [Tooltip("Thu liên tục như cũ, hoặc thu theo đợt có khoảng nghỉ. Chỉ dùng cho Area.")]
    [SerializeField] private AreaShrinkMode areaShrinkMode;
    [Tooltip("Tổng số giây thực sự thu Area về tâm, không tính các khoảng nghỉ. Hết toàn bộ tiến trình sẽ thu hồi quái còn lại.")]
    [SerializeField][Min(0.01f)] private float shrinkDuration = 5f;
    [Tooltip("Số giây thu mỗi đợt, sau đó dừng lại. Chỉ dùng khi chọn Thu theo đợt.")]
    [SerializeField][Min(0.01f)] private float shrinkBurstDuration = 1f;
    [Tooltip("Số giây đứng nguyên bán kính giữa hai đợt thu. Tâm vòng giữ cố định tại chỗ spawn; 0 = không nghỉ. Không nghỉ sau đợt cuối.")]
    [SerializeField][Min(0f)] private float shrinkPauseDuration = 1f;

    /// <summary>Chỉ chọn cách spawn/chuyển động: cả khối theo Path, từng thành viên theo Path, hoặc Area quanh Player lúc spawn. Hình Đặc/Rỗng lấy từ Formation asset.</summary>
    public EnemyPatternType Type => type;
    /// <summary>Mốc bắt đầu tính bằng giây từ đầu Stage, độc lập với Wave.</summary>
    public float Delay => delay;
    /// <summary>Danh sách quái riêng, không lấy từ Wave và không trừ spawnCount của Wave.</summary>
    public IReadOnlyList<CharacterDataSO> Enemies => enemies;
    /// <summary>Cấp của mọi thành viên trong lượt Pattern, từ 1 trở lên; độc lập số thứ tự wave vì Pattern chạy theo mốc giờ Stage.</summary>
    public int EnemyLevel => enemyLevel;
    /// <summary>Chỉ con chạm player nhận knockback qua EnemyController rồi đi tiếp độc lập, không về slot đội hình.</summary>
    public float KnockbackDistance => knockbackDistance;
    /// <summary>Thời gian đẩy lùi thành viên sau khi chạm Player, tính bằng giây gameplay; phải lớn hơn 0.</summary>
    public float KnockbackDuration => knockbackDuration;
    /// <summary>Hình học bắt buộc cho cả ba cách spawn: Đặc/Rỗng, Shape, Level, Size và Rotation. Số slot là số quái cần sinh.</summary>
    public EnemyFormationSO Formation => formation;
    /// <summary>Chỉ dùng cho Formation và SequentialPath. Area không sử dụng Path.</summary>
    public EnemyPathSO Path => path;
    /// <summary>Tốc độ chung của nhóm trên Path. Con tách đội hình giữ tốc độ này để chạy nốt đường.</summary>
    public float Speed => speed;
    /// <summary>1: A → B. 2: A → B → A. Hết hành trình thu hồi, không chuyển sang chase.</summary>
    public int Passes => passes;
    /// <summary>Khoảng cách thời gian giữa hai con xuất hiện tại A, chỉ dùng cho SequentialPath.</summary>
    public float SpawnInterval => spawnInterval;
    /// <summary>Thu liên tục như cũ, hoặc thu theo đợt có khoảng nghỉ. Chỉ dùng cho Area.</summary>
    public AreaShrinkMode AreaShrinkMode => areaShrinkMode;
    /// <summary>Tổng số giây thực sự thu Area về tâm, không tính các khoảng nghỉ. Hết toàn bộ tiến trình sẽ thu hồi quái còn lại.</summary>
    public float ShrinkDuration => shrinkDuration;
    /// <summary>Số giây thu mỗi đợt, sau đó dừng lại. Chỉ dùng khi chọn Thu theo đợt.</summary>
    public float ShrinkBurstDuration => shrinkBurstDuration;
    /// <summary>Số giây đứng nguyên bán kính giữa hai đợt thu. Tâm vòng giữ cố định tại chỗ spawn; 0 = không nghỉ. Không nghỉ sau đợt cuối.</summary>
    public float ShrinkPauseDuration => shrinkPauseDuration;

    #endregion

    #region Count & Validation

    /// <summary>Cả ba cách spawn đều dùng đúng số slot của cùng một hình học.</summary>
    public int Count => formation != null ? formation.SlotCount : throw new InvalidOperationException("Pattern cần Formation asset.");
    public AreaShrinkTiming ShrinkTiming => new AreaShrinkTiming(areaShrinkMode, shrinkDuration, shrinkBurstDuration, shrinkPauseDuration);

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
        if (formation == null) throw new InvalidOperationException("Cả ba cách spawn đều cần hình học Formation.");
        formation.Validate();
        if (type == EnemyPatternType.SequentialPath && !Positive(spawnInterval))
            throw new InvalidOperationException("SequentialPath cần nhịp spawn dương.");
        if (type == EnemyPatternType.Area)
        {
            if (!Enum.IsDefined(typeof(AreaShrinkMode), areaShrinkMode) ||
                (areaShrinkMode == AreaShrinkMode.Burst && (!Positive(shrinkBurstDuration) ||
                    float.IsNaN(shrinkPauseDuration) || float.IsInfinity(shrinkPauseDuration) || shrinkPauseDuration < 0f)))
                throw new InvalidOperationException("Area cần mode thu hợp lệ, thời gian mỗi đợt dương và thời gian nghỉ từ 0 trở lên.");
            if (!Positive(shrinkDuration))
                throw new InvalidOperationException("Area cần thời gian thu nhỏ dương.");
            if (!Positive(ShrinkTiming.TotalDuration))
                throw new InvalidOperationException("Tổng thời gian Area phải hữu hạn và dương.");
        }
    }

    #region Runtime Snapshot

    public RuntimeData CreateRuntimeData() { Validate(); return new RuntimeData(this); }

    /// <summary>Chốt cấu hình và slot một lần để quota/đích không lệch khi asset đổi trong thời gian chờ.</summary>
    public sealed class RuntimeData
    {
        internal readonly Vector2[] Slots, PathPoints;
        internal readonly CharacterDataSO[] Enemies;
        public readonly EnemyPatternType Type;
        public readonly int EnemyLevel, Passes;
        public readonly float Delay, SpawnInterval, Speed, KnockbackDistance, KnockbackDuration;
        public readonly AreaShrinkTiming ShrinkTiming;
        public int Count => Slots.Length;

        internal RuntimeData(EnemyPatternSO data)
        {
            Type = data.type;
            EnemyLevel = data.enemyLevel;
            Passes = data.passes;
            Delay = data.delay;
            SpawnInterval = data.spawnInterval;
            Speed = data.speed;
            KnockbackDistance = data.knockbackDistance;
            KnockbackDuration = data.knockbackDuration;
            ShrinkTiming = data.ShrinkTiming;
            Slots = FormationBuilder.Build(data.formation);
            Enemies = (CharacterDataSO[])data.enemies.Clone();
            if (Type != EnemyPatternType.Area)
            {
                PathPoints = EnemyPathBuilder.BuildLocal(data.path);
                for (int i = 0; i < PathPoints.Length; i++) PathPoints[i] += data.path.CenterOffset;
            }
        }

        // Chỉ lấy camera thật khi bắt đầu spawn, giữ Path bám camera giống gameplay hiện tại.
        public EnemyPathRoute CreateRoute(Transform camera)
        {
            if (Type == EnemyPatternType.Area) return null;
            if (camera == null) throw new InvalidOperationException("Path cần camera spawn.");
            var world = new Vector2[PathPoints.Length];
            for (int i = 0; i < world.Length; i++) world[i] = PathPoints[i] + (Vector2)camera.position;
            return new EnemyPathRoute(world, camera);
        }
    }

    #endregion

    /// <summary>Kiểm tra giá trị dương, không NaN hoặc vô cực.</summary>
    private static bool Positive(float value) => value > 0f && !float.IsInfinity(value);
    #endregion
}
