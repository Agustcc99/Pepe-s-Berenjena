using System.Text;
using UnityEngine;

namespace FarmIdle
{
    // El pad "Skins" (F-17, tanda 3): parado encima un momento, compra la SIGUIENTE skin del jugador si alcanzan las monedas (y se la
    // pone) o, si no alcanzan o ya están todas, cambia a la siguiente de las que ya tiene (y vuelta al color de fábrica). Una acción por
    // visita (DwellPad). Sin monedas y sin skins compradas no actúa.
    public sealed class SkinBuyPad : DwellPad
    {
        private SkinManager _skins;
        private EconomyManager _economy;
        private readonly StringBuilder _text = new StringBuilder(80);

        protected override void OnEnable()
        {
            base.OnEnable();
            _skins = SkinManager.Instance;
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

            _skins = null;
        }

        private void OnBalanceChanged(long balance, long delta)
        {
            RefreshLabel();
        }

        protected override bool CanAct()
        {
            return _skins != null && _skins.CanAct;
        }

        protected override bool Act()
        {
            if (_skins == null) return false;
            return _skins.CanBuyNext ? _skins.TryBuyNext() : _skins.Cycle();
        }

        protected override void OnBlocked()
        {
            if (_skins != null) _skins.ReportRefused();
        }

        protected override int StateKey()
        {
            if (_skins == null || _economy == null) return -1;

            return (_skins.Owned * 100 + (_skins.Equipped + 1)) * 10 + (_economy.CanAfford(_skins.NextCost) ? 1 : 0);
        }

        protected override string BuildText()
        {
            _text.Clear();
            _text.Append("<size=60%>").Append(Loc.Get("pad.skins")).Append("</size>\n");

            if (_skins == null || _economy == null)
            {
                _text.Append("-");
                return _text.ToString();
            }

            if (_skins.AllOwned)
            {
                _text.Append("<color=#DCDCC8>").Append(Loc.Get("pad.skins_all")).Append("</color>");
            }
            else
            {
                _text.Append("<size=50%>").Append(_skins.Next.DisplayName).Append("</size>\n");
                _text.Append(_economy.CanAfford(_skins.NextCost) ? "<color=#FFFFFF>" : "<color=#DCDCC8>").Append(_skins.NextCost).Append("</color>");
            }

            if (_skins.Owned > 0)
            {
                SkinData equipped = _skins.EquippedSkin;
                _text.Append("\n<size=50%>").Append(Loc.Get("pad.skins_wearing")).Append(' ').Append(equipped != null ? equipped.DisplayName : Loc.Get("pad.skins_default")).Append("</size>");
            }

            return _text.ToString();
        }
    }
}
