using System;
using System.Collections.Generic;
using PrimeTween;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Lớp icon bay: bung ra quanh điểm xuất phát rồi bay cong nối đuôi nhau tới đích. Icon được tái dùng.</summary>
public sealed class RewardFlyView : MonoBehaviour
{
    #region Settings

    [Tooltip("Icon mẫu đang tắt, nằm trong lớp này.")]
    [SerializeField] private Image template;
    [SerializeField, Min(0f)] private float burstRadius = 110f;
    [SerializeField, Min(0.01f)] private float burstDuration = 0.25f;
    [SerializeField, Min(0.01f)] private float flyDuration = 0.5f;
    [SerializeField, Min(0f)] private float stagger = 0.04f;
    [Tooltip("Độ cong của đường bay, theo tỉ lệ quãng đường.")]
    [SerializeField, Range(0f, 1f)] private float curve = 0.25f;

    #endregion

    #region State

    private readonly List<Image> free = new List<Image>();
    private readonly List<Image> flying = new List<Image>();
    private readonly List<Sequence> sequences = new List<Sequence>();

    #endregion

    #region Public API

    /// <summary>Bay <paramref name="count"/> icon; <paramref name="onArrive"/> chạy cho từng icon khi tới đích.</summary>
    public void Fly(Sprite sprite, Vector3 worldFrom, RectTransform target, int count, float size, Action onArrive)
    {
        Vector2 from = transform.InverseTransformPoint(worldFrom);
        Vector2 to = transform.InverseTransformPoint(target.TransformPoint(target.rect.center));
        for (int i = 0; i < count; i++)
        {
            Vector2 burst = count > 1
                ? from + UnityEngine.Random.insideUnitCircle * burstRadius
                : from + Vector2.up * (burstRadius * 0.5f); // Một món thì nhảy lên trước khi bay.
            Launch(sprite, from, burst, to, size, i * stagger, onArrive);
        }
    }

    /// <summary>Dừng mọi icon, không gọi callback còn lại.</summary>
    public void StopAll()
    {
        foreach (Sequence sequence in sequences) sequence.Stop();
        sequences.Clear();
        for (int i = flying.Count - 1; i >= 0; i--) Release(flying[i]);
    }

    #endregion

    #region Playback

    private void Launch(Sprite sprite, Vector2 from, Vector2 burst, Vector2 to, float size, float delay, Action onArrive)
    {
        Image image = Rent();
        image.sprite = sprite;
        RectTransform rect = image.rectTransform;
        rect.sizeDelta = new Vector2(size, size);
        rect.localScale = Vector3.one;
        rect.localPosition = from;

        // Điểm điều khiển lệch ngang so với đường thẳng, bên nào ngẫu nhiên để các icon tỏa ra.
        Vector2 path = to - burst;
        float side = UnityEngine.Random.value < 0.5f ? -1f : 1f;
        Vector2 control = burst + path * 0.5f + new Vector2(-path.y, path.x) * (curve * side * UnityEngine.Random.Range(0.4f, 1f));
        sequences.Add(Sequence.Create(useUnscaledTime: true) // Home pause (timeScale = 0).
            .Chain(Tween.Custom(0f, 1f, burstDuration, t => rect.localPosition = Vector2.LerpUnclamped(from, burst, t), Ease.OutCubic))
            .ChainDelay(delay)
            .Chain(Tween.Custom(0f, 1f, flyDuration, t =>
            {
                float inverse = 1f - t;
                rect.localPosition = inverse * inverse * burst + 2f * inverse * t * control + t * t * to;
                rect.localScale = Vector3.one * Mathf.Lerp(1f, 0.5f, t);
            }, Ease.InOutCubic))
            .ChainCallback(() =>
            {
                Release(image);
                onArrive();
            }));
    }

    private Image Rent()
    {
        Image image;
        if (free.Count > 0)
        {
            image = free[free.Count - 1];
            free.RemoveAt(free.Count - 1);
        }
        else image = Instantiate(template, transform);
        image.gameObject.SetActive(true);
        flying.Add(image);
        return image;
    }

    private void Release(Image image)
    {
        image.gameObject.SetActive(false);
        flying.Remove(image);
        free.Add(image);
    }

    private void OnDisable() => StopAll();

    #endregion
}
