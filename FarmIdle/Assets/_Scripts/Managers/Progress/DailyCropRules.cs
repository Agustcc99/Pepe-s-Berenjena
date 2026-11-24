using System;
using System.Collections.Generic;

namespace FarmIdle
{
    // IN-02: las cuentas del cultivo del día. Sin escena ni reloj propio: se prueban solas.
    public static class DailyCropRules
    {
        // Elige el cultivo del día. Depende SOLO de la fecha y de la tabla (no del orden en que esté escrita): el mismo día sale el mismo,
        // también si se reinstala el juego.
        //   - Se baraja la lista ENTERA de cultivos con una semilla que sale de la fecha y gana el primero cuya zona está abierta. Así cada
        //     cultivo abierto sale con la misma probabilidad (no sale siempre el que más paga) y, si se abre una zona durante el día, el
        //     cultivo del día solo cambia si el recién abierto cae antes en el orden del día.
        //   - Devuelve null si no hay ninguno abierto.
        public static string Pick(IReadOnlyList<string> cropIds, Predicate<string> isOpen, int dayKey)
        {
            if (cropIds == null || cropIds.Count == 0) return null;

            var ids = new List<string>(cropIds.Count);
            for (int i = 0; i < cropIds.Count; i++)
            {
                if (!string.IsNullOrEmpty(cropIds[i]) && !ids.Contains(cropIds[i])) ids.Add(cropIds[i]);
            }

            ids.Sort(string.CompareOrdinal);

            // La fecha consecutiva (20261124, 20261125...) da semillas casi iguales: se mezcla antes de usarla.
            var random = new Random(unchecked((int)((uint)dayKey * 2654435761u)));
            for (int i = ids.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                string swap = ids[i];
                ids[i] = ids[j];
                ids[j] = swap;
            }

            for (int i = 0; i < ids.Count; i++)
            {
                if (isOpen == null || isOpen(ids[i])) return ids[i];
            }

            return null;
        }
    }
}
