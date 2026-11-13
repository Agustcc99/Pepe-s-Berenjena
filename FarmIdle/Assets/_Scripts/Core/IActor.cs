namespace FarmIdle
{
    // Quien puede pararse sobre una zona interactiva y actuar: el jugador hoy, un ayudante más adelante.
    // Las zonas (parcelas, venta, desbloqueo) solo conocen esta interfaz, nunca al jugador, así que Player y
    // Environment no se referencian entre sí. Es mínima a propósito: lo que haga falta (mochila, cosecha...) se
    // suma como interfaces aparte (por ejemplo IBackpackOwner, en Managers) que implementa el mismo objeto que el
    // IActor; la zona las descubre con 'actor is IBackpackOwner' y un actor que no la tiene simplemente no la usa.
    public interface IActor
    {
        // Nombre para logs y depuración ("Player"; más adelante, por ejemplo, "Helper 1").
        string ActorName { get; }

        // Si puede actuar ahora. El jugador solo en Playing: en el menú, en pausa o con un anuncio en pantalla no
        // interactúa aunque esté parado sobre una zona, y apenas pueda actuar la zona lo atiende.
        bool CanInteract { get; }
    }
}
