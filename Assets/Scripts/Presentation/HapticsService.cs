using UnityEngine;
using Yahtzee.Services;

namespace Yahtzee.Presentation
{
    /// <summary>Vibration feedback (design §5.5). Two intensities in v1: a light tap when a die is
    /// kept, and a success buzz for a five of a kind. Gated by <see cref="GameSettings.HapticsEnabled"/>
    /// and, on the OS side, by the device's own vibration setting.
    ///
    /// On Android it drives the platform Vibrator so "light" and "success" actually feel
    /// different; everywhere else (and on any device where that call fails) it falls back to
    /// <see cref="Handheld.Vibrate"/>, a single coarse buzz. All of it is wrapped in try/catch —
    /// a missing or quirky vibrator must never take down a turn.</summary>
    public static class HapticsService
    {
        /// <summary>A brief tick — keeping or releasing a die.</summary>
        public static void Light() => Vibrate(18, amplitude: 90);

        /// <summary>A firmer confirmation — currently unused, reserved for score lock-in.</summary>
        public static void Medium() => Vibrate(35, amplitude: 160);

        /// <summary>A celebratory double-buzz — a five of a kind.</summary>
        public static void Success() => Pattern(new long[] { 0, 40, 60, 120 });

        private static void Vibrate(long milliseconds, int amplitude)
        {
            if (!GameSettings.HapticsEnabled)
                return;
#if UNITY_ANDROID && !UNITY_EDITOR
            if (AndroidVibrate(milliseconds, amplitude, null))
                return;
#endif
            Fallback();
        }

        private static void Pattern(long[] pattern)
        {
            if (!GameSettings.HapticsEnabled)
                return;
#if UNITY_ANDROID && !UNITY_EDITOR
            if (AndroidVibrate(0, 0, pattern))
                return;
#endif
            Fallback();
        }

        private static void Fallback()
        {
            try { Handheld.Vibrate(); } catch { /* no vibrator; ignore */ }
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        /// <summary>Drive the Android Vibrator. Uses VibrationEffect on API 26+ (so amplitude and
        /// patterns are honoured) and the deprecated call below that. Returns false on any failure
        /// so the caller can fall back.</summary>
        private static bool AndroidVibrate(long milliseconds, int amplitude, long[] pattern)
        {
            try
            {
                using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                using var vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                if (vibrator == null || !vibrator.Call<bool>("hasVibrator"))
                    return false;

                int sdk = new AndroidJavaClass("android.os.Build$VERSION").GetStatic<int>("SDK_INT");
                if (sdk >= 26)
                {
                    using var effectClass = new AndroidJavaClass("android.os.VibrationEffect");
                    AndroidJavaObject effect = pattern != null
                        ? effectClass.CallStatic<AndroidJavaObject>("createWaveform", pattern, -1)
                        : effectClass.CallStatic<AndroidJavaObject>("createOneShot", milliseconds, Mathf.Clamp(amplitude, 1, 255));
                    vibrator.Call("vibrate", effect);
                }
                else if (pattern != null)
                {
                    vibrator.Call("vibrate", pattern, -1);
                }
                else
                {
                    vibrator.Call("vibrate", milliseconds);
                }
                return true;
            }
            catch
            {
                return false;
            }
        }
#endif
    }
}
