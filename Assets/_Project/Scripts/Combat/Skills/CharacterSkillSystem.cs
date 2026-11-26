using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gắn combat vào vòng đời của từng nhân vật: tạo bộ skill vốn có khi nhân vật xuất hiện, tick chúng mỗi frame,
/// pause khi vỏ tạm tắt, và dọn skill/effect khi nhân vật chết hoặc bị thu hồi.
/// </summary>
/// <remarks>
/// Đây là phía Combat của việc "đảo chiều" (Task 2.5): Combat <b>nghe</b> miền Character qua
/// <see cref="ICharacterRegistry"/>/<see cref="ICharacterDeathEvents"/> và event của <see cref="CharacterView"/>,
/// nên miền Character không còn biết Combat tồn tại.
/// <para>
/// Mỗi nhân vật luôn có một <see cref="CharacterSkills"/> (kể cả khi không có skill vốn có), vì nó còn là context
/// của lượt spawn mà skill gameplay nhận trong trận dựa vào.
/// </para>
/// Do <see cref="CombatManager"/> tạo; không bind vào Zenject.
/// </remarks>
public sealed class CharacterSkillSystem : IDisposable
{
    #region Types

    /// <summary>
    /// Bộ skill của một lượt spawn, kèm handle để nhận ra lượt spawn đó (vỏ có thể bị pool tái sử dụng).
    /// </summary>
    /// <remarks>
    /// Dùng lại qua pool: mỗi Entry đi kèm một <see cref="CharacterSkills"/> cố định và hai delegate tạo một lần
    /// (đọc View hiện tại của Entry), nên spawn lại không cấp phát closure mới.
    /// </remarks>
    private sealed class Entry : IPoolable
    {
        public readonly CharacterSkills Skills = new CharacterSkills();
        public readonly Action Activated; // Giữ delegate để gỡ đúng listener khi thu hồi.
        public readonly Action Deactivated;
        public CharacterHandle Handle;
        public CharacterView View;

        public Entry(CharacterSkillSystem system)
        {
            // View null = entry đang nằm trong pool (đã gỡ listener); chặn phòng khi event còn sót.
            Activated = () => { if (View != null) system.SetPaused(View, false); };
            Deactivated = () => { if (View != null) system.SetPaused(View, true); };
        }

        public void Initialize(CharacterView view)
        {
            View = view;
            Handle = new CharacterHandle(view);
        }

        // Kết thúc bộ skill nếu còn chạy, rồi bỏ tham chiếu tới vỏ của lượt cũ.
        void IPoolable.ResetForPool()
        {
            try { ((IPoolable)Skills).ResetForPool(); }
            finally
            {
                View = null;
                Handle = default;
            }
        }
    }

    #endregion

    #region Dependencies And State

    private readonly ICharacterRegistry characters;
    private readonly ICharacterDeathEvents deaths;
    private readonly SkillFactory factory;
    private readonly ICharacterProgress progress; // Level/sao và đồ/set đang mặc của Player (miền Progression), đọc lúc spawn.
    private readonly List<SkillGrant> grantBuffer = new List<SkillGrant>(8); // Dùng lại mỗi lần spawn, không cấp phát mới.
    private readonly List<EquipmentItem> equippedBuffer = new List<EquipmentItem>(6);
    private readonly List<EquipmentSetData> setBuffer = new List<EquipmentSetData>(3);
    private readonly SkillSystem gameplaySkills;
    private readonly EffectSystem effects;
    private readonly Dictionary<CharacterView, Entry> entries = new Dictionary<CharacterView, Entry>(128);
    private readonly List<Entry> tickSnapshot = new List<Entry>(128);
    private readonly List<Entry> stopSnapshot = new List<Entry>(128); // StopAll dùng lại, không tạo list mỗi lần kết thúc trận.
    private readonly Pool<Entry> entryPool; // Entry + CharacterSkills của lượt spawn, dùng lại cho lượt sau.
    // Entry bị thu hồi trong lúc chính bộ skill của nó đang tick (đòn đánh làm chủ bị despawn): chờ hết vòng tick mới trả pool,
    // để vòng tick đang chạy không đọc bộ skill đã bị dọn hoặc đã được lượt spawn khác mượn lại.
    private readonly List<Entry> pendingEntries = new List<Entry>();
    private bool ticking; // Đang duyệt tickSnapshot.
    private bool disposed;

    /// <summary>Số entry (bộ skill) đang rảnh / đang dùng (kiểm tra).</summary>
    public int AvailableEntries => entryPool.AvailableCount;
    public int LeasedEntries => entryPool.LeasedCount;

    #endregion

    #region Constructor

    public CharacterSkillSystem(ICharacterRegistry characters, ICharacterDeathEvents deaths, ICharacterProgress progress,
        SkillFactory factory, SkillSystem gameplaySkills, EffectSystem effects)
    {
        this.characters = characters ?? throw new ArgumentNullException(nameof(characters));
        this.deaths = deaths ?? throw new ArgumentNullException(nameof(deaths));
        this.progress = progress ?? throw new ArgumentNullException(nameof(progress));
        this.factory = factory ?? throw new ArgumentNullException(nameof(factory));
        this.gameplaySkills = gameplaySkills ?? throw new ArgumentNullException(nameof(gameplaySkills));
        this.effects = effects ?? throw new ArgumentNullException(nameof(effects));
        entryPool = new Pool<Entry>(() => new Entry(this));
        characters.CharacterAdded += OnCharacterAdded;
        characters.CharacterRemoved += OnCharacterRemoved;
        deaths.CharacterDied += OnCharacterDied;
    }

