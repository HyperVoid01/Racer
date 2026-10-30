using UnityEngine;

/// <summary>
/// Short vibrations on mobile. Android uses the Vibrator service so we can control the duration
/// (Handheld.Vibrate is a fixed, long buzz). iOS only exposes the standard buzz, so it is used for big events only.
/// Respects the player's haptics setting.
/// </summary>
public static class Haptics
{
#if UNITY_ANDROID && !UNITY_EDITOR
    private static AndroidJavaObject _vibrator;
    private static bool _initialised;
#endif

    public static bool Enabled
    {
        get => SaveSystem.Data.hapticsEnabled;
        set
        {
            SaveSystem.Data.hapticsEnabled = value;
            SaveSystem.MarkDirty();
        }
    }

    public static void Light() => Vibrate(20, false);
    public static void Medium() => Vibrate(45, false);
    public static void Heavy() => Vibrate(90, true);

    private static void Vibrate(long milliseconds, bool allowFullBuzz)
    {
        if (!Enabled) return;

#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            if (!_initialised)
            {
                _initialised = true;
                using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                _vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
            }

            if (_vibrator != null)
            {
                _vibrator.Call("vibrate", milliseconds);
                return;
            }
        }
        catch (System.Exception)
        {
            _vibrator = null;
        }
        // Fallback. Referencing Handheld.Vibrate also makes Unity add the VIBRATE permission to the manifest.
        if (allowFullBuzz) Handheld.Vibrate();
#elif UNITY_IOS && !UNITY_EDITOR
        if (allowFullBuzz) Handheld.Vibrate();
#endif
    }
}
