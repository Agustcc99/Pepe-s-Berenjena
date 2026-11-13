namespace FarmIdle
{
    // Opcional para un ISaveable: su estado cambia con el tiempo SIN avisar (por ejemplo, los segundos que le quedan a un buff).
    // Mientras HasVolatileState sea true, SaveManager no se fía de "no hay cambios pendientes" y escribe la partida igual al
    // pausar, perder el foco o cerrar. Así lo que queda en el archivo nunca es más viejo que el último momento de juego.
    public interface ISaveableVolatile
    {
        bool HasVolatileState { get; }
    }
}
