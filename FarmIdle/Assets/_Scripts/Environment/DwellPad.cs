using TMPro;
using UnityEngine;

namespace FarmIdle
{
    // Base de los pads de la Fase 8 (usar y comprar fertilizante, ver un video, el símbolo de oferta): un pad en el suelo que hace UNA
    // cosa cuando el jugador se queda parado encima un momento, sin botones. Es un IInteractable como los demás pads: no sabe quién
    // lo pisa (ve un IActor) y el InteractionTrigger del mismo objeto le llama a Interact en cada frame mientras el actor esté encima y
    // pueda actuar (el jugador, solo en Playing).
    //   - Espera corta al llegar (dwellSeconds, tiempo del juego) para no gastar ni pedir un video solo por cruzar el pad; una barra
    //     se llena mientras tanto. Si no hay nada que hacer (CanAct false) la espera no corre.
    //   - UNA acción por visita: después de Act (salga bien o mal) el pad no vuelve a actuar hasta que el jugador se baje y vuelva a
    //     subir. Así quedarse parado no encadena compras ni pide un video tras otro, y un rechazo no se repite en cada frame.
    //   - Lo que hace de verdad lo deciden los servicios de Managers (FertilizerManager, AdsManager); acá solo se ofrece y se muestra.
    //   - Lo cosmético (cartel, barra, "latido") usa tiempo sin escala y un solo Update; el cartel se reescribe solo cuando cambia su
    //     StateKey (se mira cada 0,2 s), no en cada frame.
    public abstract class DwellPad : MonoBehaviour, IInteractable, IInteractableExit
    {
        [SerializeField, Tooltip("Cartel en el mundo con el nombre del pad y su estado.")]
        private TMP_Text label;

        [SerializeField, Tooltip("Barra que se llena (escala en X, de 0 a 1) mientras el jugador espera parado. Puede faltar.")]
        private Transform fillBar;

        [SerializeField, Tooltip("Lo que \"late\" al actuar (el modelo del pad). Puede faltar.")]
        private Transform padVisual;

        [SerializeField, Min(0f), Tooltip("Segundos de tiempo de juego que hay que estar parado antes de que el pad actúe.")]
        private float dwellSeconds = 0.6f;

        [SerializeField, Min(1f)] private float pulseScale = 1.18f;

        private Camera _camera;
        private float _arrivedAt = -1f;
        private bool _doneThisVisit;
        private float _progress;
        private float _shownFill = -1f;
        private float _pulseAt = -10f;
        private bool _pulsing;
        private float _nextLabelCheck;
        private int _shownKey = int.MinValue;
        private float _blockedSince = -1f;
        private bool _blockedReported;

        private const float BlockedExtraSeconds = 0.4f;

        public TMP_Text Label => label;
        public bool WaitingForExit => _doneThisVisit;

        // ---------- Lo que define cada pad ----------

        // true si ahora tiene sentido actuar (hay stock, hay anuncio, alcanzan las monedas...). Barata: se llama en cada frame.
        protected abstract bool CanAct();

        // La acción. Devuelve true si hizo algo. Se llama una vez por visita, después de la espera.
        protected abstract bool Act();

        // Un número que cambia cuando cambia lo que dice el cartel. Se llama unas 5 veces por segundo.
        protected abstract int StateKey();

        // El texto del cartel. Solo se llama cuando cambió StateKey.
        protected abstract string BuildText();

        protected virtual void OnActed() { }

        // El jugador lleva un rato parado y el pad no puede actuar (sin monedas, sin stock, sin anuncio...). Una vez por visita. Cada pad
        // se lo avisa a su servicio de Managers; el audio escucha ahí.
        protected virtual void OnBlocked() { }

        // ---------- Ciclo de vida ----------

