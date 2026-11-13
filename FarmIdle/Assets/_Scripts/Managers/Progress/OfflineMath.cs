using System;

namespace FarmIdle
{
    // La cuenta del progreso offline (B-01). C# puro, sin UnityEngine: se prueba sin escena.
    public static class OfflineMath
    {
        // Segundos que pasaron entre la última vez que se vio al jugador y ahora. 0 si no hay marca, si el reloj retrocedió (el jugador
        // atrasó la hora: no se acredita nada) o si los datos no son válidos.
        public static double ElapsedSeconds(DateTime lastSeenUtc, DateTime nowUtc)
        {
            if (lastSeenUtc == default(DateTime) || nowUtc <= lastSeenUtc) return 0d;

            double seconds = (nowUtc - lastSeenUtc).TotalSeconds;
            return double.IsNaN(seconds) || seconds < 0d ? 0d : seconds;
        }

        // Monedas que se acreditan por 'elapsedSeconds' fuera, con tope de 'capSeconds'. 'baseCoinsPerSecond' es lo que rinden las
        // parcelas abiertas cosechadas sin parar (precio base / crecimiento); se multiplica por el precio (mejoras x prestigio), por la
        // velocidad de crecimiento y por la eficiencia (nadie cosecha ni vende, así que es una fracción). Se redondea hacia abajo.
        // 'maxCoinsPerHour' limita el resultado a esa cantidad por cada hora acreditada (con el tope de tiempo de 2 h y 25.000 por hora, nunca
        // más de 50.000); 0 o menos = sin límite en monedas.
        public static long Coins(double elapsedSeconds, double capSeconds, double baseCoinsPerSecond, double priceMultiplier, double growthSpeed, double efficiency, double maxCoinsPerHour = 0d)
        {
            double seconds = Math.Min(Math.Max(0d, elapsedSeconds), Math.Max(0d, capSeconds));
            double coins = seconds * Math.Max(0d, baseCoinsPerSecond) * Math.Max(0d, priceMultiplier) * Math.Max(0d, growthSpeed) * Math.Max(0d, efficiency);
            if (maxCoinsPerHour > 0d) coins = Math.Min(coins, maxCoinsPerHour * seconds / 3600d);

            if (double.IsNaN(coins) || coins <= 0d) return 0L;
            return coins >= long.MaxValue ? long.MaxValue : (long)Math.Floor(coins);
        }
    }
}
