using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

/// <summary>
/// Danh sách mission của một nhóm cùng kỳ reset: Daily.asset, Weekly.asset, mỗi event một asset.
/// Một class cho mọi loại vì chúng chỉ khác cách tính kỳ (<see cref="TryGetCycle"/>).
/// </summary>
[CreateAssetMenu(fileName = "MissionDatabase", menuName = "Shooter Game/Missions/Mission Database")]
public sealed class MissionDatabaseSO : ScriptableObject
{
    #region Configuration

    private const string DateFormat = "yyyy-MM-dd";

    [Tooltip("ID ổn định được lưu trong save; không trống, không trùng giữa các database.")]
    [SerializeField] private string id;
    [Tooltip("Chữ trên tab UI.")]
    [SerializeField] private string displayName;
    [SerializeField] private MissionCategory category;
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

    /// <summary>Các mission của nhóm.</summary>
    public IReadOnlyList<MissionDataSO> Missions => missions;

    /// <summary>Mission tổng kết của nhóm; có thể null.</summary>
    public MissionDataSO FinalMission => finalMission;

    #endregion

    #region Cycle

    /// <summary>
    /// Nơi DUY NHẤT tính kỳ của nhóm tại <paramref name="utcNow"/>. <paramref name="cycle"/> đổi nghĩa là sang kỳ mới (reset);
    /// <paramref name="endsAt"/> là lúc kỳ kết thúc. False nếu nhóm đang đóng (event ngoài thời gian).
    /// </summary>
    public bool TryGetCycle(DateTime utcNow, out long cycle, out DateTime endsAt)
    {
        long day = utcNow.Ticks / TimeSpan.TicksPerDay;
        switch (category)
        {
            case MissionCategory.Daily:
                cycle = day;
                endsAt = FromDay(day + 1);
                return true;
            case MissionCategory.Weekly:
                cycle = day / 7; // 0001-01-01 là thứ Hai nên mỗi nhóm 7 ngày bắt đầu từ thứ Hai.
                endsAt = FromDay((cycle + 1) * 7);
                return true;
            case MissionCategory.Event:
                DateTime start = ParseDate(eventStart);
                endsAt = ParseDate(eventEnd);
                cycle = start.Ticks / TimeSpan.TicksPerDay;
                return utcNow >= start && utcNow < endsAt;
            default:
                throw new ArgumentOutOfRangeException(nameof(category), category, "Unknown mission category.");
        }
    }

    private static DateTime FromDay(long day) => new DateTime(day * TimeSpan.TicksPerDay, DateTimeKind.Utc);

    private static DateTime ParseDate(string value)
        => DateTime.ParseExact(value, DateFormat, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);

    #endregion

    #region Validation

    /// <summary>ID, loại, ngày event và mọi mission hợp lệ.</summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(id)) throw new InvalidOperationException($"{name}: database chưa có ID.");
        if (!Enum.IsDefined(typeof(MissionCategory), category)) throw new InvalidOperationException($"{name}: loại không hợp lệ.");
        if (category == MissionCategory.Event)
        {
            try
            {
                if (ParseDate(eventStart) >= ParseDate(eventEnd))
                    throw new InvalidOperationException($"{name}: Event Start phải trước Event End.");
            }
            catch (Exception exception) when (exception is FormatException || exception is ArgumentNullException)
            {
                throw new InvalidOperationException($"{name}: ngày event phải có dạng {DateFormat}.");
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
