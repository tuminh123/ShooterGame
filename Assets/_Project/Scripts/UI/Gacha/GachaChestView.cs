using System;
using System.Collections.Generic;
using PrimeTween;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Màn tối mở rương của gacha: rương nảy ra giữa màn, rung lắc 3 lần (mạnh dần) rồi bật mở, ánh sáng phía sau
/// mang màu của bậc cao nhất vừa quay. Xong thì gọi callback để màn Gacha hiện phần thưởng (RewardRevealView).
/// </summary>
/// <remarks>Chỉ trình bày: phần thưởng đã được trao và lưu trước khi rương xuất hiện. Chạm màn để bỏ qua.</remarks>
public sealed class GachaChestView : MonoBehaviour
{
    #region References

    [SerializeField] private CanvasGroup group;
    [Tooltip("Nền tối phủ màn; chạm để bỏ qua.")]
    [SerializeField] private Button skipButton;
    [SerializeField] private RectTransform chest;
    [SerializeField] private Image chestImage;
    [SerializeField] private Sprite closedSprite;
    [SerializeField] private Sprite openSprite;
    [Tooltip("Ánh sáng sau rương; hiện khi mở, tô màu theo bậc cao nhất.")]
    [SerializeField] private Image glow;
    [Tooltip("Màu ánh sáng theo bậc (Common → Legendary).")]
    [SerializeField] private Color[] tierColors =
    {
        new Color(0.8f, 0.8f, 0.8f), new Color(0.4f, 0.9f, 0.4f), new Color(0.3f, 0.6f, 1f), new Color(0.75f, 0.4f, 1f), new Color(1f, 0.75f, 0.2f)
    };

    [Header("Nhịp")]
    [SerializeField, Min(1)] private int shakes = 3;
    [SerializeField, Min(0.01f)] private float shakeDuration = 0.35f;
    [SerializeField, Min(0f)] private float shakePause = 0.2f;
    [Tooltip("Góc rung của lần đầu; mỗi lần sau mạnh hơn.")]
    [SerializeField, Min(0f)] private float shakeAngle = 10f;
    [SerializeField, Min(0f)] private float openHold = 0.5f;
    [SerializeField, Min(0.01f)] private float fadeDuration = 0.2f;

    #endregion

    #region State

    private readonly List<Tween> motions = new List<Tween>();
    private Sequence sequence;
    private Action opened;
    private bool playing;
    private bool isOpen;
    private Sprite currentClosed;
    private Sprite currentOpen;
    private Vector2 baseSize; // Kích thước khung rương trong scene, ứng với ảnh rương đóng.

    #endregion

    #region Lifecycle

    private void OnEnable() => skipButton.onClick.AddListener(Skip);

    private void OnDisable()
    {
        skipButton.onClick.RemoveListener(Skip);
        StopMotions();
        playing = false;
        opened = null;
    }

    #endregion

    #region Play

    /// <summary>
    /// Diễn mở rương của banner (<paramref name="closed"/> / <paramref name="open"/>; null = rương mặc định);
    /// <paramref name="onOpened"/> chạy khi rương đã mở và màn tối đã tắt.
    /// </summary>
    public void Play(Sprite closed, Sprite open, GachaTier highest, Action onOpened)
    {
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        StopMotions();
        opened = onOpened;
        playing = true;
        isOpen = false;
        currentClosed = closed != null ? closed : closedSprite;
        currentOpen = open != null ? open : openSprite;
        if (baseSize == Vector2.zero) baseSize = chest.sizeDelta;
        chest.sizeDelta = baseSize;
        chestImage.sprite = currentClosed;
        chest.localRotation = Quaternion.identity;
        glow.color = TierColor(highest);
        glow.gameObject.SetActive(false);

        group.alpha = 0f;
        chest.localScale = Vector3.zero;
        // Home pause (timeScale = 0) nên mọi tween dùng unscaled time.
        sequence = Sequence.Create(useUnscaledTime: true)
            .Group(Tween.Alpha(group, 0f, 1f, fadeDuration))
            .Group(Tween.Scale(chest, Vector3.zero, Vector3.one, 0.35f, Ease.OutBack));
        for (int i = 0; i < shakes; i++)
        {
            float angle = shakeAngle * (1f + i * 0.5f); // Mạnh dần: hồi hộp trước khi mở.
            sequence = sequence.ChainDelay(shakePause)
                .Chain(Tween.ShakeLocalRotation(chest, new Vector3(0f, 0f, angle), shakeDuration, 12f));
        }
        sequence = sequence.ChainCallback(this, view => view.Open());
    }

    // Bật nắp: đổi hình, nhún mạnh, ánh sáng xoay và lớn dần; giữ một nhịp rồi tắt màn tối.
    private void Open()
    {
        if (!playing) return;
        StopMotions();
        chest.localRotation = Quaternion.identity;
        chest.localScale = Vector3.one;
        isOpen = true;
        chestImage.sprite = currentOpen;
        // Ảnh rương mở thường rộng hơn (có tia sáng quanh rương): phóng khung theo tỉ lệ pixel để thân rương giữ nguyên cỡ.
        if (currentClosed != null && currentOpen != null && currentClosed.rect.width > 0f)
            chest.sizeDelta = baseSize * (currentOpen.rect.width / currentClosed.rect.width);
        glow.gameObject.SetActive(true);
        Transform light = glow.transform;
        Track(Tween.PunchScale(chest, Vector3.one * 0.3f, 0.35f, 8f, useUnscaledTime: true));
        Track(Tween.Scale(light, Vector3.zero, Vector3.one, 0.35f, Ease.OutBack, useUnscaledTime: true));
        Track(Tween.LocalRotation(light, Vector3.zero, new Vector3(0f, 0f, -360f), 4f, Ease.Linear, -1, useUnscaledTime: true));
        Track(Tween.Delay(this, openHold, view => view.Close(), useUnscaledTime: true));
    }

    private void Close()
    {
        if (!playing) return;
        playing = false;
        sequence.Stop();
        Track(Tween.Alpha(group, group.alpha, 0f, fadeDuration, useUnscaledTime: true).OnComplete(this, view => view.Finish()));
    }

    private void Finish()
    {
        Action callback = opened; // OnDisable xóa callback nên lấy trước.
        gameObject.SetActive(false);
        callback?.Invoke();
    }

    /// <summary>Tắt ngay, không gọi callback (màn Gacha bị đóng).</summary>
    public void Hide()
    {
        opened = null;
        gameObject.SetActive(false);
    }

    #endregion

    #region Skip And Cleanup

    // Đang rung thì mở luôn; đã mở thì tắt màn tối ngay.
    private void Skip()
    {
        if (!playing) return;
        if (!isOpen) Open();
        else Close();
    }

    private Color TierColor(GachaTier tier)
    {
        int index = (int)tier;
        return index >= 0 && index < tierColors.Length ? tierColors[index] : Color.white;
    }

    private void Track(Tween tween) => motions.Add(tween);

    private void StopMotions()
    {
        sequence.Stop();
        foreach (Tween tween in motions) tween.Stop();
        motions.Clear();
    }

    #endregion
}
