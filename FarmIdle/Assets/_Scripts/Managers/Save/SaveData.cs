using System;

namespace FarmIdle
{
    // El archivo de la partida: un sobre versionado con una sección por sistema (ver ISaveable). JsonUtility lo entiende
    // tal cual. 'version' es la del SOBRE; cada sección lleva además su propia versión adentro.
    [Serializable]
    public sealed class SaveData
    {
        public const int CurrentVersion = 1;

        public int version = CurrentVersion;
        public string savedAtUtc = string.Empty;   // ISO 8601, solo informativo (el panel de debug lo muestra)
        public SaveSection[] sections = new SaveSection[0];

        // La sección con ese id, o null.
        public SaveSection Find(string id)
        {
            if (sections == null || string.IsNullOrEmpty(id)) return null;

            for (int i = 0; i < sections.Length; i++)
            {
                if (sections[i] != null && sections[i].id == id) return sections[i];
            }

            return null;
        }

        // Agrega la sección o reemplaza la que ya tenía ese id.
        public void Set(string id, string json)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("La sección necesita un id.", nameof(id));

            SaveSection existing = Find(id);
            if (existing != null)
            {
                existing.json = json ?? string.Empty;
                return;
            }

            int length = sections != null ? sections.Length : 0;
            var next = new SaveSection[length + 1];
            if (length > 0) Array.Copy(sections, next, length);
            next[length] = new SaveSection { id = id, json = json ?? string.Empty };
            sections = next;
        }
    }

    // El estado de un sistema: su id y su JSON propio.
    [Serializable]
    public sealed class SaveSection
    {
        public string id;
        public string json;
    }

    // Cómo salió la lectura de un texto de partida (ver SaveSerializer.Parse).
    public enum SaveParseStatus
    {
        Ok,         // versión actual, lista para usar
        Migrated,   // venía de una versión vieja y se llevó a la actual
        Empty,      // texto vacío o en blanco
        Corrupt,    // no es JSON, le falta la versión o las secciones, o no se pudo migrar
        Future      // la versión es más nueva que la que entiende este juego
    }

    public sealed class SaveParseResult
    {
        public SaveParseStatus Status;
        public SaveData Data;       // solo con Ok / Migrated
        public string Message = string.Empty;

        public bool IsUsable => Status == SaveParseStatus.Ok || Status == SaveParseStatus.Migrated;
    }
}
