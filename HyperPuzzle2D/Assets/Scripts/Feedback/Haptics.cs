using UnityEngine;

namespace HyperPuzzle2D.Feedback
{
    /// <summary>
    /// Platform vibration. Editor and desktop builds no-op; mobile uses Handheld.Vibrate.
    /// Gate is Progress.HapticsEnabled so the settings toggle is one place.
    /// </summary>
    public static class Haptics
    {
        public static void Light() => Pulse(18, 55);
        public static void Medium() => Pulse(32, 125);
        public static void Heavy() => Pulse(55, 220);

        static void Pulse(long milliseconds, int amplitude)
        {
            if (!Meta.Progress.HapticsEnabled)
            {
                return;
            }

#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator"))
                using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
                {
                    if (version.GetStatic<int>("SDK_INT") >= 26)
                    {
                        using (var effectClass = new AndroidJavaClass("android.os.VibrationEffect"))
                        using (var effect = effectClass.CallStatic<AndroidJavaObject>("createOneShot", milliseconds, amplitude))
                        {
                            vibrator.Call("vibrate", effect);
                        }
                    }
                    else
                    {
                        vibrator.Call("vibrate", milliseconds);
                    }
                }
            }
            catch
            {
                Handheld.Vibrate();
            }
#elif UNITY_IOS && !UNITY_EDITOR
            Handheld.Vibrate();
#endif
        }
    }
}
