using System;
using System.Collections.Generic;

namespace FarmIdle
{
    // Lleva una partida guardada con un formato viejo al formato actual, un salto de versión por vez (1 -> 2 -> 3...).
    // Cada paso sabe convertir de la versión N a la N+1 y nada más; así agregar el formato 4 es sumar UN paso, sin tocar
    // los anteriores. Hoy el juego está en la versión 1 y no hay pasos; la clase está probada con pasos de prueba para que el
    // día que haga falta el primero ya esté todo andando.
    public sealed class SaveMigrator
    {
        // Convierte de la versión N a la N+1. Puede cambiar el objeto que recibe o devolver uno nuevo.
        public delegate SaveData Step(SaveData data);

        private readonly Dictionary<int, Step> _steps = new Dictionary<int, Step>();

        public SaveMigrator(int currentVersion, IReadOnlyDictionary<int, Step> steps = null)
        {
            if (currentVersion < 1) throw new ArgumentOutOfRangeException(nameof(currentVersion), "La versión actual tiene que ser al menos 1.");

            CurrentVersion = currentVersion;
            if (steps != null)
            {
                foreach (KeyValuePair<int, Step> step in steps) _steps[step.Key] = step.Value;
            }
        }

        // El migrador del juego: formato actual y los pasos reales (ninguno todavía).
        public static SaveMigrator Default { get; } = new SaveMigrator(SaveData.CurrentVersion);

        public int CurrentVersion { get; }

        // Lleva 'data' hasta CurrentVersion y deja en 'data' el resultado (un paso puede reemplazar el objeto). Devuelve false,
        // con el motivo, si la versión es más nueva que la actual, si falta algún paso o si un paso falla; en ese caso 'data'
        // no se modifica.
        public bool TryMigrate(ref SaveData data, out string error)
        {
            error = string.Empty;

            SaveData current = data;
            if (current == null)
            {
                error = "No hay datos para migrar.";
                return false;
            }

            if (current.version > CurrentVersion)
            {
                error = "La versión " + current.version + " es más nueva que la " + CurrentVersion + " que entiende este juego.";
                return false;
            }

            while (current.version < CurrentVersion)
            {
                int from = current.version;
                if (from < 1 || !_steps.TryGetValue(from, out Step step) || step == null)
                {
                    error = "No hay cómo migrar la versión " + from + " a la " + (from + 1) + ".";
                    return false;
                }

                SaveData migrated;
                try
                {
                    migrated = step(current);
                }
                catch (Exception e)
                {
                    error = "El paso " + from + " -> " + (from + 1) + " falló: " + e.Message;
                    return false;
                }

                if (migrated == null)
                {
                    error = "El paso " + from + " -> " + (from + 1) + " no devolvió datos.";
                    return false;
                }

                migrated.version = from + 1;   // la versión la pone el migrador: un paso no puede quedarse en el mismo número y colgar el ciclo
                current = migrated;
            }

            data = current;
            return true;
        }
    }
}
