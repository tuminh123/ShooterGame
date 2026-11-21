using PrimeTween;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Một dòng perk trên tab Stars: icon, tiêu đề "STAR n" và mô tả. Ba trạng thái nhìn khác nhau:
/// đã mở (sáng), kế tiếp (nền cam để người chơi biết sao sau mở gì) và khóa. Mọi perk chưa mở (kế tiếp lẫn khóa)
/// bị phủ lớp tối có biểu tượng khóa, chữ xám đi nhưng vẫn đọc được. Vừa mở thì dòng nảy lên một lần (<see cref="PlayUnlock"/>).
/// </summary>
/// <remarks>Chỉ hiển thị model; không đọc service, không giữ trạng thái giữa các lần Bind.</remarks>
public sealed class StarPerkRowView : MonoBehaviour
{
    #region References

    [SerializeField] private Image background;
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text title;
    [SerializeField] private TMP_Text description;
    [Tooltip("Lớp tối + icon khóa phủ lên dòng; bật cho mọi perk chưa mở.")]
    [SerializeField] private GameObject lockOverlay;

    #endregion

    #region Style

    [SerializeField] private Color unlockedBackground = new Color(0.43f, 0.51f, 0.64f, 1f);
    [SerializeField] private Color nextBackground = new Color(0.95f, 0.62f, 0.12f, 1f);
    [SerializeField] private Color lockedBackground = new Color(0.3f, 0.33f, 0.4f, 1f);
    [SerializeField] private Color unlockedTitle = new Color(1f, 0.85f, 0.25f, 1f);
    [SerializeField] private Color nextTitle = new Color(1f, 0.97f, 0.75f, 1f);
    [SerializeField] private Color lockedTitle = new Color(0.75f, 0.78f, 0.85f, 1f);
    [Tooltip("Màu mô tả khi perk chưa mở: xám nhưng vẫn đọc được qua lớp tối.")]
    [SerializeField] private Color lockedDescription = new Color(0.8f, 0.82f, 0.88f, 1f);

    #endregion

    #region Unlock Animation

    [Tooltip("Tỉ lệ phóng to khi vừa mở perk.")]
    [SerializeField, Min(1f)] private float unlockPeakScale = 1.12f;
    [SerializeField, Min(0.01f)] private float unlockGrowDuration = 0.15f;
    [SerializeField, Min(0.01f)] private float unlockSettleDuration = 0.2f;

    private Sequence unlockSequence;

    #endregion

    #region Lifecycle

    // Bị tắt giữa chừng (đổi tab, rời màn) thì trả về kích thước gốc.
    private void OnDisable()
    {
        unlockSequence.Stop();
        transform.localScale = Vector3.one;
    }

    #endregion

    #region Presentation

    /// <summary>Hiện một perk theo trạng thái mở của nó.</summary>
    public void Bind(StarPerkModel model)
    {
        if (model.Icon != null) icon.sprite = model.Icon;
        description.text = model.Description;

        switch (model.State)
        {
            case StarPerkState.Unlocked:
                title.text = $"STAR {model.Star}";
                Apply(unlockedBackground, unlockedTitle, false);
                break;
            case StarPerkState.Next:
                title.text = $"STAR {model.Star} - NEXT";
                Apply(nextBackground, nextTitle, true);
                break;
            default:
                title.text = $"STAR {model.Star} - LOCKED";
                Apply(lockedBackground, lockedTitle, true);
                break;
        }
    }

    /// <summary>Nảy lên rồi về kích thước gốc; gọi sau Bind khi perk này vừa được mở.</summary>
    public void PlayUnlock()
    {
        unlockSequence.Stop();
        transform.localScale = Vector3.one;
        unlockSequence = Sequence.Create(useUnscaledTime: true) // Màn Character chạy khi game đang pause.
            .Chain(Tween.Scale(transform, unlockPeakScale, unlockGrowDuration, Ease.OutBack))
            .Chain(Tween.Scale(transform, 1f, unlockSettleDuration, Ease.OutQuad));
    }

    private void Apply(Color backgroundColor, Color titleColor, bool locked)
    {
        background.color = backgroundColor;
        title.color = titleColor;
        description.color = locked ? lockedDescription : Color.white;
        if (lockOverlay != null) lockOverlay.SetActive(locked);
    }

    #endregion
}
