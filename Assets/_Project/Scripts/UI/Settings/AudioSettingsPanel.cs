using UnityEngine;
using Zenject;

/// <summary>
/// Ba công tắc Sound / Music / Vibration trong popup Settings (Home) và Pause (trong trận); nối thẳng vào <see cref="IAudioSettings"/>.
/// </summary>
/// <remarks>Một component dùng cho cả hai popup để không lặp code. Đọc lại trạng thái mỗi lần mở popup.</remarks>
public sealed class AudioSettingsPanel : MonoBehaviour
{
    #region References

    [SerializeField] private ToggleSwitch soundSwitch;
    [SerializeField] private ToggleSwitch musicSwitch;
    [SerializeField] private ToggleSwitch vibrationSwitch;

    private IAudioSettings audioSettings;

    #endregion

    #region Lifecycle

    [Inject]
    private void Construct(IAudioSettings settings)
    {
        audioSettings = settings;
        if (isActiveAndEnabled) Refresh(); // Panel đã bật trước khi được inject.
    }

    private void OnEnable()
    {
        soundSwitch.Toggled += OnSoundToggled;
        musicSwitch.Toggled += OnMusicToggled;
        vibrationSwitch.Toggled += OnVibrationToggled;
        if (audioSettings != null) Refresh();
    }

    private void OnDisable()
    {
        soundSwitch.Toggled -= OnSoundToggled;
        musicSwitch.Toggled -= OnMusicToggled;
        vibrationSwitch.Toggled -= OnVibrationToggled;
    }

    #endregion

    #region Private Helpers

    private void Refresh()
    {
        soundSwitch.SetIsOn(audioSettings.IsOn(AudioCategory.Sound), animate: false);
        musicSwitch.SetIsOn(audioSettings.IsOn(AudioCategory.Music), animate: false);
        vibrationSwitch.SetIsOn(audioSettings.IsOn(AudioCategory.Haptic), animate: false);
    }

    private void OnSoundToggled(bool on) => audioSettings?.SetOn(AudioCategory.Sound, on);
    private void OnMusicToggled(bool on) => audioSettings?.SetOn(AudioCategory.Music, on);
    private void OnVibrationToggled(bool on) => audioSettings?.SetOn(AudioCategory.Haptic, on);

    #endregion
}
