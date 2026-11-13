using System;
using UnityEngine;

namespace FarmIdle
{
    // F-17 (tanda 3): skins (colores) cosméticas del jugador. Dueño de cuáles tiene y cuál lleva puesta:
    //   - Se compran en el orden de GameConfig.Skins (TryBuyNext, por EconomyManager.TrySpend) y la recién comprada se pone sola.
    //   - Equipped: -1 = el color de fábrica; 0 a Owned-1 = esa skin. Cycle() recorre fábrica y las compradas.
    //   - Son cosméticas y permanentes: una nueva temporada no las quita. La vista del jugador (Player) escucha Changed.
    //   - Partida guardada: ISaveable "skins" con cuántas tiene y cuál lleva. Una partida sin la sección arranca sin skins; restaurar no cobra.
    public sealed class SkinManager : MonoBehaviour, ISaveable
    {
        public static SkinManager Instance { get; private set; }

        private const int SaveVersion = 1;

        [Serializable]
        private sealed class SkinSave
        {
            public int version = SaveVersion;
            public int owned;
            public int equipped = -1;
        }

        private GameConfig _config;
        private int _owned;
        private int _equipped = -1;

        public int Owned => _owned;
        public int Total => _config != null ? _config.Skins.Count : 0;
        public bool AllOwned => _owned >= Total;
        public int Equipped => _equipped;

        // La puesta, o null si lleva el color de fábrica.
        public SkinData EquippedSkin => _equipped >= 0 && _equipped < Total ? _config.Skins[_equipped] : null;

        // La que vende el pad ahora, o null si ya están todas.
        public SkinData Next => !AllOwned ? _config.Skins[_owned] : null;
        public long NextCost => Next != null ? Next.Cost : 0L;

        public bool CanBuyNext
        {
            get
            {
                EconomyManager economy = EconomyManager.Instance;
                return !AllOwned && economy != null && economy.CanAfford(NextCost);
            }
        }

        // Hay algo que hacer en el pad: comprar la que viene o cambiar entre las que ya tiene.
        public bool CanAct => CanBuyNext || _owned > 0;

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
        }

        public bool TryBuyNext()
        {
            if (AllOwned) return false;

            EconomyManager economy = EconomyManager.Instance;
            if (economy == null || !economy.TrySpend(NextCost)) return false;

            _owned++;
            _equipped = _owned - 1;
            Changed?.Invoke();
            Bought?.Invoke(_equipped);
            SaveRequested?.Invoke();
            return true;
        }

        // Pasa a la siguiente skin que tiene (fábrica, 1.a, 2.a... y vuelta a fábrica). Sin skins compradas no hace nada.
        public bool Cycle()
        {
            if (_owned <= 0) return false;

            _equipped = _equipped + 1 >= _owned ? -1 : _equipped + 1;
            Changed?.Invoke();
            SaveRequested?.Invoke();
            return true;
        }

        // ---------- Partida guardada ----------

        public string SaveId => "skins";

        public event Action SaveRequested;

        public string Capture()
        {
            return JsonUtility.ToJson(new SkinSave { owned = _owned, equipped = _equipped });
        }

        // Con null, vacío o ilegible: sin skins y el color de fábrica. Todo se acota a las que existen.
        public void Restore(string json)
        {
            SkinSave save = null;
            if (!string.IsNullOrWhiteSpace(json))
            {
                try
                {
                    save = JsonUtility.FromJson<SkinSave>(json);
                }
                catch (ArgumentException)
                {
                    save = null;
                }
            }

            _owned = save != null ? Mathf.Clamp(save.owned, 0, Total) : 0;
            _equipped = save != null ? Mathf.Clamp(save.equipped, -1, _owned - 1) : -1;
            Changed?.Invoke();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
