using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Danh mục âm thanh, nhạc và rung: mỗi <see cref="AudioId"/> một entry. AudioManager dựng bảng tra từ đây lúc khởi động.
/// </summary>
/// <remarks>Chỉ chứa tham số, không ghi trạng thái runtime. Thêm âm mới = thêm giá trị enum + một entry ở đây.</remarks>
[CreateAssetMenu(fileName = "AudioDatabase", menuName = "Shooter Game/Data/Audio Database")]
public sealed class AudioDatabaseSO : ScriptableObject
{
    #region Entry

    /// <summary>Thông số của một âm thanh, bài nhạc hoặc kiểu rung.</summary>
    [Serializable]
    public struct Entry
    {
        [Tooltip("Khóa tra cứu. Mỗi id chỉ khai báo một lần; không dùng None.")]
        [SerializeField] private AudioId id;
        [Tooltip("Sound / Music / Haptic. Phải khớp dải số của id (Sound 1–99, Music 100–199, Haptic 200–299).")]
        [SerializeField] private AudioCategory category;
        [Tooltip("Sound: chọn ngẫu nhiên một clip mỗi lần phát. Music: dùng clip đầu. Để trống thì chỉ ghi log (dùng khi chưa có file).")]
        [SerializeField] private AudioClip[] clips;
        [Tooltip("Âm lượng của riêng entry này (cân giữa các file), 0..1.")]
        [SerializeField, Range(0f, 1f)] private float volume;
        [Tooltip("Phát lặp tới khi gọi Stop (âm aura, tia...). Music: bật cho nhạc nền, tắt cho đoạn nhạc ngắn phát một lần (Victory/Defeat).")]
        [SerializeField] private bool loop;
        [Tooltip("Khoảng tối thiểu (giây, thời gian thật) giữa hai lần phát cùng id; chống rè khi nhiều quái cùng chết. 0 = không giới hạn.")]
        [SerializeField, Min(0f)] private float minInterval;
        [Tooltip("Sound: vẫn kêu khi game pause (âm nút UI). Music luôn kêu khi pause.")]
        [SerializeField] private bool ignorePause;
        [Tooltip("Haptic: thời gian rung (mili giây).")]
        [SerializeField, Min(0)] private int hapticMilliseconds;

        public AudioId Id => id;
        public AudioCategory Category => category;
        public IReadOnlyList<AudioClip> Clips => clips ?? Array.Empty<AudioClip>();
        public float Volume => volume;
        public bool Loop => loop;
        public float MinInterval => minInterval;
        public bool IgnorePause => ignorePause;
        public int HapticMilliseconds => hapticMilliseconds;

        /// <summary>Tạo entry mặc định (âm lượng 1) cho tool tạo asset.</summary>
        public Entry(AudioId id, AudioCategory category, float minInterval, bool ignorePause, int hapticMilliseconds)
        {
            this.id = id;
            this.category = category;
            clips = Array.Empty<AudioClip>();
            volume = 1f;
            loop = false;
            this.minInterval = minInterval;
            this.ignorePause = ignorePause;
            this.hapticMilliseconds = hapticMilliseconds;
        }
    }

    #endregion

    [SerializeField] private Entry[] entries = Array.Empty<Entry>();

    /// <summary>Mọi entry theo thứ tự khai báo.</summary>
    public IReadOnlyList<Entry> Entries => entries;

    #region Validation

    /// <summary>Loại đúng của một id theo dải số của nó; dùng để chặn khai báo nhầm loại.</summary>
    public static AudioCategory CategoryOf(AudioId id)
        => (int)id >= 200 ? AudioCategory.Haptic : (int)id >= 100 ? AudioCategory.Music : AudioCategory.Sound;

    /// <summary>Chặn id None/trùng/không có trong enum và loại không khớp dải số. Clip trống là hợp lệ (chỉ log).</summary>
    public void Validate()
    {
        var seen = new HashSet<AudioId>();
        foreach (Entry entry in entries)
        {
            if (entry.Id == AudioId.None || !Enum.IsDefined(typeof(AudioId), entry.Id) || !seen.Add(entry.Id))
                throw new InvalidOperationException($"{name}: audio id {entry.Id} is None, unknown or declared twice.");
            if (entry.Category != CategoryOf(entry.Id))
                throw new InvalidOperationException($"{name}: {entry.Id} must use category {CategoryOf(entry.Id)}.");
        }
    }

    #endregion
}
