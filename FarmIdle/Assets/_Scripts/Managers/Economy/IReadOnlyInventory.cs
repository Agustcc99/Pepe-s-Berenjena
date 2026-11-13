using System;
using System.Collections.Generic;

namespace FarmIdle
{
    // Un cambio de la mochila: qué producto entró o salió y cuántos quedaron adentro.
    public readonly struct InventoryChange
    {
        public readonly CropData Crop;   // el producto que entró o salió
        public readonly bool Added;      // true: entró; false: salió
        public readonly int Count;       // unidades en la mochila después del cambio

        public InventoryChange(CropData crop, bool added, int count)
        {
            Crop = crop;
            Added = added;
            Count = count;
        }

        // El contenido entero se reemplazó de una vez (partida guardada): Crop es null y hay que releer la mochila completa.
        public bool IsReplacement => Crop == null;

        public static InventoryChange Replaced(int count)
        {
            return new InventoryChange(null, false, count);
        }
    }

    // Lo que la UI y el gameplay pueden hacer con una mochila: mirar qué lleva y escuchar sus cambios.
    // Para meter o sacar productos hay que pasar por quien la maneja (BackpackManager, o el IBackpackOwner del actor).
    public interface IReadOnlyInventory
    {
        int Capacity { get; }
        int Count { get; }
        bool IsFull { get; }

        // De abajo hacia arriba: el último es el de arriba, el primero que sale (LIFO).
        IReadOnlyList<CropData> Items { get; }

        // Cuántas unidades de ese cultivo lleva.
        int CountOf(CropData crop);

        // Se dispara después de cada cambio (entra o sale un producto).
        event Action<InventoryChange> Changed;

        // Se dispara después de que la capacidad crece (una mejora de mochila), con la capacidad nueva.
        event Action<int> CapacityChanged;
    }
}
