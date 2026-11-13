using System;
using System.Collections.Generic;
using UnityEngine;

namespace FarmIdle
{
    // Pasa la partida de objeto a texto y de texto a objeto. No toca archivos ni escena: solo JsonUtility, así que se
    // prueba en EditMode. Parse nunca tira excepciones: un texto vacío, roto, de una versión desconocida o que no se
    // puede migrar vuelve como un resultado con su estado y su motivo, y quien carga decide qué hacer (SaveFile).
    public static class SaveSerializer
    {
        // Lo mínimo para mirar un texto antes de confiar en él. Sin valores por defecto a propósito: un JSON cualquiera
        // (un objeto vacío, otro juego, un archivo pisado) tiene version 0 y se descarta, en vez de pasar por una partida válida y vacía.
        [Serializable]
        private sealed class Probe
        {
            public int version;
            public SaveSection[] sections;
        }

        [Serializable]
        private sealed class StampProbe
        {
            public string savedAtUtc;
        }

        // Cuándo se escribió ese texto de partida (el sobre lleva la fecha). DateTime.MinValue si no se puede leer: sirve para comparar
        // cuál de dos copias es más nueva sin tener que migrarlas.
        public static DateTime SavedAtOf(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return DateTime.MinValue;

            try
            {
                StampProbe probe = JsonUtility.FromJson<StampProbe>(text);
                if (probe != null && DateTime.TryParse(probe.savedAtUtc, System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.RoundtripKind, out DateTime stamp))
                {
                    return stamp.ToUniversalTime();
                }
            }
            catch (Exception)
            {
                // Un texto que no es JSON no tiene fecha.
            }

            return DateTime.MinValue;
        }

        public static string Serialize(SaveData data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            return JsonUtility.ToJson(data);
        }

        public static SaveParseResult Parse(string text, SaveMigrator migrator)
        {
            if (migrator == null) throw new ArgumentNullException(nameof(migrator));

            if (string.IsNullOrWhiteSpace(text))
            {
                return new SaveParseResult { Status = SaveParseStatus.Empty, Message = "El archivo está vacío." };
            }

            Probe probe;
            try
            {
                probe = JsonUtility.FromJson<Probe>(text);
            }
            catch (Exception e)
            {
                return new SaveParseResult { Status = SaveParseStatus.Corrupt, Message = "No es un JSON válido: " + e.Message };
            }

            if (probe == null || probe.version <= 0 || probe.sections == null)
            {
                return new SaveParseResult { Status = SaveParseStatus.Corrupt, Message = "Falta la versión o la lista de secciones." };
            }

            if (probe.version > migrator.CurrentVersion)
            {
                return new SaveParseResult
                {
                    Status = SaveParseStatus.Future,
                    Message = "La partida es de la versión " + probe.version + " y este juego entiende hasta la " + migrator.CurrentVersion + "."
                };
            }

            SaveData data;
            try
            {
                data = JsonUtility.FromJson<SaveData>(text);
            }
            catch (Exception e)
            {
                return new SaveParseResult { Status = SaveParseStatus.Corrupt, Message = "No se pudo leer la partida: " + e.Message };
            }

            if (data == null)
            {
                return new SaveParseResult { Status = SaveParseStatus.Corrupt, Message = "No se pudo leer la partida." };
            }

            CleanSections(data);

            SaveParseStatus status = SaveParseStatus.Ok;
            if (data.version < migrator.CurrentVersion)
            {
                if (!migrator.TryMigrate(ref data, out string error))
                {
                    return new SaveParseResult { Status = SaveParseStatus.Corrupt, Message = error };
                }

                CleanSections(data);
                status = SaveParseStatus.Migrated;
            }

            return new SaveParseResult { Status = status, Data = data };
        }

        // Saca las secciones nulas o sin id, y si un id se repite se queda con la última.
        private static void CleanSections(SaveData data)
        {
            if (data.sections == null)
            {
                data.sections = new SaveSection[0];
                return;
            }

            var seen = new Dictionary<string, int>();
            var kept = new List<SaveSection>(data.sections.Length);
            foreach (SaveSection section in data.sections)
            {
                if (section == null || string.IsNullOrEmpty(section.id)) continue;

                if (section.json == null) section.json = string.Empty;

                if (seen.TryGetValue(section.id, out int index))
                {
                    kept[index] = section;
                }
                else
                {
                    seen[section.id] = kept.Count;
                    kept.Add(section);
                }
            }

            data.sections = kept.ToArray();
        }
    }
}
