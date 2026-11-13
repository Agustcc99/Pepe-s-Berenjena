using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace FarmIdle
{
    // Dueño del progreso offline (B-01): lo que producen las parcelas mientras el jugador no está.
    //   - Cuándo: al abrir el juego (la hora de la última vez que se lo vio viene en la partida) y al volver de segundo plano en el celular.
    //     El tiempo fuera cuenta con un tope (GameConfig.OfflineBaseCapSeconds, 2 h) y solo si pasó el mínimo (OfflineMinSeconds).
    //   - Cuánto: OfflineMath.Coins con lo que rinden las parcelas abiertas (IProductionSource, registradas por los campos activos), el
    //     multiplicador de precio (mejoras x prestigio, sin buffs), la velocidad de crecimiento y la eficiencia del GameConfig, con un
    //     tope en monedas por hora fuera (GameConfig.OfflineMaxCoinsPerHour; se aplica antes de Duplicar).
    //   - Reloj local, sin red: si la hora retrocedió no se acredita nada (R-08); si se adelantó, el tope limita el truco.
    //   - Nada se acredita solo: el resultado queda "pendiente" y la tarjeta de bienvenida ofrece Reclamar o Duplicar (video, ver
    //     RewardKind.OfflineDouble). Lo pendiente se guarda, así cerrar sin reclamar no lo pierde.
    //   - Partida guardada: ISaveable "offline" con la hora de la última vez (UTC), lo pendiente y la ventana de tiempo. Es volátil: el
    //     SaveManager escribe al pausar o cerrar aunque no haya otros cambios, así la hora nunca queda vieja. Una partida anterior a B-01
    //     no trae la sección y arranca sin nada pendiente.
    public sealed class OfflineManager : MonoBehaviour, ISaveable, ISaveableVolatile
    {
        public static OfflineManager Instance { get; private set; }

        private const int SaveVersion = 1;

        [Serializable]
        private sealed class OfflineSave
        {
            public int version = SaveVersion;
            public string lastSeenUtc = string.Empty;   // ISO 8601
            public long pendingCoins;
            public float pendingSeconds;
        }

        private GameConfig _config;
        private GameManager _game;
        private readonly List<IProductionSource> _sources = new List<IProductionSource>(8);

        private DateTime _lastSeenOnLoad;     // la marca del archivo, mientras haya tiempo fuera sin resolver
        private double _unresolvedSeconds;    // tiempo fuera ya medido que todavía no se convirtió en monedas (hace falta que los campos estén)
        private long _pendingCoins;
        private float _pendingSeconds;
        private DateTime _pausedAtUtc;
        private bool _paused;

        // La hora actual en UTC; las pruebas la reemplazan.
        public Func<DateTime> Clock { get; set; } = () => DateTime.UtcNow;

        // Monedas listas para reclamar y el tiempo fuera al que corresponden (para mostrarlo).
        public long PendingCoins => _pendingCoins;
        public float PendingSeconds => _pendingSeconds;
        public bool HasPending => _pendingCoins > 0L;

        // Tope de tiempo fuera que se acredita, en segundos: el base del GameConfig más lo que suma la mejora "Almacén" (nunca baja del base).
        public float CapSeconds
        {
            get
            {
                if (_config == null) return 0f;

                ProgressionManager progression = ProgressionManager.Instance;
                return _config.OfflineBaseCapSeconds + (progression != null ? progression.Modifiers.OfflineCapBonusSeconds : 0) + PrestigeManager.OfflineCapBonus;
            }
        }

        // Cambió lo pendiente (apareció, se reclamó, se cargó la partida).
        public event Action PendingChanged;

        // Se reclamó: monedas acreditadas y si fue con el video (duplicado).
        public event Action<long, bool> Claimed;

        // Lo llama el Bootstrapper, después de la economía y la progresión; hasta entonces Instance es null.
        public void Initialize(GameConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _game = GameManager.Instance;
            if (_game != null) _game.StateChanged += OnStateChanged;
            Instance = this;
        }

        // ---------- Fuentes de producción ----------

        public void Register(IProductionSource source)
        {
            if (source != null && !_sources.Contains(source)) _sources.Add(source);
        }

        public void Unregister(IProductionSource source)
        {
            _sources.Remove(source);
        }

        // Lo que rinden por segundo las fuentes abiertas, sin multiplicadores.
        public float BaseCoinsPerSecond
        {
            get
            {
                float total = 0f;
                for (int i = 0; i < _sources.Count; i++) total += _sources[i].BaseCoinsPerSecond;
                return total;
            }
        }

        // Lo que producen hoy las parcelas abiertas por segundo, ya con las mejoras, el prestigio y la velocidad de crecimiento (sin la
        // eficiencia offline). También sirve de vara para los premios de las misiones diarias (F-05).
        public double ProductionPerSecond
        {
            get
            {
                EconomyManager economy = EconomyManager.Instance;
                double price = economy != null ? (double)economy.Pricing.UpgradeMultiplier * economy.Pricing.PrestigeMultiplier : 1d;

                ProgressionManager progression = ProgressionManager.Instance;
                double growth = (progression != null ? progression.Modifiers.GrowthSpeed : 1d) * PrestigeManager.GrowthFactor;

                return BaseCoinsPerSecond * price * growth;
            }
        }

        // ---------- Resolver el tiempo fuera ----------

        private void OnStateChanged(GameState previous, GameState current)
        {
            if (current == GameState.Playing) Resolve();
        }

        // Convierte el tiempo fuera medido en monedas pendientes. Se hace al pasar a Playing (los campos ya están registrados) y no
        // antes: en Boot/Ready todavía no hay parcelas.
        public void Resolve()
        {
            if (_unresolvedSeconds <= 0d) return;

            double seconds = _unresolvedSeconds;
            _unresolvedSeconds = 0d;
            _lastSeenOnLoad = default(DateTime);

            if (seconds < _config.OfflineMinSeconds) return;

            long coins = CoinsFor(seconds);
            if (coins <= 0L) return;

            long maxCoins = CoinsFor(CapSeconds);
            _pendingCoins = Math.Min(maxCoins, SaturatingAdd(_pendingCoins, coins));
            _pendingSeconds = (float)Math.Min(seconds, CapSeconds);

            PendingChanged?.Invoke();
            SaveRequested?.Invoke();
        }

        private long CoinsFor(double seconds)
        {
            EconomyManager economy = EconomyManager.Instance;
            double price = economy != null ? (double)economy.Pricing.UpgradeMultiplier * economy.Pricing.PrestigeMultiplier : 1d;

            ProgressionManager progression = ProgressionManager.Instance;
            double growth = (progression != null ? progression.Modifiers.GrowthSpeed : 1d) * PrestigeManager.GrowthFactor;

            return OfflineMath.Coins(seconds, CapSeconds, BaseCoinsPerSecond, price, growth, _config.OfflineEfficiency, _config.OfflineMaxCoinsPerHour);
        }

        private static long SaturatingAdd(long a, long b)
        {
            return a > long.MaxValue - b ? long.MaxValue : a + b;
        }

        // Android/iOS: la app pasó a segundo plano y volvió (sin cerrarse). El tiempo entre las dos cuenta como tiempo fuera.
        // Mensaje de Unity; es público para poder probarlo sin escena.
        public void OnApplicationPause(bool paused)
        {
            if (_config == null) return;

            if (paused)
            {
                _pausedAtUtc = Clock();
                _paused = true;
                return;
            }

            if (!_paused) return;
            _paused = false;

            double away = OfflineMath.ElapsedSeconds(_pausedAtUtc, Clock());
            if (away < _config.OfflineMinSeconds) return;

            _unresolvedSeconds = Math.Min(_unresolvedSeconds + away, CapSeconds);
            if (_game == null || _game.IsPlaying) Resolve();
        }

        // ---------- Reclamar ----------

        // Acredita lo pendiente tal cual. Devuelve false si no había nada.
        public bool Claim()
        {
            return Credit(1L, doubled: false);
        }

        // Acredita lo pendiente x2 (lo llama AdsManager al terminar el video). Devuelve false si no había nada.
        public bool ClaimDoubled()
        {
            return Credit(2L, doubled: true);
        }

        private bool Credit(long factor, bool doubled)
        {
            if (_pendingCoins <= 0L) return false;

            long coins = _pendingCoins > long.MaxValue / factor ? long.MaxValue : _pendingCoins * factor;
            _pendingCoins = 0L;
            _pendingSeconds = 0f;

            EconomyManager economy = EconomyManager.Instance;
            if (economy != null) economy.AddCoins(coins);

            Claimed?.Invoke(coins, doubled);
            PendingChanged?.Invoke();
            SaveRequested?.Invoke();
            return true;
        }

        // ---------- Partida guardada ----------

        public string SaveId => "offline";

        public event Action SaveRequested;

        // Escribe en el archivo aunque no haya cambios: la hora de la última vez tiene que estar al día.
        public bool HasVolatileState => true;

        public string Capture()
        {
            // Con tiempo fuera sin resolver (la partida se cargó y todavía no se empezó a jugar) se conserva la marca vieja: restamparla
            // con "ahora" haría perder ese tiempo si el jugador cierra sin llegar a jugar.
            DateTime stamp = _unresolvedSeconds > 0d && _lastSeenOnLoad != default(DateTime) ? _lastSeenOnLoad : Clock();

            return JsonUtility.ToJson(new OfflineSave
            {
                lastSeenUtc = stamp.ToString("o", CultureInfo.InvariantCulture),
                pendingCoins = _pendingCoins,
                pendingSeconds = _pendingSeconds
            });
        }

        // Con null, vacío o ilegible: nada pendiente y sin marca (partida nueva o anterior a B-01). Mide el tiempo fuera contra la hora
        // actual pero no lo convierte todavía en monedas (ver Resolve).
        public void Restore(string json)
        {
            OfflineSave save = null;
            if (!string.IsNullOrWhiteSpace(json))
            {
                try
                {
                    save = JsonUtility.FromJson<OfflineSave>(json);
                }
                catch (ArgumentException)
                {
                    save = null;
                }
            }

            _unresolvedSeconds = 0d;
            _lastSeenOnLoad = default(DateTime);
            _pendingCoins = save != null ? Math.Max(0L, save.pendingCoins) : 0L;
            _pendingSeconds = save != null ? Math.Max(0f, save.pendingSeconds) : 0f;

            if (save != null && DateTime.TryParse(save.lastSeenUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime lastSeen))
            {
                double elapsed = OfflineMath.ElapsedSeconds(lastSeen, Clock());
                if (elapsed > 0d)
                {
                    _lastSeenOnLoad = lastSeen;
                    _unresolvedSeconds = Math.Min(elapsed, CapSeconds);
                }
            }

            PendingChanged?.Invoke();
            if (_game != null && _game.IsPlaying) Resolve();
        }

        private void OnDestroy()
        {
            if (_game != null) _game.StateChanged -= OnStateChanged;
            if (Instance == this) Instance = null;
        }
    }
}
