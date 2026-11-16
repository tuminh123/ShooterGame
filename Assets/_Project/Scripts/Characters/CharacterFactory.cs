using System;
using System.Collections.Generic;
using UnityEngine;

// * Ghép dữ liệu, hành vi và vỏ tại một chỗ; Player/Enemy dùng chính xác cùng prefab và pool của vỏ.
public sealed class CharacterFactory : IDisposable, ICharacterFactory, ICharacterSpawn, ICharacterDespawn
{

    #region Dependencies And State
    private readonly CharacterView prefab; // * Prefab vỏ duy nhất do GameSceneInstaller truyền vào.
    private readonly IGameObjectPool pools; // * Chuẩn bị clone dưới nhánh tắt trước khi cho OnEnable chạy.
    private readonly ICharacterRegistration characters;
    private readonly ICharacterRuntimeLease runtimes;
    private readonly HashSet<CharacterView> activeCharacters = new HashSet<CharacterView>(); // * Chỉ thu hồi các cá thể do factory này tạo, kể cả lúc đóng scene.
    private bool disposed; // * Ngăn tạo thêm nhân vật khi scene đang giải phóng dịch vụ.

    #endregion

    #region Initialization

    public CharacterFactory(
        CharacterView prefab,
        IPoolSystem poolSystem,
        ICharacterSystem characterSystem,
        ICharacterRuntimePool characterRuntimePool)
    {
        this.prefab = prefab;
        this.pools = poolSystem.GetGameObjectPool();
        this.characters = characterSystem.GetCharacterRegistration();
        this.runtimes = characterRuntimePool.GetCharacterRuntimeLease();
    }

    #endregion

    #region Creation

    public CharacterView Spawn(
        CharacterDataSO template,
        StatBonus bonus,
        ICharacterController controller,
        Vector3 position)
    {
        if (disposed) throw new ObjectDisposedException(nameof(CharacterFactory));
        CharacterRuntimeData runtime = runtimes.Rent(template, bonus);
        ulong generation = runtime.Generation;
        try
        {
            CharacterView character = Spawn(template, runtime, controller, position);
            if (character == null) runtimes.TryReturn(runtime, generation);
            return character;
        }
        catch { runtimes.TryReturn(runtime, generation); throw; }
    }

    public CharacterView Spawn(
        CharacterDataSO template,
        CharacterRuntimeData runtime,
        ICharacterController controller,
        Vector3 position)
    {
        if (disposed)
            throw new ObjectDisposedException(nameof(CharacterFactory));
        if (template == null || prefab == null)
            throw new InvalidOperationException("Assign character data and the shared Character prefab.");
        PoolGroup group = template.Team == CharacterTeam.Enemy ? PoolGroup.Enemies : PoolGroup.Other;
        GameObject shell = pools.Spawn(prefab.gameObject, position, Quaternion.identity, group,
            beforeActivate: clone =>
            {
                clone.transform.localScale = prefab.transform.localScale;
                CharacterView view = clone.GetComponent<CharacterView>();
                try
                {
                    view.Initialize(template, runtime, controller);
                }
                catch
                {
                    // * Vỏ chưa bật nên OnDisable có thể chưa chạy; dọn cả visual/trạng thái chuẩn bị dở.
                    // * Đặt xử lý riêng của nhân vật tại factory để IGameObjectPool tiếp tục dùng chung cho mọi object.
                    view.ClearSpawn();
                    throw;
                }
            });
        if (shell == null)
            return null;
        CharacterView character = shell.GetComponent<CharacterView>();
        activeCharacters.Add(character);
        character.Released += OnReleased;
        try { characters.Register(character, template); }
        catch { Despawn(character); throw; }
        return character;
    }

    #endregion

    #region Cleanup

    // * Một đầu mối thu hồi cả nhân vật: ngừng hành vi → trả visual → báo Released → trả vỏ.
    // * Xóa quyền sở hữu trước callback để gọi Despawn lặp/reentrant không thu hồi cùng vỏ hai lần.
    public void Despawn(CharacterView character)
    {
        if (character == null || !activeCharacters.Remove(character))
            return;
        character.Released -= OnReleased;
        try
        {
            try { characters.Unregister(character); }
            finally { character.ClearSpawn(); }
        }
        finally
        {
            pools.Despawn(character.gameObject);
        }
    }

    #endregion

    #region Runtime Operations

    private void OnReleased(CharacterView character)
    {
        activeCharacters.Remove(character);
        character.Released -= OnReleased;
    }

    #endregion

    #region Cleanup

    // * Installer gọi trước PoolManager.Dispose để visual con luôn được trả trước khi vỏ bị tắt.
    public void Dispose()
    {
        if (disposed)
            return;
        disposed = true;
        var remaining = new CharacterView[activeCharacters.Count];
        activeCharacters.CopyTo(remaining);
        foreach (CharacterView character in remaining)
            Despawn(character);
        activeCharacters.Clear();
    }

    #endregion

    #region Facade Access

    // Các getter trả về vai trò của cùng instance; không tạo thêm service.
    public ICharacterSpawn GetCharacterSpawn() => this;

    public ICharacterDespawn GetCharacterDespawn() => this;

    #endregion
}
