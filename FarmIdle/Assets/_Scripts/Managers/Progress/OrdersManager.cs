using System;
using UnityEngine;

namespace FarmIdle
{
    // F-18: pedidos del pueblo. Dueño del cartel (OrderBoard, lógica pura) y de lo que lo conecta con el juego:
    //   - Aparece al comprar GameConfig.OrdersMinZones zonas. Con el cartel activo, cada lugar vacío recibe un pedido nuevo cuando
    //     termina su espera: un cultivo de las zonas ya abiertas (peso por precio) y una cantidad según la capacidad de la mochila.
    //   - Se entrega a mano: TryDeliver saca de la mochila los productos del cultivo pedido (sin pasar por la venta) y suma al pedido;
    //     se puede en varios viajes. Al completarse paga monedas = unidades x precio final x (1 + bonus) y, el pedido grande, 1 semilla
    //     dorada (hasta GameConfig.OrderSeedsPerSeasonCap por temporada). Después el lugar espera y sale otro pedido.
    //   - Una temporada nueva (prestigio) vacía el cartel y el contador de semillas.
    //   - Partida guardada: ISaveable "orders". Una partida sin la sección arranca con el cartel vacío.
    //   - La UI y el mundo solo leen y escuchan Changed / Delivered / Completed; este servicio no los conoce.
    //   - Números PROVISORIOS: se calibran con el bot de balance y datos reales (B-05).
    public sealed class OrdersManager : MonoBehaviour, ISaveable
    {
        public static OrdersManager Instance { get; private set; }

        private const int SaveVersion = 1;

        [Serializable]
        private sealed class OrdersSave
        {
            public int version = SaveVersion;
            public string[] cropIds = new string[0];
            public int[] quantities = new int[0];
            public int[] delivered = new int[0];
            public float[] cooldowns = new float[0];
            public int seedsThisSeason;
        }

        private GameConfig _config;
        private readonly OrderBoard _board = new OrderBoard();
        private PrestigeManager _prestige;
        private int[] _weights = new int[8];
        private int[] _candidates = new int[8];

        // Para pruebas: el azar de la elección del cultivo (devuelve un entero en [0, max)).
        public Func<int, int> Roll { get; set; } = max => UnityEngine.Random.Range(0, max);

        public int SlotCount => OrderBoard.SlotCount;
        public bool IsBig(int slot) => OrderBoard.IsBig(slot);
        public bool IsActive(int slot) => _board[slot].IsActive;
        public int QuantityOf(int slot) => _board[slot].Quantity;
        public int DeliveredOf(int slot) => _board[slot].Delivered;
        public float CooldownOf(int slot) => _board[slot].Cooldown;
        public int SeedsThisSeason => _board.SeedsThisSeason;
        public int SeedsLeft => _config != null ? Mathf.Max(0, _config.OrderSeedsPerSeasonCap - _board.SeedsThisSeason) : 0;

        // Zonas que hay que comprar para que el cartel abra (para el texto de "cerrado").
        public int MinZones => _config != null ? _config.OrdersMinZones : 0;

        // El cultivo pedido en ese lugar, o null si está vacío (o si el cultivo ya no existe).
        public CropData CropOf(int slot)
        {
            return _config != null && _board[slot].IsActive ? _config.FindCrop(_board[slot].CropId) : null;
        }

        // Monedas que pagaría completar ese pedido ahora (con los precios de hoy).
        public long RewardCoins(int slot)
        {
            CropData crop = CropOf(slot);
            EconomyManager economy = EconomyManager.Instance;
            if (crop == null || economy == null) return 0L;

            float bonus = OrderBoard.IsBig(slot) ? _config.OrderBigBonus : _config.OrderBonus;
            return (long)Math.Round(_board[slot].Quantity * (double)economy.Pricing.FinalPrice(crop.SellPrice, crop.Id) * (1d + bonus));
        }

