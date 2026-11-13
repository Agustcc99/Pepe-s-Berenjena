using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FarmIdle
{
    // La tarjeta de bienvenida del progreso offline (B-01): "Mientras no estabas, tus parcelas produjeron N monedas", con "Reclamar" y
    // "Duplicar (video)". Aparece sola cuando hay monedas pendientes y el juego ya está en marcha (no debajo del "Tocá para jugar").
    // Solo habla con OfflineManager (reclamar) y AdsManager (pedir el video); nada se acredita hasta tocar un botón.
    //   - El botón de duplicar se apaga si el video no se puede pedir ahora (sin anuncio disponible, enfriamiento...) y se reevalúa
    //     mientras la tarjeta está abierta, porque el servicio de anuncios puede tardar en tener uno listo.
    //   - Si el video se completa, OfflineManager acredita el doble y avisa PendingChanged: la tarjeta se cierra sola.
    public sealed class OfflineRewardView : MonoBehaviour
    {
        [SerializeField, Tooltip("La recompensa de anuncio que duplica lo ganado (RewardKind.OfflineDouble).")]
        private RewardData reward;

        [SerializeField, Tooltip("El fondo oscuro con la tarjeta (arranca apagado).")]
        private GameObject panel;

        [SerializeField] private TMP_Text bodyLabel;
        [SerializeField] private Button claimButton;
        [SerializeField] private Button doubleButton;

        [SerializeField, Min(0.1f)] private float recheckSeconds = 0.5f;

        private OfflineManager _offline;
        private AdsManager _ads;
        private GameManager _game;
        private float _nextCheck;

        public bool IsOpen => panel != null && panel.activeSelf;
        public bool CanDouble => doubleButton != null && doubleButton.interactable;

        private void OnEnable()
        {
            _offline = OfflineManager.Instance;
            _ads = AdsManager.Instance;
            _game = GameManager.Instance;
            if (_offline == null || _game == null)
            {
                if (panel != null) panel.SetActive(false);
                _offline = null;
                return;
            }

            claimButton.onClick.AddListener(OnClaimClicked);
            doubleButton.onClick.AddListener(OnDoubleClicked);
            _offline.PendingChanged += Refresh;
            _game.StateChanged += OnStateChanged;
            if (_ads != null) _ads.AdFinished += OnAdFinished;
            Loc.Changed += Refresh;

            panel.SetActive(false);
            Refresh();
        }

        private void OnDisable()
        {
            if (_offline == null) return;

            claimButton.onClick.RemoveListener(OnClaimClicked);
            doubleButton.onClick.RemoveListener(OnDoubleClicked);
            _offline.PendingChanged -= Refresh;
            _game.StateChanged -= OnStateChanged;
            if (_ads != null) _ads.AdFinished -= OnAdFinished;
            Loc.Changed -= Refresh;
            _offline = null;
            _ads = null;
            _game = null;
        }

        private void OnStateChanged(GameState previous, GameState current)
        {
            Refresh();
        }

        private void OnAdFinished(RewardData finished, AdResult result, bool granted)
        {
            Refresh();
        }

        private void OnClaimClicked()
        {
            _offline.Claim();
        }

        private void OnDoubleClicked()
        {
            if (_ads != null && reward != null) _ads.TryRequest(reward);
        }

        private void Update()
        {
            if (_offline == null || !panel.activeSelf || Time.unscaledTime < _nextCheck) return;

            _nextCheck = Time.unscaledTime + recheckSeconds;
            RefreshDoubleButton();
        }

        private void Refresh()
        {
            if (_offline == null) return;

            // Detrás del "Tocá para jugar" (Boot/Ready) no se muestra: espera a que el juego esté andando.
            bool started = _game.State != GameState.Boot && _game.State != GameState.Ready;
            bool show = _offline.HasPending && started;
            panel.SetActive(show);
            if (!show) return;

            bodyLabel.text = Loc.Format("offline.body", FormatCoins(_offline.PendingCoins), FormatAway(_offline.PendingSeconds));
            RefreshDoubleButton();
        }

        private void RefreshDoubleButton()
        {
            bool offered = AdPolicy.RewardedAdsOffered;   // sin anuncios (Basic Launch de la web) el botón no existe
            doubleButton.gameObject.SetActive(offered);
            if (!offered) return;

            doubleButton.interactable = _ads != null && reward != null && _ads.Evaluate(reward) == AdRequestStatus.Ready;
        }

        // 12.345 en español y 12,345 en inglés.
        private static string FormatCoins(long coins)
        {
            string text = coins.ToString("N0", CultureInfo.InvariantCulture);
            return Loc.DecimalSeparator == ',' ? text.Replace(',', '.') : text;
        }

        // "1 h 20 min" o "35 min".
        private static string FormatAway(float seconds)
        {
            int minutes = Mathf.Max(1, Mathf.RoundToInt(seconds / 60f));
            return minutes >= 60 ? Loc.Format("offline.time_hm", minutes / 60, minutes % 60) : Loc.Format("offline.time_m", minutes);
        }
    }
}
