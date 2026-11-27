using System;
using UnityEngine;
using Zenject;
using Object = UnityEngine.Object;

/// <summary>
/// Manager của miền Audio: phát âm thanh (Sound), nhạc nền (Music) và rung (Haptic) theo <see cref="AudioId"/>,
/// và giữ công tắc bật/tắt của người chơi. Đây là class <b>duy nhất</b> của Audio được bind vào Zenject.
/// </summary>
/// <remarks>
/// Nơi gọi chỉ cần <c>audio.Play(AudioId.X)</c>; Manager tra entry trong <see cref="AudioDatabaseSO"/> rồi switch
/// theo <see cref="AudioCategory"/> của entry. Thêm âm mới không sửa class này (chỉ thêm enum + entry).
/// <para>
/// Công tắc là dữ liệu nhẹ, mất cũng không sao, nên lưu bằng PlayerPrefs thay vì save game.
/// Entry chưa có clip thì chỉ ghi log, để test luồng gọi khi chưa có file âm thanh.
/// </para>
/// </remarks>
public sealed class AudioManager : DomainManager, IAudio, IAudioSettings, IInitializable, ITickable, IDisposable
{
    #region Settings

    /// <summary>Cấu hình Audio, gán trên GameSceneInstaller.</summary>
    [Serializable]
    public sealed class Settings
    {
        [Tooltip("Danh mục âm thanh, nhạc và rung theo AudioId.")]
        [SerializeField] private AudioDatabaseSO database;
        [Tooltip("Số âm thanh kêu cùng lúc tối đa. Hết chỗ thì âm mới bị bỏ qua.")]
        [SerializeField, Min(1)] private int voiceCount = 12;
        [Tooltip("Thời gian (giây) nhạc cũ nhỏ dần và nhạc mới to dần khi đổi bài.")]
        [SerializeField, Min(0f)] private float musicFadeSeconds = 0.4f;

        public AudioDatabaseSO Database => database;
        public int VoiceCount => voiceCount;
        public float MusicFadeSeconds => musicFadeSeconds;

        /// <summary>Bắt buộc có database hợp lệ.</summary>
        public void Validate()
        {
            if (database == null) throw new InvalidOperationException("Assign Audio Database in GameSceneInstaller > Audio.");
            database.Validate();
        }
    }

    #endregion

    #region Fields

    private const string PrefsKeyPrefix = "Audio.On."; // + tên AudioCategory, giá trị 1/0.
    private const float HapticMinInterval = 0.1f; // Chống rung dồn khi nhiều sự kiện cùng lúc.

    private readonly Settings settings;
    private AudioDatabaseSO.Entry[] entries; // Tra theo số enum; Id = None nghĩa là chưa khai báo.
    private float[] lastPlayedAt; // Thời điểm (realtime) phát gần nhất của từng id, cho minInterval.
    private readonly bool[] categoryOn = new bool[3]; // Theo số của AudioCategory.

    private GameObject root; // Chứa mọi AudioSource của Manager.
    private AudioSource[] voices; // Pool nguồn Sound tạo sẵn.
    private AudioId[] voiceIds; // Id đang kêu trên từng nguồn, để Stop đúng id.
    private AudioSource musicSource;
    private AudioId currentMusic; // Bài đang phát (hoặc đang chờ phát lại khi bật Music).
    private AudioId pendingMusic; // Bài sẽ phát khi nhạc cũ nhỏ dần xong; None = không đổi bài.
    private float musicFade; // 0..1: hệ số âm lượng nhạc hiện tại.
    private bool listenerPaused;
    private bool disposed;

    #endregion

    #region Lifecycle

    public AudioManager(Settings settings) => this.settings = settings ?? throw new ArgumentNullException(nameof(settings));

    /// <summary>Dựng bảng tra, tạo nguồn âm và đọc công tắc từ PlayerPrefs.</summary>
    public override void Initialize()
    {
        BuildLookup();
        CreateSources();
        foreach (AudioCategory category in (AudioCategory[])Enum.GetValues(typeof(AudioCategory)))
            categoryOn[(int)category] = PlayerPrefs.GetInt(PrefsKeyPrefix + category, 1) == 1;
    }

