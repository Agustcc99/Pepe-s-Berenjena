namespace FarmIdle
{
    // Algo que produce monedas por sí solo y cuenta para el progreso offline (hoy: un campo de parcelas). Vive en Core por la misma
    // razón que IFertilizable: lo implementa el entorno (PlotField) y lo usa un servicio de Managers (OfflineManager), y esas dos capas
    // no se conocen. El campo se registra al activarse y se da de baja al apagarse, así una zona cerrada no suma.
    public interface IProductionSource
    {
        // Monedas BASE por segundo que rinde si se lo cosecha y vende sin parar: precio base / segundos de crecimiento, sumado sobre sus
        // parcelas. Sin mejoras ni prestigio ni eficiencia (eso lo pone OfflineManager).
        float BaseCoinsPerSecond { get; }
    }
}
