using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Khung hiển thị nhân vật UI (<see cref="CharacterUIVisual"/>, đứng Idle) vừa một Image có sẵn; thiếu prefab thì hiện ảnh tĩnh.
/// Lớp thuần dùng chung cho màn Detail và Home, để logic tạo/co/đặt nhân vật chỉ nằm một chỗ.
/// </summary>
public sealed class CharacterPreviewSlot
{
    #region State

    private readonly Image frame; // Khung và cũng là ảnh dự phòng.
    // Mỗi prefab chỉ tạo một lần rồi bật/tắt khi đổi nhân vật, tránh Instantiate lại.
    private readonly Dictionary<CharacterUIVisual, CharacterUIVisual> instances = new Dictionary<CharacterUIVisual, CharacterUIVisual>();

    /// <summary>Nhân vật UI đang hiện; null khi đang dùng ảnh tĩnh.</summary>
    public CharacterUIVisual Current { get; private set; }

    #endregion

    /// <summary>Dùng <paramref name="frame"/> làm khung đặt nhân vật.</summary>
    public CharacterPreviewSlot(Image frame) => this.frame = frame;

    #region Public API

    /// <summary>
    /// Hiện nhân vật; chỉ chạy lại Idle khi đổi sang nhân vật khác, các lần vẽ lại cùng nhân vật giữ nguyên animation.
    /// </summary>
    public void Show(CharacterUIVisual prefab, Sprite fallbackPortrait)
    {
        CharacterUIVisual next = prefab != null ? GetOrCreate(prefab) : null;
        if (next != Current)
        {
            if (Current != null) Current.gameObject.SetActive(false);
            Current = next;
            if (Current != null)
            {
                Current.gameObject.SetActive(true);
                Current.PlayIdle();
            }
        }
        // Tắt component Image (không tắt GameObject) để nhân vật UI con vẫn hiện; ảnh tĩnh chỉ là dự phòng.
        frame.enabled = Current == null;
        if (Current == null && fallbackPortrait != null) frame.sprite = fallbackPortrait;
    }

    /// <summary>Phát Victory một lần rồi tự về Idle; không có nhân vật UI thì bỏ qua.</summary>
    public void Celebrate()
    {
        if (Current != null) Current.Celebrate();
    }

    #endregion

    #region Helpers

    private CharacterUIVisual GetOrCreate(CharacterUIVisual prefab)
    {
        if (instances.TryGetValue(prefab, out CharacterUIVisual instance) && instance != null) return instance;
        instance = Object.Instantiate(prefab, frame.rectTransform, false);
        Fit((RectTransform)instance.transform);
        instances[prefab] = instance;
        return instance;
    }

    // Co nhân vật vừa khung và đặt giữa khung; pivot của prefab nằm ở chân nên phải bù độ lệch so với tâm.
    private void Fit(RectTransform character)
    {
        Rect area = frame.rectTransform.rect;
        Vector2 size = character.sizeDelta;
        float scale = Mathf.Min(area.width / Mathf.Max(1f, size.x), area.height / Mathf.Max(1f, size.y));
        character.anchorMin = character.anchorMax = new Vector2(0.5f, 0.5f);
        character.localScale = Vector3.one * scale;
        character.anchoredPosition = (character.pivot - new Vector2(0.5f, 0.5f)) * size * scale;
    }

    #endregion
}
