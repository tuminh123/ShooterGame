using System;
using UnityEngine;

/// <summary>VFX hiển thị một object/sprite tĩnh trong một khoảng thời gian cố định rồi báo đã xong.</summary>
/// <remarks>Không cần Animator hay ParticleSystem. Không đổi tên class: prefab tham chiếu script qua GUID.</remarks>
public sealed class ObjectEffectVisual : MonoBehaviour, IEffectVisual
{
    #region Frame

    private int activationFrame = -1; // Frame bắt đầu phát; không trừ thời gian ngay trong frame đó.

    #endregion

    #region Playback & Duration

    [SerializeField, Min(0.01f)] private float duration = 0.2f;
    private float remaining;
    private bool playing;

    public void Play()
    {
        StopAndReset();
        if (!(duration > 0f) || float.IsInfinity(duration))
            throw new InvalidOperationException("Object effect duration must be positive and finite.");
        remaining = duration;
        playing = true;
        activationFrame = Time.frameCount;
        gameObject.SetActive(true);
    }

    /// <summary>
    /// CombatVfx gọi ở LateTick (qua EffectView): đếm ngược thời gian hiển thị, trả về true khi hết giờ.
    /// Pause (deltaTime = 0) thì đứng yên.
    /// </summary>
    public bool Advance(float deltaTime)
    {
        if (!playing) return true;
        if (!isActiveAndEnabled || Time.frameCount <= activationFrame || deltaTime <= 0f) return false;
        remaining = Mathf.Max(0f, remaining - deltaTime);
        if (remaining > 0f) return false;
        StopAndReset();
        return true;
    }

    // Tắt/thu hồi: ẩn và xóa thời gian còn lại, lần lấy ra pool sau bắt đầu lại từ đầu.
    public void StopAndReset()
    {
        playing = false;
        remaining = 0f;
        gameObject.SetActive(false);
    }

    private void OnDisable() => StopAndReset();
    private void OnDestroy() => StopAndReset();

    #endregion
}
