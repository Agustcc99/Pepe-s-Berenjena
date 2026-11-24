using System.Text;
using UnityEngine;

namespace FarmIdle
{
    // El pad "Camión": parado encima un momento, compra la SIGUIENTE mejora visual del camión de reparto (TruckManager.TryBuyNext, que
    // cobra por EconomyManager.TrySpend). Una compra por visita (DwellPad). Sin monedas o con todas compradas no actúa y no cobra.
    // Muestra el nombre y el precio de la que viene. Puro adorno: no da ninguna ventaja.
    public sealed class TruckBuyPad : DwellPad
    {
        private TruckManager _truck;
        private EconomyManager _economy;
        private readonly StringBuilder _text = new StringBuilder(64);

        protected override void OnEnable()
        {
            base.OnEnable();
            _truck = TruckManager.Instance;
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

            _truck = null;
        }

        private void OnBalanceChanged(long balance, long delta)
        {
            RefreshLabel();
        }

        protected override bool CanAct()
        {
            return _truck != null && _truck.CanBuyNext;
        }

        protected override bool Act()
        {
            return _truck != null && _truck.TryBuyNext();
        }

        protected override void OnBlocked()
        {
            if (_truck != null) _truck.ReportRefused();
        }

        protected override int StateKey()
        {
            if (_truck == null || _economy == null) return -1;

            return _truck.Owned * 10 + (_economy.CanAfford(_truck.NextCost) ? 2 : 0);
        }

        protected override string BuildText()
        {
            _text.Clear();
            _text.Append("<size=60%>").Append(Loc.Get("pad.truck")).Append("</size>\n");

            if (_truck == null || _economy == null)
            {
                _text.Append("-");
            }
            else if (_truck.AllOwned)
            {
                _text.Append("<color=#DCDCC8>").Append(Loc.Get("pad.truck_done")).Append("</color>");
            }
            else
            {
                _text.Append("<size=50%>").Append(Loc.Get(_truck.Next.NameKey)).Append("</size>\n");
                _text.Append(_economy.CanAfford(_truck.NextCost) ? "<color=#FFFFFF>" : "<color=#DCDCC8>").Append(_truck.NextCost).Append("</color>");
                _text.Append("\n<size=50%>").Append(_truck.Owned).Append('/').Append(_truck.Total).Append("</size>");
            }

            return _text.ToString();
        }
    }
}
