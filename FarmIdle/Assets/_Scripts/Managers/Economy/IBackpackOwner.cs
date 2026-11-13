using UnityEngine;

namespace FarmIdle
{
    // Capacidad de un actor: tener una mochila donde guarda lo que cosecha. Vive en Managers porque usa CropData
    // (Core no ve Data) y es aparte de IActor a propósito: IActor sigue siendo mínimo y cada capacidad es una
    // interfaz chica más.
    //
    // La regla para sumarla: la implementa el MISMO objeto que implementa IActor (el jugador hoy, un ayudante
    // mañana). Las zonas (parcelas, venta) solo reciben un IActor y descubren lo que sabe hacer con
    // 'actor is IBackpackOwner', sin conocer al jugador; un actor sin mochila simplemente no cosecha.
    public interface IBackpackOwner
    {
        // Recibe un cultivo cosechado. Si le entra lo guarda al instante (la transacción es autoritativa) y devuelve
        // true; con la mochila llena devuelve false y no cambia nada. 'from' es el punto del mundo de donde sale el
        // producto: solo sirve para animarlo hasta la mochila, no cambia el resultado.
        // Se llama desde Interact (en cada frame mientras la parcela esté lista), así que tiene que ser barata y no
        // escribir en la consola cuando rechaza.
        bool TryAccept(CropData crop, Vector3 from);
    }
}
