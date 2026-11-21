using System;
using UnityEngine;

/// <summary>
/// Phần trình bày của một nhân vật: hướng quay, visual state (đứng, chạy, trúng đòn, chết, thắng/thua…),
/// bật/tắt visual con theo camera, và bounds để kiểm tra camera.
/// </summary>
/// <remarks>
/// Gameplay chỉ phát yêu cầu bằng enum <see cref="CharacterVisualState"/>; <see cref="CharacterVisualHandle"/> trên prefab
/// visual nghe các event ở đây và tự chọn animation, nên view không biết tên state Animator của từng prefab.
/// Do <see cref="CharacterView"/> sở hữu (composition); mỗi lượt spawn gọi <see cref="Begin"/> rồi <see cref="End"/>.
/// </remarks>
public sealed class CharacterPresentation
{
    #region State

    private readonly CharacterView owner;
    private readonly CharacterFeedback feedback; // Áp lại màu khi visual vừa hiện.
    private GameObject visual;
    private SpriteRenderer[] sprites;
    private bool resultLocked; // Đang phát thắng/thua: chỉ nhận state kết quả, visual luôn hiện.
    private bool deathLocked; // Boss đang phát animation chết: visual luôn hiện tới khi xong.

    #endregion

    #region Public State And Events

    /// <summary>Trạng thái di chuyển hiện tại (Idle hoặc Move); visual dùng làm nền khi không có hành động nào.</summary>
    public CharacterVisualState MovementVisualState { get; private set; } = CharacterVisualState.Idle;

    /// <summary>Nhân vật đang quay mặt sang phải.</summary>
    public bool FacingRight { get; private set; } = true;

    /// <summary>Lượt spawn hiện tại còn sống (visual dùng để chọn Death khi bind giữa chừng).</summary>
    public bool IsAlive => owner.RuntimeData != null && owner.RuntimeData.IsAlive;

    /// <summary>Visual con đang hiện.</summary>
    public bool IsVisible => visual != null && visual.activeSelf;

    /// <summary>Đang phát animation thắng/thua; gameplay của nhân vật đã bị khóa.</summary>
    public bool IsResultLocked => resultLocked;

    /// <summary>Gameplay yêu cầu một visual state; <see cref="CharacterVisualHandle"/> chọn animation tương ứng.</summary>
    public event Action<CharacterVisualState> VisualStateRequested;

    /// <summary>Hướng quay đổi (true = phải).</summary>
    public event Action<bool> FacingChanged;

    /// <summary>Visual con vừa hiện hoặc ẩn (ví dụ quái ra/vào camera). Thanh máu của boss nghe để ẩn theo.</summary>
    public event Action<bool> VisibilityChanged;

    #endregion

    #region Lifecycle

    public CharacterPresentation(CharacterView owner, CharacterFeedback feedback)
    {
        this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
        this.feedback = feedback ?? throw new ArgumentNullException(nameof(feedback));
    }

    /// <summary>Bắt đầu một lượt spawn với visual con vừa thuê. Phải gọi trước khi <see cref="CharacterVisualHandle"/> bind.</summary>
    internal void Begin(GameObject spawnVisual, SpriteRenderer[] spawnSprites)
    {
        visual = spawnVisual;
        sprites = spawnSprites;
        resultLocked = false;
        deathLocked = false;
        MovementVisualState = CharacterVisualState.Idle;
        FacingRight = true;
    }

    /// <summary>Kết thúc lượt spawn: quên visual (bên sở hữu tự trả về pool) và gỡ mọi listener.</summary>
    internal void End()
    {
        visual = null;
        sprites = null;
        deathLocked = false;
        VisualStateRequested = null;
        FacingChanged = null;
        VisibilityChanged = null;
    }

    /// <summary>Khóa ở trạng thái kết quả thắng/thua.</summary>
    internal void LockForResult() => resultLocked = true;

    /// <summary>Boss: giữ visual hiện suốt animation chết, kể cả khi ra khỏi camera.</summary>
    internal void LockForDeath()
    {
        deathLocked = true;
        SetVisible(true);
    }

    #endregion

    #region Facing And Visual State

