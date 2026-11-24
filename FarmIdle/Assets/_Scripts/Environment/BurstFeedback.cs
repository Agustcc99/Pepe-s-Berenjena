using UnityEngine;

namespace FarmIdle
{
    // Estallido de trocitos de color (J-02): al cosechar (hojas) y al completar una compra de un pad, sea mejora o zona (dorado).
    // Es cosmético: la cosecha y la compra ya están hechas cuando llega el aviso (PlotView.Harvested, PurchasePad.PurchaseCompleted).
    //   - Los trocitos son cubos del material de la paleta, sacados del PoolManager (Prewarm al activarse): sin Instantiate en juego.
    //   - Un solo Update para todos, con tiempo sin escala, y sin asignaciones por cuadro.
    //   - Si no quedan lugares libres, ese estallido sale con menos trocitos (o ninguno); no pasa nada más.
    public sealed class BurstFeedback : MonoBehaviour
    {
        private struct Chip
        {
            public GameObject Instance;
            public Vector3 Origin;
            public Vector3 Velocity;
            public float StartedAt;
        }

        [SerializeField, Tooltip("Trocito verde para la cosecha (cubo con el material de la paleta).")]
        private GameObject harvestChip;

        [SerializeField, Tooltip("Trocito dorado para las compras.")]
        private GameObject purchaseChip;

        [SerializeField, Min(1), Tooltip("Trocitos que se crean de antemano de cada tipo; también es el máximo que puede verse a la vez.")]
        private int prewarm = 32;

        [SerializeField, Min(1)] private int harvestPieces = 5;
        [SerializeField, Min(1)] private int purchasePieces = 12;
        [SerializeField, Min(0.1f)] private float seconds = 0.55f;
        [SerializeField, Min(0f)] private float speed = 3.2f;
        [SerializeField, Min(0f)] private float gravity = 9f;
        [SerializeField] private float harvestHeight = 0.6f;
        [SerializeField] private float purchaseHeight = 0.3f;

        private Chip[] _chips;
        private int _active;
        private bool _wired;

        // Para pruebas y depuración.
        public int ActiveChips => _active;
        public GameObject HarvestChip => harvestChip;
        public GameObject PurchaseChip => purchaseChip;

        private void OnEnable()
        {
            _wired = false;

            if (harvestChip == null || purchaseChip == null)
            {
                Debug.LogError("[FarmIdle] BurstFeedback '" + name + "': faltan los prefabs de los trocitos.", this);
                return;
            }

            PoolManager pools = PoolManager.Instance;
            if (pools == null)
            {
                Debug.LogError("[FarmIdle] BurstFeedback: no hay PoolManager. Arrancá el juego desde la escena Bootstrap.", this);
                return;
            }

            pools.Prewarm(harvestChip, prewarm);
            pools.Prewarm(purchaseChip, prewarm);
            _chips = new Chip[prewarm];   // nunca hay más de 'prewarm' en total, así que ningún pool se queda corto

            PlotView.Harvested += OnHarvested;
            PlotView.RareHarvested += OnRareHarvested;
            PurchasePad.PurchaseCompleted += OnPurchased;
            DecorationSet.Placed += OnDecorationPlaced;
            _wired = true;
        }

        private void OnDisable()
        {
            if (!_wired) return;

            PlotView.Harvested -= OnHarvested;
            PlotView.RareHarvested -= OnRareHarvested;
            PurchasePad.PurchaseCompleted -= OnPurchased;
            DecorationSet.Placed -= OnDecorationPlaced;
            _wired = false;

            for (int i = 0; i < _chips.Length; i++) Release(ref _chips[i]);
            _active = 0;
        }

        private void OnHarvested(CropData crop, Vector3 position)
        {
            Spawn(harvestChip, position + Vector3.up * harvestHeight, harvestPieces);
        }

        // J-04: la cosecha rara estalla con el doble de fichas.
        private void OnRareHarvested(CropData crop, Vector3 position, long bonus)
        {
            Spawn(harvestChip, position + Vector3.up * harvestHeight, harvestPieces * 2);
        }

        private void OnPurchased(Vector3 position)
        {
            Spawn(purchaseChip, position + Vector3.up * purchaseHeight, purchasePieces);
        }

        // F-22: una decoración nueva estalla en dorado, con el doble de trocitos que una compra común (tope: los que hay en el pool).
        private void OnDecorationPlaced(Vector3 position, int charmPercent)
        {
            Spawn(purchaseChip, position + Vector3.up * DecorationBurstHeight, purchasePieces * 2);
        }

        private const float DecorationBurstHeight = 1.2f;

        private void Spawn(GameObject prefab, Vector3 origin, int count)
        {
            PoolManager pools = PoolManager.Instance;
            if (pools == null || !_wired) return;

            float now = Time.unscaledTime;
            int made = 0;

            for (int i = 0; i < _chips.Length && made < count; i++)
            {
                if (_chips[i].Instance != null) continue;

                // Salen en abanico hacia arriba y a los costados.
                Vector2 side = Random.insideUnitCircle.normalized;
                Vector3 velocity = new Vector3(side.x, 0f, side.y) * (speed * Random.Range(0.4f, 1f)) + Vector3.up * (speed * Random.Range(0.8f, 1.5f));

                GameObject instance = pools.Get(prefab, origin, Random.rotation);
                _chips[i] = new Chip { Instance = instance, Origin = origin, Velocity = velocity, StartedAt = now };
                _active++;
                made++;
            }
        }

        private void LateUpdate()
        {
            if (!_wired || _active == 0) return;

            float now = Time.unscaledTime;

            for (int i = 0; i < _chips.Length; i++)
            {
                if (_chips[i].Instance == null) continue;

                float t = now - _chips[i].StartedAt;
                float k = t / seconds;
                if (k >= 1f)
                {
                    Release(ref _chips[i]);
                    _active--;
                    continue;
                }

                Vector3 position = _chips[i].Origin + _chips[i].Velocity * t;
                position.y -= 0.5f * gravity * t * t;
                Transform tr = _chips[i].Instance.transform;
                tr.position = position;
                tr.localScale = Vector3.one * (k < 0.5f ? 1f : 2f * (1f - k));   // se achica en la segunda mitad
            }
        }

        private static void Release(ref Chip chip)
        {
            if (chip.Instance == null) return;

            if (PoolManager.Instance != null)
            {
                chip.Instance.transform.localScale = Vector3.one;
                PoolManager.Instance.Release(chip.Instance);
            }

            chip = default;
        }
    }
}
