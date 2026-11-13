using System;
using UnityEngine;

namespace FarmIdle
{
    // Dueño del premio de login diario (F-07): una racha de 7 días con un premio chico por día que se reclama a mano, una vez por día.
    //   - Se puede reclamar una vez por día de calendario (fecha local). Reclamar al día siguiente sigue la racha; con GameConfig.DailyGraceDays
    //     días salteados (1 por defecto) todavía sigue; saltear más la corta y vuelve al día 1. Después del día 7 vuelve al 1.
    //   - Reloj local: con la hora atrasada no se puede reclamar y no se pierde nada (atrasar el reloj no da premios dobles); adelantarla
    //     sí da un premio, pero chico (R-08), igual que las misiones.
    //   - Premio: monedas = minutos de producción actual de las parcelas (OfflineManager.ProductionPerSecond, con MissionRewardScale y
    //     MissionMinCoins), o fertilizante en los días que lo dan; con el stock lleno cae a monedas. TryClaim solo funciona en Playing.
    //   - Partida guardada: ISaveable "daily" con la fecha del último reclamo y la racha. Una partida anterior a F-07 no la trae: el primer
    //     día de juego ya se puede reclamar.
    //   - BecameAvailable avisa cuando el premio pasa a estar listo (al empezar a jugar o al cambiar el día con el juego abierto): la UI
    //     decide si lo muestra.
    public sealed class DailyRewardManager : MonoBehaviour, ISaveable
    {
        public static DailyRewardManager Instance { get; private set; }

        private const int SaveVersion = 1;
        private const float DayCheckSeconds = 30f;

        [Serializable]
        private sealed class DailySave
        {
            public int version = SaveVersion;
            public int lastClaim;   // la fecha del último reclamo como 20261026 (0 = nunca)
            public int streak;      // días reclamados seguidos
        }

        private GameConfig _config;
        private GameManager _game;
        private FertilizerManager _fertilizer;
        private EconomyManager _economy;
        private int _lastClaim;
        private int _streak;
        private bool _wasClaimable;
        private float _nextCheck;

        // La fecha local de hoy; las pruebas la reemplazan.
        public Func<DateTime> Clock { get; set; } = () => DateTime.Now;

        // Días reclamados seguidos (0 si nunca reclamó o la racha se cortó y todavía no volvió a reclamar).
        public int Streak => _streak;

        // Hay un premio listo para reclamar hoy.
        public bool IsClaimable => DailyRewardRules.CanClaim(_lastClaim, _streak, DailyRewardRules.DayKeyOf(Clock()), Grace, out _);

        // El día de la escalera (1 a 7) del premio que se reclamaría hoy; si hoy ya se reclamó, el del último reclamo (0 si nunca).
        public int Day
        {
            get
            {
                return DailyRewardRules.CanClaim(_lastClaim, _streak, DailyRewardRules.DayKeyOf(Clock()), Grace, out int next)
                    ? DailyRewardRules.DayOfCycle(next)
                    : DailyRewardRules.DayOfCycle(_streak);
            }
        }

        // Cambió algo que se ve (reclamo, partida cargada, cambió el día).
        public event Action Changed;

        // El premio pasó a estar listo (una vez por cada vez que pasa de no listo a listo, incluido al empezar a jugar).
        public event Action BecameAvailable;

        // Se reclamó el premio: el día de la escalera, las monedas y el fertilizante que se acreditaron.
        public event Action<int, long, int> Claimed;

        private int Grace => _config != null ? _config.DailyGraceDays : 1;
        private bool IsPlaying => _game == null || _game.IsPlaying;

        // Lo llama el Bootstrapper, después de los sistemas que usa; hasta entonces Instance es null.
        public void Initialize(GameConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));

            _game = GameManager.Instance;
            if (_game != null) _game.StateChanged += OnStateChanged;

