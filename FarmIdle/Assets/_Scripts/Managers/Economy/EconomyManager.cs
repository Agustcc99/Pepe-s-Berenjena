using System;
using UnityEngine;

namespace FarmIdle
{
    // Dueño de la economía: tiene la billetera y es la única puerta para ganar o gastar monedas.
    // La UI y el gameplay ven la billetera como IReadOnlyWallet (saldo + evento) y piden los cambios acá.
    // La venta también pasa por acá: SellProduct pone el precio final (PriceCalculator: base x mejoras x buffs) en la
    // billetera. Es el único punto que gana monedas por una venta; la zona de venta solo saca el producto de la mochila
    // y se lo entrega.
    // Se guarda en la partida (ISaveable "economy"): solo el saldo. Lo vendido de la sesión no se guarda.
    public sealed class EconomyManager : MonoBehaviour, ISaveable
    {
        public static EconomyManager Instance { get; private set; }

        private const int SaveVersion = 1;

        [Serializable]
        private sealed class EconomySave
        {
            public int version = SaveVersion;
            public long coins;
        }

        private GameConfig _config;
        private Wallet _wallet;
        private PriceCalculator _pricing;

        // Vista de solo lectura: saldo y evento BalanceChanged.
        public IReadOnlyWallet Wallet => _wallet;

        public long Balance => _wallet.Balance;

        // El cálculo del precio final. Las mejoras (Fase 6) y los buffs de anuncio (Fase 8) fijan sus multiplicadores acá.
        public PriceCalculator Pricing => _pricing;

        // Lo vendido en esta sesión (no se guarda: la partida solo lleva el saldo y el progreso).
        public int SoldUnits { get; private set; }
        public long SoldCoins { get; private set; }

        // Se dispara después de cada venta, con el cultivo y las monedas que pagó (la billetera ya las tiene).
        public event Action<CropData, long> ProductSold;

        // Lo llama el Bootstrapper; hasta entonces Instance es null.
        public void Initialize(GameConfig config)
        {
            _config = config;
            _wallet = new Wallet(config.StartingCoins);
            _pricing = new PriceCalculator();
            _wallet.BalanceChanged += OnBalanceChanged;
            Instance = this;
        }

        // ---------- Partida guardada ----------

        public string SaveId => "economy";

        public event Action SaveRequested;

        private void OnBalanceChanged(long balance, long delta)
        {
            SaveRequested?.Invoke();
        }

        public string Capture()
        {
            return JsonUtility.ToJson(new EconomySave { coins = _wallet.Balance });
        }

        // Con null o vacío vuelve a las monedas iniciales de una partida nueva (y borra lo vendido de la sesión). Los datos
        // dañados no tiran excepciones: un saldo ilegible se toma como partida nueva.
        public void Restore(string json)
        {
            SoldUnits = 0;
            SoldCoins = 0;

            EconomySave save = null;
            if (!string.IsNullOrWhiteSpace(json))
            {
                try
                {
                    save = JsonUtility.FromJson<EconomySave>(json);
                }
                catch (ArgumentException)
                {
                    save = null;
                }
            }

            _wallet.Restore(save != null ? save.coins : _config.StartingCoins);
        }

        // Nueva temporada (F-03): la billetera vuelve a las monedas iniciales. Lo vendido de la sesión no se toca.
        public void ResetCoins(long bonus = 0L)
        {
            _wallet.Restore(_config.StartingCoins + System.Math.Max(0L, bonus));   // 'bonus': la mejora de monedas iniciales (F-11)
        }

        public void AddCoins(long amount)
        {
            _wallet.Add(amount);
        }

        public bool TrySpend(long amount)
        {
            return _wallet.TrySpend(amount);
        }

        public bool CanAfford(long amount)
        {
            return _wallet.CanAfford(amount);
        }

        // Paga una unidad de 'crop' que ya salió de la mochila: calcula el precio final, lo suma a la billetera en el
        // mismo instante y devuelve cuántas monedas fueron.
        public long SellProduct(CropData crop)
        {
            if (crop == null) throw new ArgumentNullException(nameof(crop));

            long coins = _pricing.FinalPrice(crop.SellPrice);
            _wallet.Add(coins);

            SoldUnits++;
            SoldCoins += coins;
            ProductSold?.Invoke(crop, coins);
            return coins;
        }

        // J-04: azar de la cosecha rara (0 a 1). Las pruebas lo reemplazan por uno fijo.
        public Func<float> RareRoll { get; set; } = () => UnityEngine.Random.value;

        // J-04: al cosechar, sortea si salió rara. Si sí, acredita el premio (multiplicador x precio final) y devuelve las
        // monedas; si no, devuelve 0. No cuenta como venta (ni XP). Sin asignaciones.
        public long TryRareHarvest(CropData crop)
        {
            if (crop == null || _config == null) return 0L;

            float chance = _config.RareHarvestChance;
            if (chance <= 0f || RareRoll() >= chance) return 0L;

            long bonus = (long)Math.Round(_pricing.FinalPrice(crop.SellPrice) * (double)_config.RareHarvestMultiplier);
            if (bonus <= 0L) return 0L;

            _wallet.Add(bonus);
            return bonus;
        }

        private void OnDestroy()
        {
            if (_wallet != null) _wallet.BalanceChanged -= OnBalanceChanged;
            if (Instance == this) Instance = null;
        }
    }
}
