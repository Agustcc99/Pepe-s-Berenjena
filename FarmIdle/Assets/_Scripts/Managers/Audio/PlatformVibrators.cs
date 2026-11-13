using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace FarmIdle
{
#if UNITY_ANDROID && !UNITY_EDITOR
    // Vibración de Android con VibrationEffect.createOneShot (API 26+, el minSdk del juego es 26). Los objetos de Java se piden una sola
    // vez; si algo falla (sin motor, sin permiso) se apaga solo y el juego sigue sin vibrar. El permiso VIBRATE sale de
    // Plugins/Android/FarmIdleVibration.androidlib.
    public sealed class AndroidVibrator : IVibrator
    {
        private AndroidJavaObject _vibrator;
        private AndroidJavaClass _effects;
        private bool _failed;

        public string Name => "Android";

        public void Pulse(int milliseconds, int amplitude)
        {
            if (_failed) return;

            try
            {
                if (_vibrator == null)
                {
                    using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                    using (AndroidJavaObject activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                    {
                        _vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                    }

                    _effects = new AndroidJavaClass("android.os.VibrationEffect");
                }

                if (!_vibrator.Call<bool>("hasVibrator")) return;

                using (AndroidJavaObject effect = _effects.CallStatic<AndroidJavaObject>("createOneShot", (long)milliseconds, Mathf.Clamp(amplitude, 1, 255)))
                {
                    _vibrator.Call("vibrate", effect);
                }
            }
            catch (Exception e)
            {
                _failed = true;
                Debug.LogWarning("[FarmIdle] Vibración: se apaga (" + e.Message + ").");
            }
        }
    }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
    // Vibración del navegador (navigator.vibrate): solo en pantallas táctiles; el puente está en FarmIdleWeb.jslib.
    public sealed class WebVibrator : IVibrator
    {
        [DllImport("__Internal")]
        private static extern void FI_Vibrate(int milliseconds);

        public string Name => "Web";

        public void Pulse(int milliseconds, int amplitude)
        {
            FI_Vibrate(milliseconds);
        }
    }
#endif
}
