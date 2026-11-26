using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Danh sách mission của một nhóm cùng kỳ reset: Daily.asset, Weekly.asset, mỗi event một asset.
/// Một class cho mọi loại vì chúng chỉ khác cách tính kỳ (<see cref="TryGetCycle"/>).
/// </summary>
[CreateAssetMenu(fileName = "MissionDatabase", menuName = "Shooter Game/Missions/Mission Database")]
public sealed class MissionDatabaseSO : ScriptableObject
{
    #region Configuration

    [Tooltip("ID ổn định được lưu trong save; không trống, không trùng giữa các database.")]
    [SerializeField] private string id;
    [Tooltip("Chữ trên tab UI.")]
    [SerializeField] private string displayName;
    [SerializeField] private MissionCategory category;
    [Tooltip("Nơi lấy giờ để tính kỳ và cửa sổ event. Device: giờ máy, chỉnh được (Daily/Weekly). " +
             "Server: giờ server, chống chỉnh giờ; chưa có giờ (mất mạng) thì nhóm bị ẩn và không nhận được (Event).")]
    [SerializeField] private ClockSource clock = ClockSource.Device;
    [Tooltip("Chỉ dùng cho Event: ngày mở (UTC), dạng yyyy-MM-dd.")]
    [SerializeField] private string eventStart;
    [Tooltip("Chỉ dùng cho Event: ngày đóng (UTC, không gồm ngày này), dạng yyyy-MM-dd.")]
    [SerializeField] private string eventEnd;
    [SerializeField] private List<MissionDataSO> missions = new List<MissionDataSO>();
    [Tooltip("Thẻ lớn ở đầu tab, thường có mục tiêu Complete Mission. Để trống thì không có thẻ này.")]
    [SerializeField] private MissionDataSO finalMission;

    /// <summary>ID ổn định.</summary>
    public string Id => id;

    /// <summary>Chữ trên tab UI.</summary>
    public string DisplayName => displayName;

    /// <summary>Loại nhóm, quyết định kỳ reset.</summary>
    public MissionCategory Category => category;

    /// <summary>Nơi lấy giờ của nhóm: giờ máy hay giờ server (đọc qua <see cref="IGameClock"/>).</summary>
    public ClockSource Clock => clock;

    /// <summary>Các mission của nhóm.</summary>
    public IReadOnlyList<MissionDataSO> Missions => missions;

    /// <summary>Mission tổng kết của nhóm; có thể null.</summary>
    public MissionDataSO FinalMission => finalMission;

    #endregion

    #region Cycle

    /// <summary>
    /// Nơi DUY NHẤT tính kỳ của nhóm tại <paramref name="utcNow"/>. <paramref name="cycle"/> đổi nghĩa là sang kỳ mới (reset);
    /// <paramref name="endsAt"/> là lúc kỳ kết thúc. False nếu nhóm đang đóng (event ngoài thời gian).
    /// Hàm thuần: giờ do bên gọi đưa vào theo <see cref="Clock"/>, phép tính ngày/tuần nằm ở <see cref="TimeCycles"/>.
    /// </summary>
    public bool TryGetCycle(DateTime utcNow, out long cycle, out DateTime endsAt)
    {
        long day = TimeCycles.DayOf(utcNow);
        switch (category)
        {
            case MissionCategory.Daily:
                cycle = day;
                endsAt = TimeCycles.DayStart(day + 1);
                return true;
            case MissionCategory.Weekly:
                cycle = TimeCycles.WeekOf(day);
                endsAt = TimeCycles.WeekStart(cycle + 1);
                return true;
            case MissionCategory.Event:
                // Mã kỳ theo ngày mở: gia hạn eventEnd không làm mất tiến độ.
                DateTime start = TimeCycles.ParseDate(eventStart);
                endsAt = TimeCycles.ParseDate(eventEnd);
                cycle = TimeCycles.DayOf(start);
                return utcNow >= start && utcNow < endsAt;
            default:
                throw new ArgumentOutOfRangeException(nameof(category), category, "Unknown mission category.");
        }
    }

    #endregion

    #region Validation

    /// <summary>ID, loại, ngày event và mọi mission hợp lệ.</summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(id)) throw new InvalidOperationException($"{name}: database chưa có ID.");
        if (!Enum.IsDefined(typeof(MissionCategory), category)) throw new InvalidOperationException($"{name}: loại không hợp lệ.");
        if (!Enum.IsDefined(typeof(ClockSource), clock)) throw new InvalidOperationException($"{name}: nguồn giờ không hợp lệ.");
        if (category == MissionCategory.Event)
        {
            try
            {
                if (TimeCycles.ParseDate(eventStart) >= TimeCycles.ParseDate(eventEnd))
                    throw new InvalidOperationException($"{name}: Event Start phải trước Event End.");
            }
            catch (Exception exception) when (exception is FormatException || exception is ArgumentNullException)
            {
                throw new InvalidOperationException($"{name}: ngày event phải có dạng {TimeCycles.DateFormat}.");
            }
        }
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (MissionDataSO mission in missions)
        {
            if (mission == null) throw new InvalidOperationException($"{name}: danh sách mission có phần tử trống.");
            mission.Validate();
            if (!ids.Add(mission.Id)) throw new InvalidOperationException($"{name}: trùng Mission ID {mission.Id}.");
            foreach (MissionTask task in mission.Tasks)
                if (task.Activity == MissionActivity.Mission)
                    throw new InvalidOperationException($"{name}/{mission.Id}: Complete Mission chỉ dùng cho Final Mission.");
        }
        if (finalMission == null) return;
        finalMission.Validate();
        if (!ids.Add(finalMission.Id)) throw new InvalidOperationException($"{name}: trùng Mission ID {finalMission.Id}.");
        // Final Mission mở khi đã nhận HẾT mission của nhóm nên Target phải đúng bằng số mission.
        bool counts = false;
        foreach (MissionTask task in finalMission.Tasks)
        {
            if (task.Activity != MissionActivity.Mission) continue;
            counts = true;
            if (task.Target != missions.Count)
                throw new InvalidOperationException($"{name}: Final Mission có Target {task.Target}, phải bằng số mission của nhóm ({missions.Count}).");
        }
        if (!counts) throw new InvalidOperationException($"{name}: Final Mission cần một mission con Complete Mission.");
    }

    #endregion
}
