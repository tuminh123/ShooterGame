using System;
using System.Collections.Generic;
using UnityEngine;

// * Ghép dữ liệu, hành vi và vỏ tại một chỗ; Player/Enemy dùng chính xác cùng prefab và pool của vỏ.
public sealed class CharacterFactory : IDisposable
{
    private readonly CharacterView prefab; // * Prefab vỏ duy nhất do GameSceneInstaller truyền vào.
    private readonly PoolManager pools; // * Chuẩn bị clone dưới nhánh tắt trước khi cho OnEnable chạy.
    private readonly HashSet<CharacterView> activeCharacters = new HashSet<CharacterView>(); // * Chỉ thu hồi các cá thể do factory này tạo, kể cả lúc đóng scene.
    private bool disposed; // * Ngăn tạo thêm nhân vật khi scene đang giải phóng dịch vụ.

    public CharacterFactory(CharacterView prefab, PoolManager pools)
    {
        this.prefab = prefab;
        this.pools = pools;
    }

    public CharacterView Spawn(CharacterDataSO template, CharacterRuntimeData runtime,
        ICharacterController controller, Vector3 position)
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
                    // * Đặt xử lý riêng của nhân vật tại factory để PoolManager tiếp tục dùng chung cho mọi object.
                    view.ClearSpawn();
                    throw;
                }
            });
        if (shell == null)
            return null;
        CharacterView character = shell.GetComponent<CharacterView>();
        activeCharacters.Add(character);
        character.Released += OnReleased;
        return character;
    }

    // * Một đầu mối thu hồi cả nhân vật: ngừng hành vi → trả visual → báo Released → trả vỏ.
    // * Xóa quyền sở hữu trước callback để gọi Despawn lặp/reentrant không thu hồi cùng vỏ hai lần.
    public void Despawn(CharacterView character)
    {
        if (character == null || !activeCharacters.Remove(character))
            return;
        character.Released -= OnReleased;
        try
        {
            character.ClearSpawn();
        }
        finally
        {
            pools.Despawn(character.gameObject);
        }
    }

    private void OnReleased(CharacterView character)
    {
        activeCharacters.Remove(character);
        character.Released -= OnReleased;
    }

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
}
