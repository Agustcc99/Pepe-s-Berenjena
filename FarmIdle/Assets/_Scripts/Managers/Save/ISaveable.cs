using System;

namespace FarmIdle
{
    // Un sistema que se guarda en la partida (la billetera, la mochila, el progreso... y mañana los buffs de la Fase 8).
    // Cada uno es dueño de su sección: la serializa a un texto JSON propio con su propio 'version' adentro, y SaveManager
    // solo la guarda bajo el SaveId, sin saber qué contiene. Así un sistema nuevo no toca el formato del archivo ni a los
    // demás, y una sección de un sistema que ya no existe se conserva tal cual en vez de perderse.
    public interface ISaveable
    {
        // Identificador estable de la sección en el archivo. Es un contrato: una vez usado no se cambia ("economy", "backpack"...).
        string SaveId { get; }

        // El estado actual como JSON (JsonUtility sobre una clase serializable propia). No toca ningún objeto de la escena.
        string Capture();

        // Reemplaza el estado por el de 'json'. Con null o vacío vuelve al estado de una partida nueva. Tiene que ser
        // tolerante: ids que ya no existen, listas nulas o valores fuera de rango se ignoran sin tirar excepciones.
        // Restaurar NO dispara efectos de juego (una zona abierta aparece de una, la mochila se llena sin vuelos).
        void Restore(string json);

        // Se dispara cuando cambió algo que vale la pena guardar. SaveManager junta los avisos seguidos en un solo guardado.
        event Action SaveRequested;
    }
}
