using System.Text;
using UnityEngine;

namespace FarmIdle
{
    // F-18 (tanda 2): un lugar del cartel de pedidos del pueblo. Parado encima un momento, entrega de la mochila lo que haya del
    // cultivo pedido (OrdersManager.TryDeliver; se puede en varios viajes). El cartel dice qué se pide, cuánto falta y qué paga; con
    // el lugar vacío, cuánto falta para el pedido nuevo; con el cartel cerrado, cuántas granjas hay que abrir.
    // No decide nada: todo lo resuelve OrdersManager. El cartel se reescribe solo cuando cambia StateKey.
    public sealed class OrderPad : DwellPad
    {
        [SerializeField, Min(0), Tooltip("Qué lugar del cartel atiende este pad (0 a OrderBoard.SlotCount - 1; el último es el pedido grande).")]
        private int slot;

        // La espera se muestra de a pasos, para no reescribir el cartel cada segundo.
        private const int WaitStepSeconds = 5;

        private OrdersManager _orders;
        private readonly StringBuilder _text = new StringBuilder(96);

        public int Slot => slot;

        protected override void OnEnable()
        {
            base.OnEnable();
            _orders = OrdersManager.Instance;
            RefreshLabel();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            _orders = null;
        }

        private bool Ready => _orders != null && slot < _orders.SlotCount;

        protected override bool CanAct()
        {
            return Ready && _orders.CanDeliver(slot);
        }

        protected override bool Act()
        {
            return Ready && _orders.TryDeliver(slot) > 0;
        }

        private int WaitShown()
        {
            float left = _orders.CooldownOf(slot);
            return left <= 0f ? 0 : Mathf.CeilToInt(left / WaitStepSeconds) * WaitStepSeconds;
        }

        protected override int StateKey()
        {
            if (!Ready) return -1;
            if (!_orders.IsUnlocked) return -2;
            if (!_orders.IsActive(slot)) return -10 - WaitShown();

            unchecked
            {
                CropData crop = _orders.CropOf(slot);
                int key = crop != null ? crop.Id.GetHashCode() : 0;
                key = key * 31 + _orders.QuantityOf(slot);
                key = key * 31 + _orders.DeliveredOf(slot);
                key = key * 31 + (int)_orders.RewardCoins(slot);
                key = key * 31 + _orders.RewardSeeds(slot);
                return key & 0x3FFFFFFF;
            }
        }

        protected override string BuildText()
        {
            _text.Clear();
            bool big = Ready && _orders.IsBig(slot);
            _text.Append("<size=60%>").Append(Loc.Get(big ? "pad.order_big" : "pad.order")).Append("</size>\n");

            if (!Ready)
            {
                _text.Append('-');
            }
            else if (!_orders.IsUnlocked)
            {
                _text.Append("<size=50%><color=#9A9A82>").Append(Loc.Format("pad.order_locked", _orders.MinZones)).Append("</color></size>");
            }
            else if (!_orders.IsActive(slot))
            {
                int wait = WaitShown();
                _text.Append("<size=50%><color=#9A9A82>").Append(Loc.Get("pad.order_wait")).Append(' ');
                _text.Append(wait / 60).Append(':').Append((wait % 60) / 10).Append((wait % 60) % 10).Append("</color></size>");
            }
            else
            {
                CropData crop = _orders.CropOf(slot);
                _text.Append("<size=50%>").Append(crop != null ? crop.DisplayName : "-").Append("</size>\n");
                _text.Append(_orders.DeliveredOf(slot)).Append('/').Append(_orders.QuantityOf(slot));
                _text.Append("\n<size=50%><color=#FFE066>+").Append(_orders.RewardCoins(slot)).Append("</color>");
                if (_orders.RewardSeeds(slot) > 0) _text.Append("  <color=#FFE066>").Append(Loc.Get("pad.order_seed")).Append("</color>");
                _text.Append("</size>");
            }

            return _text.ToString();
        }
    }
}
