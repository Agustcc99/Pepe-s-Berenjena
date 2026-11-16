using System.Text;
using UnityEngine;

namespace FarmIdle
{
    // El pad "Comprar fertilizante": parado encima un momento, paga el precio del GameConfig por EconomyManager.TrySpend (a través de
    // FertilizerManager.TryBuy) y suma una unidad al stock. A diferencia de las zonas y las mejoras, la compra es de una vez y no
    // "gotea": cuesta poco, no hay pagos a medias que guardar, y una sola compra por visita corta cualquier encadenado.
    // Con el stock lleno, o sin monedas, no actúa y no cobra nada.
    public sealed class FertilizerBuyPad : DwellPad
    {
        private FertilizerManager _fertilizer;
        private EconomyManager _economy;
        private readonly StringBuilder _text = new StringBuilder(64);

        protected override void OnEnable()
        {
            base.OnEnable();
            _fertilizer = FertilizerManager.Instance;
            _economy = EconomyManager.Instance;
            if (_economy != null) _economy.Wallet.BalanceChanged += OnBalanceChanged;
            RefreshLabel();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (_economy != null)
            {
                _economy.Wallet.BalanceChanged -= OnBalanceChanged;
                _economy = null;
            }

            _fertilizer = null;
        }

        private void OnBalanceChanged(long balance, long delta)
        {
            RefreshLabel();
        }

        protected override bool CanAct()
        {
            return _fertilizer != null && _fertilizer.CanBuy;
        }

        protected override bool Act()
        {
            return _fertilizer != null && _fertilizer.TryBuy();
        }

        protected override void OnBlocked()
        {
            if (_fertilizer != null) _fertilizer.ReportRefused();
        }

        protected override int StateKey()
        {
            if (_fertilizer == null || _economy == null) return -1;

            long price = _fertilizer.Price;
            int key = _fertilizer.Stock * 100000 + Mathf.CeilToInt(_fertilizer.BuyCooldownRemaining) * 10 + (_economy.CanAfford(price) ? 1 : 0);
            return unchecked(key * 31 + (int)price);   // el precio cambia al abrir una zona (B-08): el cartel se repinta
        }

        protected override string BuildText()
        {
            _text.Clear();
            _text.Append("<size=60%>").Append(Loc.Get("pad.buy_fertilizer")).Append("</size>\n");

            if (_fertilizer == null || _economy == null)
            {
                _text.Append("-");
            }
            else if (_fertilizer.AtBuyLimit)
            {
                _text.Append("<color=#DCDCC8>").Append(Loc.Get("pad.full")).Append(' ').Append(_fertilizer.Stock).Append('/').Append(_fertilizer.BuyMaxStock).Append("</color>");
            }
            else if (_fertilizer.BuyCooldownRemaining > 0f)
            {
                _text.Append("<color=#DCDCC8>").Append(Loc.Get("pad.wait")).Append(' ').Append(Mathf.CeilToInt(_fertilizer.BuyCooldownRemaining)).Append(" s</color>");
            }
            else
            {
                _text.Append(_economy.CanAfford(_fertilizer.Price) ? "<color=#FFFFFF>" : "<color=#DCDCC8>").Append(_fertilizer.Price).Append("</color>");
                _text.Append("\n<size=50%>").Append(Loc.Get("pad.you_have")).Append(' ').Append(_fertilizer.Stock).Append('/').Append(_fertilizer.MaxStock).Append("</size>");
            }

            return _text.ToString();
        }
    }
}
