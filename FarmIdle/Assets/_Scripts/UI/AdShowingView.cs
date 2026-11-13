using System.Text;
using TMPro;
using UnityEngine;

namespace FarmIdle
{
    // La capa que tapa el juego mientras hay un anuncio en pantalla (estado AdShowing): oscurece todo, bloquea los toques y dice qué
    // anuncio es. Un anuncio real (AdMob, un portal) se dibuja por encima de esto; con el Mock muestra los segundos que le quedan.
    // Solo escucha al GameManager y al AdsManager: no decide nada del anuncio. Está siempre en la escena, apagada con CanvasGroup (no
    // se crea ni se destruye nada jugando).
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class AdShowingView : MonoBehaviour
    {
        [SerializeField] private TMP_Text message;

        private CanvasGroup _group;
        private GameManager _game;
        private AdsManager _ads;
        private readonly StringBuilder _text = new StringBuilder(64);
        private int _shownSeconds = int.MinValue;

        private void Awake()
        {
            _group = GetComponent<CanvasGroup>();
        }

        private void OnEnable()
        {
            _game = GameManager.Instance;
            _ads = AdsManager.Instance;
            if (_game != null) _game.StateChanged += OnStateChanged;
            Loc.Changed += OnLanguageChanged;

            SetVisible(_game != null && _game.State == GameState.AdShowing);
        }

        // El texto se reescribe solo si cambia la cuenta regresiva; al cambiar el idioma hay que forzarlo.
        private void OnLanguageChanged()
        {
            _shownSeconds = int.MinValue;
        }

        private void OnDisable()
        {
            Loc.Changed -= OnLanguageChanged;
            if (_game != null)
            {
                _game.StateChanged -= OnStateChanged;
                _game = null;
            }

            _ads = null;
        }

        private void OnStateChanged(GameState previous, GameState current)
        {
            SetVisible(current == GameState.AdShowing);
        }

        private void SetVisible(bool visible)
        {
            if (_group == null) _group = GetComponent<CanvasGroup>();

            _group.alpha = visible ? 1f : 0f;
            _group.blocksRaycasts = visible;
            _group.interactable = visible;
            _shownSeconds = int.MinValue;
            if (visible) RefreshMessage();
        }

        private void Update()
        {
            if (_group.alpha > 0f) RefreshMessage();
        }

        private void RefreshMessage()
        {
            if (message == null) return;

            int seconds = -1;
            string name = Loc.Get("ad.generic");
            if (_ads != null)
            {
                if (_ads.CurrentReward != null) name = _ads.CurrentReward.DisplayName;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (_ads.Service is MockAdService mock) seconds = Mathf.CeilToInt(mock.RemainingSeconds);   // la cuenta regresiva del simulado
#endif
            }

            if (seconds == _shownSeconds) return;

            _shownSeconds = seconds;
            _text.Clear();
            _text.Append(Loc.Get("ad.watching")).Append("\n<size=60%>").Append(name);
            if (seconds >= 0) _text.Append("  (").Append(Loc.Get("ad.simulated")).Append(", ").Append(seconds).Append(" s)");
            _text.Append("</size>");
            message.text = _text.ToString();
        }
    }
}
