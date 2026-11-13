using System;

namespace FarmIdle
{
    // Dos reglas del audio que no necesitan oído para probarse. Es C# puro, sin UnityEngine: el reloj se lo pasa AudioManager (tiempo sin
    // escala).
    //   - Límite por sonido: dos disparos del mismo sonido no pueden estar más cerca que su mínimo (una cosecha en cada frame no es una
    //     ametralladora). El primero siempre suena.
    //   - Combo de venta: vender varias unidades seguidas sube el tono un paso por unidad (hasta un tope); si pasa un rato sin vender
    //     vuelve a empezar. Es el "ding, ding, ding" ascendente de cualquier juego de monedas.
    public sealed class SoundThrottle
    {
        private readonly double[] _lastAt;
        private readonly bool[] _played;

        public SoundThrottle(int cueCount)
        {
            if (cueCount < 1) throw new ArgumentOutOfRangeException(nameof(cueCount));

            _lastAt = new double[cueCount];
            _played = new bool[cueCount];
        }

        // true si ese sonido puede sonar ahora; si puede, anota el disparo.
        public bool TryPlay(int cue, double now, double minInterval)
        {
            if (cue < 0 || cue >= _lastAt.Length) return false;

            if (_played[cue] && now - _lastAt[cue] < minInterval) return false;

            _played[cue] = true;
            _lastAt[cue] = now;
            return true;
        }

        public void Reset()
        {
            Array.Clear(_played, 0, _played.Length);
            Array.Clear(_lastAt, 0, _lastAt.Length);
        }
    }

    public sealed class ComboCounter
    {
        private readonly double _windowSeconds;
        private readonly int _maxSteps;
        private double _lastAt;
        private int _step = -1;

        public ComboCounter(double windowSeconds, int maxSteps)
        {
            _windowSeconds = windowSeconds > 0d ? windowSeconds : 0.01d;
            _maxSteps = maxSteps < 0 ? 0 : maxSteps;
        }

        // El paso de este disparo: 0 si es el primero (o pasó la ventana), y uno más cada vez, hasta el tope.
        public int Next(double now)
        {
            if (_step < 0 || now - _lastAt > _windowSeconds) _step = 0;
            else if (_step < _maxSteps) _step++;

            _lastAt = now;
            return _step;
        }

        public void Reset()
        {
            _step = -1;
        }
    }
}
