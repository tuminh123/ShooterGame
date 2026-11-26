using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Dữ liệu vẽ một ô ngày: nhãn ngày, phần thưởng và trạng thái.</summary>
public readonly struct LoginCheckInDayModel
{
    public readonly string DayText;
    public readonly RewardSlotModel Reward;
    public readonly LoginCheckInDayState State;

    public LoginCheckInDayModel(string dayText, RewardSlotModel reward, LoginCheckInDayState state)
    {
        DayText = dayText;
        Reward = reward;
        State = state;
    }
}

/// <summary>
/// Một thẻ ngày của popup đăng nhập; chỉ hiển thị. Mỗi trạng thái (chưa tới / nhận hôm nay / đã nhận) bật một nhóm object con
/// của bản dựng và tô màu nền, phần nhấn (dải đầu thẻ, góc số) và nhãn ngày. Ô nhận hôm nay nhấp nháy.
/// </summary>
/// <remarks>Các nhóm object và màu được gán trên Inspector, nên một class dùng được cho mọi bố cục thẻ (7 ngày, 30 ô, ô lớn).</remarks>
public sealed class LoginCheckInDayView : MonoBehaviour
{
    #region References

    [Tooltip("Vùng bấm để nhận; chỉ bấm được ở ô nhận hôm nay. Không gắn ButtonClickTween (giành scale với AttentionPulse).")]
    [SerializeField] private Button button;
    [SerializeField] private Image background;
    [Tooltip("Phần nhấn đổi màu theo trạng thái (góc số của thẻ 30 ô); để trống nếu không có.")]
    [SerializeField] private Image accent;
    [SerializeField] private Image icon;
    [Tooltip("Số lượng thưởng; để trống nếu thẻ không có.")]
    [SerializeField] private TMP_Text amount;
    [SerializeField] private TMP_Text dayLabel;
    [SerializeField] private AttentionPulse pulse;

    [Header("Object bật theo trạng thái")]
    [Tooltip("Chưa tới lượt, ví dụ viền sáng Glow.")]
    [SerializeField] private GameObject[] showWhenLocked = Array.Empty<GameObject>();
    [Tooltip("Nhận hôm nay, ví dụ Focus, hiệu ứng sáng, dải trắng đầu thẻ.")]
    [SerializeField] private GameObject[] showWhenClaimable = Array.Empty<GameObject>();
    [Tooltip("Đã nhận, ví dụ dấu tích.")]
    [SerializeField] private GameObject[] showWhenClaimed = Array.Empty<GameObject>();

    #endregion

    #region State Colors

    [Header("Màu theo trạng thái")]
    [SerializeField] private Color lockedBackground = new Color32(0x07, 0x87, 0xFF, 0xFF);
    [SerializeField] private Color claimableBackground = new Color32(0x24, 0xE7, 0x15, 0xFF);
    [SerializeField] private Color claimedBackground = new Color32(0xA8, 0xBB, 0xD1, 0xFF);
    [SerializeField] private Color lockedAccent = new Color32(0x15, 0x65, 0xE6, 0xFF);
    [SerializeField] private Color claimableAccent = Color.white;
    [SerializeField] private Color claimedAccent = Color.white;
    [SerializeField] private Color lockedDayText = Color.white;
    [SerializeField] private Color claimableDayText = new Color32(0x0D, 0xB3, 0x1B, 0xFF);
    [SerializeField] private Color claimedDayText = Color.white;
    [SerializeField, Range(0f, 1f)] private float claimedIconAlpha = 0.35f;
    [SerializeField, Range(0f, 1f)] private float claimedAmountAlpha = 0.6f;

    #endregion

    #region Events

    /// <summary>Người chơi bấm vào thẻ (chỉ phát khi thẻ đang bấm được).</summary>
    public event Action<LoginCheckInDayView> Clicked;

    private void OnEnable()
    {
        if (button != null) button.onClick.AddListener(OnClick);
    }

    private void OnDisable()
    {
        if (button != null) button.onClick.RemoveListener(OnClick);
    }

    private void OnClick() => Clicked?.Invoke(this);

    #endregion

    #region Draw

    /// <summary>Vẽ ô theo dữ liệu; ô nhận hôm nay thì nhấp nháy, ô khác đứng yên.</summary>
    public void Draw(in LoginCheckInDayModel model)
    {
        LoginCheckInDayState state = model.State;
        bool claimed = state == LoginCheckInDayState.Claimed;
        bool claimable = state == LoginCheckInDayState.Claimable;

        if (background != null) background.color = Pick(state, lockedBackground, claimableBackground, claimedBackground);
        if (accent != null) accent.color = Pick(state, lockedAccent, claimableAccent, claimedAccent);
        if (dayLabel != null)
        {
            dayLabel.text = model.DayText;
            dayLabel.color = Pick(state, lockedDayText, claimableDayText, claimedDayText);
        }
        if (icon != null)
        {
            icon.sprite = model.Reward.Icon;
            icon.preserveAspect = true; // Icon thưởng có tỉ lệ khác icon mẫu của bản dựng: giữ tỉ lệ, không kéo giãn.
            icon.color = WithAlpha(Color.white, claimed ? claimedIconAlpha : 1f);
        }
        if (amount != null)
        {
            amount.text = model.Reward.Label;
            amount.color = WithAlpha(Color.white, claimed ? claimedAmountAlpha : 1f);
        }
        SetActive(showWhenLocked, state == LoginCheckInDayState.Locked);
        SetActive(showWhenClaimable, claimable);
        SetActive(showWhenClaimed, claimed);
        if (button != null) button.interactable = claimable;
        if (pulse == null) return;
        if (claimable) pulse.Play();
        else pulse.Stop();
    }

    private static Color Pick(LoginCheckInDayState state, Color locked, Color claimable, Color claimed)
    {
        switch (state)
        {
            case LoginCheckInDayState.Claimable: return claimable;
            case LoginCheckInDayState.Claimed: return claimed;
            default: return locked;
        }
    }

    private static Color WithAlpha(Color color, float alpha)
    {
        color.a = alpha;
        return color;
    }

    private static void SetActive(GameObject[] targets, bool active)
    {
        foreach (GameObject target in targets)
            if (target != null && target.activeSelf != active) target.SetActive(active);
    }

    #endregion
}
