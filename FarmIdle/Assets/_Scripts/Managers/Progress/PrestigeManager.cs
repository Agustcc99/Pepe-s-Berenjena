using System;
using UnityEngine;

namespace FarmIdle
{
    // Dueño del prestigio ("Nueva temporada", F-03): el reinicio voluntario que da un multiplicador PERMANENTE al precio de venta.
    //   - Cuándo: con todo comprado (todas las zonas abiertas y todas las mejoras al máximo: ProgressionManager.IsEverythingBought).
    //     Es el punto en que la temporada ya no tiene más metas; empezar otra con más ganancia estira la vida del juego.
    //   - Qué hace: suma una temporada, y devuelve a una partida nueva las monedas, las zonas, las mejoras y la mochila. Se conservan la
    //     XP (el nivel) con los perks elegidos, el fertilizante, los buffs, los ajustes y la guía, y las temporadas ya hechas.
    //   - Cuánto: multiplicador = 1 + paso x temporadas (GameConfig.PrestigeStep; lineal, más fácil de balancear que uno compuesto).
    //     Es un tercer multiplicador del PriceCalculator, aparte de mejoras y buffs; no cambia la XP, que sale del precio base.
    //   - Partida guardada: ISaveable "prestige" con las temporadas. Una partida anterior a F-03 no la trae y arranca en 0 (x1).
    //   - Los demás sistemas no lo conocen: la UI lo consulta y escucha Changed, y la analítica escucha SeasonStarted.
    public sealed class PrestigeManager : MonoBehaviour, ISaveable
    {
        public static PrestigeManager Instance { get; private set; }

        private const int SaveVersion = 2;

        [Serializable]
        private sealed class PrestigeSave
        {
            public int version = SaveVersion;
            public int seasons;
            public int seeds;           // v2 (F-11): semillas doradas sin gastar
            public string[] shopIds;    // v2: id de cada mejora comprada...
            public int[] shopLevels;    // ...y su nivel (mismo largo)
        }

        private GameConfig _config;
        private int _seasons;
        private int _seeds;
        private int[] _levels = new int[0];   // nivel de cada mejora de GameConfig.PrestigeShop, por posición

        // ---------- Tienda de prestigio (F-11) ----------
        // Cada «Nueva temporada» da semillas doradas (GameConfig.SeedsForSeason); se gastan en mejoras permanentes que sobreviven a los
        // reinicios. Los demás sistemas consultan los multiplicadores de abajo; nadie más conoce el catálogo.

        public int Seeds => _seeds;
        public int NextSeeds => _config != null ? _config.SeedsForSeason(_seasons + 1) : 0;   // lo que daría cerrar la temporada actual
        public int ShopCount => _config != null ? _config.PrestigeShop.Count : 0;
        public PrestigeShopItem ShopItem(int index) => _config.PrestigeShop[index];
        public int ShopLevel(int index) => index >= 0 && index < _levels.Length ? _levels[index] : 0;

        // Costo del próximo nivel de la mejora 'index', o 0 si ya está al máximo.
        public int ShopNextCost(int index)
        {
            PrestigeShopItem item = _config.PrestigeShop[index];
            int level = ShopLevel(index);
            return level >= item.MaxLevel ? 0 : item.CostOf(level);
        }

        public bool CanBuyShop(int index)
        {
            if (_config == null || index < 0 || index >= ShopCount) return false;

            int cost = ShopNextCost(index);
            if (cost <= 0 || _seeds < cost) return false;

            GameManager game = GameManager.Instance;
            return game == null || game.IsPlaying;
        }

        public bool TryBuyShop(int index)
        {
            if (!CanBuyShop(index)) return false;

            _seeds -= ShopNextCost(index);
            _levels[index]++;
            ApplyMultiplier();
            Changed?.Invoke();
            ShopBought?.Invoke(index);
            SaveRequested?.Invoke();
            return true;
        }

        // N-06: se compró un nivel de la mejora 'index' de la tienda, jugando. Nunca al restaurar una partida.
        public event Action<int> ShopBought;

        // F-18: semillas doradas extra (el premio de un pedido grande). Suma, avisa y pide guardar.
        public void GrantSeeds(int amount)
        {
            if (amount <= 0) return;

            _seeds += amount;
            Changed?.Invoke();
            SaveRequested?.Invoke();
        }

        // Valor total de las mejoras de un tipo (si hubiera dos del mismo tipo, rige la última; el catálogo trae una por tipo).
        private float ShopValue(PrestigeShopKind kind)
        {
            float value = PrestigeShopItem.Neutral(kind);
            if (_config == null) return value;

            for (int i = 0; i < _config.PrestigeShop.Count; i++)
            {
                if (_config.PrestigeShop[i].Kind == kind) value = _config.PrestigeShop[i].ValueAt(ShopLevel(i));
            }

            return value;
        }

        public long StartCoinsBonus => (long)ShopValue(PrestigeShopKind.StartCoins);
        public int StartFertilizerBonus => (int)ShopValue(PrestigeShopKind.StartFertilizer);
        public float OfflineCapBonusSeconds => ShopValue(PrestigeShopKind.OfflineCap);
        public float XpMultiplier => ShopValue(PrestigeShopKind.XpGain);
        public float GrowthMultiplier => ShopValue(PrestigeShopKind.GrowthSpeed);
        private float SellShopMultiplier => ShopValue(PrestigeShopKind.SellPrice);

