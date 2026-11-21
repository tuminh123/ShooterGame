using System;
using UnityEngine;

/// <summary>Thông số màu và nháy của phản hồi hình ảnh. Giá trị đến từ field <c>[SerializeField]</c> trên prefab vỏ nhân vật.</summary>
public readonly struct CharacterFeedbackSettings
{
    public readonly float HitFlashDuration; // Thời gian nháy trắng sau một đòn trúng (giây).
    public readonly float HitWhiteBlend; // Mức pha trắng khi nháy (0..1).
    public readonly float StatusColorBlend; // Mức pha màu trạng thái lên màu gốc (0..1).
    public readonly Color PoisonColor, BurnColor, SlowColor;

    public CharacterFeedbackSettings(float hitFlashDuration, float hitWhiteBlend, float statusColorBlend,
        Color poisonColor, Color burnColor, Color slowColor)
    {
        HitFlashDuration = hitFlashDuration;
        HitWhiteBlend = hitWhiteBlend;
        StatusColorBlend = statusColorBlend;
        PoisonColor = poisonColor;
        BurnColor = burnColor;
        SlowColor = slowColor;
    }
}

/// <summary>
/// Phản hồi hình ảnh trên sprite của một nhân vật: nháy trắng khi trúng đòn, và pha màu trạng thái (độc, cháy, slow).
/// </summary>
/// <remarks>
/// Controller báo trạng thái qua <see cref="ICharacterController.FeedbackChanged"/>; class này chỉ quyết định màu.
/// Nhiều trạng thái cùng lúc được trộn với nhau; nháy trắng pha nhẹ lên màu hiện tại và giữ alpha gốc.
/// Do <see cref="CharacterView"/> sở hữu (composition); mỗi lượt spawn gọi <see cref="Bind"/> rồi <see cref="Clear"/>.
/// </remarks>
public sealed class CharacterFeedback
{
    #region State

    private readonly CharacterView owner;
    private readonly bool[] statusColors = new bool[4]; // Index = CharacterFeedbackKind; index 0 (Hit) không dùng.
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
    internal void Bind(CharacterFeedbackSettings feedbackSettings, SpriteRenderer[] spawnSprites, ICharacterController controller)
    {
        settings = feedbackSettings;
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
    /// Kết thúc lượt spawn: gỡ controller, xóa trạng thái và trả sprite về màu gốc.
    /// Màu được trả trước khi quên màu gốc, để vỏ tái sử dụng không giữ màu của lượt trước.
    /// </summary>
    internal void Clear()
    {
        if (source != null) source.FeedbackChanged -= OnFeedbackChanged;
        source = null;
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
}
