using UnityEngine;

namespace FarmIdle
{
    // Los pasos de la guía de los primeros minutos, en orden. Done = ya no hay nada que mostrar.
    public enum TutorialStep
    {
        Sow,       // pisar el campo: se siembra solo
        Harvest,   // volver a pisarlo cuando está listo: se cosecha solo
        Sell,      // llevar la mochila al pad de venta
        Buy,       // gastar las primeras monedas en una mejora
        Done
    }

    // Un lugar del mundo al que la guía puede llevar al jugador (el campo, el pad de venta, la primera mejora). Lo implementa un
    // componente de Environment y se registra en el TutorialManager, que decide cuál es el actual; la UI lo lee para la flecha del
    // borde de la pantalla. Así el entorno y la UI no se conocen.
    public interface ITutorialTarget
    {
        // En qué paso de la guía hay que ir a este lugar.
        TutorialStep Step { get; }

        // Hacia dónde apunta la guía.
        Vector3 Position { get; }

        // El lugar es el actual del paso (la flecha del mundo se muestra) o dejó de serlo (se apaga).
        void SetCurrent(bool isCurrent);
    }
}
