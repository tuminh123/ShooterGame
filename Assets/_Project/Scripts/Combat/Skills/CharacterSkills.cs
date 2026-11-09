using System;
using System.Collections.Generic;

// Context của đúng lượt spawn, không sở hữu danh sách runtime hoặc một vòng tick production riêng.
public sealed class CharacterSkills : IDisposable
{
    #region Owner Context

    private readonly SkillManager manager; // Nơi duy nhất giữ runtime của tất cả owner.
    private readonly IReadOnlyList<SkillDataSO> initialDefinitions; // Chỉ giữ cấu hình ban đầu để rebuild khi reset owner còn sống.
    private readonly ulong generation; // Không dùng context cũ khi pool tái sử dụng CharacterRuntimeData.
    private bool disposed; // Context kết thúc trước khi owner chuyển sang lượt spawn mới.
    internal CharacterView Owner { get; }
    internal CharacterRuntimeData Identity { get; }
    internal bool IsValid => !disposed && Owner != null && ReferenceEquals(Owner.RuntimeData, Identity) && Identity.Generation == generation && Identity.IsAlive;
    internal bool CanRun => IsValid && Owner.isActiveAndEnabled;

    #endregion

    #region Initialization And Access

    // CharacterView có runtime trước; đăng ký skill ban đầu vào manager trước khi nhân vật được bật.
    public CharacterSkills(CharacterView owner, IReadOnlyList<SkillDataSO> definitions, SkillManager manager)
    {
        Owner = owner ?? throw new ArgumentNullException(nameof(owner));
        Identity = owner.RuntimeData ?? throw new ArgumentException("Initialize character data before skills.");
        generation = Identity.Generation;
        initialDefinitions = definitions ?? throw new ArgumentNullException(nameof(definitions));
        this.manager = manager ?? throw new ArgumentNullException(nameof(manager));
        try { RebuildInitialSkills(); }
        catch { Dispose(); throw; }
    }

    // Dựng từ definition: gameplay cấp một; nhân vật/trang bị giữ cấp config, từng buff tự quản lý token.
    internal void RebuildInitialSkills()
    {
        var unique = new HashSet<SkillDataSO>();
        foreach (SkillDataSO definition in initialDefinitions)
        {
            if (definition == null || !unique.Add(definition)) throw new ArgumentException("Initial skills must be non-null and unique.");
            if (definition.Source == SkillSource.Gameplay) throw new ArgumentException("Initial character skills must use separate Character/Equipment definitions.");
            if (!manager.AddInitialSkill(definition, this)) throw new InvalidOperationException("Could not register initial skill.");
        }
        Identity.RefillSpawnVitals(); // Chốt HP/shield đầy sau khi mọi bonus skill ban đầu đã được áp dụng.
    }

    // Facade tiện cho caller cũ; không tìm trong collection riêng.
    public bool TryGetRuntime(SkillDataSO definition, out SkillRuntime runtime) => manager.TryGetRuntime(definition, Owner, out runtime);
    // Manual simulation cho test; production chỉ SkillManager.Tick được Zenject lên lịch.
    public void Tick(float deltaTime) => manager.TickOwner(Owner, deltaTime);
    // Unity owner tạm tắt/bật thì manager cập nhật mọi skill của owner đó.
    internal void SetPaused(bool paused) => manager.SetOwnerPaused(Owner, paused);

    #endregion

    #region Cleanup

    // Xóa cả skill ban đầu lẫn skill nhận thêm của đúng owner.
    public void Dispose()
    {
        if (disposed) return;
        disposed = true; manager.RemoveOwner(Owner);
    }

    #endregion
}