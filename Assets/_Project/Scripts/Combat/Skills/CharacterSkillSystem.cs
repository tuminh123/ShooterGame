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

    /// <summary>Bộ skill của một lượt spawn, kèm handle để nhận ra lượt spawn đó (vỏ có thể bị pool tái sử dụng).</summary>
    private sealed class Entry
    {
        public readonly CharacterHandle Handle;
        public readonly CharacterSkills Skills;
        public readonly Action Activated;   // Giữ delegate để gỡ đúng listener khi thu hồi.
        public readonly Action Deactivated;

        public Entry(CharacterHandle handle, CharacterSkills skills, Action activated, Action deactivated)
        {
            Handle = handle;
            Skills = skills;
            Activated = activated;
            Deactivated = deactivated;
        }
    }

    #endregion

    #region Dependencies And State

    private readonly ICharacterRegistry characters;
    private readonly ICharacterDeathEvents deaths;
    private readonly SkillFactory factory;
    private readonly SkillSystem gameplaySkills;
    private readonly EffectSystem effects;
    private readonly Dictionary<CharacterView, Entry> entries = new Dictionary<CharacterView, Entry>(128);
    private readonly List<Entry> tickSnapshot = new List<Entry>(128);
    private bool disposed;

    #endregion

    #region Constructor

    public CharacterSkillSystem(ICharacterRegistry characters, ICharacterDeathEvents deaths,
        SkillFactory factory, SkillSystem gameplaySkills, EffectSystem effects)
    {
        this.characters = characters ?? throw new ArgumentNullException(nameof(characters));
        this.deaths = deaths ?? throw new ArgumentNullException(nameof(deaths));
        this.factory = factory ?? throw new ArgumentNullException(nameof(factory));
        this.gameplaySkills = gameplaySkills ?? throw new ArgumentNullException(nameof(gameplaySkills));
        this.effects = effects ?? throw new ArgumentNullException(nameof(effects));
        characters.CharacterAdded += OnCharacterAdded;
        characters.CharacterRemoved += OnCharacterRemoved;
        deaths.CharacterDied += OnCharacterDied;
    }

    #endregion

    #region Lookup

    /// <summary>Context skill của lượt spawn hiện tại của <paramref name="owner"/>; null nếu nhân vật không còn hợp lệ.</summary>
    public CharacterSkills GetContext(CharacterView owner)
        => owner != null && entries.TryGetValue(owner, out Entry entry) && entry.Skills.IsValid ? entry.Skills : null;

    #endregion

    #region Frame Update

    /// <summary>Tick skill vốn có của mọi nhân vật. CombatManager gọi sau skill gameplay, trước đạn.</summary>
    public void Tick(float deltaTime)
    {
        if (disposed || deltaTime <= 0f) return;
        // Chụp danh sách trước: một đòn đánh có thể giết nhân vật khác và làm entry bị gỡ ngay trong vòng lặp.
        tickSnapshot.Clear();
        tickSnapshot.AddRange(entries.Values);
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
        finally { tickSnapshot.Clear(); }
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

        CharacterSkills skills = new CharacterSkills(view, template.Skills, factory);
        Action activated = () => SetPaused(view, false);
        Action deactivated = () => SetPaused(view, true);
        entries.Add(view, new Entry(new CharacterHandle(view), skills, activated, deactivated));
        view.Activated += activated;
        view.Deactivated += deactivated;
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
        foreach (Entry entry in new List<Entry>(entries.Values))
            DisposeSafely(entry.Skills, entry.Handle.View);
    }

    private void Release(CharacterView view)
    {
        if (!entries.TryGetValue(view, out Entry entry)) return;
        entries.Remove(view);
        view.Activated -= entry.Activated;
        view.Deactivated -= entry.Deactivated;
        EndCombat(view, entry);
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
    }

    #endregion
}
