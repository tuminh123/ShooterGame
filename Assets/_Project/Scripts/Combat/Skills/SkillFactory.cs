using System;
using System.Collections.Generic;

/// <summary>
/// Tạo skill đang chạy (<see cref="SkillRuntime"/>) và các hành vi của nó từ template trong <see cref="SkillDataSO"/>,
/// và sở hữu pool để dùng lại chúng.
/// </summary>
/// <remarks>
/// Không quản lý cấp hay vòng tick; việc đó thuộc <see cref="SkillRuntime"/> và <see cref="SkillSystem"/>.
/// Do <see cref="CombatManager"/> tạo; không bind vào Zenject.
/// <list type="bullet">
/// <item><see cref="Rent"/> / <see cref="Release"/>: skill vốn có của mọi nhân vật (mỗi lượt spawn quái), dùng lại qua pool.</item>
/// <item><see cref="Create"/>: skill nhận trong trận — tạo mới, không pool, vì UI chọn skill có thể còn giữ tham chiếu.</item>
/// <item>Hành vi (<see cref="CreateBehaviour"/> / <see cref="ReleaseBehaviour"/>): luôn pool, mỗi loại một pool; chỉ
/// <see cref="SkillRuntime"/> giữ chúng nên trả về là an toàn.</item>
/// </list>
/// </remarks>
public sealed class SkillFactory
{
    #region Dependencies And Pools

    private readonly SkillServices services; // Truyền vào mọi hành vi được tạo; factory không sở hữu vòng đời của chúng.
    private readonly Pool<SkillRuntime> runtimes = new Pool<SkillRuntime>(() => new SkillRuntime());
    // Mỗi loại hành vi một pool, đánh chỉ số theo số của enum (Dictionary khóa enum bị boxing trong Mono mỗi lần tra).
    private Pool<Skill>[] behaviours = new Pool<Skill>[8];
    // Asset skill không đổi trong lúc chơi: kiểm tra một lần mỗi asset thay vì mỗi lần spawn (Validate tạo HashSet).
    private readonly HashSet<SkillDataSO> validated = new HashSet<SkillDataSO>();

    public SkillFactory(SkillServices services)
        => this.services = services ?? throw new ArgumentNullException(nameof(services));

    /// <summary>Số runtime đang rảnh / đang cho thuê (kiểm tra).</summary>
    public int AvailableRuntimes => runtimes.AvailableCount;
    public int LeasedRuntimes => runtimes.LeasedCount;

    /// <summary>Tổng số hành vi đang rảnh / đang cho thuê, mọi loại (kiểm tra).</summary>
    public int AvailableBehaviours
    {
        get
        {
            int count = 0;
            foreach (Pool<Skill> pool in behaviours) if (pool != null) count += pool.AvailableCount;
            return count;
        }
    }

    public int LeasedBehaviours
    {
        get
        {
            int count = 0;
            foreach (Pool<Skill> pool in behaviours) if (pool != null) count += pool.LeasedCount;
            return count;
        }
    }

    #endregion

    #region Runtime

    /// <summary>
    /// Thuê skill vốn có cho <paramref name="owner"/> từ pool. Trả lại bằng <see cref="Release"/> khi bộ skill của lượt spawn được dọn.
    /// </summary>
    /// <param name="startLevel">Cấp bắt đầu do nơi tạo quyết định (xem CharacterSkillSystem).</param>
    public SkillRuntime Rent(SkillDataSO definition, CharacterSkills owner, int startLevel)
    {
        EnsureValid(definition);
        SkillRuntime runtime = runtimes.Rent();
        try { runtime.Initialize(definition, owner, this, startLevel); }
        catch
        {
            runtimes.Return(runtime); // Initialize lỗi đã tự Dispose; cất lại cho lượt sau.
            throw;
        }
        return runtime;
    }

    /// <summary>Tạo skill nhận trong trận (không pool). Runtime tự mở các hành vi có data ở cấp bắt đầu.</summary>
    /// <param name="startLevel">Cấp bắt đầu (skill trong màn: <see cref="SkillSystem.GameplayStartLevel"/>).</param>
    public SkillRuntime Create(SkillDataSO definition, CharacterSkills owner, int startLevel)
    {
        EnsureValid(definition);
        var runtime = new SkillRuntime();
        runtime.Initialize(definition, owner, this, startLevel);
        return runtime;
    }

