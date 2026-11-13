using System;
using System.Collections.Generic;
using UnityEngine;

namespace FarmIdle
{
    // Dueño de los logros (F-06): metas de por vida (cosechá 500, llegá al nivel 10, cerrá una temporada...) con un premio chico que se
    // reclama a mano, como el de las misiones.
    //   - Cuenta escuchando a los sistemas que ya existen (la mochila, las ventas, las compras, el fertilizante, los videos, el Turbo, las
    //     misiones, el prestigio): ninguno sabe que esto existe. Solo cuenta jugando (estado Playing); restaurar una partida no suma nada.
    //   - Los contadores NO se reinician con el prestigio. El nivel no es un contador: se guarda el más alto alcanzado.
    //   - Un logro se desbloquea una sola vez, cuando su contador cruza la meta (evento Unlocked); cumplir varios de golpe avisa a cada uno.
    //   - Premio: igual que las misiones (minutos de producción actual con GameConfig.MissionRewardScale, o fertilizante; con el stock lleno
    //     cae a monedas). Se reclama con TryClaim (solo en Playing).
    //   - Partida guardada: ISaveable "achievements" con los contadores y los ids reclamados. Una partida anterior a F-06 no la trae y
    //     arranca en cero (el nivel se pone al día al empezar a jugar).
    public sealed class AchievementManager : MonoBehaviour, ISaveable
    {
        public static AchievementManager Instance { get; private set; }

        private const int SaveVersion = 1;
        private static readonly int KindCount = Enum.GetValues(typeof(AchievementKind)).Length;

        [Serializable]
        private sealed class AchievementSave
        {
            public int version = SaveVersion;
            public long[] counters = new long[0];   // por AchievementKind, en el orden del enum
            public string[] claimed = new string[0];
        }

        private GameConfig _config;
        private GameManager _game;
        private IReadOnlyInventory _backpack;
        private EconomyManager _economy;
        private FertilizerManager _fertilizer;
        private ProgressionManager _progression;
        private AdsManager _ads;
        private BuffManager _buffs;
        private MissionManager _missions;
        private PrestigeManager _prestige;
        private readonly long[] _counters = new long[KindCount];
        private readonly HashSet<string> _claimed = new HashSet<string>();

        // Los logros del juego, en el orden de la tarjeta.
        public int Count => _config != null ? _config.Achievements.Count : 0;

        // Logros cumplidos con el premio todavía sin reclamar (el aviso del botón del HUD).
        public int ClaimableCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < Count; i++)
                {
                    if (IsCompleteAt(i) && !IsClaimedAt(i)) count++;
                }

