using System;
using System.Collections.Generic;
using UnityEngine;

// Theo dõi nhân vật đang sống và phát animation ăn mừng khi trận thua.
public sealed class CharacterManager : IDisposable
{
    private readonly Dictionary<CharacterView, CharacterDataSO> characters = new Dictionary<CharacterView, CharacterDataSO>();
    private bool disposed;
    private int celebrationVersion;
    private int waitingCelebrations;
    public IReadOnlyDictionary<CharacterView, CharacterDataSO> Characters => characters;
    public event Action<CharacterView, CharacterDataSO> CharacterAdded;
    public event Action<CharacterView> CharacterRemoved;
    public event Action<CharacterView> CharacterDied;
    public event Action EnemyCelebrationsCompleted;

    public CharacterManager() => GameUIEventBus.StateChanged += OnGameStateChanged;

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
}
