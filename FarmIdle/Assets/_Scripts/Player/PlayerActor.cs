using System;
using UnityEngine;

namespace FarmIdle
{
    // El lado "actor" del jugador: lo que ven las zonas interactivas (parcelas, venta...) a través de IActor.
    // Las zonas lo encuentran por el collider del jugador (CharacterController) y no conocen nada más de él.
    // Además es el IBackpackOwner (recibe lo que cosecha) y el IBackpackDispenser (entrega lo que vende) del jugador:
    // las capacidades extra las implementa este mismo objeto, y la parcela y la zona de venta las descubren con
    // 'actor is IBackpackOwner' / 'actor is IBackpackDispenser' sin saber que es el jugador.
    public sealed class PlayerActor : MonoBehaviour, IActor, IBackpackOwner, IBackpackDispenser
    {
        public string ActorName => "Player";

        // Un producto acaba de entrar a la mochila, con el punto del mundo de donde salió. Lo escucha la pila visible
        // (BackpackStackView) para animarlo; cuando se dispara, la mochila ya tiene el valor final.
        public event Action<CropData, Vector3> ProductReceived;

        // El producto de arriba de la mochila está por salir hacia un punto del mundo (la zona de venta), con lo que
        // tarda en llegar. Se dispara justo ANTES de sacarlo del inventario, dentro de la misma llamada de TryDispense:
        // la pila de la espalda separa ese producto para volarlo (en vez de devolverlo al pool en el acto) y suelta su
        // lugar antes de que le llegue el cambio del inventario. Es solo cosmético: el resultado no depende de nadie.
        public event Action<CropData, Vector3, float> ProductLeaving;

        // Igual que el movimiento: el jugador solo actúa en Playing (no en el menú, en pausa ni con un anuncio).
        // Sin GameManager (una escena de prueba suelta) siempre puede.
        public bool CanInteract
        {
            get
            {
                GameManager game = GameManager.Instance;
                return game == null || game.IsPlaying;
            }
        }

        // IBackpackOwner: la mochila del jugador es la que maneja el BackpackManager. Sin él (una escena suelta) no
        // hay mochila y no recibe nada.
        public bool TryAccept(CropData crop, Vector3 from)
        {
            BackpackManager backpack = BackpackManager.Instance;
            if (backpack == null || !backpack.TryAddOrReject(crop)) return false;

            ProductReceived?.Invoke(crop, from);
            return true;
        }

        // IBackpackDispenser: entrega el producto de arriba (LIFO). Con la mochila vacía, o sin BackpackManager (una
        // escena suelta), no hace nada y no escribe en la consola: la zona de venta lo llama en cada frame.
        public bool TryDispense(Vector3 to, float flightSeconds, out CropData crop)
        {
            crop = null;

            BackpackManager backpack = BackpackManager.Instance;
            if (backpack == null) return false;

            IReadOnlyInventory inventory = backpack.Backpack;
            if (inventory.Count == 0) return false;

            ProductLeaving?.Invoke(inventory.Items[inventory.Count - 1], to, flightSeconds);
            return backpack.TryPop(out crop);
        }
    }
}
