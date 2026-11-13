using System;
using System.Collections.Generic;
using UnityEngine;

namespace FarmIdle
{
    // Dueño de las misiones diarias (F-05): cada día le toca al jugador un puñado de metas cortas (cosechá 25, vendé 30...) con un
    // premio que se reclama a mano.
    //   - Las del día salen de las plantillas del GameConfig con un sorteo que depende solo de la fecha (MissionRules.Pick): mismas
    //     misiones el mismo día, también si se reinstala el juego.
    //   - El avance se cuenta escuchando a los sistemas que ya existen (la mochila, las ventas, el fertilizante, las compras): ninguno
    //     sabe que esto existe. Solo cuenta jugando (estado Playing); restaurar una partida no suma nada.
    //   - Reloj local: el día cambia por la fecha del aparato. Si la hora retrocede NO se regenera nada (se conservan las de hoy), así
    //     atrasar el reloj no da misiones nuevas; adelantarla sí las da, pero los premios son chicos (R-08).
    //   - Premio: monedas = unos minutos de lo que producen hoy las parcelas (OfflineManager.ProductionPerSecond), o fertilizante; con el
    //     stock lleno cae a las monedas. Se reclama con TryClaim (solo en Playing).
    //   - Partida guardada: ISaveable "missions" con el día, las misiones del día, su avance y cuáles se reclamaron. Una partida anterior a
    //     F-05 no la trae y las misiones se sortean al empezar a jugar.
    public sealed class MissionManager : MonoBehaviour, ISaveable
    {
        public static MissionManager Instance { get; private set; }

        private const int SaveVersion = 1;
        private const float DayCheckSeconds = 30f;

        [Serializable]
        private sealed class MissionEntry
        {
            public string id;
            public int progress;
            public bool claimed;
        }

        [Serializable]
        private sealed class MissionSave
        {
            public int version = SaveVersion;
            public int day;
            public MissionEntry[] missions = new MissionEntry[0];
        }

        private sealed class Slot
        {
            public MissionData Data;
            public int Progress;
            public bool Claimed;
            public bool IsComplete => Progress >= Data.Target;
        }

        private GameConfig _config;
        private GameManager _game;
        private IReadOnlyInventory _backpack;
        private EconomyManager _economy;
        private FertilizerManager _fertilizer;
        private ProgressionManager _progression;
        private readonly List<Slot> _slots = new List<Slot>(5);
        private int _day;
        private float _nextDayCheck;

        // La fecha local de hoy; las pruebas la reemplazan.
        public Func<DateTime> Clock { get; set; } = () => DateTime.Now;

        public int Count => _slots.Count;
        public int Day => _day;

        // Misiones completas con el premio todavía sin reclamar (el aviso del botón del HUD).
        public int ClaimableCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < _slots.Count; i++)
                {
                    if (_slots[i].IsComplete && !_slots[i].Claimed) count++;
                }

