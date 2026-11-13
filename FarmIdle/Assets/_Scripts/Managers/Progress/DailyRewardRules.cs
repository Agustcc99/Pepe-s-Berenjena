using System;

namespace FarmIdle
{
    // Las cuentas del premio de login diario (F-07). Sin escena ni reloj propio: se prueban solas.
    public static class DailyRewardRules
    {
        // La racha es de 7 días: después del 7.º vuelve al 1.º con la misma escalera de premios.
        public const int CycleDays = 7;

        // La fecha como número que ordena bien (20261026); es la misma clave que usan las misiones.
        public static int DayKeyOf(DateTime date)
        {
            return MissionRules.DayKey(date);
        }

        // La fecha de una clave 20261026 (MissionRules.DayKey), o false si no es una fecha válida (0, basura de una partida rota).
        public static bool TryDate(int dayKey, out DateTime date)
        {
            date = default;
            if (dayKey <= 0) return false;

            int year = dayKey / 10000;
            int month = dayKey / 100 % 100;
            int day = dayKey % 100;
            if (year < 1 || year > 9999 || month < 1 || month > 12 || day < 1 || day > DateTime.DaysInMonth(year, month)) return false;

            date = new DateTime(year, month, day);
            return true;
        }

        // Si hoy se puede reclamar y cuánto vale la racha después de hacerlo.
        //   - Nunca reclamó (o la fecha guardada es ilegible): se puede y empieza la racha en 1.
        //   - Hoy o un día futuro (reloj atrasado): no se puede, y no se pierde nada.
        //   - Ayer: la racha sigue (+1). Con 'graceDays' días salteados todavía sigue (con 1, saltear un solo día se perdona);
        //     saltear más días la pierde y vuelve a 1.
        public static bool CanClaim(int lastClaimKey, int streak, int todayKey, int graceDays, out int nextStreak)
        {
            nextStreak = 1;
            if (!TryDate(todayKey, out DateTime today)) return false;
            if (!TryDate(lastClaimKey, out DateTime last)) return true;

            int gap = (int)(today - last).TotalDays;
            if (gap <= 0) return false;

            nextStreak = gap <= 1 + Math.Max(0, graceDays) ? Math.Max(0, streak) + 1 : 1;
            return true;
        }

        // El número de día de la escalera (1 a 7) que corresponde a una racha de 'streak' días reclamados seguidos.
        public static int DayOfCycle(int streak)
        {
            return streak <= 0 ? 0 : (streak - 1) % CycleDays + 1;
        }
    }
}