        // Para los consumidores que no quieren chequear null: sin PrestigeManager valen lo neutro.
        public static float GrowthFactor => Instance != null ? Instance.GrowthMultiplier : 1f;
        public static float XpFactor => Instance != null ? Instance.XpMultiplier : 1f;
        public static float OfflineCapBonus => Instance != null ? Instance.OfflineCapBonusSeconds : 0f;

        // Temporadas completadas (0 en una partida nueva).
        public int Seasons => _seasons;

        // El multiplicador de precio que rige ahora y el que se tendría al cerrar la temporada actual.
        public float Multiplier => MultiplierFor(_seasons, _config != null ? _config.PrestigeStep : 0f) * SellShopMultiplier;
        public float NextMultiplier => MultiplierFor(_seasons + 1, _config != null ? _config.PrestigeStep : 0f) * SellShopMultiplier;

        // Cambió el número de temporadas (se empezó una o se cargó la partida).
        public event Action Changed;

        // Se empezó una temporada jugando, con su número. Nunca al restaurar una partida.
        public event Action<int> SeasonStarted;

        // 1 + paso x temporadas. Es C# puro: se prueba sin escena.
        public static float MultiplierFor(int seasons, float step)
        {
            return 1f + Mathf.Max(0f, step) * Mathf.Max(0, seasons);
        }

        // Lo llama el Bootstrapper, después de la economía, la mochila y la progresión; hasta entonces Instance es null.
        public void Initialize(GameConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _levels = new int[config.PrestigeShop.Count];
            Instance = this;
            ApplyMultiplier();
        }

        // true si no queda nada por comprar en esta temporada (la UI muestra el botón; no mira el estado del juego).
        public bool IsReady
        {
            get
            {
                ProgressionManager progression = ProgressionManager.Instance;
                return progression != null && progression.IsEverythingBought;
            }
        }

        // true si se puede empezar una temporada nueva ahora: está lista y el juego está en Playing.
        public bool CanStartSeason
        {
            get
            {
                if (!IsReady) return false;

                GameManager game = GameManager.Instance;
                return game == null || game.IsPlaying;
            }
        }

        // Cierra la temporada: reinicia monedas, zonas, mejoras y mochila, y sube el multiplicador. Devuelve false (sin tocar nada)
        // si todavía no se puede.
        public bool TryStartSeason()
        {
            if (!CanStartSeason) return false;

            _seasons++;
            _seeds += _config.SeedsForSeason(_seasons);
            ApplyMultiplier();

            EconomyManager economy = EconomyManager.Instance;
            if (economy != null) economy.ResetCoins(StartCoinsBonus);

            FertilizerManager fertilizer = FertilizerManager.Instance;
            if (fertilizer != null && StartFertilizerBonus > 0) fertilizer.Grant(StartFertilizerBonus);

            ProgressionManager.Instance.ResetSeason();

            BackpackManager backpack = BackpackManager.Instance;
            if (backpack != null) backpack.Restore(null);

            Changed?.Invoke();
            SeasonStarted?.Invoke(_seasons);
            SaveRequested?.Invoke();

            // Un reinicio es un cambio grande: se escribe de una, no a los pocos segundos (cerrar la app justo después no lo pierde).
            SaveManager save = SaveManager.Instance;
            if (save != null) save.SaveNow(force: true);
            return true;
        }

        private void ApplyMultiplier()
        {
            EconomyManager economy = EconomyManager.Instance;
            if (economy != null) economy.Pricing.SetPrestigeMultiplier(Multiplier);
        }

        // ---------- Partida guardada ----------

        public string SaveId => "prestige";

        public event Action SaveRequested;

        public string Capture()
        {
            var ids = new string[_levels.Length];
            for (int i = 0; i < ids.Length; i++) ids[i] = _config != null ? _config.PrestigeShop[i].Id : "";
            return JsonUtility.ToJson(new PrestigeSave { seasons = _seasons, seeds = _seeds, shopIds = ids, shopLevels = (int[])_levels.Clone() });
        }

        // Con null, vacío o ilegible vuelve a 0 temporadas. No pide guardar ni dispara SeasonStarted.
        public void Restore(string json)
        {
            PrestigeSave save = null;
            if (!string.IsNullOrWhiteSpace(json))
            {
                try
                {
                    save = JsonUtility.FromJson<PrestigeSave>(json);
                }
                catch (ArgumentException)
                {
                    save = null;
                }
            }

            _seasons = save != null ? Mathf.Max(0, save.seasons) : 0;
            _seeds = save != null ? Mathf.Max(0, save.seeds) : 0;   // una partida v1 no trae semillas ni mejoras

            // Los niveles se buscan por id (reordenar el catálogo no rompe partidas) y se recortan al máximo actual.
            _levels = new int[ShopCount];
            if (save != null && save.shopIds != null && save.shopLevels != null)
            {
                for (int i = 0; i < _levels.Length; i++)
                {
                    for (int j = 0; j < save.shopIds.Length && j < save.shopLevels.Length; j++)
                    {
                        if (save.shopIds[j] == _config.PrestigeShop[i].Id)
                        {
                            _levels[i] = Mathf.Clamp(save.shopLevels[j], 0, _config.PrestigeShop[i].MaxLevel);
                            break;
                        }
                    }
                }
            }

            ApplyMultiplier();
            Changed?.Invoke();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
