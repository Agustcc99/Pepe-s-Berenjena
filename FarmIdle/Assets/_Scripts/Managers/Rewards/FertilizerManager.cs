using System;
using System.Collections.Generic;
using UnityEngine;

namespace FarmIdle
{
    // Dueño del fertilizante: un consumible que hace crecer de golpe TODAS las plantas que están creciendo. Es la única puerta:
    //   - Conseguirlo: TryBuy (monedas, por EconomyManager.TrySpend) o Grant (el video, lo llama AdsManager). Los dos suman al stock,
    //     que es guardado y tiene tope (GameConfig.FertilizerMaxStock): se acumula para usarlo cuando conviene, no se pierde por
    //     estar mal parado cuando termina el video. La compra con monedas está limitada a propósito (tope BuyMaxStock, enfriamiento
    //     y precio alto): el video es la fuente principal, así comprar+usar en loop no rompe la economía.
    //   - Usarlo: TryUse gasta UNA unidad y termina el crecimiento de todos los campos registrados. Solo en Playing, y no gasta
    //     nada si no hay una sola planta creciendo (no se desperdicia).
    //   - Managers no conoce a Environment: los campos (PlotField) se registran acá como IFertilizable (Core) al activarse y se dan de
    //     baja al apagarse. Una zona cerrada tiene el campo apagado, así que no está registrado y el fertilizante no la toca.
    //   - Partida guardada: ISaveable "fertilizer" con el stock. Restaurar no gasta ni cobra nada.
    public sealed class FertilizerManager : MonoBehaviour, ISaveable
    {
        public static FertilizerManager Instance { get; private set; }

        private const int SaveVersion = 1;

        [Serializable]
        private sealed class FertilizerSave
        {
            public int version = SaveVersion;
            public int stock;
        }

        private GameConfig _config;
        private FertilizerStock _stock;
        private readonly List<IFertilizable> _targets = new List<IFertilizable>(4);
        private float _buyReadyAt;

        public int Stock => _stock.Count;
        public int MaxStock => _stock.Max;
        public bool IsFull => _stock.IsFull;
        // B-08: el precio sube con las zonas abiertas (GameConfig.FertilizerPriceFor); sin ProgressionManager cuenta 0 zonas.
        public long Price
        {
            get
            {
                if (_config == null) return 0L;

                ProgressionManager progression = ProgressionManager.Instance;
                return _config.FertilizerPriceFor(progression != null ? progression.UnlockedZoneCount : 0);
            }
        }

        // Plantas que están creciendo ahora en los campos abiertos.
        public int GrowingCount
        {
            get
            {
                int total = 0;
                for (int i = 0; i < _targets.Count; i++) total += _targets[i].GrowingCount;
                return total;
            }
        }

        // Cambió el stock (compra, video, uso, partida cargada).
        public event Action Changed;

        // Se usó un fertilizante jugando, con cuántas plantas terminó de crecer.
        public event Action<int> Used;

        // Un pad de fertilizante avisa que el jugador se quedó parado y no se puede (sin monedas, stock lleno o vacío, nada creciendo).
        public event Action Refused;

        public void ReportRefused()
        {
            Refused?.Invoke();
        }

        public void Initialize(GameConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _stock = new FertilizerStock(config.FertilizerMaxStock);
            _stock.Changed += OnStockChanged;
            Instance = this;
        }

        // ---------- Campos ----------

        public void Register(IFertilizable target)
        {
            if (target != null && !_targets.Contains(target)) _targets.Add(target);
        }

        public void Unregister(IFertilizable target)
        {
            _targets.Remove(target);
        }

        // ---------- Conseguir ----------

        // Con monedas se compra hasta este stock (el video sigue hasta MaxStock): que no se pueda "farmear" fertilizante a pura plata.
        public int BuyMaxStock => _config != null ? Math.Min(_config.FertilizerBuyMaxStock, _stock.Max) : 0;
        public bool AtBuyLimit => _stock.Count >= BuyMaxStock;

        // Segundos que faltan para poder volver a comprar (0 = ya se puede). Reloj sin escala; no se guarda.
        public float BuyCooldownRemaining => Mathf.Max(0f, _buyReadyAt - Time.unscaledTime);

        // true si se puede comprar ahora: no se llegó al tope de compra, pasó el enfriamiento, alcanzan las monedas y el juego está en Playing.
        public bool CanBuy
        {
            get
            {
                EconomyManager economy = EconomyManager.Instance;
                return IsPlaying() && !AtBuyLimit && BuyCooldownRemaining <= 0f && economy != null && economy.CanAfford(Price);
            }
        }

        public bool TryBuy()
        {
            EconomyManager economy = EconomyManager.Instance;
            if (!IsPlaying() || AtBuyLimit || BuyCooldownRemaining > 0f || economy == null) return false;
            if (!economy.TrySpend(Price)) return false;

            _buyReadyAt = Time.unscaledTime + _config.FertilizerBuyCooldownSeconds;
            _stock.Add(1);
            return true;
        }

        // La recompensa de un video: suma 'amount' unidades al stock (sin pasarse del tope). Devuelve cuántas entraron. No mira el
        // estado del juego ni cobra: quien otorga (AdsManager) ya verificó que el anuncio se completó.
        public int Grant(int amount)
        {
            return _stock.Add(amount);
        }

        // ---------- Usar ----------

        // Gasta una unidad y termina de crecer todo lo que está creciendo. Devuelve cuántas plantas llegaron a "listas" (0 = no se
        // gastó nada: sin stock, fuera de Playing o sin plantas creciendo).
        public int TryUse()
        {
            if (!IsPlaying() || _stock.Count <= 0 || GrowingCount <= 0) return 0;

            _stock.TryTake();

            int grown = 0;
            for (int i = 0; i < _targets.Count; i++) grown += _targets[i].GrowAllNow();

            Used?.Invoke(grown);
            return grown;
        }

        // Debug y pruebas: suma stock sin pasar por la compra ni el video.
        public void DebugGive(int amount)
        {
            _stock.Add(amount);
        }

        private static bool IsPlaying()
        {
            GameManager game = GameManager.Instance;
            return game == null || game.IsPlaying;
        }

        private void OnStockChanged()
        {
            Changed?.Invoke();
            SaveRequested?.Invoke();
        }

        // ---------- Partida guardada ----------

        public string SaveId => "fertilizer";

        public event Action SaveRequested;

        public string Capture()
        {
            return JsonUtility.ToJson(new FertilizerSave { stock = _stock.Count });
        }

        // Con null, vacío o ilegible vuelve a 0. El stock se acota al máximo actual.
        public void Restore(string json)
        {
            FertilizerSave save = null;
            if (!string.IsNullOrWhiteSpace(json))
            {
                try
                {
                    save = JsonUtility.FromJson<FertilizerSave>(json);
                }
                catch (ArgumentException)
                {
                    save = null;
                }
            }

            _stock.Set(save != null ? save.stock : 0);
        }

        private void OnDestroy()
        {
            if (_stock != null) _stock.Changed -= OnStockChanged;
            if (Instance == this) Instance = null;
        }
    }
}
