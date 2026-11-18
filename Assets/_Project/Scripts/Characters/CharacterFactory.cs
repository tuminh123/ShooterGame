using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Ghép template, runtime, controller và vỏ thành một nhân vật tại một chỗ duy nhất, và thu hồi ngược lại.
/// Player và quái dùng chính xác cùng một prefab vỏ và cùng pool.
/// </summary>
/// <remarks>Do <see cref="CharacterManager"/> tạo; miền khác dùng qua <see cref="ICharacterSpawn"/>/<see cref="ICharacterDespawn"/>.</remarks>
public sealed class CharacterFactory : IDisposable, ICharacterSpawn, ICharacterDespawn
{
    #region Dependencies And State

    private readonly CharacterView prefab; // * Prefab vỏ duy nhất do GameSceneInstaller truyền vào.
    private readonly IGameObjectPool pools; // * Chuẩn bị clone dưới nhánh tắt trước khi cho OnEnable chạy.
    private readonly CharacterRegistry characters;
    private readonly CharacterRuntimePool runtimes;
    private readonly CharacterContext context; // Dịch vụ truyền vào mọi vỏ lúc spawn.
    private readonly HashSet<CharacterView> activeCharacters = new HashSet<CharacterView>(); // * Chỉ thu hồi các cá thể do factory này tạo, kể cả lúc đóng scene.
    private bool disposed; // * Ngăn tạo thêm nhân vật khi scene đang giải phóng dịch vụ.

    #endregion

    #region Initialization

    public CharacterFactory(CharacterView prefab, IGameObjectPool pools, CharacterRegistry characters, CharacterContext context)
    {
        this.prefab = prefab;
        this.pools = pools ?? throw new ArgumentNullException(nameof(pools));
        this.characters = characters ?? throw new ArgumentNullException(nameof(characters));
        this.context = context ?? throw new ArgumentNullException(nameof(context));
        runtimes = context.Runtimes;
    }

    #endregion

    #region Creation

    /// <inheritdoc/>
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

    // Overload nội bộ: runtime đã được thuê sẵn từ pool.
    private CharacterView Spawn(
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
                    view.Initialize(context, template, runtime, controller);
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

    /// <summary>
    /// Thu hồi mọi nhân vật do factory này tạo: Player, quái, boss, kể cả nhân vật đang chạy animation chết.
    /// Duyệt danh sách của factory (nguồn đầy đủ nhất), không duyệt registry. Một nhân vật lỗi không chặn các nhân vật còn lại.
    /// </summary>
    public void DespawnAll()
    {
        // Despawn sửa activeCharacters (và có thể gọi lồng nhau qua callback), nên duyệt bản sao.
        var remaining = new CharacterView[activeCharacters.Count];
        activeCharacters.CopyTo(remaining);
        foreach (CharacterView character in remaining)
        {
            try { Despawn(character); }
            catch (Exception exception) { Debug.LogException(exception); }
        }
    }

    // * Installer gọi trước PoolManager.Dispose để visual con luôn được trả trước khi vỏ bị tắt.
    public void Dispose()
    {
        if (disposed)
            return;
        disposed = true;
        DespawnAll();
        activeCharacters.Clear();
    }

    #endregion
}
