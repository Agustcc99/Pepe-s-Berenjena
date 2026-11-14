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

    // El tono del combo de cosecha (J-08): cada cosecha seguida sube una nota de una escala pentatónica mayor (Do, Re, Mi, Sol,
    // La, Do...), que suena bien en cualquier orden; subir de a pasos iguales de frecuencia, como la venta, desafina.
    public static class HarvestScale
    {
        private static readonly int[] Semitones = { 0, 2, 4, 7, 9, 12, 14, 16 };

        public static int MaxStep => Semitones.Length - 1;

        // Por cuánto se multiplica el tono en ese paso del combo (1 en el primero).
        public static float PitchFor(int step)
        {
            if (step < 0) step = 0;
            if (step > MaxStep) step = MaxStep;

            return (float)Math.Pow(2d, Semitones[step] / 12d);
        }
    }

    // Una tanda de eventos seguidos (ventas) y el aviso, UNA vez, de que terminó una tanda grande: la bocina de la venta grande.
    public sealed class StreakTracker
    {
        private readonly double _windowSeconds;
        private readonly int _threshold;
        private double _lastAt;
        private int _count;

        public StreakTracker(double windowSeconds, int threshold)
        {
            _windowSeconds = windowSeconds > 0d ? windowSeconds : 0.01d;
            _threshold = threshold < 1 ? 1 : threshold;
        }

        public int Count => _count;

        // Un evento más. Devuelve cuántos van en la tanda (1 si es el primero o pasó la ventana).
        public int Add(double now)
        {
            if (_count > 0 && now - _lastAt > _windowSeconds) _count = 0;

            _count++;
            _lastAt = now;
            return _count;
        }

        // true una sola vez, cuando una tanda que llegó al umbral ya terminó (pasó la ventana sin eventos nuevos).
        public bool PollBigStreakEnded(double now)
        {
            if (_count == 0 || now - _lastAt <= _windowSeconds) return false;

            bool big = _count >= _threshold;
            _count = 0;
            return big;
        }

        public void Reset()
        {
            _count = 0;
        }
    }
}
