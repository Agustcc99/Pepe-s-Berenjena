using System;
using UnityEngine;

namespace FarmIdle
{
    // F-17 (tanda 2): ayudantes automáticos que cosechan y venden solos, el sumidero de monedas con efecto. Dueño del nivel:
    //   - Se compran en orden (TryBuyNext, por EconomyManager.TrySpend); alcanza con guardar el NIVEL (0 = ninguno).
    //   - Efecto: mientras se juega (GameManager en Playing), suman por segundo monedas = OfflineManager.ProductionPerSecond x
    //     Share del nivel. Es la misma vara que ya usan las misiones y el progreso offline, así que sigue a las mejoras, el
    //     prestigio y las zonas abiertas sin tocarlos. Las monedas se acreditan enteras (el resto queda acumulado).
    //   - Una nueva temporada (prestigio) los reinicia, como a las mejoras: es el sumidero que vuelve a aparecer en cada vuelta.
    //   - Partida guardada: ISaveable "helpers" con el nivel. Restaurar no cobra nada; una partida sin la sección arranca en 0.
    //   - Números PROVISORIOS (10 / 20 / 35 %): se ajustan con datos reales (B-05); el bot de balance no los compra.
    public sealed class HelperManager : MonoBehaviour, ISaveable
    {
        public static HelperManager Instance { get; private set; }

        private const int SaveVersion = 1;

        [Serializable]
        private sealed class HelperSave
        {
            public int version = SaveVersion;
            public int level;
        }

        private GameConfig _config;
        private int _level;
        private double _carry;
        private PrestigeManager _prestige;

        public int Level => _level;
        public int Total => _config != null ? _config.Helpers.Count : 0;
        public bool IsMaxed => _level >= Total;

        // La que vende el pad ahora, o null si ya están todos.
        public HelperData Next => !IsMaxed ? _config.Helpers[_level] : null;
        public long NextCost => Next != null ? Next.Cost : 0L;

        // Efecto del nivel actual (0 sin ayudantes).
        public float Share => _level > 0 ? _config.Helpers[_level - 1].Share : 0f;

        public bool CanBuyNext
        {
            get
            {
                EconomyManager economy = EconomyManager.Instance;
                return !IsMaxed && economy != null && economy.CanAfford(NextCost);
            }
        }

        public event Action Changed;
        public event Action<int> Bought;
        public event Action Refused;

        public void ReportRefused()
        {
            Refused?.Invoke();
        }

        public void Initialize(GameConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            Instance = this;

            _prestige = PrestigeManager.Instance;
            if (_prestige != null) _prestige.SeasonStarted += OnSeasonStarted;
        }

        private void OnSeasonStarted(int season)
        {
            ResetSeason();
        }

        public void ResetSeason()
        {
            _level = 0;
            _carry = 0d;
            Changed?.Invoke();
            SaveRequested?.Invoke();
        }

        public bool TryBuyNext()
        {
            if (IsMaxed) return false;

            EconomyManager economy = EconomyManager.Instance;
            if (economy == null || !economy.TrySpend(NextCost)) return false;

            _level++;
            Changed?.Invoke();
            Bought?.Invoke(_level);
            SaveRequested?.Invoke();
            return true;
        }

        private void Update()
        {
            GameManager game = GameManager.Instance;
            if (game == null || !game.IsPlaying) return;

            Tick(Time.deltaTime);
        }

        // Acredita lo que rinden los ayudantes en 'dt' segundos de juego. Sin asignaciones: dos cuentas y, a lo sumo, una suma.
        public void Tick(float dt)
        {
            if (_level <= 0 || dt <= 0f) return;

            OfflineManager offline = OfflineManager.Instance;
            EconomyManager economy = EconomyManager.Instance;
            if (offline == null || economy == null) return;

            _carry += offline.ProductionPerSecond * Share * dt;
            if (_carry < 1d) return;

            long whole = (long)_carry;
            _carry -= whole;
            economy.AddCoins(whole);
        }

        // ---------- Partida guardada ----------

        public string SaveId => "helpers";

        public event Action SaveRequested;

        public string Capture()
        {
            return JsonUtility.ToJson(new HelperSave { level = _level });
        }

        // Con null, vacío o ilegible vuelve a 0. El nivel se acota a los que existen.
        public void Restore(string json)
        {
            HelperSave save = null;
            if (!string.IsNullOrWhiteSpace(json))
            {
                try
                {
                    save = JsonUtility.FromJson<HelperSave>(json);
                }
                catch (ArgumentException)
                {
                    save = null;
                }
            }

            _level = save != null ? Mathf.Clamp(save.level, 0, Total) : 0;
            _carry = 0d;
            Changed?.Invoke();
        }

        private void OnDestroy()
        {
            if (_prestige != null) _prestige.SeasonStarted -= OnSeasonStarted;
            if (Instance == this) Instance = null;
        }
    }
}
