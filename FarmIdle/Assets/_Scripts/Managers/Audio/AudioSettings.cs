using System;

namespace FarmIdle
{
    // Los ajustes de audio del jugador: un volumen general, uno de música, uno de efectos y el mute. Es C# puro, sin UnityEngine: se
    // prueba sin escena. SettingsManager los guarda en la partida ("settings") y AudioManager los traduce a volumen real.
    //   - Los controles van de 0 a 1 en línea recta (lo que se ve en el deslizador); la ganancia real es el cuadrado, que se oye más
    //     parejo que una recta (el oído es logarítmico).
    //   - Cualquier valor raro (NaN, fuera de rango) se acota: nunca rompe el mezclador.
    public sealed class AudioSettings
    {
        public const float DefaultMaster = 1f;
        public const float DefaultMusic = 0.7f;
        public const float DefaultSfx = 1f;

        public float Master { get; private set; } = DefaultMaster;
        public float Music { get; private set; } = DefaultMusic;
        public float Sfx { get; private set; } = DefaultSfx;
        public bool Muted { get; private set; }

        // Se dispara cuando algo cambió de verdad (no si se vuelve a poner el mismo valor).
        public event Action Changed;

        // Ganancia final (0 a 1) de cada categoría; con mute vale 0.
        public float MusicGain => Muted ? 0f : Curve(Master) * Curve(Music);
        public float SfxGain => Muted ? 0f : Curve(Master) * Curve(Sfx);

        public void SetMaster(float value)
        {
            float v = Clamp01(value);
            if (v == Master) return;

            Master = v;
            Changed?.Invoke();
        }

        public void SetMusic(float value)
        {
            float v = Clamp01(value);
            if (v == Music) return;

            Music = v;
            Changed?.Invoke();
        }

        public void SetSfx(float value)
        {
            float v = Clamp01(value);
            if (v == Sfx) return;

            Sfx = v;
            Changed?.Invoke();
        }

        public void SetMuted(bool value)
        {
            if (value == Muted) return;

            Muted = value;
            Changed?.Invoke();
        }

        // Reemplaza todo de una vez (partida guardada o "nueva partida"): un solo aviso.
        public void Load(float master, float music, float sfx, bool muted)
        {
            float m = Clamp01(master), mu = Clamp01(music), s = Clamp01(sfx);
            if (m == Master && mu == Music && s == Sfx && muted == Muted) return;

            Master = m;
            Music = mu;
            Sfx = s;
            Muted = muted;
            Changed?.Invoke();
        }

        public void ResetToDefaults()
        {
            Load(DefaultMaster, DefaultMusic, DefaultSfx, false);
        }

        public static float Curve(float slider)
        {
            float v = Clamp01(slider);
            return v * v;
        }

        private static float Clamp01(float value)
        {
            if (float.IsNaN(value)) return 0f;
            if (value < 0f) return 0f;
            return value > 1f ? 1f : value;
        }
    }
}
