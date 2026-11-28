using System;
using UnityEngine;

/// <summary>
/// Rung máy theo thời gian (mili giây) trên từng nền tảng. Do <see cref="AudioManager"/> tạo và gọi khi phát id loại Haptic.
/// </summary>
/// <remarks>
/// Tách khỏi AudioManager vì đây là code riêng nền tảng (gọi Java trên Android), không liên quan tới phát âm.
/// <list type="bullet">
/// <item>Android: gọi <c>Vibrator</c> của hệ điều hành với đúng thời gian (API 26+ dùng <c>VibrationEffect</c>).</item>
/// <item>iOS: Unity chỉ có <see cref="Handheld.Vibrate"/> (rung cố định ~0.4 giây), nên chỉ rung cho kiểu mạnh,
/// để rung nhẹ khi trúng đòn không thành rung dài liên tục.</item>
/// <item>Editor/PC: không có máy rung, chỉ ghi log khi bật log.</item>
/// </list>
/// Tham chiếu <see cref="Handheld.Vibrate"/> trong code cũng là cách để Unity tự thêm quyền VIBRATE vào manifest Android.
/// </remarks>
public sealed class HapticDevice : IDisposable
{
    #region Fields

    private const int IosMinMilliseconds = 60; // Dưới mức này (Light/Medium) iOS bỏ qua, xem remarks.
    private const int VibrationEffectApiLevel = 26;
    private const int DefaultAmplitude = -1; // VibrationEffect.DEFAULT_AMPLITUDE.

    private readonly bool log;

#if UNITY_ANDROID && !UNITY_EDITOR
    private AndroidJavaObject vibrator; // null = máy không có bộ rung hoặc lấy service lỗi.
    private AndroidJavaClass vibrationEffect; // Chỉ có từ API 26.
#endif

    #endregion

    #region Lifecycle

    /// <summary>Lấy bộ rung của hệ điều hành một lần; lỗi thì rung về sau sẽ dùng Handheld.Vibrate.</summary>
    /// <param name="log">Ghi log mỗi lần rung (dùng để test luồng gọi trong Editor).</param>
    public HapticDevice(bool log)
    {
        this.log = log;
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
            {
                vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                if (version.GetStatic<int>("SDK_INT") >= VibrationEffectApiLevel)
                    vibrationEffect = new AndroidJavaClass("android.os.VibrationEffect");
            }
            if (vibrator != null && !vibrator.Call<bool>("hasVibrator")) Release();
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[Audio] Android vibrator unavailable: {exception.Message}");
            Release();
        }
#endif
    }

    /// <summary>Trả các object Java đang giữ.</summary>
    public void Dispose()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        Release();
#endif
    }

    #endregion

    #region Vibrate

    /// <summary>Rung một lần trong <paramref name="milliseconds"/> mili giây; 0 hoặc âm thì bỏ qua.</summary>
    public void Vibrate(int milliseconds)
    {
        if (milliseconds <= 0) return;
        if (log) Debug.Log($"[Audio] Haptic {milliseconds}ms");
#if UNITY_ANDROID && !UNITY_EDITOR
        VibrateAndroid(milliseconds);
#elif UNITY_IOS && !UNITY_EDITOR
        if (milliseconds >= IosMinMilliseconds) Handheld.Vibrate();
#endif
    }

    #endregion

#if UNITY_ANDROID && !UNITY_EDITOR
    #region Android

    // Mỗi lần rung tạo một VibrationEffect (object Java): rung cách nhau tối thiểu 0.1 giây nên không đáng kể.
    private void VibrateAndroid(int milliseconds)
    {
        if (vibrator == null)
        {
            Handheld.Vibrate();
            return;
        }
        try
        {
            if (vibrationEffect == null)
            {
                vibrator.Call("vibrate", (long)milliseconds);
                return;
            }
            using (AndroidJavaObject effect = vibrationEffect.CallStatic<AndroidJavaObject>("createOneShot", (long)milliseconds, DefaultAmplitude))
                vibrator.Call("vibrate", effect);
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[Audio] Vibrate failed: {exception.Message}");
            Release();
            Handheld.Vibrate();
        }
    }

    private void Release()
    {
        vibrator?.Dispose();
        vibrator = null;
        vibrationEffect?.Dispose();
        vibrationEffect = null;
    }

    #endregion
#endif
}
