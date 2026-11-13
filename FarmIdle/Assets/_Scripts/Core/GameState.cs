namespace FarmIdle
{
    // Estados del juego. Las transiciones válidas las define GameManager.
    public enum GameState
    {
        Boot,       // el Bootstrap está creando los servicios y cargando el nivel
        Ready,      // nivel cargado, esperando el "Tap to Play"
        Playing,    // jugando
        AdShowing,  // anuncio en pantalla: el mundo queda congelado
        Paused      // pausa: el mundo queda congelado
    }
}
