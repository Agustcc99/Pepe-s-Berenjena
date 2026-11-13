using TMPro;
using UnityEngine;

namespace FarmIdle
{
    // Lo que se ve al cosechar: un "+1" sale de la parcela, sube y se desvanece. Es cosmético: la cosecha ya está hecha cuando
    // PlotView avisa con Harvested. Un solo objeto en la escena escucha a todas las parcelas.
    //   - Los textos salen del PoolManager (Prewarm al activarse): nada de Instantiate durante el juego.
    //   - Un solo Update para todos los textos, con tiempo sin escala, igual que SellFeedback.
    //   - Si no quedan lugares libres (muchas cosechas seguidas), esa cosecha no muestra texto; no pasa nada más.
    public sealed class HarvestFeedback : MonoBehaviour
    {
        private struct Popup
        {
            public GameObject Instance;
            public TMP_Text Label;
            public Vector3 Origin;
            public float StartedAt;
        }

        [SerializeField, Tooltip("Prefab del texto \"+1\" (TextMeshPro en el mundo). Sale del PoolManager.")]
        private GameObject popupPrefab;

        [SerializeField, Min(1), Tooltip("Textos que se crean de antemano (los que pueden verse a la vez, con margen).")]
        private int prewarm = 8;

        [SerializeField, Min(0.1f), Tooltip("Cuánto dura cada texto.")]
        private float seconds = 0.7f;

        [SerializeField, Min(0f), Tooltip("Cuánto sube.")]
        private float rise = 1.2f;

        [SerializeField, Tooltip("Altura sobre la planta donde nace el texto.")]
        private float startHeight = 0.8f;

        private const float RareExtraHeight = 0.6f;
        private static readonly Color RareColor = new Color(1f, 0.82f, 0.18f, 1f);   // dorado

        private string _defaultText = "+1";
        private Color _defaultColor = Color.white;
        private Popup[] _popups;
        private int _active;
        private Camera _camera;
        private bool _wired;

        // Para pruebas y depuración.
        public int ActivePopups => _active;
        public GameObject PopupPrefab => popupPrefab;

        private void OnEnable()
        {
            _wired = false;

            if (popupPrefab == null)
            {
                Debug.LogError("[FarmIdle] HarvestFeedback '" + name + "': falta el prefab del texto.", this);
                return;
            }

            PoolManager pools = PoolManager.Instance;
            if (pools == null)
            {
                Debug.LogError("[FarmIdle] HarvestFeedback: no hay PoolManager. Arrancá el juego desde la escena Bootstrap.", this);
                return;
            }

            pools.Prewarm(popupPrefab, prewarm);
            _popups = new Popup[prewarm];

            if (popupPrefab.TryGetComponent(out TMP_Text prefabLabel))
            {
                _defaultText = prefabLabel.text;
                _defaultColor = prefabLabel.color;
            }

            PlotView.Harvested += OnHarvested;
            PlotView.RareHarvested += OnRareHarvested;
            _wired = true;
        }

        private void OnDisable()
        {
            if (!_wired) return;

            PlotView.Harvested -= OnHarvested;
            PlotView.RareHarvested -= OnRareHarvested;
            _wired = false;

            // Los textos viven en el pool, no en esta escena: tienen que volver antes de que la escena se descargue.
            for (int i = 0; i < _popups.Length; i++) Release(ref _popups[i]);
            _active = 0;
        }

        private void OnHarvested(CropData crop, Vector3 position)
        {
            Show(position, 0f, -1L);
        }

        // J-04: cosecha rara. Un segundo texto, más alto y dorado, con las monedas del premio (el "+N" sale de la venta, pero
        // acá el texto sí se arma porque pasa pocas veces).
        private void OnRareHarvested(CropData crop, Vector3 position, long bonus)
        {
            Show(position, RareExtraHeight, bonus);
        }

        private void Show(Vector3 position, float extraHeight, long bonus)
        {
            PoolManager pools = PoolManager.Instance;
            if (pools == null) return;

            for (int i = 0; i < _popups.Length; i++)
            {
                if (_popups[i].Instance != null) continue;

                Vector3 origin = position + Vector3.up * (startHeight + extraHeight);
                GameObject instance = pools.Get(popupPrefab, origin, Quaternion.identity);
                if (!instance.TryGetComponent(out TMP_Text label))
                {
                    pools.Release(instance);
                    return;
                }

                // El objeto vuelve del pool con lo que le dejó el último uso: se pone siempre texto y color.
                if (bonus >= 0L)
                {
                    label.text = "+" + bonus;
                    label.color = RareColor;
                }
                else
                {
                    label.text = _defaultText;
                    label.color = _defaultColor;
                }

                _popups[i] = new Popup { Instance = instance, Label = label, Origin = origin, StartedAt = Time.unscaledTime };
                label.alpha = 1f;
                _active++;
                return;
            }
        }

        private void LateUpdate()
        {
            if (!_wired || _active == 0) return;

            if (_camera == null) _camera = Camera.main;
            float now = Time.unscaledTime;

            for (int i = 0; i < _popups.Length; i++)
            {
                if (_popups[i].Instance == null) continue;

                float k = (now - _popups[i].StartedAt) / seconds;
                if (k >= 1f)
                {
                    Release(ref _popups[i]);
                    _active--;
                    continue;
                }

                float ease = 1f - (1f - k) * (1f - k);   // sube rápido y frena arriba
                Transform t = _popups[i].Instance.transform;
                t.position = _popups[i].Origin + Vector3.up * (ease * rise);
                if (_camera != null) t.rotation = _camera.transform.rotation;   // mira a la cámara
                t.localScale = Vector3.one * (1f + 0.3f * Mathf.Clamp01(1f - k / 0.2f));   // saltito al nacer
                _popups[i].Label.alpha = k < 0.6f ? 1f : 1f - (k - 0.6f) / 0.4f;
            }
        }

        private static void Release(ref Popup popup)
        {
            if (popup.Instance == null) return;

            // Sin PoolManager (el juego se está cerrando) el texto se destruye junto con su contenedor.
            if (PoolManager.Instance != null) PoolManager.Instance.Release(popup.Instance);
            popup = default;
        }
    }
}
