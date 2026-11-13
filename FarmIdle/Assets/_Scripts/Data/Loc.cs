using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace FarmIdle
{
    // Idiomas que el juego sabe mostrar. El orden es el de las columnas de la tabla de textos (después de la clave).
    public enum GameLanguage
    {
        Spanish = 0,
        English = 1
    }

    // Tabla de textos del juego (ROADMAP U-03): una clave por texto visible y una columna por idioma (Resources/Localization.csv:
    // key,es,en). Es estática y sin dependencias de escena, para que la usen las vistas, los pads, los datos y las pruebas por igual.
    //   - Get no asigna memoria (es una búsqueda en un diccionario): se puede llamar al armar un cartel. Format sí asigna (boxing y
    //     string.Format): usarlo solo en eventos, nunca por cuadro.
    //   - Sin traducción al inglés usa el español; sin clave devuelve la propia clave (se ve a simple vista y una prueba lo atrapa).
    //   - La tabla se carga sola la primera vez que se usa. Cambiar el idioma avisa por Changed para que lo que ya está en pantalla se
    //     reescriba.
    //   - "\n" dentro de una celda del CSV vale como salto de línea.
    public static class Loc
    {
        public const string ResourcePath = "Localization";

        private static Dictionary<string, string[]> _table;
        private static GameLanguage _current = GameLanguage.Spanish;

        public static GameLanguage Current => _current;

        // Se dispara cuando cambia el idioma activo (no si se vuelve a poner el mismo).
        public static event Action Changed;

        public static bool IsLoaded => _table != null;

        public static int KeyCount
        {
            get
            {
                EnsureLoaded();
                return _table.Count;
            }
        }

        // Separador decimal del idioma activo, para los pocos números con coma o punto que se arman a mano (x1,5 / x1.5).
        public static char DecimalSeparator => _current == GameLanguage.Spanish ? ',' : '.';

        public static void SetLanguage(GameLanguage language)
        {
            if (language == _current) return;

            _current = language;
            Changed?.Invoke();
        }

        // Solo para pruebas: fija el idioma del "sistema" para que no dependan de la máquina donde corren.
        public static SystemLanguage? SystemLanguageOverride { get; set; }

        // Idioma que informa el portal que aloja el juego (SDK de CrazyGames: "es-AR", "en-US"). Vacío = no hay portal o todavía no lo dijo.
        public static string PortalLocale { get; set; }

        // En la web "Automático" es siempre inglés (CrazyGames pide inglés por defecto); el español se elige a mano (menú y Ajustes).
        public static bool AutoIsEnglishOnly { get; set; }

        private static bool IsSpanishLocale(string locale)
        {
            return locale.Equals("es", StringComparison.OrdinalIgnoreCase)
                || locale.StartsWith("es-", StringComparison.OrdinalIgnoreCase)
                || locale.StartsWith("es_", StringComparison.OrdinalIgnoreCase);
        }

        // "Automático": español si el portal, el sistema o el navegador están en español; cualquier otro idioma, inglés. El portal manda
        // sobre el sistema (CrazyGames pide usar su locale), y sin portal vale el idioma del sistema.
        public static GameLanguage ResolveAuto()
        {
            if (AutoIsEnglishOnly) return GameLanguage.English;

            if (!string.IsNullOrEmpty(PortalLocale))
            {
                return IsSpanishLocale(PortalLocale) ? GameLanguage.Spanish : GameLanguage.English;
            }

            SystemLanguage system = SystemLanguageOverride ?? Application.systemLanguage;
            return system == SystemLanguage.Spanish ? GameLanguage.Spanish : GameLanguage.English;
        }

        public static bool Has(string key)
        {
            EnsureLoaded();
            return key != null && _table.ContainsKey(key);
        }

        public static string Get(string key)
        {
            return Get(key, _current);
        }

        public static string Get(string key, GameLanguage language)
        {
            EnsureLoaded();
            if (key == null) return string.Empty;
            if (!_table.TryGetValue(key, out string[] row)) return key;

            string text = row[(int)language];
            return string.IsNullOrEmpty(text) ? row[(int)GameLanguage.Spanish] : text;
        }

        // Para los nombres de los datos: la tabla manda, y si no hay clave queda el texto guardado en el asset.
        public static string GetOr(string key, string fallback)
        {
            EnsureLoaded();
            return key != null && _table.ContainsKey(key) ? Get(key, _current) : fallback;
        }

        public static string Format(string key, object arg0)
        {
            return string.Format(Get(key), arg0);
        }

        public static string Format(string key, object arg0, object arg1)
        {
            return string.Format(Get(key), arg0, arg1);
        }

        // Para los textos con tres o más datos (solo al refrescar una vista, nunca por cuadro).
        public static string Format(string key, params object[] args)
        {
            return string.Format(Get(key), args);
        }

        // ---------- Carga ----------

        // Vuelve a leer la tabla en el próximo uso (las pruebas la cambian a mano).
        public static void Unload()
        {
            _table = null;
        }

        public static void Load(string csv)
        {
            _table = Parse(csv);
        }

        private static void EnsureLoaded()
        {
            if (_table != null) return;

            TextAsset asset = Resources.Load<TextAsset>(ResourcePath);
            if (asset == null)
            {
                Debug.LogError("[FarmIdle] Falta Resources/" + ResourcePath + ".csv (la tabla de textos).");
                _table = new Dictionary<string, string[]>();
                return;
            }

            _table = Parse(asset.text);
        }

        // CSV simple: coma como separador, comillas dobles para celdas con comas o saltos, "" dentro de comillas es una comilla.
        // La primera fila es el encabezado (key,es,en). Las filas vacías o que empiezan con # se ignoran.
        public static Dictionary<string, string[]> Parse(string csv)
        {
            var table = new Dictionary<string, string[]>(StringComparer.Ordinal);
            if (string.IsNullOrEmpty(csv)) return table;

            List<List<string>> rows = ReadRows(csv);
            int languages = Enum.GetValues(typeof(GameLanguage)).Length;

            for (int i = 1; i < rows.Count; i++)
            {
                List<string> cells = rows[i];
                string key = cells.Count > 0 ? cells[0].Trim() : string.Empty;
                if (key.Length == 0 || key[0] == '#') continue;

                var values = new string[languages];
                for (int language = 0; language < languages; language++)
                {
                    string cell = cells.Count > language + 1 ? cells[language + 1] : string.Empty;
                    values[language] = cell.Replace("\\n", "\n");
                }

                table[key] = values;
            }

            return table;
        }

        private static List<List<string>> ReadRows(string csv)
        {
            var rows = new List<List<string>>();
            var cells = new List<string>();
            var cell = new StringBuilder();
            bool quoted = false;

            for (int i = 0; i < csv.Length; i++)
            {
                char c = csv[i];
                if (quoted)
                {
                    if (c == '"')
                    {
                        if (i + 1 < csv.Length && csv[i + 1] == '"')
                        {
                            cell.Append('"');
                            i++;
                        }
                        else
                        {
                            quoted = false;
                        }
                    }
                    else
                    {
                        cell.Append(c);
                    }

                    continue;
                }

                if (c == '"') quoted = true;
                else if (c == ',')
                {
                    cells.Add(cell.ToString());
                    cell.Length = 0;
                }
                else if (c == '\n' || c == '\r')
                {
                    if (c == '\r' && i + 1 < csv.Length && csv[i + 1] == '\n') i++;
                    cells.Add(cell.ToString());
                    cell.Length = 0;
                    rows.Add(cells);
                    cells = new List<string>();
                }
                else
                {
                    cell.Append(c);
                }
            }

            if (cell.Length > 0 || cells.Count > 0)
            {
                cells.Add(cell.ToString());
                rows.Add(cells);
            }

            return rows;
        }
    }
}
