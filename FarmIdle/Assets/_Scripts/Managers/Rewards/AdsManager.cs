using System;
using UnityEngine;

namespace FarmIdle
{
    // Qué pasó al pedir un anuncio (o al preguntar si se puede).
    public enum AdRequestStatus
    {
        Ready,            // (solo Evaluate) se podría pedir ahora
        Started,          // el anuncio empezó: el juego pasó a AdShowing
        NoReward,         // no hay recompensa
        NotPlaying,       // el juego no está en Playing (menú, pausa, otro anuncio)
        Busy,             // ya hay un anuncio en pantalla
        RewardCooldown,   // esa recompensa todavía se está enfriando (tiempo de juego)
        NotUseful,        // no serviría de nada: el stock de fertilizante está lleno, o el buff ya está activo
        Cooldown,         // pasó muy poco desde el anuncio anterior (tiempo real)
        NoAd              // el servicio no tiene un anuncio listo
    }

    // La ÚNICA puerta para pedir un anuncio con recompensa. Ni la UI ni el gameplay hablan con un SDK: piden la recompensa acá
    // (los pads y el símbolo de oferta de Environment, por ejemplo) y se enteran por los eventos.
    //   - Elige el servicio por plataforma (IAdService): Mock en el Editor, WebAdService en WebGL, AdMobAdService en Android si está
    //     el plugin (define FARMIDLE_ADMOB); sin plugin, un build de desarrollo usa el Mock y uno final usa NoAdService (nunca hay
    //     anuncio, no regala nada). El Mock solo se compila en el Editor y en los builds de desarrollo.
    //   - Mientras se muestra, el juego está en AdShowing (GameManager.BeginAd congela el tiempo) y vuelve a Playing con EndAd pase
    //     lo que pase: completo, cerrado, error, sin anuncio o cortado por tiempo.
    //   - La recompensa se otorga SOLO si el resultado es Rewarded, UNA vez (AdSession ignora resultados repetidos o tardíos) y por
    //     Managers: fertilizante al FertilizerManager, buffs al BuffManager.
    //   - Enfriamientos: mínimo entre anuncios (reloj sin escala, GameConfig.AdMinIntervalSeconds) y el de cada recompensa (tiempo de
    //     juego, RewardData.CooldownSeconds). No se guardan: reiniciar el juego los reinicia (riesgo conocido y acotado por el tope de
    //     stock y por la duración de los buffs).
    //   - Si la app se pausa o pierde el foco durante el anuncio no se hace nada especial: un SDK real devuelve el resultado al volver. Si
    //     nunca llega (se colgó), a los GameConfig.AdTimeoutSeconds de reloj sin escala (que no cuentan el tiempo en segundo plano) se
    //     da por cortado: vuelve a Playing, no se otorga nada, y un resultado que llegue después se ignora. Si la app se cierra durante
    //     el anuncio no se otorga ni se pierde nada (la recompensa nunca se guardó antes de ganarse).
    //   - Al otorgar pide guardar YA (SaveManager.SaveNow): lo ganado no queda esperando el guardado diferido.
    public sealed class AdsManager : MonoBehaviour
    {
        public static AdsManager Instance { get; private set; }

        private GameConfig _config;
        private IAdService _service;
        private AdSession _session;
        private readonly CooldownTracker _rewardCooldown = new CooldownTracker();
        private RewardData _current;

        public IAdService Service => _service;
        public bool IsShowing => _session != null && _session.IsShowing;

        // La recompensa del anuncio en pantalla (null si no hay).
        public RewardData CurrentReward => IsShowing ? _current : null;

        // Empezó un anuncio por esa recompensa (el juego ya está en AdShowing).
        public event Action<RewardData> AdStarted;

        // Terminó un anuncio: recompensa, resultado y si se otorgó (solo con Rewarded). El juego ya volvió a Playing.
        public event Action<RewardData, AdResult, bool> AdFinished;

        // Se pidió una recompensa y no se pudo (no llegó a empezar ningún anuncio).
        public event Action<RewardData, AdRequestStatus> RequestRejected;

        // Lo llama el Bootstrapper, después del SaveManager; hasta entonces Instance es null. Con 'service' (pruebas) se usa ese en
        // vez del que corresponde a la plataforma.
        public void Initialize(GameConfig config, IAdService service = null)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _service = service ?? CreateDefaultService();
            _session = new AdSession(config.AdMinIntervalSeconds, config.AdTimeoutSeconds);
            Instance = this;
            Debug.Log("[FarmIdle] Anuncios: servicio " + _service.Name + ".");
        }