    #endregion

    #region Lookup

    /// <summary>Context skill của lượt spawn hiện tại của <paramref name="owner"/>; null nếu nhân vật không còn hợp lệ.</summary>
    public CharacterSkills GetContext(CharacterView owner)
    {
        if (owner == null)
            return null;
        if (!entries.TryGetValue(owner, out Entry entry))
            return null;
        if (!entry.Skills.IsValid)
            return null;
        return entry.Skills;
    }

    #endregion

    #region Frame Update

    /// <summary>Tick skill vốn có của mọi nhân vật. CombatManager gọi sau skill gameplay, trước đạn.</summary>
    public void Tick(float deltaTime)
    {
        if (disposed || deltaTime <= 0f) return;
        // Chụp danh sách trước: một đòn đánh có thể giết nhân vật khác và làm entry bị gỡ ngay trong vòng lặp.
        tickSnapshot.Clear();
        tickSnapshot.AddRange(entries.Values);
        ticking = true;
        try
        {
            foreach (Entry entry in tickSnapshot)
            {
                if (disposed) break;
                // CharacterSkills.Tick tự bỏ qua khi nhân vật đã chết, đang tắt hoặc bị khóa đòn đánh (CanRun).
                if (!entry.Handle.IsCurrent) continue;
                try { entry.Skills.Tick(deltaTime); }
                catch (Exception exception) { Debug.LogException(exception, entry.Handle.View); }
            }
        }
        finally
        {
            ticking = false;
            tickSnapshot.Clear();
            FlushPendingEntries(); // Entry thu hồi giữa vòng chỉ về pool khi không còn trong bản chụp.
        }
    }

    #endregion

    #region Character Lifecycle (nghe miền Character)

    // Factory phát CharacterAdded sau khi vỏ đã bật, ngay trong lời gọi Spawn: chưa có frame nào trôi qua,
    // nên skill vẫn có mặt trước lượt tick đầu tiên giống như khi còn được tạo trong CharacterView.Initialize.
    private void OnCharacterAdded(CharacterView view, CharacterDataSO template)
    {
        if (disposed || view == null || view.RuntimeData == null) return;
        // Phòng trường hợp không nhận được CharacterRemoved của lượt spawn trước trên cùng vỏ.
        if (entries.ContainsKey(view)) Release(view);

        CollectSkillGrants(template, grantBuffer);
        Entry entry = entryPool.Rent();
        try
        {
            entry.Initialize(view);
            entry.Skills.Initialize(view, grantBuffer, factory);
        }
        catch
        {
            entryPool.Return(entry); // Khởi tạo skill lỗi: dọn và cất lại, không lọt entry dở dang.
            throw;
        }
        entries.Add(view, entry);
        view.Activated += entry.Activated;
        view.Deactivated += entry.Deactivated;
    }

