using System;
using System.Collections.Generic;

namespace FarmIdle
{
    // Las cuentas de las misiones diarias (F-05). Sin escena ni reloj propio: se prueban solas.
    public static class MissionRules
    {
        // La fecha como número que ordena bien (20261026): comparar dos días es comparar dos enteros.
        public static int DayKey(DateTime date)
        {
            return date.Year * 10000 + date.Month * 100 + date.Day;
        }

        // Elige las misiones de un día. El resultado depende SOLO de la fecha, el nivel y la tabla (no del orden en que esté escrita ni
        // de cuántas veces se pida): el mismo día salen las mismas, también si se reinstala el juego.
        //   - Solo entran las que el nivel permite.
        //   - Primero una de cada tipo (para no repetir "cosechá" tres veces) y después, si faltan, se completa con cualquiera.
        public static List<MissionData> Pick(IReadOnlyList<MissionData> pool, int level, int dayKey, int count)
        {
            var eligible = new List<MissionData>();
            if (pool != null)
            {
                for (int i = 0; i < pool.Count; i++)
                {
                    if (pool[i] != null && pool[i].MinLevel <= level) eligible.Add(pool[i]);
                }
            }

            eligible.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));

            // Fisher-Yates con una semilla que sale de la fecha.
            var random = new Random(dayKey);
            for (int i = eligible.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                MissionData swap = eligible[i];
                eligible[i] = eligible[j];
                eligible[j] = swap;
            }

            var picked = new List<MissionData>(count);
            var kinds = new HashSet<MissionKind>();
            for (int i = 0; i < eligible.Count && picked.Count < count; i++)
            {
                if (kinds.Add(eligible[i].Kind)) picked.Add(eligible[i]);
            }

            for (int i = 0; i < eligible.Count && picked.Count < count; i++)
            {
                if (!picked.Contains(eligible[i])) picked.Add(eligible[i]);
            }

            return picked;
        }

        // Monedas de un premio de 'minutes' minutos de producción, con un mínimo. Con un ritmo ilegible (NaN, negativo) vale el mínimo.
        public static long Coins(double coinsPerSecond, double minutes, long minCoins)
        {
            double coins = coinsPerSecond * 60d * minutes;
            long floor = Math.Max(0L, minCoins);

            if (double.IsNaN(coins) || coins <= floor) return floor;
            return coins >= long.MaxValue ? long.MaxValue : (long)Math.Floor(coins);
        }
    }
}
