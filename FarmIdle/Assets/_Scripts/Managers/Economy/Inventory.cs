using System;
using System.Collections.Generic;

namespace FarmIdle
{
    // La mochila: una sola capacidad, cultivos mezclados y orden LIFO (el último en entrar es el primero en salir:
    // la Fase 5 vende de arriba hacia abajo).
    // Es C# puro, sin UnityEngine y sin conocer la vista (BackpackStackView) ni la UI: cuando cambia solo dispara
    // Changed, y quien quiera mostrarla se suscribe. Toda operación es instantánea y autoritativa; las animaciones
    // (el producto que vuela hasta la espalda) son cosméticas.
    // Igual que Wallet, vive en Managers porque usa datos de diseño (CropData). Cada actor tiene la suya.
    public sealed class Inventory : IReadOnlyInventory
    {
        private readonly List<CropData> _items;

        public Inventory(int capacity)
        {
            if (capacity < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity), "La mochila tiene que tener lugar para al menos 1 producto.");
            }

            Capacity = capacity;
            _items = new List<CropData>(capacity);
        }

        public int Capacity { get; private set; }
        public int Count => _items.Count;
        public bool IsFull => _items.Count >= Capacity;

        // De abajo hacia arriba. Es la lista real (no una copia): no se modifica desde afuera.
        public IReadOnlyList<CropData> Items => _items;

        public event Action<InventoryChange> Changed;

        // Se dispara después de que la capacidad crece, con la capacidad nueva.
        public event Action<int> CapacityChanged;

        // Agranda la mochila (las mejoras de capacidad). Lo que lleva se conserva tal cual. La capacidad solo sube:
        // si 'newCapacity' no es mayor que la actual no hace nada y devuelve false.
        public bool TryGrowTo(int newCapacity)
        {
            if (newCapacity <= Capacity) return false;

            Capacity = newCapacity;
            CapacityChanged?.Invoke(newCapacity);
            return true;
        }

        // Fija la capacidad de golpe, también hacia abajo (solo para restaurar una partida guardada o borrarla: jugando, la
        // capacidad nunca baja). Si queda más chica que lo que lleva, se descartan los de arriba. Avisa CapacityChanged.
        public void SetCapacity(int newCapacity)
        {
            if (newCapacity < 1) throw new ArgumentOutOfRangeException(nameof(newCapacity), "La mochila tiene que tener lugar para al menos 1 producto.");
            if (newCapacity == Capacity) return;

            Capacity = newCapacity;
            if (_items.Count > newCapacity)
            {
                _items.RemoveRange(newCapacity, _items.Count - newCapacity);
                Changed?.Invoke(InventoryChange.Replaced(_items.Count));
            }

            CapacityChanged?.Invoke(newCapacity);
        }

        public int CountOf(CropData crop)
        {
            int count = 0;
            for (int i = 0; i < _items.Count; i++)
            {
                if (_items[i] == crop) count++;
            }

            return count;
        }

        // Pone el producto arriba de todo. Devuelve false (sin tocar nada ni avisar) si la mochila está llena.
        public bool TryAdd(CropData crop)
        {
            if (crop == null) throw new ArgumentNullException(nameof(crop));
            if (IsFull) return false;

            _items.Add(crop);
            Changed?.Invoke(new InventoryChange(crop, true, _items.Count));
            return true;
        }

        // Reemplaza TODO el contenido (partida guardada o nueva): 'items' va de abajo hacia arriba. Los nulos se saltean y lo que
        // no entra en la capacidad actual se descarta. Avisa UNA sola vez (Changed con Crop nulo) y devuelve cuántos
        // quedaron adentro. Hay que llamarlo después de que la capacidad ya creció, porque nunca la agranda.
        public int ReplaceContents(IReadOnlyList<CropData> items)
        {
            _items.Clear();

            if (items != null)
            {
                for (int i = 0; i < items.Count && _items.Count < Capacity; i++)
                {
                    if (items[i] != null) _items.Add(items[i]);
                }
            }

            Changed?.Invoke(InventoryChange.Replaced(_items.Count));
            return _items.Count;
        }

        // F-18: saca hasta 'amount' unidades de ese cultivo (las de más arriba primero) sin tocar el resto. Devuelve cuántas sacó. Avisa
        // UNA sola vez (Changed con Crop nulo) y solo si sacó algo; la pila visible se rearma con ese aviso.
        public int TryTake(CropData crop, int amount)
        {
            if (crop == null || amount <= 0) return 0;

            int removed = 0;
            for (int i = _items.Count - 1; i >= 0 && removed < amount; i--)
            {
                if (_items[i] != crop) continue;

                _items.RemoveAt(i);
                removed++;
            }

            if (removed > 0) Changed?.Invoke(InventoryChange.Replaced(_items.Count));
            return removed;
        }

        // Saca el de arriba (LIFO). Devuelve false (sin tocar nada ni avisar) si está vacía.
        public bool TryPop(out CropData crop)
        {
            if (_items.Count == 0)
            {
                crop = null;
                return false;
            }

            int last = _items.Count - 1;
            crop = _items[last];
            _items.RemoveAt(last);
            Changed?.Invoke(new InventoryChange(crop, false, _items.Count));
            return true;
        }
    }
}