    /// <summary>
    /// Nơi duy nhất quyết định skill vốn có của một nhân vật bắt đầu ở cấp mấy khi vào trận.
    /// Skill nhận trong màn không đi qua đây: SkillSystem luôn tạo ở <see cref="SkillSystem.GameplayStartLevel"/>.
    /// </summary>
    private void CollectSkillGrants(CharacterDataSO template, List<SkillGrant> result)
    {
        switch (template.Team)
        {
            case CharacterTeam.Player:
                // Player: skill mở và cấp skill suy ra từ level + sao đã lưu (không lưu riêng cấp skill).
                int level = progress.GetLevel(template.CharacterType);
                int stars = progress.GetStars(template.CharacterType);
                template.GetSkillGrants(level, stars, result);
                AddEquipmentSkills(result);
                break;

            case CharacterTeam.Enemy:
                // Enemy và boss: mọi skill có sẵn, ở cấp mặc định ghi trong asset của skill.
                result.Clear();
                // Chỉ số, không foreach: chạy mỗi lần quái spawn, enumerator của IReadOnlyList sẽ cấp phát.
                for (int i = 0; i < template.Skills.Count; i++)
                {
                    SkillDataSO skill = template.Skills[i];
                    result.Add(new SkillGrant(skill, skill.Level));
                }
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(template), template.Team, "Unknown character team.");
        }
    }

    /// <summary>
    /// Skill của đồ đang mặc (theo thứ tự ô) rồi skill của set đang bật, mỗi skill một cấp.
    /// Thứ tự ưu tiên khi trùng: skill nhân vật (đã có trong list) > skill đồ > skill set; skill đã có thì bỏ bản sau,
    /// vì CharacterSkills không nhận skill trùng. Skill trong trận là asset nguồn Gameplay khác nên luôn đứng sau.
    /// </summary>
    private void AddEquipmentSkills(List<SkillGrant> result)
    {
        progress.GetEquippedItems(equippedBuffer);
        foreach (EquipmentItem item in equippedBuffer)
        {
            if (item.Definition.TryGetSkill(item.Level, out SkillDataSO skill))
                AddIfMissing(result, skill);
        }
        progress.GetActiveSets(setBuffer);
        foreach (EquipmentSetData set in setBuffer)
            AddIfMissing(result, set.Skill);
    }

    private static void AddIfMissing(List<SkillGrant> result, SkillDataSO skill)
    {
        foreach (SkillGrant grant in result)
        {
            if (grant.Skill == skill)
                return;
        }
        // Skill trang bị chỉ có một cấp.
        result.Add(new SkillGrant(skill, 1));
    }

    // Chết không phải là thu hồi: vỏ còn ở lại để phát animation chết, nhưng mọi skill và effect phải dừng ngay.
    private void OnCharacterDied(CharacterView view)
    {
        if (view == null || !entries.TryGetValue(view, out Entry entry)) return;
        // Listener khác của cùng event có thể đã thu hồi rồi spawn lại vỏ này; lượt spawn mới còn sống thì không đụng tới.
        if (entry.Handle.IsAlive) return;
        EndCombat(view, entry);
    }

    // Thu hồi (despawn, Destroy): dọn sạch và quên lượt spawn này.
    private void OnCharacterRemoved(CharacterView view) => Release(view);

    #endregion

    #region Cleanup

    /// <summary>
    /// Dừng skill vốn có của mọi nhân vật đang có, ví dụ khi trận kết thúc (nhân vật còn lại đứng yên chờ kết quả).
    /// Entry vẫn được giữ cho tới khi nhân vật bị thu hồi.
    /// </summary>
    public void StopAll()
    {
        stopSnapshot.Clear();
        stopSnapshot.AddRange(entries.Values);
        try
        {
            foreach (Entry entry in stopSnapshot)
                DisposeSafely(entry.Skills, entry.Handle.View);
        }
        finally { stopSnapshot.Clear(); }
    }

    // Quên lượt spawn: dọn combat rồi cất entry (kèm bộ skill) về pool cho lượt spawn sau.
    private void Release(CharacterView view)
    {
        if (!entries.TryGetValue(view, out Entry entry)) return;
        entries.Remove(view);
        view.Activated -= entry.Activated;
        view.Deactivated -= entry.Deactivated;
        try { EndCombat(view, entry); }
        finally
        {
            // Đang trong vòng tick (của hệ thống hoặc của chính bộ skill): chờ hết vòng, để bản chụp không gặp lại entry
            // đã được lượt spawn mới mượn (tick thừa) hay bộ skill đã dọn.
            if (ticking || entry.Skills.IsTicking) pendingEntries.Add(entry);
            else ReturnEntry(entry);
        }
    }

    private void ReturnEntry(Entry entry)
    {
        try { entryPool.Return(entry); }
        catch (Exception exception) { Debug.LogException(exception); }
    }

    // Trả các entry đã thu hồi giữa vòng tick (gọi sau vòng tick và khi dọn scene).
    private void FlushPendingEntries()
    {
        for (int i = 0; i < pendingEntries.Count; i++) ReturnEntry(pendingEntries[i]);
        pendingEntries.Clear();
    }

    // Thứ tự giữ như khi còn nằm trong CharacterView: skill vốn có → skill gameplay của owner → effect đang bám.
    // Mỗi bước được cô lập để lỗi của một bước không chặn các bước còn lại.
    private void EndCombat(CharacterView view, Entry entry)
    {
        DisposeSafely(entry.Skills, view);
        try { gameplaySkills.RemoveOwner(view); }
        catch (Exception exception) { Debug.LogException(exception, view); }
        try { effects.ClearTarget(view, entry.Skills.Identity); }
        catch (Exception exception) { Debug.LogException(exception, view); }
    }

    // Vỏ tắt tạm (ra khỏi vùng hoạt động, choáng…) giữ nguyên cooldown và thời hạn effect; bật lại thì chạy tiếp.
    private void SetPaused(CharacterView view, bool paused)
    {
        if (!entries.TryGetValue(view, out Entry entry) || !entry.Handle.IsCurrent) return;
        entry.Skills.SetPaused(paused);
        gameplaySkills.SetOwnerPaused(view, paused);
        effects.SetPaused(view, entry.Skills.Identity, paused);
    }

    private static void DisposeSafely(IDisposable system, UnityEngine.Object context)
    {
        try { system.Dispose(); }
        catch (Exception exception) { Debug.LogException(exception, context); }
    }

    /// <summary>Gỡ mọi listener và dọn các lượt spawn còn lại khi scene kết thúc.</summary>
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        characters.CharacterAdded -= OnCharacterAdded;
        characters.CharacterRemoved -= OnCharacterRemoved;
        deaths.CharacterDied -= OnCharacterDied;
        foreach (CharacterView view in new List<CharacterView>(entries.Keys)) Release(view);
        tickSnapshot.Clear();
        FlushPendingEntries();
    }

    #endregion
}
