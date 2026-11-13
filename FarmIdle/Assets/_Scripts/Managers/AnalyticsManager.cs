using System;
using UnityEngine;

namespace FarmIdle
{
    // Registra los eventos de analítica (ROADMAP N-01). Escucha a los managers que ya existen y llama a un IAnalytics: ni la UI ni el
    // gameplay saben que esto existe, y los eventos que todavía no tienen sistema (offline, prestigio, misiones) los emite quien los cree
    // con Log(AnalyticsEvents.X, ...).
    //   - Servicio: ConsoleAnalytics en el Editor y en builds de desarrollo, NullAnalytics en los finales (hasta que haya un SDK real).
    //   - Sesión: empieza la primera vez que el juego pasa a Playing y termina cuando la app se pausa o se cierra (no cuando el jugador
    //     pausa el juego); al volver empieza otra. Al terminar emite sesion_fin(duracion) y abandono(ultimo_pad), la última compra.
    //   - Restaurar una partida no cuenta como jugar: no emite compras ni niveles.
    //   - Sin asignaciones por cuadro: solo reacciona a eventos del juego (que son pocos por minuto).
    public sealed class AnalyticsManager : MonoBehaviour
    {
        public const string NoPad = "ninguno";

        private static readonly string[] TutorialNames = Enum.GetNames(typeof(TutorialStep));

        public static AnalyticsManager Instance { get; private set; }

        private IAnalytics _service;
        private bool _subscribed;
        private bool _sessionOpen;
        private float _sessionStart;
        private string _lastPad = NoPad;

        private GameManager _game;
        private ProgressionManager _progression;
        private TutorialManager _tutorial;
        private AdsManager _ads;
        private OfferManager _offers;
        private FertilizerManager _fertilizer;
        private BuffManager _buffs;
        private PrestigeManager _prestige;
        private OfflineManager _offline;
        private MissionManager _missions;
        private AchievementManager _achievements;
        private DailyRewardManager _daily;
        private OrdersManager _orders;
        private DecorationManager _decorations;
        private HelperManager _helpers;
        private SkinManager _skins;
        private EconomyManager _economy;

        public IAnalytics Service => _service;
        public bool SessionOpen => _sessionOpen;

        // Id de la última zona o mejora comprada en esta partida ("ninguno" si todavía no compró nada).
        public string LastPad => _lastPad;

        // Lo llama el Bootstrapper, al final (escucha a todos los demás); hasta entonces Instance es null. Con 'service' (pruebas) se usa
        // ese en vez del que corresponde al build.
        public void Initialize(IAnalytics service = null)
        {
            Unsubscribe();
            _service = service ?? CreateDefaultService();
            Subscribe();
            Instance = this;
            Debug.Log("[FarmIdle] Analítica: servicio " + _service.Name + ".");

            // Un arranque que ya está jugando (pruebas con servicios sueltos) no va a recibir el aviso: la sesión empieza de una.
            if (_game != null && _game.IsPlaying) BeginSession();
        }

        // Cambia el servicio en marcha (las pruebas ponen uno que anota lo que llega).
        public void SetService(IAnalytics service)
        {
            _service = service ?? new NullAnalytics();
        }

        private static IAnalytics CreateDefaultService()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            return new ConsoleAnalytics();
#else
            return new NullAnalytics();
#endif
        }

        // ---------- Para quien emite sus propios eventos ----------

        public void Log(string eventName)
        {
            _service.Log(eventName);
        }

        public void Log(string eventName, string key, string value)
        {
            _service.Log(eventName, key, value);
        }

        public void Log(string eventName, string key, long value)
        {
            _service.Log(eventName, key, value);
        }

        // ---------- Sesión ----------

        public void BeginSession()
        {
            if (_sessionOpen) return;

            _sessionOpen = true;
            _sessionStart = Time.realtimeSinceStartup;
            _service.Log(AnalyticsEvents.SessionStart);
        }

        public void EndSession()
        {
            if (!_sessionOpen) return;

            _sessionOpen = false;
            long seconds = (long)Mathf.Max(0f, Time.realtimeSinceStartup - _sessionStart);
            _service.Log(AnalyticsEvents.SessionEnd, AnalyticsEvents.DurationKey, seconds);
            _service.Log(AnalyticsEvents.Abandon, AnalyticsEvents.LastPadKey, _lastPad);
        }

