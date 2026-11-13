namespace FarmIdle
{
    // El motor de vibración de la plataforma, detrás de una interfaz (como IAdService): el HapticsManager decide CUÁNDO vibrar y esto
    // solo sabe CÓMO. Hay uno para Android, uno para la web (celulares) y NullVibrator para el resto (Editor, PC).
    public interface IVibrator
    {
        string Name { get; }

        // Un pulso corto. 'amplitude' va de 1 (suave) a 255 (fuerte); las plataformas que no la admiten la ignoran.
        void Pulse(int milliseconds, int amplitude);
    }

    public sealed class NullVibrator : IVibrator
    {
        public string Name => "Null";

        public void Pulse(int milliseconds, int amplitude) { }
    }
}
