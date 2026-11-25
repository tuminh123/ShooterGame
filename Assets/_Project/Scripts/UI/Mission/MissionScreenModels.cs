using System.Collections.Generic;
using UnityEngine;

// Dữ liệu hiển thị của màn Mission. MissionScreenController dựng từ IMissions + Theme rồi đẩy xuống View; View chỉ đọc.

/// <summary>Một ô thưởng: icon, chữ góc (số lượng hoặc level), màu viền theo độ hiếm và nơi bay tới khi nhận.</summary>
public readonly struct RewardSlotModel
{
    public readonly Sprite Icon;
    public readonly string Label;
    public readonly Color Frame;
    public readonly RewardFlyTarget Target;
    /// <summary>Số tiền; chỉ dùng với Gold/Diamond để thanh tiền đếm tăng.</summary>
    public readonly long Amount;

    public RewardSlotModel(Sprite icon, string label, Color frame, RewardFlyTarget target, long amount = 0)
    {
        Target = target;
        Amount = amount;
        Icon = icon;
        Label = label;
        Frame = frame;
    }
}

/// <summary>Một dòng mission con. <see cref="From"/> khác <see cref="Progress"/> thì thanh chạy sau <see cref="Delay"/> giây.</summary>
public readonly struct MissionTaskModel
{
    /// <summary>Định danh dòng, để lần vẽ lại không ngắt thanh đang chạy.</summary>
    public readonly string Key;
    public readonly string Description;
    public readonly int From;
    public readonly int Progress;
    public readonly int Target;
    public readonly float Delay;

    public MissionTaskModel(string key, string description, int from, int progress, int target, float delay)
    {
        Key = key;
        From = from;
        Delay = delay;
        Description = description;
        Progress = progress;
        Target = target;
    }
}

/// <summary>Một thẻ mission chính.</summary>
public readonly struct MissionCardModel
{
    public readonly string Id;
    public readonly string Title;
    public readonly string Description;
    public readonly MissionState State;
    public readonly string ButtonText;
    public readonly IReadOnlyList<MissionTaskModel> Tasks;
    public readonly IReadOnlyList<RewardSlotModel> Rewards;

    public MissionCardModel(string id, string title, string description, MissionState state, string buttonText,
        IReadOnlyList<MissionTaskModel> tasks, IReadOnlyList<RewardSlotModel> rewards)
    {
        Id = id;
        Title = title;
        Description = description;
        State = state;
        ButtonText = buttonText;
        Tasks = tasks;
        Rewards = rewards;
    }
}

/// <summary>Một tab nhóm mission.</summary>
public readonly struct MissionTabModel
{
    public readonly string Title;
    public readonly bool Selected;
    public readonly bool HasClaimable;

    public MissionTabModel(string title, bool selected, bool hasClaimable)
    {
        Title = title;
        Selected = selected;
        HasClaimable = hasClaimable;
    }
}

/// <summary>Cả màn Mission ở tab đang chọn.</summary>
public readonly struct MissionScreenModel
{
    public readonly string Title;
    public readonly string CompleteText;
    public readonly IReadOnlyList<MissionTabModel> Tabs;
    /// <summary>Thẻ tổng kết; null thì không hiện.</summary>
    public readonly MissionCardModel? FinalCard;
    public readonly IReadOnlyList<MissionCardModel> Cards;

    public MissionScreenModel(string title, string completeText, IReadOnlyList<MissionTabModel> tabs,
        MissionCardModel? finalCard, IReadOnlyList<MissionCardModel> cards)
    {
        Title = title;
        CompleteText = completeText;
        Tabs = tabs;
        FinalCard = finalCard;
        Cards = cards;
    }
}
