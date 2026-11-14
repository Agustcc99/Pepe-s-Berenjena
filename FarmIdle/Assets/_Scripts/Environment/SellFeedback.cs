using TMPro;
using UnityEngine;

namespace FarmIdle
{
    // Lo que se ve al vender: cuando un producto llega a la zona aparece una moneda que salta y gira, el pad "late" y un
    // "+N" flota sobre la zona con lo que se va ganando (si se vende seguido, el número suma en vez de apilar un texto por
    // venta). Todo es cosmético: la venta ya está hecha cuando esto empieza (SellZone avisa con Sold después de cobrar), y
    // si falla o se apaga nada cambia.
    //   - Los efectos arrancan 'flightSeconds' después de la venta, el tiempo que tarda el producto en llegar desde la
    //     espalda (lo anima BackpackStackView), así la moneda aparece justo cuando el producto toca el pad.
    //   - Usa tiempo sin escala, igual que las demás animaciones, y un solo Update para todo: sin una corrutina por venta.
    //   - Las monedas salen del PoolManager (Prewarm al activarse): nada de Instantiate durante el juego. El texto y el
    //     pad son objetos fijos de la escena.
    public sealed class SellFeedback : MonoBehaviour
    {
        private const int QueueSize = 32;   // ventas en vuelo a la vez (con 0,12 s entre ventas y 0,3 s de vuelo hay ~3)

        private struct Arrival
        {
            public float At;
            public long Coins;
        }

        private struct Coin
        {
            public GameObject Instance;
            public float StartedAt;
        }

        [SerializeField, Tooltip("La zona de venta que avisa de cada venta.")]
        private SellZone zone;

        [SerializeField, Tooltip("Moneda low poly que aparece al llegar el producto. Sale del PoolManager.")]
        private GameObject coinPrefab;

        [SerializeField, Tooltip("Texto en el mundo con el \"+N\".")]
        private TMP_Text popupLabel;

        [SerializeField, Tooltip("Lo que \"late\" cuando llega un producto (el modelo del pad).")]
        private Transform padVisual;

        [SerializeField, Min(1), Tooltip("Monedas que se crean de antemano (las que pueden verse a la vez, con margen).")]
        private int coinPrewarm = 12;

        [SerializeField, Min(0.1f), Tooltip("Cuánto dura cada moneda.")]
        private float coinSeconds = 0.5f;

        [SerializeField, Min(0f), Tooltip("Cuánto sube la moneda.")]
        private float coinRise = 1.3f;

        [SerializeField, Min(0.1f), Tooltip("Cuánto se queda el \"+N\" después de la última venta antes de desvanecerse.")]
        private float popupHoldSeconds = 0.6f;

        [SerializeField, Min(0.05f), Tooltip("Cuánto tarda el \"+N\" en desvanecerse.")]
        private float popupFadeSeconds = 0.35f;

        [SerializeField, Min(0f), Tooltip("Cuánto sube el \"+N\" mientras se desvanece.")]
        private float popupRise = 0.7f;

        [SerializeField, Min(1f), Tooltip("Cuánto crece el pad al recibir un producto.")]
        private float padPulseScale = 1.07f;

        [SerializeField, Min(1), Tooltip("J-08: desde cuántas unidades vendidas sin cortar el \"+N\" hace su remate de venta grande.")]
        private int bigSaleUnits = JuiceRules.BigSaleUnits;

        private readonly Arrival[] _queue = new Arrival[QueueSize];
        private Coin[] _coins;
        private int _queueHead;
        private int _queueCount;
        private int _activeCoins;

        private const float BigSaleScale = 1.5f;     // J-08: el "+N" total de una venta grande queda así de grande
        private const float BigSalePunchSeconds = 0.3f;

        private long _popupSum;
        private int _popupUnits;      // unidades que suma este "+N" (una venta grande son JuiceRules.BigSaleUnits o más)
        private bool _bigFinish;      // el "+N" ya está en su remate de venta grande
        private float _popupLastAt;
        private float _padPulseAt = -10f;
        private bool _padPulsing;
        private Vector3 _popupBasePosition;
        private Camera _camera;
        private bool _wired;

        // Para pruebas y depuración.
        public long PopupAmount => _popupSum;
        public int PopupUnits => _popupUnits;
        public bool IsBigSaleFinish => _bigFinish;
        public int ActiveCoins => _activeCoins;
        public int PendingArrivals => _queueCount;
        public GameObject CoinPrefab => coinPrefab;
        public TMP_Text PopupLabel => popupLabel;

        private void OnEnable()
        {
            _wired = false;

            if (zone == null || coinPrefab == null || popupLabel == null)
            {
                Debug.LogError("[FarmIdle] SellFeedback '" + name + "': faltan la zona de venta, la moneda o el texto.", this);
                return;
            }

            PoolManager pools = PoolManager.Instance;
            if (pools == null)
            {
                Debug.LogError("[FarmIdle] SellFeedback: no hay PoolManager. Arrancá el juego desde la escena Bootstrap.", this);
                return;
            }

            pools.Prewarm(coinPrefab, coinPrewarm);

            _coins = new Coin[coinPrewarm];
            _popupBasePosition = popupLabel.transform.localPosition;
            HidePopup();

            zone.Sold += OnSold;
            _wired = true;
        }

        private void OnDisable()
        {
            if (!_wired) return;

            zone.Sold -= OnSold;
            _wired = false;

            // Las monedas viven en el pool, no en esta escena: tienen que volver antes de que la escena se descargue.
            for (int i = 0; i < _coins.Length; i++)
            {
                ReleaseCoin(ref _coins[i]);
            }

            _activeCoins = 0;
            _queueCount = 0;
            _queueHead = 0;
            HidePopup();
            if (padVisual != null) padVisual.localScale = Vector3.one;
            _padPulsing = false;
        }

