using UnityEngine;

namespace HyperPuzzle2D.Feedback
{
    /// <summary>
    /// Platform vibration. Editor and desktop builds no-op; mobile uses Handheld.Vibrate.
    /// Gate is Progress.HapticsEnabled so the settings toggle is one place.
    /// </summary>
    public static class Haptics
    {
        public static void Light()
        {
            if (!Meta.Progress.HapticsEnabled)
            {
                return;
            }

#if UNITY_IOS || UNITY_ANDROID
            Handheld.Vibrate();
#endif
        }
    }
}