        // Semillas que daría ese pedido al completarse.
        public int RewardSeeds(int slot)
        {
            return OrderBoard.IsBig(slot) && SeedsLeft > 0 ? 1 : 0;
        }

        // true si el cartel ya está disponible (zonas compradas suficientes).
        public bool IsUnlocked
        {
            get
            {
                if (_config == null) return false;
                if (_config.OrdersMinZones <= 0) return true;

                ProgressionManager progression = ProgressionManager.Instance;
                return progression != null && progression.UnlockedZoneCount >= _config.OrdersMinZones;
            }
        }

        // Cambió algo del cartel (pedido nuevo, entrega, cobro, partida cargada).
        public event Action Changed;

        // Se entregaron unidades a un lugar (lugar, unidades).
        public event Action<int, int> Delivered;

        // Se completó un pedido ya pagado (lugar, monedas, semillas).
        public event Action<int, long, int> Completed;

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
            _board.Clear();
            Changed?.Invoke();
            SaveRequested?.Invoke();
        }

        private void Update()
        {
            GameManager game = GameManager.Instance;
            if (game == null || !game.IsPlaying) return;

            Tick(Time.deltaTime);
        }

        // Pasa 'dt' segundos de juego: corre las esperas y pone pedidos en los lugares que toca. Sin asignaciones cuando no hay nada
        // que poner.
        public void Tick(float dt)
        {
            if (_config == null || !IsUnlocked) return;

            _board.Tick(dt);

            for (int i = 0; i < OrderBoard.SlotCount; i++)
            {
                if (_board.NeedsFill(i)) TryFill(i);
            }
        }

        private bool TryFill(int slot)
        {
            var rules = _config.OrderCrops;
            if (rules == null || rules.Count == 0) return false;

            if (_weights.Length < rules.Count)
            {
                _weights = new int[rules.Count];
                _candidates = new int[rules.Count];
            }

            // Solo los cultivos cuya zona ya está abierta; _candidates guarda la posición en la lista de reglas.
            int count = 0;
            for (int i = 0; i < rules.Count; i++)
            {
                OrderCropRule rule = rules[i];
                if (rule == null || _config.FindCrop(rule.CropId) == null || !IsZoneOpen(rule.ZoneId)) continue;

                _candidates[count] = i;
                _weights[count] = rule.Weight;
                count++;
            }

            int total = OrderBoard.WeightTotal(_weights, count);
            if (total <= 0) return false;

            int pick = OrderBoard.PickIndex(_weights, count, Roll(total));
            if (pick < 0) return false;

            BackpackManager backpack = BackpackManager.Instance;
            int capacity = backpack != null ? backpack.Backpack.Capacity : _config.BackpackCapacity;
            bool big = OrderBoard.IsBig(slot);
            int quantity = OrderBoard.QuantityFor(capacity, big, _config.OrderQuantityShare, _config.OrderMinQuantity, _config.OrderMaxQuantity, _config.OrderBigFactor, _config.OrderBigMaxQuantity);

            _board.Fill(slot, rules[_candidates[pick]].CropId, quantity);
            Changed?.Invoke();
            SaveRequested?.Invoke();
            return true;
        }

        private bool IsZoneOpen(string zoneId)
        {
            if (string.IsNullOrEmpty(zoneId)) return true;

            ProgressionManager progression = ProgressionManager.Instance;
            if (progression == null) return false;

            var zones = _config.Zones;
            for (int i = 0; i < zones.Count; i++)
            {
                if (zones[i] != null && zones[i].Id == zoneId) return progression.IsZoneUnlocked(zones[i]);
            }

            return false;
        }

        // true si se puede entregar ahora a ese lugar: hay pedido, la mochila lleva algo de ese cultivo y el juego está en Playing.
        public bool CanDeliver(int slot)
        {
            if (slot < 0 || slot >= OrderBoard.SlotCount || !_board[slot].IsActive) return false;

            GameManager game = GameManager.Instance;
            if (game != null && !game.IsPlaying) return false;

            CropData crop = CropOf(slot);
            BackpackManager backpack = BackpackManager.Instance;
            return crop != null && backpack != null && backpack.Backpack.CountOf(crop) > 0;
        }

