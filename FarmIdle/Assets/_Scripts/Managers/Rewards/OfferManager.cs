using System;
using UnityEngine;

namespace FarmIdle
{
    // El ritmo del símbolo de oferta: cada cierto tiempo de juego aparece un símbolo en el mundo que ofrece un video a cambio de la
    // velocidad (la recompensa MoveSpeedBuff del GameConfig). Este servicio solo decide CUÁNDO y DÓNDE (qué lugar de los que declaró
    // el entorno); dibujarlo y detectar al jugador es de Environment (OfferSpawner / RewardSymbol), que lo escucha por eventos.
    //   - Ritmo (GameConfig): el primero a los 60 s de juego, después entre 120 y 180 s (sorteado) tras irse el anterior, y el
    //     símbolo se queda 25 s. Todo en tiempo de juego: no corre en pausa, en el menú ni con un anuncio en pantalla.
    //   - Un solo símbolo a la vez. No aparece con el buff activo, con un anuncio en pantalla, sin anuncio listo o con la recompensa
    //     enfriándose: en esos casos la cuenta regresiva se congela (no se acumula una oferta atrasada).
    //   - Se va solo al vencer su tiempo o cuando el jugador gana la recompensa (AdsManager.AdFinished con granted).
    //   - No se guarda: al reabrir el juego la espera vuelve a empezar (el buff sí se guarda, ver BuffManager).
    public sealed class OfferManager : MonoBehaviour
    {
        public static OfferManager Instance { get; private set; }

        // En la web no hay símbolo flotante: la regla de rewarded de CrazyGames pide que el botón de pedir el video no aparezca en una
        // pantalla de juego activo y en un lugar consistente (ROADMAP D-04; REWARDED_REGLAS.md). El Turbo por video queda sin forma de
        // pedirse en la web hasta tener un pad fijo en la plaza. En Android y en el Editor el símbolo sigue igual.
        public static bool SymbolsAllowedOn(RuntimePlatform platform)
        {
            return platform != RuntimePlatform.WebGLPlayer;
        }

        private GameConfig _config;
        private OfferTimer _timer;
        private RewardData _reward;
        private AdsManager _ads;
        private int _slotCount;
        private int _slot = -1;
        private int _lastSlot = -1;
        private System.Random _random;

        // La recompensa que se ofrece (null = no hay ninguna en el GameConfig: no se ofrece nada).
        public RewardData Reward => _reward;
        public bool HasOffer => _timer != null && _timer.IsOffering;

        // Segundos de juego que le quedan al símbolo (0 si no hay).
        public float OfferRemaining => HasOffer ? _timer.Remaining : 0f;

        // Segundos de juego que faltan para la próxima oferta (0 mientras hay una).
        public float NextOfferIn => _timer != null && !_timer.IsOffering ? _timer.Remaining : 0f;

        // El lugar (índice) donde está el símbolo ahora, o -1.
        public int CurrentSlot => HasOffer ? _slot : -1;

        // Apareció el símbolo, con su recompensa y el lugar.
        public event Action<RewardData, int> OfferStarted;

        // Se fue el símbolo; true si el jugador ganó la recompensa.
        public event Action<bool> OfferEnded;

        // Lo llama el Bootstrapper, después del AdsManager; hasta entonces Instance es null. 'seed' (pruebas) fija el azar.
        public void Initialize(GameConfig config, int? seed = null)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _reward = config.FirstRewardOfKind(RewardKind.MoveSpeedBuff);
            _random = seed.HasValue ? new System.Random(seed.Value) : new System.Random();

            _timer = new OfferTimer(config.OfferFirstDelaySeconds, config.OfferMinIntervalSeconds, config.OfferMaxIntervalSeconds,
                config.OfferLifetimeSeconds, () => (float)_random.NextDouble());
            _timer.Started += OnTimerStarted;
            _timer.Ended += OnTimerEnded;

            _ads = AdsManager.Instance;
            if (_ads != null) _ads.AdFinished += OnAdFinished;

            Instance = this;
        }

        // El entorno dice cuántos lugares tiene para el símbolo (OfferSpawner). Sin lugares no se ofrece nada.
        public void SetSlotCount(int count)
        {
            _slotCount = Mathf.Max(0, count);
        }

        // Debug y pruebas: la próxima oferta aparece en el siguiente frame en que se pueda.
        public void ForceOfferSoon()
        {
            if (_timer != null) _timer.ForceNextOffer();
        }

        // Debug y pruebas: el símbolo se va ya.
        public void CancelOffer()
        {
            if (_timer != null) _timer.Cancel(taken: false);
        }

        private void Update()
        {
            if (_timer == null) return;

            GameManager game = GameManager.Instance;
            if (game != null && !game.IsPlaying) return;

            float delta = Time.deltaTime;
            if (!(delta > 0f)) return;

            _timer.Tick(delta, CanOfferNow());
        }

        private bool CanOfferNow()
        {
            if (_reward == null || _slotCount <= 0 || _ads == null) return false;

            return _ads.Evaluate(_reward) == AdRequestStatus.Ready;
        }

        private void OnTimerStarted()
        {
            // Un lugar distinto del anterior, si hay más de uno.
            int slot = _random.Next(_slotCount);
            if (_slotCount > 1 && slot == _lastSlot) slot = (slot + 1 + _random.Next(_slotCount - 1)) % _slotCount;

            _slot = slot;
            _lastSlot = slot;
            OfferStarted?.Invoke(_reward, slot);
        }

        private void OnTimerEnded(bool taken)
        {
            _slot = -1;
            OfferEnded?.Invoke(taken);
        }

        private void OnAdFinished(RewardData reward, AdResult result, bool granted)
        {
            if (granted && reward == _reward && _timer.IsOffering) _timer.Cancel(taken: true);
        }

        private void OnDestroy()
        {
            if (_timer != null)
            {
                _timer.Started -= OnTimerStarted;
                _timer.Ended -= OnTimerEnded;
            }

            if (_ads != null) _ads.AdFinished -= OnAdFinished;
            if (Instance == this) Instance = null;
        }
    }
}