        private void OnApplicationPause(bool paused)
        {
            if (_service == null) return;

            if (paused)
            {
                EndSession();
            }
            else if (_game != null && _game.State != GameState.Boot && _game.State != GameState.Ready)
            {
                BeginSession();
            }
        }

        private void OnApplicationQuit()
        {
            if (_service != null) EndSession();
        }

        // ---------- Escuchar al juego ----------

        private void Subscribe()
        {
            if (_subscribed) return;
            _subscribed = true;

            _game = GameManager.Instance;
            if (_game != null) _game.StateChanged += OnStateChanged;

            _progression = ProgressionManager.Instance;
            if (_progression != null)
            {
                _progression.Changed += OnProgressChanged;
                _progression.LevelChanged += OnLevelChanged;
            }

            _tutorial = TutorialManager.Instance;
            if (_tutorial != null) _tutorial.StepChanged += OnTutorialStep;

            _ads = AdsManager.Instance;
            if (_ads != null)
            {
                _ads.AdStarted += OnAdStarted;
                _ads.AdFinished += OnAdFinished;
            }

            _offers = OfferManager.Instance;
            if (_offers != null) _offers.OfferStarted += OnOfferStarted;

            _fertilizer = FertilizerManager.Instance;
            if (_fertilizer != null) _fertilizer.Used += OnFertilizerUsed;

            _buffs = BuffManager.Instance;
            if (_buffs != null) _buffs.BuffStarted += OnBuffStarted;

            _prestige = PrestigeManager.Instance;
            if (_prestige != null)
            {
                _prestige.SeasonStarted += OnSeasonStarted;
                _prestige.ShopBought += OnSeedShopBought;
            }

            _orders = OrdersManager.Instance;
            if (_orders != null) _orders.Completed += OnOrderCompleted;

            _decorations = DecorationManager.Instance;
            if (_decorations != null) _decorations.Bought += OnDecorationBought;

            _helpers = HelperManager.Instance;
            if (_helpers != null) _helpers.Bought += OnHelperBought;

            _skins = SkinManager.Instance;
            if (_skins != null) _skins.Bought += OnSkinBought;

            _economy = EconomyManager.Instance;
            if (_economy != null) _economy.RareHarvested += OnRareHarvested;

            _offline = OfflineManager.Instance;
            if (_offline != null) _offline.Claimed += OnOfflineClaimed;

            _missions = MissionManager.Instance;
            if (_missions != null) _missions.Completed += OnMissionCompleted;

            _achievements = AchievementManager.Instance;
            if (_achievements != null) _achievements.Unlocked += OnAchievementUnlocked;

            _daily = DailyRewardManager.Instance;
            if (_daily != null) _daily.Claimed += OnDailyClaimed;
        }

        private void Unsubscribe()
        {
            if (!_subscribed) return;
            _subscribed = false;

            if (_game != null) _game.StateChanged -= OnStateChanged;

            if (_progression != null)
            {
                _progression.Changed -= OnProgressChanged;
                _progression.LevelChanged -= OnLevelChanged;
            }

            if (_tutorial != null) _tutorial.StepChanged -= OnTutorialStep;

            if (_ads != null)
            {
                _ads.AdStarted -= OnAdStarted;
                _ads.AdFinished -= OnAdFinished;
            }

            if (_offers != null) _offers.OfferStarted -= OnOfferStarted;
            if (_fertilizer != null) _fertilizer.Used -= OnFertilizerUsed;
            if (_buffs != null) _buffs.BuffStarted -= OnBuffStarted;
            if (_prestige != null)
            {
                _prestige.SeasonStarted -= OnSeasonStarted;
                _prestige.ShopBought -= OnSeedShopBought;
            }

            if (_orders != null) _orders.Completed -= OnOrderCompleted;
            if (_decorations != null) _decorations.Bought -= OnDecorationBought;
            if (_helpers != null) _helpers.Bought -= OnHelperBought;
            if (_skins != null) _skins.Bought -= OnSkinBought;
            if (_economy != null) _economy.RareHarvested -= OnRareHarvested;
            if (_offline != null) _offline.Claimed -= OnOfflineClaimed;
            if (_missions != null) _missions.Completed -= OnMissionCompleted;
            if (_achievements != null) _achievements.Unlocked -= OnAchievementUnlocked;
            if (_daily != null) _daily.Claimed -= OnDailyClaimed;
        }

        // ---------- N-06: pedidos, sumideros, tienda de semillas y cosecha rara ----------