    /// <summary>
    /// Nhận lại runtime đã thuê (tự kết thúc nếu còn chạy). Runtime tạo bằng <see cref="Create"/> hoặc đã trả rồi thì bỏ qua.
    /// </summary>
    /// <remarks>
    /// Runtime đang chạy code hành vi (callback của chính nó dẫn tới việc dọn) thì chỉ được đánh dấu: nó tự trả về khi
    /// lượt chạy ngoài cùng kết thúc, để không xóa Owner/Services hay cho chủ khác mượn lại giữa chừng.
    /// </remarks>
    public bool Release(SkillRuntime runtime)
    {
        if (runtime == null || !runtimes.IsLeased(runtime)) return false;
        if (runtime.IsBusy)
        {
            runtime.RequestRelease();
            return true;
        }
        return runtimes.Return(runtime);
    }

    // Runtime gọi khi hết bận sau khi đã được yêu cầu trả.
    internal void ReleaseNow(SkillRuntime runtime) => runtimes.Return(runtime);

    private void EnsureValid(SkillDataSO definition)
    {
        if (definition == null) throw new ArgumentNullException(nameof(definition));
        if (validated.Contains(definition)) return;
        definition.Validate();
        validated.Add(definition);
    }

    #endregion

    #region Behaviours

    /// <summary>Lấy hành vi <paramref name="type"/> (từ pool) và gắn cho <paramref name="owner"/>; mỗi lượt là một object riêng.</summary>
    public ISkill CreateBehaviour(SkillDataSO definition, SkillBehaviourType type, CharacterSkills owner)
    {
        if (!definition.HasBehaviour(type))
            throw new InvalidOperationException($"No {type} behaviour in {definition.name}.");
        Skill behaviour = BehaviourPool(type).Rent();
        behaviour.Bind(owner, services);
        return behaviour;
    }

    /// <summary>Nhận lại hành vi đã Dispose (SkillRuntime gọi khi gỡ hành vi hoặc kết thúc skill).</summary>
    public void ReleaseBehaviour(ISkill action)
    {
        if (!(action is Skill behaviour)) return;
        int index = (int)behaviour.Type;
        if (index >= 0 && index < behaviours.Length && behaviours[index] != null) behaviours[index].Return(behaviour);
    }

    private Pool<Skill> BehaviourPool(SkillBehaviourType type)
    {
        int index = (int)type;
        if (index < 0) throw new ArgumentOutOfRangeException(nameof(type), type, "Behaviour type must not be negative.");
        if (index >= behaviours.Length) Array.Resize(ref behaviours, index + 1); // Enum mới thêm ở cuối: nới mảng một lần.
        return behaviours[index] ?? (behaviours[index] = CreatePool(type));
    }

    // Lambda nằm ở hàm riêng: nếu đặt trong BehaviourPool, C# tạo closure ở đầu hàm cho MỌI lần gọi, kể cả khi pool đã có.
    private static Pool<Skill> CreatePool(SkillBehaviourType type) => new Pool<Skill>(() => New(type));

    // Nơi DUY NHẤT ánh xạ enum → class hành vi. Thêm hành vi mới = thêm giá trị enum (ở cuối) + một dòng ở đây.
    private static Skill New(SkillBehaviourType type)
    {
        switch (type)
        {
            case SkillBehaviourType.NormalAttack: return new NormalAttackSkill();
            case SkillBehaviourType.Shooting: return new ShootingSkill();
            case SkillBehaviourType.OrbitSaw: return new OrbitSawSkill();
            case SkillBehaviourType.SlowAura: return new FrostAuraSkill();
            case SkillBehaviourType.Regeneration: return new RegenerationSkill();
            case SkillBehaviourType.EmergencyShield: return new EmergencyShieldSkill();
            case SkillBehaviourType.StatBuff: return new StatBonusSkill();
            case SkillBehaviourType.Strike: return new StrikeSkill();
            case SkillBehaviourType.Zone: throw new ArgumentOutOfRangeException(nameof(type), type, "Zone chỉ tạo qua tác động SpawnZone.");
            case SkillBehaviourType.Chain: return new ChainSkill();
            case SkillBehaviourType.Beam: return new BeamSkill();
            case SkillBehaviourType.Slash: return new SlashSkill();
            case SkillBehaviourType.Summon: return new SummonSkill();
            case SkillBehaviourType.KillStack: return new KillStackSkill();
            case SkillBehaviourType.SkillScaling: return new SkillScalingSkill();
            case SkillBehaviourType.ShieldRegen: return new ShieldRegenSkill();
            default: throw new ArgumentOutOfRangeException(nameof(type), type, "No behaviour class for this type.");
        }
    }

    #endregion
}