        private IAdService CreateDefaultService()
        {
#if UNITY_EDITOR
            return new MockAdService();
#elif UNITY_WEBGL
            return new WebAdService(new JsWebAdBridge(allowSimulatedAds: Debug.isDebugBuild), gameObject.name, nameof(OnWebAdResult));
#elif UNITY_ANDROID && FARMIDLE_ADMOB
            return new AdMobAdService();
#elif DEVELOPMENT_BUILD
            Debug.LogWarning("[FarmIdle] Anuncios: no hay SDK en este build; se usa el anuncio simulado (solo desarrollo).");
            return new MockAdService();
#else
            // Un build final sin SDK NO tiene el anuncio simulado (ni siquiera está compilado): los videos no están disponibles.
            Debug.LogWarning("[FarmIdle] Anuncios: no hay SDK en este build; los videos no están disponibles.");
            return new NoAdService();
#endif
        }

        // ---------- Consultas ----------

        // El botón "Privacidad" de los ajustes: solo hay que mostrarlo si el servicio maneja el consentimiento (AdMob con UMP) y la región lo
        // exige (por ejemplo, la Unión Europea).
        public bool PrivacyOptionsRequired => _service is IConsentAware consent && consent.PrivacyOptionsRequired;

        public void ShowPrivacyOptions(Action<string> onDone = null)
        {
            if (_service is IConsentAware consent) consent.ShowPrivacyOptions(onDone ?? (_ => { }));
        }

        // Segundos de tiempo de juego que faltan para volver a pedir esa recompensa (0 = ya se puede).
        public float RewardCooldownRemaining(RewardData reward)
        {
            return reward != null ? _rewardCooldown.Remaining(reward.Id) : 0f;
        }

        // Segundos de reloj real que faltan para poder mostrar otro anuncio.
        public float AdCooldownRemaining()
        {
            return _session != null ? _session.CooldownRemaining(Time.unscaledTime) : 0f;
        }

        // ¿Se podría pedir ahora? Ready si sí; si no, el motivo. No muestra nada ni cambia nada.
        public AdRequestStatus Evaluate(RewardData reward)
        {
            if (reward == null) return AdRequestStatus.NoReward;

            GameManager game = GameManager.Instance;
            if (game != null && !game.IsPlaying) return _session.IsShowing ? AdRequestStatus.Busy : AdRequestStatus.NotPlaying;
            if (_session.IsShowing) return AdRequestStatus.Busy;
            if (!_rewardCooldown.IsReady(reward.Id)) return AdRequestStatus.RewardCooldown;
            if (!IsUseful(reward)) return AdRequestStatus.NotUseful;
            if (_session.CooldownRemaining(Time.unscaledTime) > 0f) return AdRequestStatus.Cooldown;
            if (!_service.IsReady(reward.Placement)) return AdRequestStatus.NoAd;

            return AdRequestStatus.Ready;
        }

        // Un pad de video avisa que el jugador se quedó parado y no se puede pedir: se anuncia el motivo por RequestRejected, igual que si
        // lo hubiera pedido, pero sin pedir nada (no empieza ningún anuncio ni toca el estado del juego).
        public void ReportRefused(RewardData reward)
        {
            AdRequestStatus status = Evaluate(reward);
            if (status != AdRequestStatus.Ready) RequestRejected?.Invoke(reward, status);
        }

        // ---------- Pedir ----------

        // Pide un anuncio por 'reward'. Si se puede, pasa el juego a AdShowing, lo muestra y devuelve Started; el resultado llega por
        // AdFinished. Si no, devuelve el motivo y avisa RequestRejected (el juego no se toca).
        public AdRequestStatus TryRequest(RewardData reward)
        {
            AdRequestStatus status = Evaluate(reward);
            if (status != AdRequestStatus.Ready)
            {
                RequestRejected?.Invoke(reward, status);
                return status;
            }

            if (_session.TryBegin(Time.unscaledTime, out int requestId) != AdRejection.None)
            {
                // No debería pasar (Evaluate ya miró lo mismo), pero un estado raro no puede dejar el juego trabado.
                RequestRejected?.Invoke(reward, AdRequestStatus.Busy);
                return AdRequestStatus.Busy;
            }

            GameManager game = GameManager.Instance;
            if (game != null && !game.BeginAd())
            {
                _session.Abort(Time.unscaledTime);
                RequestRejected?.Invoke(reward, AdRequestStatus.NotPlaying);
                return AdRequestStatus.NotPlaying;
            }

            _current = reward;
            AdStarted?.Invoke(reward);

            try
            {
                _service.ShowRewarded(reward.Placement, result => OnServiceResult(requestId, result));
            }
            catch (Exception e)
            {
                Debug.LogError("[FarmIdle] Anuncios: el servicio falló al mostrar (" + e.Message + ").");
                OnServiceResult(requestId, AdResult.Failed);
            }

            return AdRequestStatus.Started;
        }