                return count;
            }
        }

        // Cambió algo que se ve (avance, reclamo, misiones nuevas, partida cargada).
        public event Action Changed;

        // Una misión llegó a su meta (una sola vez por misión y día).
        public event Action<MissionData> Completed;

        // Se reclamó el premio: la misión, las monedas y el fertilizante que se acreditaron.
        public event Action<MissionData, long, int> Claimed;

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
            if (_progression != null) _progression.Changed += OnProgressChanged;

            Instance = this;
        }

        // ---------- Consultas ----------

        public MissionData DataAt(int index) { return _slots[index].Data; }
        public int ProgressAt(int index) { return Math.Min(_slots[index].Progress, _slots[index].Data.Target); }
        public bool IsCompleteAt(int index) { return _slots[index].IsComplete; }
        public bool IsClaimedAt(int index) { return _slots[index].Claimed; }

        // El premio que se acreditaría ahora si se reclamara: fertilizante si la misión lo da y hay lugar, si no monedas.
        public int RewardFertilizerAt(int index)
        {
            MissionData data = _slots[index].Data;
            return data.RewardFertilizer > 0 && _fertilizer != null && !_fertilizer.IsFull ? data.RewardFertilizer : 0;
        }

        public long RewardCoinsAt(int index)
        {
            return RewardFertilizerAt(index) > 0 ? 0L : CoinsFor(_slots[index].Data);
        }

        private long CoinsFor(MissionData data)
        {
            OfflineManager offline = OfflineManager.Instance;
            double rate = offline != null ? offline.ProductionPerSecond : 0d;
            return MissionRules.Coins(rate, data.RewardMinutes * _config.MissionRewardScale, _config.MissionMinCoins);
        }

        // ---------- El día ----------

        // Sortea las misiones de hoy si es un día nuevo (o todavía no hay ninguna). Con la hora atrasada o el mismo día no hace nada.
        public void EnsureToday()
        {
            int today = MissionRules.DayKey(Clock());
            if (_slots.Count > 0 && today <= _day) return;

            int level = _progression != null ? _progression.Level : 1;
            List<MissionData> picked = MissionRules.Pick(_config.Missions, level, today, _config.MissionsPerDay);

            _slots.Clear();
            foreach (MissionData data in picked) _slots.Add(new Slot { Data = data });
            _day = today;

            Changed?.Invoke();
            SaveRequested?.Invoke();
        }

        private void OnStateChanged(GameState previous, GameState current)
        {
            if (current == GameState.Playing) EnsureToday();
        }

        // La app volvió de segundo plano (puede haber pasado la medianoche).
        public void OnApplicationPause(bool paused)
        {
            if (!paused && _config != null && IsPlaying) EnsureToday();
        }

        // Con el juego abierto de un día para el otro: se mira la fecha cada tanto (sin asignar memoria).
        private void Update()
        {
            if (_config == null || Time.unscaledTime < _nextDayCheck) return;

            _nextDayCheck = Time.unscaledTime + DayCheckSeconds;
            if (IsPlaying) EnsureToday();
        }

        private bool IsPlaying => _game == null || _game.IsPlaying;

        // ---------- Avance ----------

        private void OnInventoryChanged(InventoryChange change)
        {
            if (change.Added && !change.IsReplacement) Add(MissionKind.Harvest, 1);
        }

        private void OnProductSold(CropData crop, long coins)
        {
            Add(MissionKind.Sell, 1);
        }

        private void OnFertilizerUsed(int grown)
        {
            Add(MissionKind.UseFertilizer, 1);
        }

        private void OnProgressChanged(ProgressChange change)
        {
            if (change.Kind == ProgressChangeKind.ZoneUnlocked || change.Kind == ProgressChangeKind.UpgradeLevelChanged) Add(MissionKind.Buy, 1);
        }

        private void Add(MissionKind kind, int amount)
        {
            if (!IsPlaying) return;

            bool changed = false;
            for (int i = 0; i < _slots.Count; i++)
            {
                Slot slot = _slots[i];
                if (slot.Data.Kind != kind || slot.IsComplete) continue;

                slot.Progress = Math.Min(slot.Data.Target, slot.Progress + amount);
                changed = true;
                if (slot.IsComplete) Completed?.Invoke(slot.Data);
            }

            if (!changed) return;

            Changed?.Invoke();
            SaveRequested?.Invoke();
        }

        // ---------- Reclamar ----------

        // Acredita el premio de la misión 'index'. Devuelve false (sin tocar nada) si no está completa, ya se reclamó o el juego no está en
        // marcha.
        public bool TryClaim(int index)
        {
            if (index < 0 || index >= _slots.Count || !IsPlaying) return false;

            Slot slot = _slots[index];
            if (!slot.IsComplete || slot.Claimed) return false;

            int fertilizer = 0;
            long coins = 0L;
            if (RewardFertilizerAt(index) > 0) fertilizer = _fertilizer.Grant(slot.Data.RewardFertilizer);
            if (fertilizer <= 0) coins = CoinsFor(slot.Data);

            if (coins > 0L && _economy != null) _economy.AddCoins(coins);

            slot.Claimed = true;
            Claimed?.Invoke(slot.Data, coins, fertilizer);
            Changed?.Invoke();
            SaveRequested?.Invoke();
            return true;
        }

        // ---------- Partida guardada ----------

        public string SaveId => "missions";

        public event Action SaveRequested;

        public string Capture()
        {
            var save = new MissionSave { day = _day, missions = new MissionEntry[_slots.Count] };
            for (int i = 0; i < _slots.Count; i++)
            {
                save.missions[i] = new MissionEntry { id = _slots[i].Data.Id, progress = _slots[i].Progress, claimed = _slots[i].Claimed };
            }

            return JsonUtility.ToJson(save);
        }

        // Con null, vacío o ilegible: sin misiones (se sortean al empezar a jugar). Las que ya no existen se descartan y el avance se acota
        // a la meta, sin tirar excepciones. Restaurar no cuenta como jugar.
        public void Restore(string json)
        {
            MissionSave save = null;
            if (!string.IsNullOrWhiteSpace(json))
            {
                try
                {
                    save = JsonUtility.FromJson<MissionSave>(json);
                }
                catch (ArgumentException)
                {
                    save = null;
                }
            }

            _slots.Clear();
            _day = 0;

            if (save != null && save.missions != null)
            {
                foreach (MissionEntry entry in save.missions)
                {
                    MissionData data = entry != null ? _config.FindMission(entry.id) : null;
                    if (data == null) continue;

                    _slots.Add(new Slot { Data = data, Progress = Mathf.Clamp(entry.progress, 0, data.Target), Claimed = entry.claimed });
                }

                if (_slots.Count > 0) _day = Math.Max(0, save.day);
            }

            Changed?.Invoke();
            if (IsPlaying && _game != null) EnsureToday();
        }

        private void OnDestroy()
        {
            if (_game != null) _game.StateChanged -= OnStateChanged;
            if (_backpack != null) _backpack.Changed -= OnInventoryChanged;
            if (_economy != null) _economy.ProductSold -= OnProductSold;
            if (_fertilizer != null) _fertilizer.Used -= OnFertilizerUsed;
            if (_progression != null) _progression.Changed -= OnProgressChanged;
            if (Instance == this) Instance = null;
        }
    }
}
