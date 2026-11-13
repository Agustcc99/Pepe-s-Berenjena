using System.Globalization;
using UnityEngine;

namespace FarmIdle
{
    // Escribe cada evento en el log: "[FarmIdle][Analytics] pad_comprado id=zone-2". Sirve para ver en el Editor y en builds de
    // desarrollo que cada punto del juego emite lo que debe. Asigna memoria al formatear: no se usa en builds finales.
    public sealed class ConsoleAnalytics : IAnalytics
    {
        public const string Prefix = "[FarmIdle][Analytics] ";

        public string Name => "Console";

        public static string Format(string eventName, string key, string value)
        {
            return string.IsNullOrEmpty(key) ? Prefix + eventName : Prefix + eventName + " " + key + "=" + value;
        }

        public void Log(string eventName)
        {
            Debug.Log(Format(eventName, null, null));
        }

        public void Log(string eventName, string key, string value)
        {
            Debug.Log(Format(eventName, key, value));
        }

        public void Log(string eventName, string key, long value)
        {
            Debug.Log(Format(eventName, key, value.ToString(CultureInfo.InvariantCulture)));
        }
    }
}
