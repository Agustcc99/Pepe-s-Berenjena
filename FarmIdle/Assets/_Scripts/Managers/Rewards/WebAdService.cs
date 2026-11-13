using System;
using System.Globalization;
using System.Runtime.InteropServices;

namespace FarmIdle
{
    // La puerta de C# con el JavaScript de la página (Assets/Plugins/WebGL/FarmIdleWeb.jslib). Detrás de una interfaz para poder
    // probar WebAdService sin navegador.
    public interface IWebAdBridge
    {
        bool IsReady(string placement);

        // Pide mostrar el anuncio. El resultado vuelve por SendMessage al objeto/método dados, como "<requestId>|<resultado>".
        void Show(string placement, int requestId, string gameObjectName, string methodName);
    }

    // El puente real: solo existe en un build de WebGL; en el Editor y en los demás destinos no hace nada (IsReady false), así el
    // resto del código no necesita #if.
    //   - 'allowSimulatedAds': si NO hay adaptador de portal (window.FarmIdleAdAdapter), el JS muestra un anuncio SIMULADO que regala la
    //     recompensa. Eso solo se permite en builds de desarrollo; en un build final, sin adaptador, no hay anuncio (nofill). Una página
    //     que aloje una demo puede pedirlo a propósito con window.FarmIdleAllowSimAds = true.
    public sealed class JsWebAdBridge : IWebAdBridge
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void FI_ConfigureSimulatedAds(int allowed);
        [DllImport("__Internal")] private static extern int FI_AdIsReady(string placement);
        [DllImport("__Internal")] private static extern void FI_ShowRewardedAd(string placement, int requestId, string gameObjectName, string methodName);
#endif

        public JsWebAdBridge(bool allowSimulatedAds)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            FI_ConfigureSimulatedAds(allowSimulatedAds ? 1 : 0);
#endif
        }

        public bool IsReady(string placement)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return FI_AdIsReady(placement) != 0;
#else
            return false;
#endif
        }

        public void Show(string placement, int requestId, string gameObjectName, string methodName)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            FI_ShowRewardedAd(placement, requestId, gameObjectName, methodName);
#endif
        }
    }

    // Anuncio con recompensa en WebGL. No muestra nada él mismo: le pide al JavaScript de la página que lo haga.
    //   - Hoy el JS muestra un anuncio SIMULADO (un overlay HTML con cuenta regresiva, "Cerrar" al final y "Saltar"), solo hasta
    //     elegir portal.
    //   - Para un portal real (CrazyGames, Poki, Game Distribution...) NO hay que tocar C# ni AdsManager: la página define
    //     window.FarmIdleAdAdapter = { isReady(placement) -> bool, showRewarded(placement, done) } y llama done('rewarded' | 'closed' |
    //     'failed' | 'nofill'). Ver los comentarios de FarmIdleWeb.jslib.
    //   - Cada pedido lleva un número: el resultado de un pedido que ya no está en curso se ignora.
    public sealed class WebAdService : IAdService
    {
        private readonly IWebAdBridge _bridge;
        private readonly string _gameObjectName;
        private readonly string _methodName;
        private Action<AdResult> _callback;
        private int _requestId;

        public WebAdService(IWebAdBridge bridge, string gameObjectName, string methodName)
        {
            _bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
            _gameObjectName = gameObjectName ?? string.Empty;
            _methodName = methodName ?? string.Empty;
        }

        public string Name => "Web";

        public bool IsReady(string placement)
        {
            return _callback == null && _bridge.IsReady(placement);
        }

        public void ShowRewarded(string placement, Action<AdResult> onFinished)
        {
            if (onFinished == null) throw new ArgumentNullException(nameof(onFinished));

            if (_callback != null)
            {
                onFinished(AdResult.Failed);
                return;
            }

            _requestId++;
            _callback = onFinished;
            _bridge.Show(placement ?? string.Empty, _requestId, _gameObjectName, _methodName);
        }

        public void Tick(float unscaledDeltaSeconds)
        {
        }

        // Lo llama AdsManager.OnWebAdResult con lo que mandó el JS: "<pedido>|<resultado>". Un formato raro o un pedido que no es el
        // actual se ignora (nunca tira excepciones: viene de afuera).
        public void OnBridgeResult(string payload)
        {
            if (_callback == null || string.IsNullOrEmpty(payload)) return;

            int separator = payload.IndexOf('|');
            if (separator <= 0) return;

            if (!int.TryParse(payload.Substring(0, separator), NumberStyles.Integer, CultureInfo.InvariantCulture, out int id) || id != _requestId) return;

            AdResult result;
            switch (payload.Substring(separator + 1).Trim().ToLowerInvariant())
            {
                case "rewarded": result = AdResult.Rewarded; break;
                case "closed": result = AdResult.Closed; break;
                case "nofill": result = AdResult.NoFill; break;
                default: result = AdResult.Failed; break;
            }

            Action<AdResult> callback = _callback;
            _callback = null;
            callback(result);
        }
    }
}
