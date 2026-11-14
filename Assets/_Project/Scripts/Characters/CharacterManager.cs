using System;
using System.Collections.Generic;
using UnityEngine;
using Zenject;

// Theo dõi nhân vật đang sống và phát animation ăn mừng khi trận thua.
public sealed class CharacterManager : IDisposable, ITickable
{
    #region Frame Scheduling

    private readonly List<(CharacterView View, CharacterRuntimeData Runtime, ulong Generation)> tickSnapshot =
        new List<(CharacterView, CharacterRuntimeData, ulong)>(128);

    // Dùng registry sẵn có; snapshot giữ đúng lượt spawn khi callback sinh/thu hồi nhân vật.
    public void Tick()
    {
        if (disposed) return;
        tickSnapshot.Clear();
        foreach (var view in characters.Keys)
            if (view != null && view.RuntimeData != null)
                tickSnapshot.Add((view, view.RuntimeData, view.RuntimeData.Generation));
        try
        {
            foreach (var entry in tickSnapshot)
            {
                if (disposed) break;
                if (entry.View == null || !characters.ContainsKey(entry.View) ||
                    !ReferenceEquals(entry.View.RuntimeData, entry.Runtime) || entry.Runtime.Generation != entry.Generation) continue;
                try { entry.View.TickFrame(Time.deltaTime); }
                catch (Exception exception) { Debug.LogException(exception, entry.View); }
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

    public CharacterManager()
    {
        GameUIEventBus.StateChanged += OnGameStateChanged;
    }

    #endregion

    #region Result Presentation

    // Khi thua, giữ quái còn sống và yêu cầu visual phát animation Win.
    private void OnGameStateChanged(GameState previous, GameState current)
    {
        if (disposed || current != GameState.EndingLose) return;

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
        if (disposed) throw new ObjectDisposedException(nameof(CharacterManager));
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
        GameUIEventBus.StateChanged -= OnGameStateChanged;
        foreach (CharacterView character in new List<CharacterView>(characters.Keys)) Unregister(character);
        CharacterAdded = null; CharacterRemoved = null; CharacterDied = null; EnemyCelebrationsCompleted = null;
    }
    #endregion
}
