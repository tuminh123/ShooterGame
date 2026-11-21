using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Danh sách nhân vật đang hoạt động của miền Character: tick mọi nhân vật mỗi frame, phát sự kiện thêm/thu hồi/chết,
/// và điều phối animation ăn mừng của quái khi Player thua.
/// </summary>
/// <remarks>Do <see cref="CharacterManager"/> tạo; miền khác đọc qua <see cref="ICharacterRegistry"/> và <see cref="ICharacterDeathEvents"/>.</remarks>
public sealed class CharacterRegistry : IDisposable, ICharacterRegistry, ICharacterDeathEvents
{
    #region Frame Scheduling

    private readonly List<CharacterHandle> tickSnapshot = new List<CharacterHandle>(128);

    // Delegate tĩnh dùng lại mỗi frame, không cấp phát closure.
    private static readonly Action<CharacterView> TickStep = view => view.TickFrame(Time.deltaTime);
    private static readonly Action<CharacterView> LateTickStep = view => view.LateTickFrame();

    /// <summary>Tick mọi nhân vật (controller, feedback). CharacterManager gọi ở Tick.</summary>
    public void Tick() => ForEachCurrent(TickStep);

    /// <summary>Cập nhật animation visual của mọi nhân vật. CharacterManager gọi ở LateTick.</summary>
    public void LateTick() => ForEachCurrent(LateTickStep);

    // Chụp danh sách trước: một bước có thể spawn/thu hồi nhân vật khác (chết, triệu hồi, callback animation chết).
    // Handle giữ đúng lượt spawn, nên vỏ vừa bị thu hồi rồi spawn lại trong frame không bị gọi nhầm.
    private void ForEachCurrent(Action<CharacterView> step)
    {
        if (disposed) return;
        tickSnapshot.Clear();
        foreach (var view in characters.Keys)
            if (view != null && view.RuntimeData != null)
                tickSnapshot.Add(new CharacterHandle(view));
        try
        {
            foreach (CharacterHandle handle in tickSnapshot)
            {
                if (disposed) break;
                if (!handle.IsCurrent || !characters.ContainsKey(handle.View)) continue;
                try { step(handle.View); }
                catch (Exception exception) { Debug.LogException(exception, handle.View); }
            }
        }
        finally { tickSnapshot.Clear(); }
    }

    #endregion

    #region Character Registry & Events

    private readonly Dictionary<CharacterView, CharacterDataSO> characters = new Dictionary<CharacterView, CharacterDataSO>();
    private bool disposed;
    private int celebrationVersion;
    private int waitingCelebrations;
    public IReadOnlyDictionary<CharacterView, CharacterDataSO> Characters => characters;
    public event Action<CharacterView, CharacterDataSO> CharacterAdded;
    public event Action<CharacterView> CharacterRemoved;
    public event Action<CharacterView> CharacterDied;
    public event Action EnemyCelebrationsCompleted;

    #endregion

    #region Result Presentation

    /// <summary>
    /// Khi Player thua: mọi quái còn sống chạy animation Win. GameManager gọi hàm này
    /// sau khi đã chuyển sang EndingLose, và chờ <see cref="EnemyCelebrationsCompleted"/> trước khi hiện kết quả.
    /// </summary>
    public void PlayEnemyCelebrations()
    {
        if (disposed) return;

        // Tăng version để callback hoàn tất của lần ăn mừng trước (nếu còn treo) bị bỏ qua.
        int version = ++celebrationVersion;
        waitingCelebrations = 0;
        var snapshot = new List<CharacterView>(characters.Keys);
        foreach (CharacterView character in snapshot)
        {
            if (character == null || character.RuntimeData == null ||
                character.RuntimeData.Team != CharacterTeam.Enemy || !character.RuntimeData.IsAlive) continue;
            if (character.PlayResultAnimation(CharacterVisualState.Win, () => OnCelebrationCompleted(version)))
                waitingCelebrations++;
        }

        if (waitingCelebrations == 0) PublishCelebrationsCompleted(version);
    }

    // Bỏ callback thuộc lượt trình bày cũ.
    private void OnCelebrationCompleted(int version)
    {
        if (disposed || version != celebrationVersion || waitingCelebrations <= 0) return;
        waitingCelebrations--;
        if (waitingCelebrations == 0) PublishCelebrationsCompleted(version);
    }

    private void PublishCelebrationsCompleted(int version)
    {
        if (disposed || version != celebrationVersion) return;
        Action handlers = EnemyCelebrationsCompleted;
        if (handlers == null) return;
        foreach (Action handler in handlers.GetInvocationList())
        {
            try { handler(); }
            catch (Exception exception) { Debug.LogException(exception); }
        }
    }

    #endregion

    #region Registration & Cleanup

    public void Register(CharacterView character, CharacterDataSO definition)
    {
        if (disposed) throw new ObjectDisposedException(nameof(CharacterRegistry));
        if (character == null || character.RuntimeData == null || definition == null)
            throw new ArgumentException("Register an initialized character.");
        if (characters.ContainsKey(character)) return;
        characters.Add(character, definition);
        character.Released += Unregister;
        character.Died += OnDied;
        try { CharacterAdded?.Invoke(character, definition); }
        catch { Unregister(character); throw; }
    }

    public void Unregister(CharacterView character)
    {
        if (!characters.Remove(character)) return;
        character.Released -= Unregister;
        character.Died -= OnDied;
        CharacterRemoved?.Invoke(character);
    }

    private void OnDied(CharacterView character) => CharacterDied?.Invoke(character);

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        celebrationVersion++;
        foreach (CharacterView character in new List<CharacterView>(characters.Keys)) Unregister(character);
        CharacterAdded = null; CharacterRemoved = null; CharacterDied = null; EnemyCelebrationsCompleted = null;
    }

    #endregion
}
