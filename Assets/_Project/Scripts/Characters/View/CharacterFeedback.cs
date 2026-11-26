using System;
using UnityEngine;

/// <summary>Thông số màu, nháy và VFX trạng thái của phản hồi hình ảnh. Giá trị đến từ field <c>[SerializeField]</c> trên prefab vỏ nhân vật.</summary>
public readonly struct CharacterFeedbackSettings
{
    public readonly float HitFlashDuration; // Thời gian nháy trắng sau một đòn trúng (giây).
    public readonly float HitWhiteBlend; // Mức pha trắng khi nháy (0..1).
    public readonly float StatusColorBlend; // Mức pha màu trạng thái lên màu gốc (0..1).
    public readonly Color PoisonColor, BurnColor, SlowColor;
    public readonly GameObject PoisonVfx, BurnVfx, SlowVfx; // VFX lặp bám trên nhân vật; null = chỉ đổi màu.

    public CharacterFeedbackSettings(float hitFlashDuration, float hitWhiteBlend, float statusColorBlend,
        Color poisonColor, Color burnColor, Color slowColor, GameObject poisonVfx, GameObject burnVfx, GameObject slowVfx)
    {
        HitFlashDuration = hitFlashDuration;
        HitWhiteBlend = hitWhiteBlend;
        StatusColorBlend = statusColorBlend;
        PoisonColor = poisonColor;
        BurnColor = burnColor;
        SlowColor = slowColor;
        PoisonVfx = poisonVfx;
        BurnVfx = burnVfx;
        SlowVfx = slowVfx;
    }
}

/// <summary>
/// Phản hồi hình ảnh của một nhân vật: nháy trắng khi trúng đòn, pha màu trạng thái (độc, cháy, slow)
/// và VFX lặp bám trên nhân vật khi trạng thái đang bật.
/// </summary>
/// <remarks>
/// Controller báo trạng thái qua <see cref="ICharacterController.FeedbackChanged"/>; class này chỉ quyết định phần hình.
/// Controller đã đếm số nguồn của mỗi loại nên chỉ báo khi chuyển giữa "không có" và "có": hai nguồn cháy cùng lúc
/// không tắt nhầm màu hay VFX.
/// Nhiều trạng thái cùng lúc được trộn với nhau; nháy trắng pha nhẹ lên màu hiện tại và giữ alpha gốc.
/// Do <see cref="CharacterView"/> sở hữu (composition); mỗi lượt spawn gọi <see cref="Bind"/> rồi <see cref="Clear"/>.
/// </remarks>
public sealed class CharacterFeedback
{
    #region State

    private readonly CharacterView owner;
    private readonly bool[] statusColors = new bool[4]; // Index = CharacterFeedbackKind; index 0 (Hit) không dùng.
    private readonly GameObject[] statusVfx = new GameObject[4]; // VFX đang bật, cùng index với statusColors.
    private IGameObjectPool pools; // Thuê/trả VFX trạng thái; giữ qua các lượt spawn để Clear luôn trả được.
    private CharacterFeedbackSettings settings;
    private ICharacterController source; // Controller đang được nghe; giữ riêng để gỡ đúng object khi dọn lượt spawn.
    private SpriteRenderer[] sprites;
    private Color[] originalColors; // Màu gốc trên prefab visual, để trả lại khi hết trạng thái.
    private float flashRemaining; // Mỗi lượt spawn giữ thời hạn nháy riêng, không cần lịch callback.
    private bool flashing;

    #endregion

    #region Lifecycle

    public CharacterFeedback(CharacterView owner) => this.owner = owner ?? throw new ArgumentNullException(nameof(owner));

    /// <summary>Bắt đầu một lượt spawn: chụp màu gốc của sprite và nghe controller.</summary>
    internal void Bind(CharacterFeedbackSettings feedbackSettings, SpriteRenderer[] spawnSprites, ICharacterController controller,
        IGameObjectPool objectPools)
    {
        settings = feedbackSettings;
        pools = objectPools;
        sprites = spawnSprites;
        originalColors = new Color[sprites.Length];
        for (int i = 0; i < sprites.Length; i++) originalColors[i] = sprites[i].color;
        source = controller;
        if (source != null) source.FeedbackChanged += OnFeedbackChanged;
    }

    /// <summary>Đếm lùi thời gian nháy. View gọi mỗi frame.</summary>
    internal void Tick(float deltaTime)
    {
        if (!flashing || deltaTime <= 0f) return;
        flashRemaining = Mathf.Max(0f, flashRemaining - deltaTime);
        if (flashRemaining <= 0f) StopFlash();
    }

