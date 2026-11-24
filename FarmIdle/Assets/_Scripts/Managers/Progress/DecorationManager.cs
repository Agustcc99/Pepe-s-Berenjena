using System;
using UnityEngine;

namespace FarmIdle
{
    // F-17 (tanda 1): decoraciones cosméticas de la granja, un sumidero de monedas del final del juego. Dueño de qué decoraciones
    // tiene el jugador:
    //   - Se compran en el orden de GameConfig.Decorations: el pad vende siempre la siguiente (TryBuyNext), por EconomyManager.TrySpend.
    //     Por eso alcanza con guardar CUÁNTAS tiene (Owned).
    //   - Se desbloquean al abrir todas las zonas (así no compiten con la economía temprana).
    //   - Dan "encanto" (DecorationData.Charm, hoy +2 % cada una, +16 % las ocho): un multiplicador permanente del precio de venta
    //     (PriceCalculator.CharmMultiplier). Así el sumidero de monedas del final también hace producir más.
    //   - Son permanentes: una nueva temporada (prestigio) no las quita.
    //   - Environment las muestra escuchando Changed; Managers no conoce a Environment.
    //   - Partida guardada: ISaveable "decorations" con la cantidad. Restaurar no cobra nada; una partida sin la sección arranca en 0.
    public sealed class DecorationManager : MonoBehaviour, ISaveable
    {
        public static DecorationManager Instance { get; private set; }

        private const int SaveVersion = 1;

        [Serializable]
        private sealed class DecorationSave
        {
            public int version = SaveVersion;
            public int owned;
        }

        private GameConfig _config;
        private int _owned;

        public int Owned => _owned;
        public int Total => _config != null ? _config.Decorations.Count : 0;
        public bool AllOwned => _owned >= Total;

        // La que vende el pad ahora, o null si ya están todas.
        public DecorationData Next => !AllOwned ? _config.Decorations[_owned] : null;
        public long NextCost => Next != null ? Next.Cost : 0L;

        // La decoración número 'index' de la lista, o null si no existe.
        public DecorationData At(int index) => _config != null && index >= 0 && index < Total ? _config.Decorations[index] : null;

        // Todas las zonas abiertas (sin progresión, como en una prueba suelta, cuenta como abierto).
        public bool IsUnlocked
        {
            get
            {
                ProgressionManager progression = ProgressionManager.Instance;
                return progression == null || progression.UnlockedZoneCount >= progression.AvailableZoneCount;   // F-24: las granjas de temporada que aún no aparecieron no cuentan
            }
        }

        public bool CanBuyNext
        {
            get
            {
                EconomyManager economy = EconomyManager.Instance;
                return !AllOwned && IsUnlocked && economy != null && economy.CanAfford(NextCost);
            }
        }

        // Cambió la cantidad de decoraciones (compra, partida cargada).
        public event Action Changed;

        // Se compró una: su posición en la lista (0 = la primera).
        public event Action<int> Bought;

        // Un pad avisa que el jugador se quedó parado y no se puede (sin monedas, zonas sin abrir).
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

        public bool IsOwned(int index)
        {
            return index >= 0 && index < _owned;
        }

        // Encanto total: la suma del encanto de las decoraciones que tiene (0,16 = +16 % al precio de venta).
        public float Charm
        {
            get
            {
                float total = 0f;
                for (int i = 0; i < _owned && i < Total; i++) total += _config.Decorations[i].Charm;
                return total;
            }
        }

        // Pasa el encanto al precio de venta. Se llama en cada cambio (compra, partida cargada).
        private void ApplyCharm()
        {
            EconomyManager economy = EconomyManager.Instance;
            if (economy != null) economy.Pricing.SetCharmMultiplier(1f + Charm);
        }

        public bool TryBuyNext()
        {
            if (AllOwned || !IsUnlocked) return false;

            EconomyManager economy = EconomyManager.Instance;
            if (economy == null || !economy.TrySpend(NextCost)) return false;

            _owned++;
            ApplyCharm();
            Changed?.Invoke();
            Bought?.Invoke(_owned - 1);
            SaveRequested?.Invoke();
            return true;
        }

        public void DebugGiveAll()
        {
            _owned = Total;
            ApplyCharm();
            Changed?.Invoke();
            SaveRequested?.Invoke();
        }

        // ---------- Partida guardada ----------

        public string SaveId => "decorations";

        public event Action SaveRequested;

        public string Capture()
        {
            return JsonUtility.ToJson(new DecorationSave { owned = _owned });
        }

        // Con null, vacío o ilegible vuelve a 0. La cantidad se acota a las decoraciones que existen.
        public void Restore(string json)
        {
            DecorationSave save = null;
            if (!string.IsNullOrWhiteSpace(json))
            {
                try
                {
                    save = JsonUtility.FromJson<DecorationSave>(json);
                }
                catch (ArgumentException)
                {
                    save = null;
                }
            }

            _owned = save != null ? Mathf.Clamp(save.owned, 0, Total) : 0;
            ApplyCharm();
            Changed?.Invoke();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
