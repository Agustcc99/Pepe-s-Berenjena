namespace FarmIdle
{
    // Estados de una parcela. Las reglas son del gameplay (Environment), pero el estado lo comparten el censo
    // (Managers) y el panel de debug (UI), así que vive en Core, igual que GameState.
    public enum PlotState
    {
        Empty,      // sin sembrar
        Growing,    // sembrada: la planta crece
        Ready       // lista para cosechar
    }
}