        private void OnOrderCompleted(int slot, long coins, int seeds)
        {
            _service.Log(AnalyticsEvents.OrderCompleted, AnalyticsEvents.BigKey, OrderBoard.IsBig(slot) ? 1L : 0L);
        }

        private void OnDecorationBought(int index)
        {
            DecorationData decoration = _decorations.At(index);
            if (decoration != null) _service.Log(AnalyticsEvents.DecorationBought, AnalyticsEvents.IdKey, decoration.Id);
        }

        private void OnHelperBought(int level)
        {
            _service.Log(AnalyticsEvents.HelperBought, AnalyticsEvents.LevelKey, level);
        }

        // La skin recién comprada queda puesta: es la equipada.
        private void OnSkinBought(int index)
        {
            SkinData skin = _skins.EquippedSkin;
            if (skin != null) _service.Log(AnalyticsEvents.SkinBought, AnalyticsEvents.IdKey, skin.Id);
        }

        private void OnSeedShopBought(int index)
        {
            _service.Log(AnalyticsEvents.SeedShopBought, AnalyticsEvents.IdKey, _prestige.ShopItem(index).Id);
        }

        private void OnRareHarvested(CropData crop, long bonus)
        {
            _service.Log(AnalyticsEvents.RareHarvest, AnalyticsEvents.IdKey, crop.Id);
        }

        private void OnDailyClaimed(int day, long coins, int fertilizer)
        {
            _service.Log(AnalyticsEvents.DailyReward, AnalyticsEvents.DayKey, day);
        }

        private void OnAchievementUnlocked(AchievementData achievement)
        {
            _service.Log(AnalyticsEvents.AchievementUnlocked, AnalyticsEvents.IdKey, achievement.Id);
        }

        private void OnMissionCompleted(MissionData mission)
        {
            _service.Log(AnalyticsEvents.MissionCompleted, AnalyticsEvents.IdKey, mission.Id);
        }

        private void OnOfflineClaimed(long coins, bool doubled)
        {
            _service.Log(AnalyticsEvents.OfflineClaimed, AnalyticsEvents.DoubledKey, doubled ? 1L : 0L);
        }

        private void OnSeasonStarted(int season)
        {
            _service.Log(AnalyticsEvents.Prestige, AnalyticsEvents.SeasonKey, season);
        }

        private void OnStateChanged(GameState previous, GameState current)
        {
            if (current == GameState.Playing) BeginSession();
        }

        private void OnProgressChanged(ProgressChange change)
        {
            if (change.Kind != ProgressChangeKind.ZoneUnlocked && change.Kind != ProgressChangeKind.UpgradeLevelChanged) return;

            _lastPad = change.Id;
            _service.Log(AnalyticsEvents.PadBought, AnalyticsEvents.IdKey, change.Id);
        }

        private void OnLevelChanged(int level)
        {
            _service.Log(AnalyticsEvents.LevelUp, AnalyticsEvents.LevelKey, level);
        }

        private void OnTutorialStep(TutorialStep step)
        {
            _service.Log(AnalyticsEvents.TutorialStep, AnalyticsEvents.StepKey, TutorialNames[(int)step]);
        }

        private void OnOfferStarted(RewardData reward, int slot)
        {
            if (reward != null) _service.Log(AnalyticsEvents.RewardedOffered, AnalyticsEvents.PlacementKey, reward.Placement);
        }

        private void OnAdStarted(RewardData reward)
        {
            if (reward != null) _service.Log(AnalyticsEvents.RewardedAccepted, AnalyticsEvents.PlacementKey, reward.Placement);
        }

        private void OnAdFinished(RewardData reward, AdResult result, bool granted)
        {
            if (reward != null && result == AdResult.Rewarded) _service.Log(AnalyticsEvents.RewardedCompleted, AnalyticsEvents.PlacementKey, reward.Placement);
        }

        private void OnFertilizerUsed(int grown)
        {
            _service.Log(AnalyticsEvents.FertilizerUsed, AnalyticsEvents.AmountKey, grown);
        }

        private void OnBuffStarted(RewardData reward)
        {
            if (reward != null && reward.Kind == RewardKind.MoveSpeedBuff) _service.Log(AnalyticsEvents.TurboUsed);
        }

        private void OnDestroy()
        {
            Unsubscribe();
            if (Instance == this) Instance = null;
        }
    }
}
