namespace FarmIdle
{
    // La ÚNICA puerta para registrar eventos de analítica. Mismo esquema que IAdService: ni la UI ni el gameplay hablan con un SDK; el
    // AnalyticsManager escucha al juego y llama acá. Hoy hay NullAnalytics (no hace nada: builds finales) y ConsoleAnalytics (escribe en
    // el log: Editor y desarrollo); Firebase (ROADMAP N-03) será una implementación más.
    //   - Cada evento lleva a lo sumo UN parámetro (clave + texto o número): alcanza para los eventos mínimos y encaja con los SDK.
    //   - Los nombres salen de AnalyticsEvents (minúsculas, sin acentos, guion bajo, hasta 40 caracteres).
    public interface IAnalytics
    {
        string Name { get; }

        void Log(string eventName);

        void Log(string eventName, string key, string value);

        void Log(string eventName, string key, long value);
    }
}