        // Entrega de la mochila todo lo que se pueda de ese pedido (hasta lo que falta). Devuelve cuántas unidades entregó.
        public int TryDeliver(int slot)
        {
            if (!CanDeliver(slot)) return 0;

            CropData crop = CropOf(slot);
            BackpackManager backpack = BackpackManager.Instance;
            int wanted = Math.Min(_board[slot].Remaining, backpack.Backpack.CountOf(crop));

            int taken = backpack.TryTake(crop, wanted);
            if (taken <= 0) return 0;

            int accepted = _board.Deliver(slot, taken);
            Delivered?.Invoke(slot, accepted);

            if (_board.IsComplete(slot)) Pay(slot);

            Changed?.Invoke();
            SaveRequested?.Invoke();
            return accepted;
        }

        private void Pay(int slot)
        {
            long coins = RewardCoins(slot);
            int seeds = RewardSeeds(slot);

            EconomyManager economy = EconomyManager.Instance;
            if (economy != null && coins > 0L) economy.AddCoins(coins);

            if (seeds > 0)
            {
                _board.SeedsThisSeason += seeds;
                if (_prestige != null) _prestige.GrantSeeds(seeds);
            }

            _board.Complete(slot, OrderBoard.IsBig(slot) ? _config.OrderBigRefillSeconds : _config.OrderRefillSeconds);
            Completed?.Invoke(slot, coins, seeds);
        }

        // ---------- Partida guardada ----------

        public string SaveId => "orders";

        public event Action SaveRequested;

        public string Capture()
        {
            var save = new OrdersSave
            {
                cropIds = new string[OrderBoard.SlotCount],
                quantities = new int[OrderBoard.SlotCount],
                delivered = new int[OrderBoard.SlotCount],
                cooldowns = new float[OrderBoard.SlotCount],
                seedsThisSeason = _board.SeedsThisSeason
            };

            for (int i = 0; i < OrderBoard.SlotCount; i++)
            {
                OrderBoard.Slot slot = _board[i];
                save.cropIds[i] = slot.CropId ?? "";
                save.quantities[i] = slot.Quantity;
                save.delivered[i] = slot.Delivered;
                save.cooldowns[i] = slot.Cooldown;
            }

            return JsonUtility.ToJson(save);
        }

        // Con null, vacío o ilegible queda el cartel vacío. Un pedido de un cultivo que ya no existe se descarta; las cantidades se
        // acotan. No cobra nada ni pide guardar.
        public void Restore(string json)
        {
            OrdersSave save = null;
            if (!string.IsNullOrWhiteSpace(json))
            {
                try
                {
                    save = JsonUtility.FromJson<OrdersSave>(json);
                }
                catch (ArgumentException)
                {
                    save = null;
                }
            }

            _board.Clear();
            if (save != null)
            {
                _board.SeedsThisSeason = Mathf.Max(0, save.seedsThisSeason);

                for (int i = 0; i < OrderBoard.SlotCount; i++)
                {
                    string id = save.cropIds != null && i < save.cropIds.Length ? save.cropIds[i] : "";
                    int quantity = save.quantities != null && i < save.quantities.Length ? save.quantities[i] : 0;
                    int delivered = save.delivered != null && i < save.delivered.Length ? save.delivered[i] : 0;
                    float cooldown = save.cooldowns != null && i < save.cooldowns.Length ? save.cooldowns[i] : 0f;

                    bool known = !string.IsNullOrEmpty(id) && _config != null && _config.FindCrop(id) != null;
                    _board.Set(i, known ? id : null, known ? quantity : 0, delivered, cooldown);
                }
            }

            Changed?.Invoke();
        }

        private void OnDestroy()
        {
            if (_prestige != null) _prestige.SeasonStarted -= OnSeasonStarted;
            if (Instance == this) Instance = null;
        }
    }
}