                return count;
            }
        }

        // Cambió algo que se ve (contadores, reclamo, partida cargada).
        public event Action Changed;

        // Un logro llegó a su meta (una sola vez por logro).
        public event Action<AchievementData> Unlocked;

        // Se reclamó el premio: el logro, las monedas y el fertilizante que se acreditaron.
        public event Action<AchievementData, long, int> Claimed;

        // Lo llama el Bootstrapper, después de todos los sistemas que escucha; hasta entonces Instance es null.
        public void Initialize(GameConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));

            _game = GameManager.Instance;
            if (_game != null) _game.StateChanged += OnStateChanged;

            BackpackManager backpack = BackpackManager.Instance;
            if (backpack != null)
            {
                _backpack = backpack.Backpack;
                _backpack.Changed += OnInventoryChanged;
            }

            _economy = EconomyManager.Instance;
            if (_economy != null) _economy.ProductSold += OnProductSold;

            _fertilizer = FertilizerManager.Instance;
            if (_fertilizer != null) _fertilizer.Used += OnFertilizerUsed;

            _progression = ProgressionManager.Instance;
            if (_progression != null)
            {
                _progression.Changed += OnProgressChanged;
                _progression.LevelChanged += OnLevelChanged;
            }

            _buffs = BuffManager.Instance;
            if (_buffs != null) _buffs.BuffStarted += OnBuffStarted;

            _missions = MissionManager.Instance;
            if (_missions != null) _missions.Claimed += OnMissionClaimed;

            _prestige = PrestigeManager.Instance;
            if (_prestige != null) _prestige.SeasonStarted += OnSeasonStarted;

            Instance = this;
        }

        // El servicio de anuncios se crea después del SaveManager (la partida ya cargada), así que se engancha acá, después de crearlo.
        public void BindAds()
        {
            if (_ads != null) return;

            _ads = AdsManager.Instance;
            if (_ads != null) _ads.AdFinished += OnAdFinished;
        }

        // ---------- Consultas ----------

        public AchievementData DataAt(int index) { return _config.Achievements[index]; }
        public long CounterOf(AchievementKind kind) { return _counters[(int)kind]; }
        public long ProgressAt(int index) { return Math.Min(_counters[(int)DataAt(index).Kind], DataAt(index).Target); }
        public bool IsCompleteAt(int index) { return _counters[(int)DataAt(index).Kind] >= DataAt(index).Target; }
        public bool IsClaimedAt(int index) { return _claimed.Contains(DataAt(index).Id); }

        // El premio que se acreditaría ahora si se reclamara: fertilizante si el logro lo da y hay lugar, si no monedas.
        public int RewardFertilizerAt(int index)
        {
            AchievementData data = DataAt(index);
            return data.RewardFertilizer > 0 && _fertilizer != null && !_fertilizer.IsFull ? data.RewardFertilizer : 0;
        }

        public long RewardCoinsAt(int index)
        {
            return RewardFertilizerAt(index) > 0 ? 0L : CoinsFor(DataAt(index));
        }

        private long CoinsFor(AchievementData data)
        {
            OfflineManager offline = OfflineManager.Instance;
            double rate = offline != null ? offline.ProductionPerSecond : 0d;
            return MissionRules.Coins(rate, data.RewardMinutes * _config.MissionRewardScale, _config.MissionMinCoins);
        }

        private bool IsPlaying => _game == null || _game.IsPlaying;

        // ---------- Avance ----------

        private void OnStateChanged(GameState previous, GameState current)
        {
            if (current == GameState.Playing) SyncLevel();
        }

        // La app volvió de segundo plano o el nivel cambió mientras no se jugaba (partida cargada): se pone al día el más alto alcanzado.
        private void SyncLevel()
        {
            if (_progression != null) SetAtLeast(AchievementKind.Level, _progression.Level);
        }

        private void OnInventoryChanged(InventoryChange change)
        {
            if (change.Added && !change.IsReplacement) Add(AchievementKind.Harvest, 1);
        }

        private void OnProductSold(CropData crop, long coins)
        {
            Add(AchievementKind.Sell, 1);
            Add(AchievementKind.CoinsEarned, coins);
        }

        private void OnFertilizerUsed(int grown)
        {
            Add(AchievementKind.FertilizerUsed, 1);
        }

        private void OnProgressChanged(ProgressChange change)
        {
            if (change.Kind == ProgressChangeKind.ZoneUnlocked) Add(AchievementKind.ZonesOpened, 1);
            else if (change.Kind == ProgressChangeKind.UpgradeLevelChanged) Add(AchievementKind.UpgradesBought, 1);
        }

        private void OnLevelChanged(int level)
        {
            SetAtLeast(AchievementKind.Level, level);
        }

        private void OnAdFinished(RewardData reward, AdResult result, bool granted)
        {
            if (reward != null && result == AdResult.Rewarded) Add(AchievementKind.VideosWatched, 1);
        }

        private void OnBuffStarted(RewardData reward)
        {
            if (reward != null && reward.Kind == RewardKind.MoveSpeedBuff) Add(AchievementKind.TurboUsed, 1);
        }

        private void OnMissionClaimed(MissionData mission, long coins, int fertilizer)
        {
            Add(AchievementKind.MissionsClaimed, 1);
        }

        private void OnSeasonStarted(int season)
        {
            SetAtLeast(AchievementKind.Seasons, season);
        }

        private void Add(AchievementKind kind, long amount)
        {
            if (!IsPlaying || amount <= 0L) return;

            long before = _counters[(int)kind];
            _counters[(int)kind] = before + amount;
            AfterCounterChanged(kind, before);
        }

        private void SetAtLeast(AchievementKind kind, long value)
        {
            if (!IsPlaying) return;

            long before = _counters[(int)kind];
            if (value <= before) return;

            _counters[(int)kind] = value;
            AfterCounterChanged(kind, before);
        }

        private void AfterCounterChanged(AchievementKind kind, long before)
        {
            long after = _counters[(int)kind];
            IReadOnlyList<AchievementData> all = _config.Achievements;
            for (int i = 0; i < all.Count; i++)
            {
                AchievementData data = all[i];
                if (data != null && data.Kind == kind && before < data.Target && after >= data.Target) Unlocked?.Invoke(data);
            }

            Changed?.Invoke();
            SaveRequested?.Invoke();
        }

        // ---------- Reclamar ----------

        // Acredita el premio del logro 'index'. Devuelve false (sin tocar nada) si no está cumplido, ya se reclamó o el juego no está en
        // marcha.
        public bool TryClaim(int index)
        {
            if (index < 0 || index >= Count || !IsPlaying) return false;
            if (!IsCompleteAt(index) || IsClaimedAt(index)) return false;

            AchievementData data = DataAt(index);
            int fertilizer = 0;
            long coins = 0L;
            if (RewardFertilizerAt(index) > 0) fertilizer = _fertilizer.Grant(data.RewardFertilizer);
            if (fertilizer <= 0) coins = CoinsFor(data);

            if (coins > 0L && _economy != null) _economy.AddCoins(coins);

            _claimed.Add(data.Id);
            Claimed?.Invoke(data, coins, fertilizer);
            Changed?.Invoke();
            SaveRequested?.Invoke();
            return true;
        }

        // ---------- Partida guardada ----------

        public string SaveId => "achievements";

        public event Action SaveRequested;

        public string Capture()
        {
            var save = new AchievementSave { counters = (long[])_counters.Clone() };
            save.claimed = new string[_claimed.Count];
            _claimed.CopyTo(save.claimed);
            return JsonUtility.ToJson(save);
        }

        // Con null, vacío o ilegible: todo en cero. Los logros que ya no existen se descartan y los contadores negativos se corrigen, sin
        // tirar excepciones. Restaurar no cuenta como jugar (no avisa Unlocked).
        public void Restore(string json)
        {
            AchievementSave save = null;
            if (!string.IsNullOrWhiteSpace(json))
            {
                try
                {
                    save = JsonUtility.FromJson<AchievementSave>(json);
                }
                catch (ArgumentException)
                {
                    save = null;
                }
            }

            Array.Clear(_counters, 0, _counters.Length);
            _claimed.Clear();

            if (save != null)
            {
                if (save.counters != null)
                {
                    for (int i = 0; i < _counters.Length && i < save.counters.Length; i++) _counters[i] = Math.Max(0L, save.counters[i]);
                }

                if (save.claimed != null)
                {
                    foreach (string id in save.claimed)
                    {
                        if (_config != null && _config.FindAchievement(id) != null) _claimed.Add(id);
                    }
                }
            }

            Changed?.Invoke();
            if (IsPlaying && _game != null) SyncLevel();
        }

        private void OnDestroy()
        {
            if (_game != null) _game.StateChanged -= OnStateChanged;
            if (_backpack != null) _backpack.Changed -= OnInventoryChanged;
            if (_economy != null) _economy.ProductSold -= OnProductSold;
            if (_fertilizer != null) _fertilizer.Used -= OnFertilizerUsed;
            if (_progression != null)
            {
                _progression.Changed -= OnProgressChanged;
                _progression.LevelChanged -= OnLevelChanged;
            }

            if (_ads != null) _ads.AdFinished -= OnAdFinished;
            if (_buffs != null) _buffs.BuffStarted -= OnBuffStarted;
            if (_missions != null) _missions.Claimed -= OnMissionClaimed;
            if (_prestige != null) _prestige.SeasonStarted -= OnSeasonStarted;
            if (Instance == this) Instance = null;
        }
    }
}
