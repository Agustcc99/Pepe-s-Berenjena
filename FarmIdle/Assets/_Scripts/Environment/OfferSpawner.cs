using UnityEngine;

namespace FarmIdle
{
    // Pone el símbolo de oferta en el mundo cuando el OfferManager lo pide. Es el lado "entorno" de la oferta: el servicio decide
    // CUÁNDO y en cuál de los lugares (índices); acá se tienen los lugares (puntos de la escena) y el símbolo, que sale del PoolManager
    // (Prewarm al cargar el nivel, Get al aparecer, Release al irse): jugando nunca se hace Instantiate ni Destroy.
    public sealed class OfferSpawner : MonoBehaviour
    {
        [SerializeField, Tooltip("El prefab del símbolo (con RewardSymbol). Sale del pool.")]
        private GameObject symbolPrefab;

        [SerializeField, Tooltip("Los lugares donde puede aparecer el símbolo, libres de otros pads.")]
        private Transform[] slots = new Transform[0];

        private OfferManager _offers;
        private GameObject _active;

        public GameObject SymbolPrefab => symbolPrefab;
        public int SlotCount => slots.Length;
        public Transform Slot(int index) { return slots[index]; }

        // El símbolo que está en el mundo ahora (null si no hay).
        public RewardSymbol ActiveSymbol => _active != null ? _active.GetComponent<RewardSymbol>() : null;

        private void OnEnable()
        {
            _offers = OfferManager.Instance;
            if (_offers == null || symbolPrefab == null || slots.Length == 0)
            {
                Debug.LogError("[FarmIdle] OfferSpawner '" + name + "': falta el OfferManager, el prefab del símbolo o los lugares.", this);
                _offers = null;
                return;
            }

            // Sin lugares el OfferManager no ofrece nada: así se apaga el símbolo en la web (ver OfferManager.SymbolsAllowedOn).
            _offers.SetSlotCount(OfferManager.SymbolsAllowedOn(Application.platform) ? slots.Length : 0);
            _offers.OfferStarted += OnOfferStarted;
            _offers.OfferEnded += OnOfferEnded;
        }

        private void OnDisable()
        {
            if (_offers != null)
            {
                _offers.OfferStarted -= OnOfferStarted;
                _offers.OfferEnded -= OnOfferEnded;
                _offers.SetSlotCount(0);
                _offers = null;
            }

            Release();   // el símbolo vive en el pool, no en esta escena: tiene que volver antes de que la escena se descargue
        }

        private void Start()
        {
            // El Prewarm se declara una vez, al cargar el nivel; de ahí en más el pool entrega sin Instantiate.
            PoolManager pools = PoolManager.Instance;
            if (pools != null && symbolPrefab != null) pools.Prewarm(symbolPrefab, 1);
        }

        private void OnOfferStarted(RewardData reward, int slot)
        {
            Release();

            PoolManager pools = PoolManager.Instance;
            if (pools == null || slot < 0 || slot >= slots.Length) return;

            Transform place = slots[slot];
            _active = pools.Get(symbolPrefab, place.position, place.rotation);
            if (_active == null) return;

            // Una línea por oferta (cada 2 o 3 minutos): sirve para saber dónde apareció cuando se prueba en la web o en un celular.
            Debug.Log("[FarmIdle] Oferta: " + reward.DisplayName + " en el lugar " + slot + " (x " + place.position.x.ToString("0.0") + ", z " + place.position.z.ToString("0.0") + ").");

            if (_active.TryGetComponent(out RewardSymbol symbol)) symbol.SetReward(reward);
            else Debug.LogError("[FarmIdle] El prefab del símbolo '" + symbolPrefab.name + "' no tiene RewardSymbol.", symbolPrefab);
        }

        private void OnOfferEnded(bool taken)
        {
            Release();
        }

        private void Release()
        {
            if (_active == null) return;

            GameObject instance = _active;
            _active = null;

            // Sin PoolManager (el juego se está cerrando) el símbolo se destruye junto con su contenedor.
            if (PoolManager.Instance != null) PoolManager.Instance.Release(instance);
        }
    }
}
