using System;

namespace FarmIdle
{
    // El stock de fertilizante, sin Unity: cuántos hay y el máximo que se acumula.
    public sealed class FertilizerStock
    {
        public FertilizerStock(int maxStock)
        {
            if (maxStock < 1) throw new ArgumentOutOfRangeException(nameof(maxStock), "El stock máximo tiene que ser al menos 1.");

            Max = maxStock;
        }

        public int Max { get; }
        public int Count { get; private set; }
        public bool IsFull => Count >= Max;

        public event Action Changed;

        // Suma hasta 'amount' unidades sin pasarse del máximo y devuelve cuántas entraron.
        public int Add(int amount)
        {
            if (amount <= 0) return 0;

            int added = Math.Min(amount, Max - Count);
            if (added <= 0) return 0;

            Count += added;
            Changed?.Invoke();
            return added;
        }

        public bool TryTake()
        {
            if (Count <= 0) return false;

            Count--;
            Changed?.Invoke();
            return true;
        }

        // Pone la cantidad (partida guardada): se acota a 0..Max. Avisa Changed.
        public void Set(int count)
        {
            Count = Math.Max(0, Math.Min(count, Max));
            Changed?.Invoke();
        }
    }
}
