using UnityEngine;

// Animator Controller trên prefab chứa animation VFX; không tham gia logic gameplay.
[RequireComponent(typeof(Animator))]
public sealed class AnimationEffectVisual : MonoBehaviour, IEffectVisual
{
    #region Animation State

    private Animator animator; // State mặc định của controller là animation cần phát.

    #endregion

    #region Playback

    // Rebind đưa controller về trạng thái mặc định thay vì tiếp tục animation của lần thuê trước.
    public void Play()
    {
        if (animator == null) animator = GetComponent<Animator>();
        animator.enabled = true;
        animator.Rebind(); // Reset state và parameter trước khi phát lại từ đầu.
        animator.Update(0f);
    }

    // Reset controller rồi tắt cập nhật Animator; EffectView vẫn quyết định tuổi thọ của vỏ.
    public void StopAndReset()
    {
        if (animator == null) animator = GetComponent<Animator>();
        animator.Rebind();
        animator.enabled = false; // Play sẽ khởi động lại khi tái sử dụng.
    }

    #endregion

    #region Disable Cleanup

    // Dọn cả khi hierarchy bị tắt trực tiếp mà không đi qua EffectView.Clear.
    private void OnDisable() => StopAndReset();

    #endregion
}
