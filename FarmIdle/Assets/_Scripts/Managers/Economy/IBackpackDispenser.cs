using UnityEngine;

namespace FarmIdle
{
    // Capacidad de un actor: entregar el producto de arriba de su mochila (para venderlo, o para lo que venga: tirarlo,
    // dárselo a un ayudante). Es la pareja de IBackpackOwner, que solo sabe recibir. Vive en Managers por la misma razón
    // (usa CropData) y la implementa el MISMO objeto que implementa IActor: la zona de venta descubre lo que el actor
    // sabe hacer con 'actor is IBackpackDispenser' sin conocer al jugador, y un actor sin mochila no vende.
    public interface IBackpackDispenser
    {
        // Saca el producto de arriba de la mochila (LIFO) y lo entrega. La transacción es instantánea y autoritativa:
        // cuando devuelve true el producto ya no está en la mochila y 'crop' es el que salió. Con la mochila vacía
        // devuelve false y no cambia nada.
        // 'to' es el punto del mundo hacia donde viaja el producto y 'flightSeconds' lo que tarda en llegar: solo sirven
        // para animarlo (la pila de la espalda lo hace volar hasta ahí), no cambian el resultado. Quien recibe el
        // producto programa lo suyo (la moneda, el "+N") para ese mismo instante.
        // Se llama desde Interact (en cada frame mientras haya alguien encima), así que tiene que ser barata y no
        // escribir en la consola cuando está vacía.
        bool TryDispense(Vector3 to, float flightSeconds, out CropData crop);
    }
}