        private bool IsUseful(RewardData reward)
        {
            switch (reward.Kind)
            {
                case RewardKind.Fertilizer:
                    FertilizerManager fertilizer = FertilizerManager.Instance;
                    return fertilizer != null && !fertilizer.IsFull;

                case RewardKind.MoveSpeedBuff:
                case RewardKind.SellPriceBuff:
                    BuffManager buffs = BuffManager.Instance;
                    return buffs != null && !buffs.IsActive(reward);

                case RewardKind.OfflineDouble:
                    OfflineManager offline = OfflineManager.Instance;
                    return offline != null && offline.HasPending;

                default:
                    return false;
            }
        }

        // ---------- Resultado ----------

        // Lo llama el servicio cuando el anuncio termina. Cuenta una sola vez por pedido.
        private void OnServiceResult(int requestId, AdResult result)
        {
            bool wasShown = result == AdResult.Rewarded || result == AdResult.Closed;
            if (!_session.TryComplete(requestId, Time.unscaledTime, wasShown))
            {
                Debug.LogWarning("[FarmIdle] Anuncios: resultado " + result + " ignorado (repetido o tardío, ya se había cerrado ese pedido).");
                return;
            }

            Finish(result);
        }

        private void Finish(AdResult result)
        {
            RewardData reward = _current;
            _current = null;

            // Primero el juego vuelve a Playing: así lo otorgado se aplica con el juego andando.
            GameManager game = GameManager.Instance;
            if (game != null) game.EndAd();

            bool granted = result == AdResult.Rewarded && reward != null && Grant(reward);
            if (granted)
            {
                _rewardCooldown.Start(reward.Id, reward.CooldownSeconds);
                SaveManager save = SaveManager.Instance;
                if (save != null) save.SaveNow(force: true);
            }

            AdFinished?.Invoke(reward, result, granted);
        }

        // Otorga la recompensa por Managers. Devuelve true si algo cambió.
        private static bool Grant(RewardData reward)
        {
            switch (reward.Kind)
            {
                case RewardKind.Fertilizer:
                    FertilizerManager fertilizer = FertilizerManager.Instance;
                    return fertilizer != null && fertilizer.Grant(Mathf.Max(1, Mathf.RoundToInt(reward.Magnitude))) > 0;

                case RewardKind.MoveSpeedBuff:
                case RewardKind.SellPriceBuff:
                    BuffManager buffs = BuffManager.Instance;
                    return buffs != null && buffs.Activate(reward);

                case RewardKind.OfflineDouble:
                    OfflineManager offline = OfflineManager.Instance;
                    return offline != null && offline.ClaimDoubled();

                default:
                    return false;
            }
        }

        // ---------- Tiempo ----------

        private void Update()
        {
            if (_session == null) return;

            _service.Tick(Time.unscaledDeltaTime);   // el Mock termina acá; puede llamar a OnServiceResult

            GameManager game = GameManager.Instance;
            if (game == null || game.IsPlaying) _rewardCooldown.Tick(Time.deltaTime);

            if (_session.HasTimedOut(Time.unscaledTime))
            {
                Debug.LogWarning("[FarmIdle] Anuncios: el anuncio no respondió en " + _config.AdTimeoutSeconds + " s; se da por cortado y no se otorga nada.");
                _session.Abort(Time.unscaledTime);
                Finish(AdResult.Failed);
            }
        }

        // La web devuelve el resultado por SendMessage (puente FarmIdleWeb.jslib): "<pedido>|<rewarded|closed|failed|nofill>".
        public void OnWebAdResult(string payload)
        {
            if (_service is WebAdService web) web.OnBridgeResult(payload);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
