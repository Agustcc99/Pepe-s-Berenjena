using System.Text;
using UnityEngine;

namespace FarmIdle
{
    // El pad "Decorar" (F-17): parado encima un momento, compra la SIGUIENTE decoración de la granja (DecorationManager.TryBuyNext, que
    // cobra por EconomyManager.TrySpend). Una compra por visita (DwellPad). Cerrado hasta abrir todas las zonas; sin monedas o con
    // todas compradas no actúa y no cobra. Muestra el nombre y el precio de la que viene.
    public sealed class DecorationBuyPad : DwellPad
    {
        private DecorationManager _decorations;
        private EconomyManager _economy;
        private readonly StringBuilder _text = new StringBuilder(64);

        protected override void OnEnable()
        {
            base.OnEnable();
            _decorations = DecorationManager.Instance;
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

            _decorations = null;
        }

        private void OnBalanceChanged(long balance, long delta)
        {
            RefreshLabel();
        }

        protected override bool CanAct()
        {
            return _decorations != null && _decorations.CanBuyNext;
        }

        protected override bool Act()
        {
            return _decorations != null && _decorations.TryBuyNext();
        }

        protected override void OnBlocked()
        {
            if (_decorations != null) _decorations.ReportRefused();
        }

        protected override int StateKey()
        {
            if (_decorations == null || _economy == null) return -1;

            int key = _decorations.Owned * 10 + (_decorations.IsUnlocked ? 1 : 0) + (_economy.CanAfford(_decorations.NextCost) ? 2 : 0);
            return key;
        }

        protected override string BuildText()
        {
            _text.Clear();
            _text.Append("<size=60%>").Append(Loc.Get("pad.decorate")).Append("</size>\n");

            if (_decorations == null || _economy == null)
            {
                _text.Append("-");
            }
            else if (_decorations.AllOwned)
            {
                _text.Append("<color=#9A9A82>").Append(Loc.Get("pad.deco_done")).Append("</color>");
            }
            else if (!_decorations.IsUnlocked)
            {
                _text.Append("<size=50%><color=#9A9A82>").Append(Loc.Get("pad.deco_locked")).Append("</color></size>");
            }
            else
            {
                _text.Append("<size=50%>").Append(_decorations.Next.DisplayName).Append("</size>\n");
                _text.Append(_economy.CanAfford(_decorations.NextCost) ? "<color=#FFFFFF>" : "<color=#9A9A82>").Append(_decorations.NextCost).Append("</color>");
                _text.Append("\n<size=50%>").Append(_decorations.Owned).Append('/').Append(_decorations.Total).Append("</size>");
            }

            return _text.ToString();
        }
    }
}