    /// <summary>
    /// Kết thúc lượt spawn (chết, phát kết quả, thu hồi): gỡ controller, trả VFX trạng thái về pool,
    /// xóa trạng thái và trả sprite về màu gốc.
    /// Màu được trả trước khi quên màu gốc, để vỏ tái sử dụng không giữ màu của lượt trước.
    /// </summary>
    internal void Clear()
    {
        if (source != null) source.FeedbackChanged -= OnFeedbackChanged;
        source = null;
        for (int i = 1; i < statusVfx.Length; i++) ReleaseStatusVfx(i);
        Array.Clear(statusColors, 0, statusColors.Length);
        StopFlash();
        originalColors = null;
    }

    #endregion

    #region Colors

    /// <summary>Dừng nháy ngay (ví dụ khi vỏ tạm tắt) và áp lại màu trạng thái hiện tại.</summary>
    internal void StopFlash()
    {
        flashRemaining = 0f;
        flashing = false;
        Apply();
    }

    /// <summary>Áp màu hiện tại lên sprite. Gọi khi visual vừa hiện lại hoặc trạng thái đổi.</summary>
    internal void Apply()
    {
        if (sprites == null || originalColors == null) return;
        Color combined = new Color(0f, 0f, 0f, 0f);
        int count = 0;
        for (int i = 1; i < statusColors.Length; i++)
        {
            if (!statusColors[i]) continue;
            combined += StatusColor((CharacterFeedbackKind)i);
            count++;
        }
        Color tint = count > 0 ? Color.Lerp(Color.white, combined / count, settings.StatusColorBlend) : Color.white;
        for (int i = 0; i < sprites.Length; i++)
        {
            if (sprites[i] == null) continue;
            Color color = originalColors[i] * tint;
            if (flashing) color = Color.Lerp(color, Color.white, settings.HitWhiteBlend);
            color.a = originalColors[i].a;
            sprites[i].color = color;
        }
    }

    private void OnFeedbackChanged(CharacterFeedbackKind kind, bool active)
    {
        switch (kind)
        {
            case CharacterFeedbackKind.Hit:
                // Vỏ đang tắt thì không nháy: không có frame nào để đếm lùi và tắt nháy.
                if (!active || !owner.isActiveAndEnabled) return;
                flashRemaining = settings.HitFlashDuration;
                flashing = true;
                break;
            case CharacterFeedbackKind.Poison:
            case CharacterFeedbackKind.Burn:
            case CharacterFeedbackKind.Slow:
                statusColors[(int)kind] = active;
                SetStatusVfx(kind, active);
                break;
            default: return;
        }
        Apply();
    }

    private Color StatusColor(CharacterFeedbackKind kind)
    {
        switch (kind)
        {
            case CharacterFeedbackKind.Poison: return settings.PoisonColor;
            case CharacterFeedbackKind.Burn: return settings.BurnColor;
            case CharacterFeedbackKind.Slow: return settings.SlowColor;
            default: return Color.white;
        }
    }

    #endregion

    #region Status Vfx

    /// <summary>Cho VFX trạng thái bám theo tâm hitbox. View gọi ở LateTick, sau khi Simulation đã áp di chuyển.</summary>
    internal void LateTick()
    {
        for (int i = 1; i < statusVfx.Length; i++)
            if (statusVfx[i] != null) statusVfx[i].transform.position = owner.HitPosition;
    }

    /// <summary>
    /// Vỏ bị Destroy (thường là lúc hủy scene): chỉ bỏ tham chiếu. Không trả về pool từ callback hủy,
    /// vì pool và chính VFX có thể đang bị hủy cùng lúc.
    /// </summary>
    internal void ForgetStatusVfx() => Array.Clear(statusVfx, 0, statusVfx.Length);

    // VFX không làm con của vỏ: trạng thái có thể bật/tắt ngay trong OnEnable/OnDisable của vỏ (Combat dừng effect
    // khi vỏ tắt), lúc Unity cấm đổi cha của object con. Vì vậy VFX nằm trong pool và bám theo ở LateTick.
    private void SetStatusVfx(CharacterFeedbackKind kind, bool active)
    {
        int index = (int)kind;
        if (!active)
        {
            ReleaseStatusVfx(index);
            return;
        }
        GameObject prefab = StatusVfx(kind);
        if (statusVfx[index] != null || prefab == null || pools == null) return;
        statusVfx[index] = pools.Spawn(prefab, owner.HitPosition, Quaternion.identity, PoolGroup.Other);
    }

    // Xóa tham chiếu trước khi trả, để gọi lặp (callback lồng nhau) không trả cùng một VFX hai lần.
    private void ReleaseStatusVfx(int index)
    {
        GameObject previous = statusVfx[index];
        statusVfx[index] = null;
        if (previous != null) pools.Despawn(previous);
    }

    private GameObject StatusVfx(CharacterFeedbackKind kind)
    {
        switch (kind)
        {
            case CharacterFeedbackKind.Poison: return settings.PoisonVfx;
            case CharacterFeedbackKind.Burn: return settings.BurnVfx;
            case CharacterFeedbackKind.Slow: return settings.SlowVfx;
            default: return null;
        }
    }

    #endregion
}