    /// <summary>Quay mặt theo hướng ngang. Bỏ qua hướng gần 0 để không giật qua lại khi đứng yên.</summary>
    internal void Face(float horizontalDirection)
    {
        if (Mathf.Abs(horizontalDirection) <= 0.01f || !IsAlive) return;
        bool facingRight = horizontalDirection > 0f;
        if (FacingRight == facingRight) return;
        FacingRight = facingRight;
        try { FacingChanged?.Invoke(facingRight); }
        catch (Exception exception) { Debug.LogException(exception, owner); }
    }

    /// <summary>Cập nhật Idle/Move theo lệnh di chuyển của frame. Chỉ phát yêu cầu khi trạng thái thật sự đổi.</summary>
    internal void UpdateLocomotion(Vector2 direction, float speed, float maxDistance)
    {
        if (!IsAlive) return;
        CharacterVisualState next = speed > 0.0001f && direction.sqrMagnitude > 0.000001f && maxDistance > 0.0001f
            ? CharacterVisualState.Move : CharacterVisualState.Idle;
        if (MovementVisualState == next) return;
        MovementVisualState = next;
        RequestVisualState(next);
    }

    /// <summary>Yêu cầu một visual state; các luật lọc bên dưới đảm bảo gameplay không phát state không hợp lệ.</summary>
    public void RequestVisualState(CharacterVisualState state)
    {
        CharacterRuntimeData runtime = owner.RuntimeData;
        if (runtime == null) return;
        bool resultState = state == CharacterVisualState.Win || state == CharacterVisualState.Lose;
        // Chỉ Player có Win/Lose; quái chỉ có Win (ăn mừng khi Player thua).
        if (resultState && runtime.Team != CharacterTeam.Player &&
            !(runtime.Team == CharacterTeam.Enemy && state == CharacterVisualState.Win)) return;
        if (resultLocked && !resultState) return;
        if (!runtime.IsAlive && state != CharacterVisualState.Death && state != CharacterVisualState.Lose) return;
        // Player và quái thường chỉ có di chuyển/trúng đòn/chết. Attack/Cast dành riêng cho boss.
        if (!owner.IsBoss && (state == CharacterVisualState.Attack || state == CharacterVisualState.Cast)) return;
        try { VisualStateRequested?.Invoke(state); }
        catch (Exception exception) { Debug.LogException(exception, owner); }
    }

    #endregion

    #region Visibility And Bounds

    /// <summary>
    /// Bật/tắt visual con (ví dụ theo camera). Vỏ vẫn chạy AI, di chuyển và nhận sát thương bình thường.
    /// Đang phát kết quả hoặc boss đang chết thì luôn hiện.
    /// </summary>
    public void SetVisible(bool visible)
    {
        if (resultLocked || deathLocked) visible = true;
        if (visual == null || visual.activeSelf == visible) return;
        visual.SetActive(visible);
        if (visible) feedback.Apply(); // Sprite có thể đã đổi trạng thái màu trong lúc ẩn.
        VisibilityChanged?.Invoke(visible);
    }

    /// <summary>
    /// Bounds world của mọi sprite, tính từ local bounds và Transform hiện tại, nên vẫn đúng kể cả khi visual
    /// đang tắt (lúc đó Renderer.bounds chưa được cập nhật).
    /// </summary>
    public Bounds GetBounds()
    {
        Bounds combined = new Bounds(owner.transform.position, Vector3.zero);
        if (sprites == null) return combined;
        bool hasSprite = false;
        foreach (SpriteRenderer sprite in sprites)
        {
            if (sprite == null || sprite.sprite == null) continue;
            Bounds local = sprite.localBounds;
            Matrix4x4 matrix = sprite.transform.localToWorldMatrix;
            Vector3 x = matrix.MultiplyVector(new Vector3(local.extents.x, 0f, 0f));
            Vector3 y = matrix.MultiplyVector(new Vector3(0f, local.extents.y, 0f));
            Vector3 z = matrix.MultiplyVector(new Vector3(0f, 0f, local.extents.z));
            Vector3 extent = new Vector3(Mathf.Abs(x.x) + Mathf.Abs(y.x) + Mathf.Abs(z.x),
                Mathf.Abs(x.y) + Mathf.Abs(y.y) + Mathf.Abs(z.y), Mathf.Abs(x.z) + Mathf.Abs(y.z) + Mathf.Abs(z.z));
            Bounds world = new Bounds(matrix.MultiplyPoint3x4(local.center), extent * 2f);
            if (hasSprite) combined.Encapsulate(world);
            else
            {
                combined = world;
                hasSprite = true;
            }
        }
        return combined;
    }

    #endregion
}
