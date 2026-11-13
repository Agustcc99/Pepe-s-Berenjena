using System;

namespace FarmIdle.EditorTools
{
    // La versión de los builds de release. El nombre es lo que ve el jugador (0.9.0 = casi lista); el código es el número entero que
    // Google Play compara: en CADA subida tiene que ser mayor que el de la anterior. Se pueden pisar sin tocar el código con las variables
    // de entorno FARMIDLE_VERSION_NAME y FARMIDLE_VERSION_CODE.
    internal static class ReleaseVersion
    {
        private const string DefaultName = "0.9.0";
        private const int DefaultCode = 1;

        public static string Name
        {
            get
            {
                string value = Environment.GetEnvironmentVariable("FARMIDLE_VERSION_NAME");
                return string.IsNullOrWhiteSpace(value) ? DefaultName : value.Trim();
            }
        }

        public static int Code
        {
            get
            {
                string value = Environment.GetEnvironmentVariable("FARMIDLE_VERSION_CODE");
                return int.TryParse(value, out int code) && code > 0 ? code : DefaultCode;
            }
        }
    }
}