            _fertilizer = FertilizerManager.Instance;
            _economy = EconomyManager.Instance;
            Instance = this;
        }

        // ---------- Consultas ----------

        // El premio que se acreditaría ahora: fertilizante en los días que lo dan (si hay lugar), si no monedas.
        public int RewardFertilizerForDay(int day)
        {
            int amount = _config.DailyRewardFertilizer(day);
            return amount > 0 && _fertilizer != null && !_fertilizer.IsFull ? amount : 0;
        }

        public int FertilizerOfDay(int day) { return _config.DailyRewardFertilizer(day); }

        public long RewardCoinsForDay(int day)
        {
            return RewardFertilizerForDay(day) > 0 ? 0L : CoinsFor(day);
        }

        private long CoinsFor(int day)
        {
            OfflineManager offline = OfflineManager.Instance;
            double rate = offline != null ? offline.ProductionPerSecond : 0d;
            return MissionRules.Coins(rate, _config.DailyRewardMinutes(day) * _config.MissionRewardScale, _config.MissionMinCoins);
        }

        // ---------- El día ----------

        private void OnStateChanged(GameState previous, GameState current)
        {
            if (current == GameState.Playing) CheckAvailability(force: true);
        }

        // La app volvió de segundo plano (puede haber pasado la medianoche).
        public void OnApplicationPause(bool paused)
        {
            if (!paused && _config != null && IsPlaying) CheckAvailability(force: false);
        }

        // Con el juego abierto de un día para el otro: se mira la fecha cada tanto (sin asignar memoria).
        private void Update()
        {
            if (_config == null || Time.unscaledTime < _nextCheck) return;

            _nextCheck = Time.unscaledTime + DayCheckSeconds;
            if (IsPlaying) CheckAvailability(force: false);
        }

        // Avisa si el premio pasó a estar listo (con 'force', también si ya lo estaba: al empezar a jugar).
        private void CheckAvailability(bool force)
        {
            bool claimable = IsClaimable;
            bool became = claimable && (force || !_wasClaimable);
            bool changed = claimable != _wasClaimable;
            _wasClaimable = claimable;

            if (changed) Changed?.Invoke();
            if (became) BecameAvailable?.Invoke();
        }

        // ---------- Reclamar ----------

        // Acredita el premio de hoy. Devuelve false (sin tocar nada) si ya se reclamó hoy o el juego no está en marcha.
        public bool TryClaim()
        {
            if (_config == null || !IsPlaying) return false;

            int todayKey = DailyRewardRules.DayKeyOf(Clock());
            if (!DailyRewardRules.CanClaim(_lastClaim, _streak, todayKey, Grace, out int nextStreak)) return false;

            int day = DailyRewardRules.DayOfCycle(nextStreak);
            int fertilizer = 0;
            long coins = 0L;
            if (RewardFertilizerForDay(day) > 0) fertilizer = _fertilizer.Grant(_config.DailyRewardFertilizer(day));
            if (fertilizer <= 0) coins = CoinsFor(day);

            if (coins > 0L && _economy != null) _economy.AddCoins(coins);

            _lastClaim = todayKey;
            _streak = nextStreak;
            _wasClaimable = false;

            Claimed?.Invoke(day, coins, fertilizer);
            Changed?.Invoke();
            SaveRequested?.Invoke();
            return true;
        }

        // ---------- Partida guardada ----------

        public string SaveId => "daily";

        public event Action SaveRequested;

        public string Capture()
        {
            return JsonUtility.ToJson(new DailySave { lastClaim = _lastClaim, streak = _streak });
        }

        // Con null, vacío o ilegible: como una partida nueva (nunca reclamó). Una fecha inválida o una racha negativa se corrigen, sin tirar
        // excepciones. Restaurar no reclama nada.
        public void Restore(string json)
        {
            DailySave save = null;
            if (!string.IsNullOrWhiteSpace(json))
            {
                try
                {
                    save = JsonUtility.FromJson<DailySave>(json);
                }
                catch (ArgumentException)
                {
                    save = null;
                }
            }

            _lastClaim = 0;
            _streak = 0;
            if (save != null && DailyRewardRules.TryDate(save.lastClaim, out _))
            {
                _lastClaim = save.lastClaim;
                _streak = Mathf.Max(0, save.streak);
            }

            _wasClaimable = false;
            Changed?.Invoke();
            if (IsPlaying && _game != null) CheckAvailability(force: false);
        }

        private void OnDestroy()
        {
            if (_game != null) _game.StateChanged -= OnStateChanged;
            if (Instance == this) Instance = null;
        }
    }
}
