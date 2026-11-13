using System.Text;
using TMPro;
using UnityEngine;

namespace FarmIdle
{
    // F-18 (tanda 2): los pedidos del pueblo en el HUD, para saber qué juntar sin volver al cartel: una línea por pedido activo
    // (cultivo y entregado/pedido). Solo lee OrdersManager y escucha Changed: no hay Update ni trabajo por cuadro. Con el cartel
    // cerrado o sin pedidos activos, la tarjeta queda apagada.
    public sealed class OrdersHudView : MonoBehaviour
    {
        [SerializeField, Tooltip("La tarjeta entera (se apaga sin pedidos).")]
        private GameObject card;

        [SerializeField] private TMP_Text text;

        private OrdersManager _orders;
        private readonly StringBuilder _builder = new StringBuilder(96);

        private void OnEnable()
        {
            _orders = OrdersManager.Instance;
            if (_orders != null) _orders.Changed += Refresh;
            Loc.Changed += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            Loc.Changed -= Refresh;
            if (_orders != null)
            {
                _orders.Changed -= Refresh;
                _orders = null;
            }
        }

        private void Refresh()
        {
            if (card == null || text == null) return;

            int shown = 0;
            _builder.Clear();

            if (_orders != null && _orders.IsUnlocked)
            {
                for (int i = 0; i < _orders.SlotCount; i++)
                {
                    CropData crop = _orders.CropOf(i);
                    if (crop == null) continue;

                    if (shown == 0) _builder.Append("<size=70%><color=#FFE066>").Append(Loc.Get("hud.orders")).Append("</color></size>");
                    _builder.Append('\n').Append(crop.DisplayName).Append("  ").Append(_orders.DeliveredOf(i)).Append('/').Append(_orders.QuantityOf(i));
                    shown++;
                }
            }

            if (card.activeSelf != shown > 0) card.SetActive(shown > 0);
            if (shown > 0) text.text = _builder.ToString();   // una cadena por cambio del cartel (no por cuadro)
        }
    }
}
