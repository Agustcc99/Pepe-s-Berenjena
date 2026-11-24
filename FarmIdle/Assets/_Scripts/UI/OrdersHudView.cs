using System.Collections;
using System.Globalization;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FarmIdle
{
    // F-18 (tanda 2): los pedidos del pueblo en el HUD, para saber qué juntar sin volver al cartel: una línea por pedido activo
    // (cultivo, entregado/pedido y el PREMIO: monedas y, el grande, la semilla). Solo lee OrdersManager y escucha Changed: no hay
    // Update ni trabajo por cuadro. Con el cartel cerrado o sin pedidos activos, la tarjeta queda apagada.
    //   - El premio en monedas es el de OrdersManager.RewardCoins al último cambio del cartel: si después sube el precio de venta
    //     (una mejora), el número se actualiza con la próxima entrega.
    //   - Al completarse un pedido (Completed) sale un aviso con lo ganado, unos segundos (reloj sin escala).
    public sealed class OrdersHudView : MonoBehaviour
    {
        [SerializeField, Tooltip("La tarjeta entera (se apaga sin pedidos).")]
        private GameObject card;

        [SerializeField] private TMP_Text text;

        [SerializeField, Tooltip("Plegable: el encabezado \"Pedidos del pueblo (N)\", que al tocarlo muestra u oculta el detalle. Puede faltar (el detalle queda siempre a la vista).")]
        private TMP_Text header;

        [SerializeField, Tooltip("Lo que se toca para plegar y desplegar.")]
        private Button toggleButton;

        [SerializeField, Tooltip("La tarjeta se achica a este alto plegada y crece 'lineHeight' por pedido desplegada.")]
        private RectTransform sizedRect;

        [SerializeField, Min(10f)] private float collapsedHeight = 76f;
        [SerializeField, Min(10f)] private float headerHeight = 76f;
        [SerializeField, Min(10f)] private float lineHeight = 46f;

        [SerializeField, Tooltip("El aviso de pedido completado, con lo ganado. Arranca apagado. Puede faltar.")]
        private TMP_Text completedLabel;

        [SerializeField, Tooltip("Lo que se prende y se apaga con el aviso (el fondo). Si falta, es el texto mismo.")]
        private GameObject completedRoot;

        [SerializeField, Min(0.1f)] private float completedSeconds = 3f;

        private GameObject Notice => completedRoot != null ? completedRoot : completedLabel.gameObject;

        private OrdersManager _orders;
        private readonly StringBuilder _builder = new StringBuilder(160);
        private Coroutine _noticeRoutine;
        private bool _expanded;   // arranca plegada: solo el encabezado

        // Para pruebas y depuración.
        public bool IsExpanded => _expanded;
        public string HeaderText => header != null ? header.text : string.Empty;

        public void Toggle()
        {
            _expanded = !_expanded;
            Refresh();
        }

        public bool IsNoticeShowing => completedLabel != null && Notice.activeSelf;
        public string NoticeText => completedLabel != null ? completedLabel.text : string.Empty;
        public string CardText => text != null ? text.text : string.Empty;

        private void OnEnable()
        {
            _orders = OrdersManager.Instance;
            if (_orders != null)
            {
                _orders.Changed += Refresh;
                _orders.Completed += OnCompleted;
            }

            Loc.Changed += Refresh;
            if (toggleButton != null) toggleButton.onClick.AddListener(Toggle);
            if (completedLabel != null) Notice.SetActive(false);
            Refresh();
        }

        private void OnDisable()
        {
            Loc.Changed -= Refresh;
            if (toggleButton != null) toggleButton.onClick.RemoveListener(Toggle);
            if (_orders != null)
            {
                _orders.Changed -= Refresh;
                _orders.Completed -= OnCompleted;
                _orders = null;
            }

            // Al desactivarse Unity detiene las corrutinas: el aviso queda apagado.
            _noticeRoutine = null;
            if (completedLabel != null) Notice.SetActive(false);
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

                    if (shown == 0 && header == null) _builder.Append("<size=70%><color=#FFE066>").Append(Loc.Get("hud.orders")).Append("</color></size>\n");
                    else if (shown > 0) _builder.Append('\n');
                    _builder.Append(crop.DisplayName).Append("  ").Append(_orders.DeliveredOf(i)).Append('/').Append(_orders.QuantityOf(i));
                    AppendReward(_orders.RewardCoins(i), _orders.RewardSeeds(i));
                    shown++;
                }
            }

            if (card.activeSelf != shown > 0) card.SetActive(shown > 0);
            if (shown == 0) return;

            text.text = _builder.ToString();   // una cadena por cambio del cartel (no por cuadro)
            if (header == null) return;

            // Plegable: encabezado con la cantidad y un "+"/"-", y el detalle solo desplegado.
            _builder.Clear();
            _builder.Append(Loc.Get("hud.orders")).Append(" (").Append(shown).Append(")  ").Append(_expanded ? '-' : '+');
            header.text = _builder.ToString();
            text.gameObject.SetActive(_expanded);
            if (sizedRect != null) sizedRect.sizeDelta = new Vector2(sizedRect.sizeDelta.x, _expanded ? headerHeight + lineHeight * shown + 14f : collapsedHeight);
        }

        // "  +1.250" en dorado y, si corresponde, "+1 semilla".
        private void AppendReward(long coins, int seeds)
        {
            _builder.Append("  <size=80%><color=#FFE066>+").Append(coins.ToString("N0", CultureInfo.InvariantCulture));
            if (seeds > 0) _builder.Append("  ").Append(Loc.Get("pad.order_seed"));
            _builder.Append("</color></size>");
        }

        // Se completó un pedido: el aviso dice qué se ganó. Una cadena por pedido completado.
        private void OnCompleted(int slot, long coins, int seeds)
        {
            if (completedLabel == null) return;

            _builder.Clear();
            _builder.Append(Loc.Get("hud.order_done")).Append('\n');
            _builder.Append('+').Append(coins.ToString("N0", CultureInfo.InvariantCulture));
            if (seeds > 0) _builder.Append("   ").Append(Loc.Get("pad.order_seed"));
            completedLabel.text = _builder.ToString();
            Notice.SetActive(true);

            if (_noticeRoutine != null) StopCoroutine(_noticeRoutine);
            _noticeRoutine = StartCoroutine(HideNotice());

            Refresh();   // el builder se compartió con el aviso: la tarjeta se vuelve a escribir
        }

        private IEnumerator HideNotice()
        {
            float elapsed = 0f;
            while (elapsed < completedSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            Notice.SetActive(false);
            _noticeRoutine = null;
        }
    }
}
