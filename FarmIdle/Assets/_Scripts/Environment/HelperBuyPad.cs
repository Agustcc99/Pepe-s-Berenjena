using System.Text;
using UnityEngine;

namespace FarmIdle
{
    // El pad "Ayudantes" (F-17, tanda 2): parado encima un momento, compra el SIGUIENTE nivel de ayudantes automáticos
    // (HelperManager.TryBuyNext, que cobra por EconomyManager.TrySpend). Una compra por visita (DwellPad). Sin monedas o con el
    // máximo no actúa y no cobra. Muestra el nombre, el precio y el efecto del nivel que viene.
    public sealed class HelperBuyPad : DwellPad
    {
        private HelperManager _helpers;
        private EconomyManager _economy;
        private readonly StringBuilder _text = new StringBuilder(80);

        protected override void OnEnable()
        {
            base.OnEnable();
            _helpers = HelperManager.Instance;
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

            _helpers = null;
        }

        private void OnBalanceChanged(long balance, long delta)
        {
            RefreshLabel();
        }

        protected override bool CanAct()
        {
            return _helpers != null && _helpers.CanBuyNext;
        }

        protected override bool Act()
        {
            return _helpers != null && _helpers.TryBuyNext();
        }

        protected override void OnBlocked()
        {
            if (_helpers != null) _helpers.ReportRefused();
        }

        protected override int StateKey()
        {
            if (_helpers == null || _economy == null) return -1;

            return _helpers.Level * 10 + (_economy.CanAfford(_helpers.NextCost) ? 1 : 0);
        }

        protected override string BuildText()
        {
            _text.Clear();
            _text.Append("<size=60%>").Append(Loc.Get("pad.helpers")).Append("</size>\n");

            if (_helpers == null || _economy == null)
            {
                _text.Append("-");
            }
            else if (_helpers.IsMaxed)
            {
                _text.Append("<color=#DCDCC8>").Append(Loc.Get("pad.helpers_max")).Append("</color>");
                _text.Append("\n<size=50%>").Append(Loc.Format("pad.helpers_bonus", Mathf.RoundToInt(_helpers.Share * 100f))).Append("</size>");
            }
            else
            {
                _text.Append("<size=50%>").Append(_helpers.Next.DisplayName).Append("</size>\n");
                _text.Append(_economy.CanAfford(_helpers.NextCost) ? "<color=#FFFFFF>" : "<color=#DCDCC8>").Append(_helpers.NextCost).Append("</color>");
                _text.Append("\n<size=50%>").Append(Loc.Format("pad.helpers_bonus", Mathf.RoundToInt(_helpers.Next.Share * 100f))).Append("</size>");
            }

            return _text.ToString();
        }
    }
}
