using System;
using UnityEngine;

namespace FarmIdle
{
    // Mejoras visuales del camión de reparto: un sumidero de monedas cosmético. Dueño de cuántas tiene el jugador:
    //   - Se compran en el orden de GameConfig.TruckSkins: el pad vende siempre la siguiente (TryBuyNext), por EconomyManager.TrySpend.
    //     Por eso alcanza con guardar CUÁNTAS tiene (Owned). El nivel visual del camión es Owned (0 = el de fábrica).
    //   - No dan ninguna ventaja y no se quitan con el prestigio.
    //   - SellTruck (Environment) cambia de modelo escuchando Changed; Managers no conoce a Environment.
    //   - Partida guardada: ISaveable "truck" con la cantidad. Restaurar no cobra nada; una partida sin la sección arranca en 0.
    public sealed class TruckManager : MonoBehaviour, ISaveable
    {
        public static TruckManager Instance { get; private set; }

        private const int SaveVersion = 1;

        [Serializable]
        private sealed class TruckSave
        {
            public int version = SaveVersion;
            public int owned;
        }

        private GameConfig _config;
        private int _owned;

        // Mejoras compradas = nivel visual del camión (0 = de fábrica).
        public int Owned => _owned;
        public int Total => _config != null ? _config.TruckSkins.Count : 0;
        public bool AllOwned => _owned >= Total;

        // La que vende el pad ahora, o null si ya están todas.
        public TruckSkinRule Next => !AllOwned ? _config.TruckSkins[_owned] : null;
        public long NextCost => Next != null ? Next.Cost : 0L;

        public bool CanBuyNext
        {
            get
            {
                EconomyManager economy = EconomyManager.Instance;
                return !AllOwned && economy != null && economy.CanAfford(NextCost);
            }
        }

        // Cambió el nivel del camión (compra, partida cargada).
        public event Action Changed;

        // Se compró una mejora: el nivel nuevo (1 = la primera).
        public event Action<int> Bought;

        // Un pad avisa que el jugador se quedó parado y no se puede (sin monedas).
        public event Action Refused;

        public void ReportRefused()
        {
            Refused?.Invoke();
        }

        public void Initialize(GameConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            Instance = this;
        }

        public bool TryBuyNext()
        {
            if (AllOwned) return false;

            EconomyManager economy = EconomyManager.Instance;
            if (economy == null || !economy.TrySpend(NextCost)) return false;

            _owned++;
            Changed?.Invoke();
            Bought?.Invoke(_owned);
            SaveRequested?.Invoke();
            return true;
        }

        public void DebugGiveAll()
        {
            _owned = Total;
            Changed?.Invoke();
            SaveRequested?.Invoke();
        }

        // ---------- Partida guardada ----------

        public string SaveId => "truck";

        public event Action SaveRequested;

        public string Capture()
        {
            return JsonUtility.ToJson(new TruckSave { owned = _owned });
        }

        // Con null, vacío o ilegible vuelve a 0. La cantidad se acota a las mejoras que existen.
        public void Restore(string json)
        {
            TruckSave save = null;
            if (!string.IsNullOrWhiteSpace(json))
            {
                try
                {
                    save = JsonUtility.FromJson<TruckSave>(json);
                }
                catch (ArgumentException)
                {
                    save = null;
                }
            }

            _owned = save != null ? Mathf.Clamp(save.owned, 0, Total) : 0;
            Changed?.Invoke();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
