using System.Text;

namespace FarmIdle
{
    // El pad "Usar fertilizante": parado encima un momento, gasta UNA unidad del stock y todas las plantas que están creciendo en los
    // campos abiertos quedan listas de golpe (FertilizerManager.TryUse, que respeta las zonas cerradas). Si no hay stock, o no hay
    // nada creciendo, no actúa y no gasta nada.
    public sealed class FertilizerUsePad : DwellPad
    {
        private FertilizerManager _fertilizer;
        private readonly StringBuilder _text = new StringBuilder(64);

        protected override void OnEnable()
        {
            base.OnEnable();
            _fertilizer = FertilizerManager.Instance;
            RefreshLabel();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            _fertilizer = null;
        }

        protected override bool CanAct()
        {
            return _fertilizer != null && _fertilizer.Stock > 0 && _fertilizer.GrowingCount > 0;
        }

        protected override bool Act()
        {
            return _fertilizer != null && _fertilizer.TryUse() > 0;
        }

        protected override void OnBlocked()
        {
            if (_fertilizer != null) _fertilizer.ReportRefused();
        }

        protected override int StateKey()
        {
            if (_fertilizer == null) return -1;

            return _fertilizer.Stock * 10 + (_fertilizer.GrowingCount > 0 ? 1 : 0);
        }

        protected override string BuildText()
        {
            _text.Clear();
            _text.Append("<size=60%>").Append(Loc.Get("pad.use_fertilizer")).Append("</size>\n");

            if (_fertilizer == null)
            {
                _text.Append("-");
            }
            else if (_fertilizer.Stock <= 0)
            {
                _text.Append("<color=#DCDCC8>").Append(Loc.Get("pad.no_stock")).Append("</color>");
            }
            else
            {
                _text.Append('x').Append(_fertilizer.Stock);
                if (_fertilizer.GrowingCount <= 0) _text.Append("\n<size=50%><color=#DCDCC8>").Append(Loc.Get("pad.nothing_growing")).Append("</color></size>");
            }

            return _text.ToString();
        }
    }
}
