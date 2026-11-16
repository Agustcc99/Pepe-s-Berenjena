namespace FarmIdle
{
    // Un grupo de parcelas cuyo estado se guarda en la partida (B-11): qué está sembrado y cuánto creció. Lo implementa el campo
    // (PlotField, de Environment) y lo usa PlotStateManager (Managers), que así no conoce al entorno.
    public interface IPlotStateHolder
    {
        // Estable entre sesiones: identifica al campo dentro del nivel.
        string StateId { get; }

        int PlotCount { get; }

        // Escribe, parcela por parcela, el estado (el entero de PlotState) y los segundos de crecimiento que lleva. Los arreglos
        // tienen PlotCount lugares.
        void CaptureState(int[] states, float[] elapsed);

        // Deja las parcelas como dice la foto y después las hace crecer 'awaySeconds' segundos de reloj (el campo aplica su propia
        // velocidad de crecimiento). Devuelve cuántas quedaron sembradas.
        int RestoreState(int[] states, float[] elapsed, double awaySeconds);

        // Hace crecer lo sembrado 'awaySeconds' segundos de reloj (el juego estuvo en segundo plano).
        void AdvanceAway(double awaySeconds);

        // Lo sembrado se pudrió: vacía todas las parcelas sin entregar nada. Devuelve cuántas tenían algo.
        int ClearAll();
    }
}
