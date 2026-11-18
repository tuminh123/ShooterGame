using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Bật/tắt visual của quái theo camera gameplay để giảm chi phí render; không tắt vỏ, AI, di chuyển hay hitbox.</summary>
/// <remarks>Do <see cref="CharacterManager"/> tạo; không bind vào Zenject.</remarks>
public sealed class EnemyVisibility : IDisposable
{
    #region Dependencies & Buffers
    private readonly ICharacterRegistry characters; // Nguồn nhân vật hiện có trong scene.
    private readonly Plane[] cameraPlanes = new Plane[6]; // Buffer giới hạn camera, dùng lại mỗi frame.
    private readonly List<CharacterHandle> snapshot = new List<CharacterHandle>(128); // Chốt đúng lượt spawn khi bật/tắt visual gây callback.
    private bool disposed; // Ngăn callback camera sau khi scene được dọn.

    public EnemyVisibility(ICharacterRegistry characters)
    {
        this.characters = characters ?? throw new ArgumentNullException(nameof(characters));
        Camera.onPreCull += UpdateVisibility;
    }

    #endregion

    #region Camera Visibility

    // Built-in pipeline: chạy sau movement/camera follow và trước khi camera gameplay render.
    private void UpdateVisibility(Camera renderingCamera)
    {
        if (disposed || renderingCamera.cameraType != CameraType.Game || renderingCamera != Camera.main) return;
        GeometryUtility.CalculateFrustumPlanes(renderingCamera, cameraPlanes);
        snapshot.Clear();
        foreach (var entry in characters.Characters)
            if (entry.Key != null && entry.Key.RuntimeData != null && entry.Key.RuntimeData.Team == CharacterTeam.Enemy)
                snapshot.Add(new CharacterHandle(entry.Key));

        try
        {
            for (int index = 0; index < snapshot.Count; index++)
            {
                if (!snapshot[index].TryGetCharacter(out CharacterView enemy)) continue;
                bool visible = GeometryUtility.TestPlanesAABB(cameraPlanes, enemy.Presentation.GetBounds());
                enemy.Presentation.SetVisible(visible);
            }
        }
        finally { snapshot.Clear(); } // Giữ dung lượng, không giữ tham chiếu enemy đã dùng xong.
    }

    #endregion

    #region Cleanup

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        Camera.onPreCull -= UpdateVisibility;
        snapshot.Clear();
    }

    #endregion
}