    /// <summary>
    /// Dừng Sound khi gameplay pause (timeScale = 0); âm có ignorePause và nhạc vẫn kêu.
    /// Rồi tiến fade nhạc bằng thời gian thật (Home chạy với timeScale = 0).
    /// </summary>
    public override void Tick()
    {
        bool shouldPause = Time.timeScale <= 0f;
        if (shouldPause != listenerPaused)
        {
            listenerPaused = shouldPause;
            AudioListener.pause = shouldPause;
        }
        TickMusicFade(Time.unscaledDeltaTime);
    }

    /// <summary>Dừng mọi âm và xóa nguồn âm.</summary>
    public override void Dispose()
    {
        if (disposed) return;
        disposed = true;
        AudioListener.pause = false;
        if (root != null) Object.Destroy(root);
    }

    private void BuildLookup()
    {
        int max = 0;
        foreach (AudioId id in (AudioId[])Enum.GetValues(typeof(AudioId))) max = Mathf.Max(max, (int)id);
        entries = new AudioDatabaseSO.Entry[max + 1];
        lastPlayedAt = new float[max + 1];
        for (int i = 0; i < lastPlayedAt.Length; i++) lastPlayedAt[i] = float.NegativeInfinity;
        foreach (AudioDatabaseSO.Entry entry in settings.Database.Entries) entries[(int)entry.Id] = entry;
    }

    private void CreateSources()
    {
        root = new GameObject("Audio");
        voices = new AudioSource[settings.VoiceCount];
        voiceIds = new AudioId[settings.VoiceCount];
        for (int i = 0; i < voices.Length; i++) voices[i] = CreateSource();
        musicSource = CreateSource();
        musicSource.loop = true;
        musicSource.ignoreListenerPause = true; // Nhạc vẫn chạy khi gameplay pause.
    }

    private AudioSource CreateSource()
    {
        var source = root.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f; // Game 2D: âm không phụ thuộc vị trí.
        return source;
    }

    #endregion

    #region IAudio

    /// <inheritdoc/>
    public void Play(AudioId id)
    {
        if (!TryGetEntry(id, out AudioDatabaseSO.Entry entry)) return;
        if (!IsOn(entry.Category))
        {
            // Music đang tắt vẫn nhớ bài cần phát, để bật lại thì phát đúng bài của màn hiện tại.
            if (entry.Category == AudioCategory.Music) currentMusic = id;
            return;
        }
        switch (entry.Category)
        {
            case AudioCategory.Sound: PlaySound(entry); break;
            case AudioCategory.Music: PlayMusic(entry); break;
            case AudioCategory.Haptic: Vibrate(entry); break;
        }
    }

    /// <inheritdoc/>
    public void Stop(AudioId id)
    {
        if (!TryGetEntry(id, out AudioDatabaseSO.Entry entry)) return;
        switch (entry.Category)
        {
            case AudioCategory.Sound: StopSound(id); break;
            case AudioCategory.Music: if (currentMusic == id) StopMusic(); break;
            case AudioCategory.Haptic: break; // Rung ngắn, tự hết.
        }
    }

    // Id không có entry thì bỏ qua (không lỗi): nơi gọi có thể dùng id trước khi data kịp khai báo.
    private bool TryGetEntry(AudioId id, out AudioDatabaseSO.Entry entry)
    {
        int index = (int)id;
        entry = index > 0 && entries != null && index < entries.Length ? entries[index] : default;
        return entry.Id != AudioId.None && !disposed;
    }

    #endregion

    #region Sound

    private void PlaySound(AudioDatabaseSO.Entry entry)
    {
        if (!PassInterval(entry.Id, entry.MinInterval)) return;
        AudioClip clip = PickClip(entry);
        if (clip == null)
        {
            Debug.Log($"[Audio] Sound {entry.Id} (no clip)");
            return;
        }
        int voice = FindFreeVoice();
        if (voice < 0) return; // Hết nguồn: bỏ âm này, không cướp âm đang kêu.
        AudioSource source = voices[voice];
        source.clip = clip;
        source.volume = entry.Volume;
        source.loop = entry.Loop;
        source.ignoreListenerPause = entry.IgnorePause;
        source.Play();
        voiceIds[voice] = entry.Id;
    }

    private void StopSound(AudioId id)
    {
        for (int i = 0; i < voices.Length; i++)
            if (voiceIds[i] == id && voices[i].isPlaying) voices[i].Stop();
    }

