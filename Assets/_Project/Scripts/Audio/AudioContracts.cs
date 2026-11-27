// Ranh giới của miền Audio: đây là TẤT CẢ những gì miền khác được phép dùng.
// Mọi interface trong file này do AudioManager implement. Audio không phụ thuộc miền nào;
// nơi cần âm thanh/nhạc/rung gọi xuống qua IAudio.

/// <summary>Phát và dừng âm thanh, nhạc hoặc rung. <see cref="AudioId"/> cho biết nó là gì.</summary>
public interface IAudio
{
    /// <summary>
    /// Phát theo loại của id: Sound (một lần hoặc lặp), Music (đổi bài, cùng bài thì bỏ qua), Haptic (rung).
    /// Bỏ qua khi id là None, loại đó đang tắt trong Settings, hoặc gọi dồn quá nhanh.
    /// </summary>
    void Play(AudioId id);

    /// <summary>Dừng: Sound dừng mọi bản đang kêu của id; Music dừng nếu đúng bài đang phát; Haptic không làm gì.</summary>
    void Stop(AudioId id);
}

/// <summary>Công tắc bật/tắt Sound, Music, Haptic của người chơi. Chỉ popup Settings/Pause dùng.</summary>
public interface IAudioSettings
{
    /// <summary>Loại này đang bật hay không (mặc định bật).</summary>
    bool IsOn(AudioCategory category);

    /// <summary>Bật/tắt và lưu ngay (PlayerPrefs). Tắt thì dừng ngay âm/nhạc đang kêu; bật Music lại thì phát tiếp bài hiện tại.</summary>
    void SetOn(AudioCategory category, bool on);
}
