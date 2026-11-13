using System;

namespace FarmIdle
{
    // F-18: el cartel de pedidos del pueblo como lógica pura (sin Unity: se prueba sin escena ni servicios). Tiene 3 lugares; el último
    // es el pedido grande. Cada lugar está vacío (esperando un pedido nuevo, con segundos de espera) o tiene un pedido: un cultivo, la
    // cantidad pedida y lo ya entregado. No sabe de precios, mochila ni zonas: eso lo resuelve OrdersManager.
    public sealed class OrderBoard
    {
        public const int SlotCount = 3;
        public const int BigSlot = 2;

        public struct Slot
        {
            public string CropId;      // null = lugar vacío
            public int Quantity;
            public int Delivered;
            public float Cooldown;     // segundos hasta poder poner un pedido nuevo (solo cuenta con el lugar vacío)

            public bool IsActive => CropId != null;
            public int Remaining => Quantity - Delivered;
        }

        private readonly Slot[] _slots = new Slot[SlotCount];

        // Semillas doradas que ya dieron los pedidos grandes en esta temporada.
        public int SeedsThisSeason { get; set; }

        public Slot this[int index] => _slots[index];

        public static bool IsBig(int index)
        {
            return index == BigSlot;
        }

        // Cuántas unidades pide un pedido: fracción de la capacidad de la mochila, entre un mínimo y un máximo; el grande, un múltiplo.
        public static int QuantityFor(int capacity, bool big, float share, int min, int max, float bigFactor, int bigMax)
        {
            float raw = Math.Max(1, capacity) * Math.Max(0.01f, share);
            if (big) raw *= Math.Max(1f, bigFactor);

            int lowest = Math.Max(1, min);
            int highest = big ? Math.Max(Math.Max(lowest, max), bigMax) : Math.Max(lowest, max);
            int low = big ? Math.Min(lowest * 2, highest) : lowest;
            return Math.Min(highest, Math.Max(low, (int)Math.Round(raw)));
        }

        // Elige una posición de 'weights' según sus pesos; 'roll' va de 0 a (suma de pesos - 1). -1 si no hay pesos.
        public static int PickIndex(int[] weights, int count, int roll)
        {
            int total = 0;
            for (int i = 0; i < count; i++) total += Math.Max(0, weights[i]);
            if (total <= 0) return -1;

            int target = Math.Min(Math.Max(0, roll), total - 1);
            for (int i = 0; i < count; i++)
            {
                target -= Math.Max(0, weights[i]);
                if (target < 0) return i;
            }

            return count - 1;
        }

        public static int WeightTotal(int[] weights, int count)
        {
            int total = 0;
            for (int i = 0; i < count; i++) total += Math.Max(0, weights[i]);
            return total;
        }

        // Pasa el tiempo en los lugares vacíos.
        public void Tick(float deltaSeconds)
        {
            if (deltaSeconds <= 0f) return;

            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i].IsActive || _slots[i].Cooldown <= 0f) continue;
                _slots[i].Cooldown = Math.Max(0f, _slots[i].Cooldown - deltaSeconds);
            }
        }

        // true si el lugar está vacío y ya terminó la espera: toca poner un pedido.
        public bool NeedsFill(int index)
        {
            return !_slots[index].IsActive && _slots[index].Cooldown <= 0f;
        }

        public void Fill(int index, string cropId, int quantity)
        {
            if (string.IsNullOrEmpty(cropId)) throw new ArgumentException("El pedido necesita un cultivo.", nameof(cropId));
            if (quantity < 1) throw new ArgumentOutOfRangeException(nameof(quantity));

            _slots[index] = new Slot { CropId = cropId, Quantity = quantity, Delivered = 0, Cooldown = 0f };
        }

        // Suma lo entregado (hasta lo que falta) y devuelve cuántas unidades se aceptaron.
        public int Deliver(int index, int available)
        {
            if (!_slots[index].IsActive || available <= 0) return 0;

            int taken = Math.Min(available, _slots[index].Remaining);
            _slots[index].Delivered += taken;
            return taken;
        }

        public bool IsComplete(int index)
        {
            return _slots[index].IsActive && _slots[index].Delivered >= _slots[index].Quantity;
        }

        // Vacía el lugar y arranca la espera hasta el próximo pedido.
        public void Complete(int index, float cooldownSeconds)
        {
            _slots[index] = new Slot { CropId = null, Cooldown = Math.Max(0f, cooldownSeconds) };
        }

        // Restaura un lugar tal cual se guardó (valores ya validados por quien llama).
        public void Set(int index, string cropId, int quantity, int delivered, float cooldown)
        {
            if (string.IsNullOrEmpty(cropId) || quantity < 1)
            {
                _slots[index] = new Slot { CropId = null, Cooldown = Math.Max(0f, cooldown) };
                return;
            }

            _slots[index] = new Slot { CropId = cropId, Quantity = quantity, Delivered = Math.Max(0, Math.Min(delivered, quantity)), Cooldown = 0f };
        }

        // Tablero vacío y sin espera (partida nueva o temporada nueva).
        public void Clear()
        {
            for (int i = 0; i < _slots.Length; i++) _slots[i] = default;
            SeedsThisSeason = 0;
        }
    }
}
