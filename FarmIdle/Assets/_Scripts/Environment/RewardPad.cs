using System.Text;
using UnityEngine;

namespace FarmIdle
{
    // El pad "Ver video": parado encima un momento, le pide a AdsManager el anuncio de su recompensa (hoy, un fertilizante). Nunca
    // habla con un SDK: la única puerta es AdsManager.TryRequest, que pasa el juego a AdShowing, muestra el anuncio y otorga la
    // recompensa SOLO si se completó. El pad solo ofrece y explica por qué no se puede (enfriamiento, stock lleno, sin anuncio).
    // Pedir un video es siempre una decisión del jugador: hay que quedarse parado, con la barra llenándose, y se pide una vez por visita.
    public class RewardPad : DwellPad
    {
        [SerializeField, Tooltip("La recompensa que da el video de este pad.")]
        private RewardData reward;

        private AdsManager _ads;
        private readonly StringBuilder _text = new StringBuilder(80);

        public RewardData Reward => reward;

        // Los símbolos que salen del pool reciben su recompensa en runtime (el pad fijo la trae cableada).
        public void SetReward(RewardData value)
        {
            reward = value;
            RefreshLabel();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            _ads = AdsManager.Instance;
            RefreshLabel();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            _ads = null;
        }

        protected override bool CanAct()
        {
            return _ads != null && reward != null && _ads.Evaluate(reward) == AdRequestStatus.Ready;
        }

        protected override bool Act()
        {
            return _ads != null && reward != null && _ads.TryRequest(reward) == AdRequestStatus.Started;
        }

        protected override void OnBlocked()
        {
            if (_ads != null && reward != null) _ads.ReportRefused(reward);
        }

        protected override int StateKey()
        {
            if (_ads == null || reward == null) return -1;

            AdRequestStatus status = _ads.Evaluate(reward);
            int seconds = Mathf.CeilToInt(Mathf.Max(_ads.RewardCooldownRemaining(reward), _ads.AdCooldownRemaining()));
            return (int)status * 100000 + Mathf.Min(seconds, 999) * 100 + Mathf.Min(TitleKey(), 99);
        }

        // Para los pads cuyo título cambia con el tiempo (el símbolo de oferta cuenta los segundos que le quedan).
        protected virtual int TitleKey()
        {
            return 0;
        }

        protected override string BuildText()
        {
            _text.Clear();
            _text.Append("<size=60%>").Append(Title()).Append("</size>\n");

            if (_ads == null || reward == null)
            {
                _text.Append("-");
                return _text.ToString();
            }

            switch (_ads.Evaluate(reward))
            {
                case AdRequestStatus.Ready:
                    _text.Append(Loc.Get("pad.watch"));
                    break;

                case AdRequestStatus.RewardCooldown:
                    _text.Append("<color=#DCDCC8>").Append(Loc.Get("pad.in")).Append(' ').Append(Mathf.CeilToInt(_ads.RewardCooldownRemaining(reward))).Append(" s</color>");
                    break;

                case AdRequestStatus.Cooldown:
                    _text.Append("<color=#DCDCC8>").Append(Loc.Get("pad.wait")).Append(' ').Append(Mathf.CeilToInt(_ads.AdCooldownRemaining())).Append(" s</color>");
                    break;

                case AdRequestStatus.NotUseful:
                    _text.Append("<color=#DCDCC8>").Append(Loc.Get(reward.Kind == RewardKind.Fertilizer ? "pad.stock_full" : "pad.already_active")).Append("</color>");
                    break;

                case AdRequestStatus.NoAd:
                    _text.Append("<color=#DCDCC8>").Append(Loc.Get("pad.no_ad")).Append("</color>");
                    break;

                default:
                    _text.Append("<color=#DCDCC8>...</color>");   // menú, pausa o anuncio en pantalla
                    break;
            }

            return _text.ToString();
        }

        protected virtual string Title()
        {
            if (reward == null) return Loc.Get("pad.video");

            return reward.Kind == RewardKind.Fertilizer ? Loc.Format("pad.video_fertilizer", Mathf.RoundToInt(reward.Magnitude)) : Loc.Format("pad.video_reward", reward.DisplayName);
        }
    }
}