        private void OnSold(CropData crop, long coins, float arrivesInSeconds)
        {
            if (_queueCount == QueueSize)
            {
                // Más ventas en vuelo de las que caben (no pasa con los valores normales): se pierde la más vieja.
                _queueHead = (_queueHead + 1) % QueueSize;
                _queueCount--;
            }

            int tail = (_queueHead + _queueCount) % QueueSize;
            _queue[tail] = new Arrival { At = Time.unscaledTime + arrivesInSeconds, Coins = coins };
            _queueCount++;
        }

        private void Update()
        {
            if (!_wired) return;

            float now = Time.unscaledTime;

            while (_queueCount > 0 && _queue[_queueHead].At <= now)
            {
                OnArrived(_queue[_queueHead].Coins, now);
                _queueHead = (_queueHead + 1) % QueueSize;
                _queueCount--;
            }

            UpdateCoins(now);
            UpdatePopup(now);
            UpdatePad(now);
        }

        // Billboard: el texto mira siempre a la cámara (la del juego es fija, pero así no depende de su ángulo).
        private void LateUpdate()
        {
            if (!_wired || _popupSum <= 0) return;

            if (_camera == null) _camera = Camera.main;
            if (_camera != null) popupLabel.transform.rotation = _camera.transform.rotation;
        }

        private void OnArrived(long coins, float now)
        {
            SpawnCoin(now);

            _popupSum += coins;
            _popupUnits++;
            _popupLastAt = now;
            _padPulseAt = now;
        }

        private void SpawnCoin(float now)
        {
            PoolManager pools = PoolManager.Instance;
            if (pools == null) return;

            for (int i = 0; i < _coins.Length; i++)
            {
                if (_coins[i].Instance != null) continue;

                _coins[i].Instance = pools.Get(coinPrefab, zone.Target.position, Quaternion.identity);
                _coins[i].StartedAt = now;
                _activeCoins++;
                return;
            }

            // Todos los lugares ocupados (con los valores normales no pasa): esta venta se queda sin moneda y el "+N" igual suma.
        }

        private void UpdateCoins(float now)
        {
            if (_activeCoins == 0) return;

            Vector3 origin = zone.Target.position;
            for (int i = 0; i < _coins.Length; i++)
            {
                if (_coins[i].Instance == null) continue;

                float k = (now - _coins[i].StartedAt) / coinSeconds;
                if (k >= 1f)
                {
                    ReleaseCoin(ref _coins[i]);
                    _activeCoins--;
                    continue;
                }

                float rise = 1f - (1f - k) * (1f - k);   // sube rápido y frena arriba
                Transform t = _coins[i].Instance.transform;
                t.position = origin + Vector3.up * (rise * coinRise);
                t.rotation = Quaternion.Euler(0f, k * 540f, 0f);
                t.localScale = Vector3.one * (k < 0.7f ? 1f : 1f - (k - 0.7f) / 0.3f);   // se achica al final
            }
        }

        private void UpdatePopup(float now)
        {
            if (_popupSum <= 0) return;

            float age = now - _popupLastAt;

            // J-08: venta grande. Si el "+N" que se iba a apagar sumó la mochila entera, antes hace su remate: el total crece de
            // golpe y se queda un rato más (el reloj vuelve a empezar). Una sola vez por "+N".
            if (!_bigFinish && age >= popupHoldSeconds && _popupUnits >= bigSaleUnits)
            {
                _bigFinish = true;
                _popupLastAt = now;
                age = 0f;
            }

            if (age >= popupHoldSeconds + popupFadeSeconds)
            {
                HidePopup();
                return;
            }

            popupLabel.SetText("+{0:0}", _popupSum);

            // Un saltito cada vez que suma, y al final se desvanece subiendo.
            float punch = _bigFinish
                ? BigSaleScale + 0.5f * Mathf.Clamp01(1f - age / BigSalePunchSeconds)
                : 1f + 0.3f * Mathf.Clamp01(1f - age / 0.15f);
            float fade = Mathf.Clamp01((age - popupHoldSeconds) / popupFadeSeconds);

            Transform t = popupLabel.transform;
            t.localScale = Vector3.one * punch;
            t.localPosition = _popupBasePosition + Vector3.up * (popupRise * fade);

            Color color = popupLabel.color;
            color.a = 1f - fade;
            popupLabel.color = color;
            popupLabel.enabled = true;
        }

        private void HidePopup()
        {
            _popupSum = 0;
            _popupUnits = 0;
            _bigFinish = false;
            if (popupLabel == null) return;

            popupLabel.enabled = false;
            popupLabel.transform.localPosition = _popupBasePosition;
            popupLabel.transform.localScale = Vector3.one;
        }

        private void UpdatePad(float now)
        {
            if (padVisual == null) return;

            float k = (now - _padPulseAt) / 0.18f;
            if (k >= 1f)
            {
                // En reposo no se toca el transform (una escritura por frame sería trabajo de más).
                if (_padPulsing)
                {
                    padVisual.localScale = Vector3.one;
                    _padPulsing = false;
                }

                return;
            }

            _padPulsing = true;
            float scale = 1f + (padPulseScale - 1f) * Mathf.Sin(Mathf.Clamp01(k) * Mathf.PI);
            padVisual.localScale = new Vector3(scale, 1f, scale);   // solo a lo ancho: el pad no se "infla" hacia arriba
        }

        private static void ReleaseCoin(ref Coin coin)
        {
            if (coin.Instance == null) return;

            // Sin PoolManager (el juego se está cerrando) la moneda se destruye junto con su contenedor.
            if (PoolManager.Instance != null) PoolManager.Instance.Release(coin.Instance);
            coin.Instance = null;
        }
    }
}