    private void StopAllSounds()
    {
        foreach (AudioSource voice in voices) voice.Stop();
    }

    private int FindFreeVoice()
    {
        for (int i = 0; i < voices.Length; i++)
            if (!voices[i].isPlaying) return i;
        return -1;
    }

    private static AudioClip PickClip(AudioDatabaseSO.Entry entry)
    {
        int count = entry.Clips.Count;
        return count == 0 ? null : entry.Clips[count == 1 ? 0 : UnityEngine.Random.Range(0, count)];
    }

    // Dùng thời gian thật để minInterval vẫn đúng khi timeScale = 0 (Home, menu pause).
    private bool PassInterval(AudioId id, float minInterval)
    {
        float now = Time.realtimeSinceStartup;
        if (now - lastPlayedAt[(int)id] < minInterval) return false;
        lastPlayedAt[(int)id] = now;
        return true;
    }

    #endregion

    #region Music

    private void PlayMusic(AudioDatabaseSO.Entry entry)
    {
        if (currentMusic == entry.Id && (musicSource.isPlaying || pendingMusic == entry.Id)) return;
        currentMusic = entry.Id;
        pendingMusic = entry.Id;
        if (!musicSource.isPlaying) musicFade = 0f; // Không có bài cũ: vào thẳng bài mới, chỉ fade-in.
    }

    private void StopMusic()
    {
        currentMusic = AudioId.None;
        pendingMusic = AudioId.None;
        musicSource.Stop();
        musicFade = 0f;
    }

    // Có bài chờ: nhỏ dần bài cũ về 0 rồi đổi clip; sau đó to dần tới 1.
    private void TickMusicFade(float deltaTime)
    {
        if (musicSource == null) return;
        float step = settings.MusicFadeSeconds > 0f ? deltaTime / settings.MusicFadeSeconds : 1f;
        if (pendingMusic != AudioId.None)
        {
            musicFade = musicSource.isPlaying ? Mathf.Max(0f, musicFade - step) : 0f;
            if (musicFade <= 0f) StartPendingMusic();
        }
        else if (musicSource.isPlaying && musicFade < 1f)
        {
            musicFade = Mathf.Min(1f, musicFade + step);
        }
        if (musicSource.isPlaying && TryGetEntry(currentMusic, out AudioDatabaseSO.Entry entry))
            musicSource.volume = entry.Volume * musicFade;
    }

    private void StartPendingMusic()
    {
        AudioId id = pendingMusic;
        pendingMusic = AudioId.None;
        musicSource.Stop();
        if (!TryGetEntry(id, out AudioDatabaseSO.Entry entry)) return;
        AudioClip clip = entry.Clips.Count > 0 ? entry.Clips[0] : null;
        if (clip == null)
        {
            Debug.Log($"[Audio] Music {id} (no clip)");
            return;
        }
        musicSource.clip = clip;
        musicSource.volume = 0f;
        musicSource.Play();
    }

    #endregion

    #region Haptic

    // Chưa rung thật trên thiết bị (Android cần Vibrator theo thời gian, iOS cần plugin); hiện chỉ ghi log để test luồng gọi.
    private void Vibrate(AudioDatabaseSO.Entry entry)
    {
        if (!PassInterval(entry.Id, Mathf.Max(entry.MinInterval, HapticMinInterval))) return;
        Debug.Log($"[Audio] Haptic {entry.Id} {entry.HapticMilliseconds}ms");
    }

    #endregion

    #region IAudioSettings

    /// <inheritdoc/>
    public bool IsOn(AudioCategory category) => categoryOn[(int)category];

    /// <inheritdoc/>
    public void SetOn(AudioCategory category, bool on)
    {
        if (categoryOn[(int)category] == on) return;
        categoryOn[(int)category] = on;
        PlayerPrefs.SetInt(PrefsKeyPrefix + category, on ? 1 : 0);
        PlayerPrefs.Save();
        if (disposed) return;
        if (category == AudioCategory.Sound && !on) StopAllSounds();
        if (category != AudioCategory.Music) return;
        if (!on)
        {
            musicSource.Stop(); // Giữ currentMusic để bật lại thì phát tiếp đúng bài.
            pendingMusic = AudioId.None;
        }
        else if (currentMusic != AudioId.None)
        {
            pendingMusic = currentMusic;
            musicFade = 0f;
        }
    }

    #endregion
}
