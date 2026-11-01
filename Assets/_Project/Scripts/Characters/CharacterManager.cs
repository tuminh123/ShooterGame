using System;
using System.Collections.Generic;

// Character lifetime registry. It knows nothing about skills or shooting.
public sealed class CharacterManager : IDisposable
{
    private readonly Dictionary<CharacterView, CharacterDataSO> characters = new Dictionary<CharacterView, CharacterDataSO>();
    private bool disposed;
    public IReadOnlyDictionary<CharacterView, CharacterDataSO> Characters => characters;
    public event Action<CharacterView, CharacterDataSO> CharacterAdded;
    public event Action<CharacterView> CharacterRemoved;
    public event Action<CharacterView> CharacterDied;

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
        foreach (CharacterView character in new List<CharacterView>(characters.Keys)) Unregister(character);
        CharacterAdded = null; CharacterRemoved = null; CharacterDied = null;
    }
}
