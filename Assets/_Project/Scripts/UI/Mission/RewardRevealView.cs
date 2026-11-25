using System;
using System.Collections.Generic;
using System.Globalization;
using PrimeTween;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Màn đen nhận thưởng: thưởng nảy ra như bong bóng, bấm Claim thì tiền bay lên thanh tiền, tướng/mảnh vào Heroes, trang bị vào Bag.
/// Chỉ trình bày: thưởng đã được trao và lưu trước khi mở, nên ngắt giữa chừng không mất gì.
/// </summary>
/// <remarks>Có thanh tiền và icon đích riêng (thanh bar thật đang ẩn), nên màn nào cũng dùng được. Chạm nền để bỏ qua hiệu ứng.</remarks>
public sealed class RewardRevealView : MonoBehaviour
{
    #region Types

    private enum Phase { Closed, Revealing, Waiting, Flying, Closing }

    /// <summary>Một loại tiền trên thanh đích.</summary>
    [Serializable]
    private sealed class CurrencyTarget
    {
        public RectTransform icon;
        public TMP_Text text;

        private long start;
        private long gain;
        private int total;
        private int arrived;

        public void Begin(long balance)
        {
            start = balance;
            gain = 0;
            total = 0;
            arrived = 0;
            Draw();
        }

        public void Expect(long amount, int icons)
        {
            gain += amount;
            total += icons;
        }

        public void Arrive()
        {
            arrived++;
            Draw();
        }

        public void Finish()
        {
            arrived = total;
            Draw();
        }

        // Icon cuối phải ra đúng số dư thật, không để sai số làm tròn.
        private void Draw()
        {
            long value = arrived >= total ? start + gain : start + (long)(gain * (arrived / (double)total));
            text.text = value.ToString("N0", CultureInfo.InvariantCulture);
        }
    }

    #endregion

    #region References

    [SerializeField] private CanvasGroup group;
    [Tooltip("Nền đen phủ màn; chạm để bỏ qua hiệu ứng.")]
    [SerializeField] private Button dimmer;
    [SerializeField] private Button claimButton;
    [SerializeField] private RectTransform slotRoot;
    [SerializeField] private RewardSlotView slotPrefab;
    [SerializeField] private RewardFlyView flyer;

    [Header("Đích bay")]
    [SerializeField] private CurrencyTarget gold = new CurrencyTarget();
    [SerializeField] private CurrencyTarget diamond = new CurrencyTarget();
    [SerializeField] private RectTransform heroes;
    [SerializeField] private RectTransform bag;

    [Header("Bong bóng")]
    [SerializeField, Min(1)] private int columns = 4;
    [SerializeField] private Vector2 spacing = new Vector2(190f, 200f);
    [SerializeField, Min(0.1f)] private float slotScale = 1.3f;
    [SerializeField, Min(0.01f)] private float popDuration = 0.35f;
    [SerializeField, Min(0f)] private float popStagger = 0.08f;
    [SerializeField, Min(0f)] private float floatDistance = 8f;
    [SerializeField, Min(0.01f)] private float fadeDuration = 0.2f;

    [Header("Bay")]
    [SerializeField, Min(1)] private int currencyIcons = 8;
    [SerializeField, Min(1f)] private float currencyIconSize = 64f;
    [SerializeField, Min(1f)] private float itemIconSize = 120f;

    #endregion

    #region State

    private readonly List<RewardSlotView> slots = new List<RewardSlotView>();
    private readonly List<RewardSlotModel> models = new List<RewardSlotModel>();
    private readonly List<Tween> motions = new List<Tween>();
    private Phase phase = Phase.Closed;
    private int pending; // Số icon chưa tới đích.
    private Action closed;

    #endregion

    #region Lifecycle

    private void OnEnable()
    {
        dimmer.onClick.AddListener(Skip);
        claimButton.onClick.AddListener(Claim);
    }

    private void OnDisable()
    {
        dimmer.onClick.RemoveListener(Skip);
        claimButton.onClick.RemoveListener(Claim);
        StopMotions();
        phase = Phase.Closed;
        closed = null;
    }

    #endregion

    #region Open And Close

    /// <summary>Mở với các phần thưởng thực tế và số dư TRƯỚC khi nhận; <paramref name="onClosed"/> chạy khi diễn xong.</summary>
    public void Show(IReadOnlyList<RewardSlotModel> rewards, long goldBefore, long diamondBefore, Action onClosed)
    {
        gameObject.SetActive(true);
        StopMotions();
        closed = onClosed;
        phase = Phase.Revealing;
        models.Clear();
        models.AddRange(rewards);
        gold.Begin(goldBefore);
        diamond.Begin(diamondBefore);
        heroes.gameObject.SetActive(Uses(RewardFlyTarget.Heroes));
        bag.gameObject.SetActive(Uses(RewardFlyTarget.Bag));
        heroes.localScale = bag.localScale = Vector3.zero; // Chỉ nảy ra khi bắt đầu bay.
        claimButton.gameObject.SetActive(false);

        RewardSlotView.BindAll(slots, slotPrefab, slotRoot, models);
        group.alpha = 0f;
        Track(Tween.Alpha(group, 0f, 1f, fadeDuration, useUnscaledTime: true)); // Home pause (timeScale = 0).
        for (int i = 0; i < models.Count; i++)
        {
            Transform slot = slots[i].transform;
            slot.localPosition = Place(i);
            slot.localScale = Vector3.zero;
            Track(Tween.Scale(slot, Vector3.zero, Vector3.one * slotScale, popDuration, Ease.OutBack,
                startDelay: fadeDuration + i * popStagger, useUnscaledTime: true));
        }
        Track(Tween.Delay(this, fadeDuration + models.Count * popStagger + popDuration, view => view.Settle(), useUnscaledTime: true));
    }