        protected virtual void OnEnable()
        {
            _arrivedAt = -1f;
            _doneThisVisit = false;
            _progress = 0f;
            _shownKey = int.MinValue;
            _nextLabelCheck = 0f;
            _blockedSince = -1f;
            _blockedReported = false;
            ApplyFill(0f);
            Loc.Changed += OnLanguageChanged;
        }

        // Cambió el idioma: el cartel se reescribe en la próxima revisión (unas 5 veces por segundo).
        private void OnLanguageChanged()
        {
            _shownKey = int.MinValue;
        }

        protected virtual void OnDisable()
        {
            Loc.Changed -= OnLanguageChanged;
            _arrivedAt = -1f;
            _blockedSince = -1f;
            _blockedReported = false;
            _pulsing = false;
            if (padVisual != null) padVisual.localScale = Vector3.one;
        }

        // IInteractable: llega en cada frame con el actor encima. Barata y sin escribir en la consola.
        public void Interact(IActor actor)
        {
            if (_doneThisVisit) return;

            float now = Time.time;

            if (!CanAct())
            {
                _arrivedAt = -1f;
                _progress = 0f;

                // Parado un rato en un pad que no puede actuar: una sola vez por visita se avisa (el audio hace "no se puede").
                if (_blockedSince < 0f) _blockedSince = now;
                if (!_blockedReported && now - _blockedSince >= dwellSeconds + BlockedExtraSeconds)
                {
                    _blockedReported = true;
                    OnBlocked();
                }

                return;
            }

            _blockedSince = -1f;
            if (_arrivedAt < 0f) _arrivedAt = now;

            _progress = dwellSeconds <= 0f ? 1f : Mathf.Clamp01((now - _arrivedAt) / dwellSeconds);
            if (_progress < 1f) return;

            _doneThisVisit = true;
            _progress = 0f;
            if (Act())
            {
                _pulseAt = Time.unscaledTime;
                OnActed();
            }
        }

        // IInteractableExit: el jugador se fue; la próxima visita vuelve a esperar y a poder actuar.
        public void ActorLeft(IActor actor)
        {
            _arrivedAt = -1f;
            _doneThisVisit = false;
            _progress = 0f;
            _blockedSince = -1f;
            _blockedReported = false;
        }

        // ---------- Cartel, barra y latido (cosmético) ----------

        // Billboard: el cartel mira siempre a la cámara.
        protected virtual void LateUpdate()
        {
            if (label == null || !label.enabled) return;

            if (_camera == null) _camera = Camera.main;
            if (_camera != null) label.transform.rotation = _camera.transform.rotation;
        }

        private void Update()
        {
            float now = Time.unscaledTime;
            if (now >= _nextLabelCheck)
            {
                _nextLabelCheck = now + 0.2f;
                RefreshLabel();
            }

            ApplyFill(_progress);

            if (padVisual == null) return;

            float k = (now - _pulseAt) / 0.22f;
            if (k >= 1f)
            {
                if (_pulsing)
                {
                    padVisual.localScale = Vector3.one;
                    _pulsing = false;
                }

                return;
            }

            _pulsing = true;
            float scale = 1f + (pulseScale - 1f) * Mathf.Sin(Mathf.Clamp01(k) * Mathf.PI);
            padVisual.localScale = new Vector3(scale, 1f, scale);   // solo a lo ancho, como los demás pads
        }

        // Fuerza a reescribir el cartel ya (por ejemplo, al configurarse un pad que sale del pool).
        protected void RefreshLabel()
        {
            if (label == null) return;

            int key = StateKey();
            if (key == _shownKey) return;

            _shownKey = key;
            label.text = BuildText();   // una cadena corta por cambio (unas decenas por partida): no pesa
        }

        private void ApplyFill(float fraction)
        {
            if (fillBar == null) return;

            float width = Mathf.Max(0.001f, fraction);
            if (Mathf.Approximately(width, _shownFill)) return;   // en reposo no se toca el transform

            _shownFill = width;
            fillBar.localScale = new Vector3(width, 1f, 1f);
        }
    }
}
