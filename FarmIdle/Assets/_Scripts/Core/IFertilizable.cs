namespace FarmIdle
{
    // Algo que el fertilizante puede hacer crecer de golpe (hoy: un campo de parcelas). Vive en Core porque lo implementa
    // el entorno (PlotField) y lo usa un servicio de Managers (FertilizerManager), y esas dos capas no se conocen: se
    // encuentran por esta interfaz, igual que IActor/IInteractable. El campo se registra al activarse y se da de baja al
    // apagarse, así una zona cerrada (campo apagado) queda fuera sin que nadie tenga que mirarla.
    public interface IFertilizable
    {
        // Cuántas plantas están creciendo ahora (las que el fertilizante puede terminar).
        int GrowingCount { get; }

        // Termina de una vez todo lo que está creciendo y devuelve cuántas plantas llegaron a "listas". No siembra ni cosecha.
        int GrowAllNow();
    }
}