    /// <summary>Tắt ngay, không gọi callback (màn chủ bị đóng).</summary>
    public void Hide()
    {
        closed = null;
        gameObject.SetActive(false);
    }

    private void Close()
    {
        if (phase == Phase.Closing || phase == Phase.Closed) return;
        StopMotions();
        phase = Phase.Closing;
        Track(Tween.Alpha(group, group.alpha, 0f, fadeDuration, useUnscaledTime: true)
            .OnComplete(this, view => view.FinishClose()));
    }

    private void FinishClose()
    {
        Action callback = closed; // OnDisable xóa callback nên lấy trước.
        gameObject.SetActive(false);
        callback?.Invoke();
    }

    #endregion

    #region Bubbles

    // Hiện hết, cho bong bóng trôi nhẹ lệch nhịp và mở nút Claim.
    private void Settle()
    {
        if (phase != Phase.Revealing) return;
        StopMotions();
        phase = Phase.Waiting;
        group.alpha = 1f;
        for (int i = 0; i < models.Count; i++)
        {
            Transform slot = slots[i].transform;
            Vector3 rest = Place(i);
            slot.localPosition = rest;
            slot.localScale = Vector3.one * slotScale;
            Track(Tween.LocalPositionY(slot, rest.y, rest.y + floatDistance, 0.9f + 0.13f * (i % 4), Ease.InOutSine, -1,
                CycleMode.Yoyo, useUnscaledTime: true));
        }
        claimButton.gameObject.SetActive(true);
    }

    // Xếp giữa màn, mỗi hàng căn giữa.
    private Vector3 Place(int index)
    {
        int row = index / columns;
        int rows = (models.Count + columns - 1) / columns;
        int inRow = Mathf.Min(columns, models.Count - row * columns);
        return new Vector3((index % columns - (inRow - 1) * 0.5f) * spacing.x, ((rows - 1) * 0.5f - row) * spacing.y, 0f);
    }

    #endregion

    #region Flight

    private void Claim()
    {
        if (phase != Phase.Waiting) return;
        StopMotions();
        phase = Phase.Flying;
        claimButton.gameObject.SetActive(false);
        // Scale quanh tâm nên điểm đến tính lúc còn nhỏ vẫn đúng.
        Pop(heroes);
        Pop(bag);

        pending = 0;
        foreach (RewardSlotModel model in models)
        {
            int count = IconCount(model);
            pending += count;
            Currency(model.Target)?.Expect(model.Amount, count);
        }
        for (int i = 0; i < models.Count; i++)
        {
            RewardSlotModel model = models[i];
            CurrencyTarget currency = Currency(model.Target);
            RectTransform target = Target(model.Target);
            slots[i].gameObject.SetActive(false);
            flyer.Fly(model.Icon, slots[i].transform.position, target, IconCount(model),
                currency != null ? currencyIconSize : itemIconSize, () => Arrive(currency, target));
        }
        if (pending == 0) Close();
    }

    private void Pop(RectTransform target)
    {
        if (target.gameObject.activeSelf)
            Track(Tween.Scale(target, Vector3.zero, Vector3.one, 0.25f, Ease.OutBack, useUnscaledTime: true));
    }

    private void Arrive(CurrencyTarget currency, RectTransform target)
    {
        if (phase != Phase.Flying) return;
        currency?.Arrive();
        Tween.StopAll(target); // Icon tới sát nhau: nhún lại từ scale gốc, không cộng dồn.
        target.localScale = Vector3.one;
        Track(Tween.PunchScale(target, Vector3.one * 0.25f, 0.25f, 6f, useUnscaledTime: true));
        if (--pending == 0) Track(Tween.Delay(this, 0.35f, view => view.Close(), useUnscaledTime: true));
    }

    private int IconCount(RewardSlotModel model)
        => Currency(model.Target) == null ? 1 : (int)Math.Max(1L, Math.Min(currencyIcons, model.Amount));

    private CurrencyTarget Currency(RewardFlyTarget target)
        => target == RewardFlyTarget.Gold ? gold : target == RewardFlyTarget.Diamond ? diamond : null;

    private RectTransform Target(RewardFlyTarget target)
    {
        switch (target)
        {
            case RewardFlyTarget.Gold: return gold.icon;
            case RewardFlyTarget.Diamond: return diamond.icon;
            case RewardFlyTarget.Heroes: return heroes;
            case RewardFlyTarget.Bag: return bag;
            default: throw new ArgumentOutOfRangeException(nameof(target), target, "Unknown reward target.");
        }
    }

    private bool Uses(RewardFlyTarget target)
    {
        foreach (RewardSlotModel model in models)
            if (model.Target == target) return true;
        return false;
    }

    #endregion

    #region Skip And Cleanup

    // Đang nảy thì hiện hết; đang bay thì tới đích ngay.
    private void Skip()
    {
        if (phase == Phase.Revealing) Settle();
        else if (phase == Phase.Flying)
        {
            gold.Finish();
            diamond.Finish();
            Close();
        }
    }

    private void Track(Tween tween) => motions.Add(tween);

    private void StopMotions()
    {
        foreach (Tween tween in motions) tween.Stop();
        motions.Clear();
        flyer.StopAll();
        gold.icon.localScale = diamond.icon.localScale = Vector3.one;
    }

    #endregion
}
