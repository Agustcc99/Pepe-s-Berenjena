using System;

namespace FarmIdle
{
    // La velocidad final del jugador: base x permanente (mejoras y perks) x buff temporal, con un tope final.
    // Sin Unity: se prueba con números.
    //   Números del juego: base 5 u/s, permanente máximo x1,5 = 7,5 u/s, buff x3 y tope final de 11 u/s (GameConfig.MaxFinalMoveSpeed).
    //   Sin tope, el x3 sobre lo permanente daría 22,5 u/s en un campo de ~7 unidades: incontrolable. Con el tope:
    //     jugador nuevo (permanente x1):    5 -> 11 u/s con el buff (se nota x2,2)
    //     jugador al máximo (permanente x1,5): 7,5 -> 11 u/s con el buff (se nota x1,47)
    //   El tope solo recorta lo que el BUFF suma de más: nunca baja la velocidad permanente (si algún día el permanente superara el
    //   tope, el jugador conserva el permanente).
    public static class MoveSpeedRules
    {
        public static float Final(float baseSpeed, float permanentMultiplier, float buffMultiplier, float maxFinalSpeed)
        {
            if (baseSpeed < 0f || float.IsNaN(baseSpeed)) throw new ArgumentOutOfRangeException(nameof(baseSpeed));
            if (permanentMultiplier < 0f || float.IsNaN(permanentMultiplier)) throw new ArgumentOutOfRangeException(nameof(permanentMultiplier));
            if (buffMultiplier < 0f || float.IsNaN(buffMultiplier)) throw new ArgumentOutOfRangeException(nameof(buffMultiplier));

            float permanent = baseSpeed * permanentMultiplier;
            float boosted = permanent * buffMultiplier;
            if (!(maxFinalSpeed > 0f)) return boosted;   // sin tope

            // El tope no puede estar por debajo de lo permanente, y un buff menor que 1 (ralentizar) pasa tal cual.
            return Math.Min(boosted, Math.Max(maxFinalSpeed, permanent));
        }
    }
}
