using System;

namespace FarmIdle
{
    // La curva de niveles: cuánta experiencia (XP) hace falta para llegar a cada nivel. Es C# puro, sin UnityEngine:
    // se prueba sin escena. El nivel NO se guarda, se deduce de la XP total (así nunca puede quedar desfasado de ella).
    //   XP acumulada para el nivel L = baseXp x (L - 1)^2   (el nivel 1 es el de partida, con 0 XP)
    // Con baseXp 125 y tope 20: nivel 2 a los 125, nivel 5 a los 2.000, nivel 10 a los 10.125 y nivel 20 a los 45.125 de XP.
    // Es una parábola a propósito: cada nivel pide un poco más que el anterior, pero como el jugador va ganando más por
    // minuto (zonas y cultivos caros) el tiempo entre niveles se mueve en 1 a 3,5 minutos de punta a punta.
    public sealed class LevelCurve
    {
        private readonly long _baseXp;
        private readonly int _maxLevel;

        public LevelCurve(int baseXp, int maxLevel)
        {
            if (baseXp < 1) throw new ArgumentOutOfRangeException(nameof(baseXp), "La XP base tiene que ser al menos 1.");
            if (maxLevel < 2) throw new ArgumentOutOfRangeException(nameof(maxLevel), "Tiene que haber al menos un nivel por encima del 1.");

            _baseXp = baseXp;
            _maxLevel = maxLevel;
        }

        public int MaxLevel => _maxLevel;

        // La XP total con la que se llega al nivel 'level' (el 1 vale 0). Fuera de 1..MaxLevel se acota.
        public long XpForLevel(int level)
        {
            long steps = Math.Max(0, Math.Min(level, _maxLevel) - 1);
            return _baseXp * steps * steps;
        }

        // El nivel que corresponde a esa XP total: el más alto cuyo umbral se alcanzó, hasta el tope.
        public int LevelForXp(long xp)
        {
            if (xp <= 0L) return 1;
            if (xp >= XpForLevel(_maxLevel)) return _maxLevel;   // con el tope por delante, ninguna cuenta de abajo se desborda

            // Raíz entera de xp / baseXp, corregida por redondeo: pocas vueltas, sin recorrer los 20 niveles.
            long steps = (long)Math.Sqrt(xp / (double)_baseXp);
            while ((steps + 1) * (steps + 1) * _baseXp <= xp) steps++;
            while (steps > 0 && steps * steps * _baseXp > xp) steps--;

            return (int)Math.Min(steps + 1, _maxLevel);
        }

        // Cuánta XP lleva dentro del nivel actual y cuánta pide el nivel siguiente (en el tope: 0 y 0).
        public void Progress(long xp, out long intoLevel, out long neededForNext)
        {
            int level = LevelForXp(xp);
            if (level >= _maxLevel)
            {
                intoLevel = 0L;
                neededForNext = 0L;
                return;
            }

            long floor = XpForLevel(level);
            intoLevel = Math.Max(0L, xp - floor);
            neededForNext = XpForLevel(level + 1) - floor;
        }
    }
}
