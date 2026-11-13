using System.Text;
using TMPro;
using UnityEngine;

namespace FarmIdle
{
    // Base de los pads de compra (desbloquear una zona, comprar una mejora): un pad en el suelo que se paga solo parándose
    // encima, sin botones. Es un IInteractable, así que no sabe quién lo pisa (el jugador hoy, un ayudante mañana): ve un
    // IActor, y el InteractionTrigger del mismo objeto le llama a Interact en cada frame mientras el actor esté encima y
    // pueda actuar (el jugador, solo en Playing: en pausa y con un anuncio no compra).
    //
    // El pago "gotea": un PurchaseDrip decide cuántas monedas ofrecer en cada frame (espera una fracción de segundo al llegar
    // y después paga todo el costo en ~1,2 s) y ProgressionManager.TryPay las cobra por EconomyManager.TrySpend. Lo pagado se
    // conserva en el progreso: si el jugador se va (IInteractableExit -> ActorLeft), el pago se corta y al volver sigue
    // desde donde quedó, sin perder las monedas ya entregadas.
    // Una compra por visita: al completarse una, el pad deja de cobrar hasta que el jugador se baje y vuelva a subir. Así quedarse
    // parado encima no encadena los niveles siguientes de una mejora ni se lleva todas las monedas sin querer.
    //
    // Lo cosmético no decide nada: el cartel en el mundo (nombre y monedas que faltan, que bajan mientras se paga), la barra
    // que se llena y el "latido" del pad. Usan tiempo sin escala y un solo Update; si fallan o se apagan, la compra
    // sigue igual. Lo concreto de cada pad (zona o mejora) lo ponen las subclases.
    public abstract class PurchasePad : MonoBehaviour, IInteractable, IInteractableExit, IGuidePoint
    {
        // IGuidePoint: la flecha del borde de la pantalla (UI) apunta acá cuando la compra ya se puede pagar entera.
        public Vector3 Position => transform.position;
        public int GuidePriority => GuidePoints.BuyPriority;

        public bool WantsGuide
        {
            get
            {
                if (_progression == null || _waitingForExit || !IsPurchasable()) return false;

                EconomyManager economy = EconomyManager.Instance;
                if (economy == null) return false;

                ReadPrice(out long cost, out long paid);
                return cost > paid && economy.Balance >= cost - paid;
            }
        }

        [SerializeField, Tooltip("Ritmo del pago (GameConfig: segundos que tarda una compra y espera inicial).")]
        private GameConfig config;

        [SerializeField, Tooltip("Cartel en el mundo con el nombre de la compra y las monedas que faltan.")]
        private TMP_Text label;

        [SerializeField, Tooltip("Barra que se llena (escala en X, de 0 a 1) con lo ya pagado. Puede faltar.")]
        private Transform fillBar;

        [SerializeField, Tooltip("Lo que \"late\" al pagar (el modelo del pad). Puede faltar.")]
        private Transform padVisual;

        [SerializeField, Min(1f)] private float payPulseScale = 1.04f;
        [SerializeField, Min(1f)] private float completedPulseScale = 1.2f;

        // Se completó una compra (mejora o zona) en un pad; lleva la posición del pad. Lo escuchan los efectos (BurstFeedback).
        public static event System.Action<Vector3> PurchaseCompleted;

        private PurchaseDrip _drip;
        private ProgressionManager _progression;
        private readonly StringBuilder _text = new StringBuilder(64);
        private string _title = string.Empty;
        private long _shownRemaining = -1;
        private Camera _camera;
        private float _pulseAt = -10f;
        private float _pulseScale = 1f;
        private bool _pulsing;
        private bool _configErrorLogged;
        private bool _justCompleted;      // este pad terminó una compra durante el Interact en curso
        private bool _waitingForExit;     // compró en esta visita: no cobra más hasta que el actor se vaya
        private float _visitStartedAt;    // cuándo llegó el actor (tiempo del juego)
        private bool _refusedThisVisit;   // ya se avisó "no se puede" en esta visita

        private const float RefusedAfterSeconds = 0.35f;   // cuánto después de la espera inicial, sin monedas, se avisa

        // Para pruebas y depuración.
        public bool WaitingForExit => _waitingForExit;
        public TMP_Text Label => label;
        public PurchaseDrip Drip => _drip;

        protected ProgressionManager Progression => _progression;

        // ---------- Lo que define cada subclase ----------

        // false cuando no hay nada más que comprar acá (zona ya abierta, mejora al máximo).
        protected abstract bool IsPurchasable();

        // Costo de la compra en curso y lo ya pagado.
        protected abstract void ReadPrice(out long cost, out long paid);

        // Ofrece monedas a ProgressionManager.TryPay y devuelve las que se gastaron.
        protected abstract long Pay(long coins);

        // Primera línea del cartel (nombre, y el efecto que da). Se pide cuando cambia el estado, no en cada pago.
        protected abstract string BuildTitle();

        // true si este cambio del progreso es la compra de ESTE pad completada.
        protected abstract bool IsCompletion(ProgressChange change);

        protected virtual void OnCompleted() { }

        // ---------- Ciclo de vida ----------

        protected virtual void OnEnable()
        {
            _progression = ProgressionManager.Instance;
            if (_progression == null)
            {
                Debug.LogError("[FarmIdle] PurchasePad '" + name + "': no hay ProgressionManager. Arrancá el juego desde la escena Bootstrap.", this);
                return;
            }

            _progression.Changed += OnProgressChanged;
            Loc.Changed += RefreshAll;
            RefreshAll();
            GuidePoints.Register(this);
        }

        protected virtual void OnDisable()
        {
            GuidePoints.Unregister(this);

            if (_progression != null)
            {
                Loc.Changed -= RefreshAll;
                _progression.Changed -= OnProgressChanged;
                _progression = null;
            }

            if (_drip != null) _drip.Reset();

            _waitingForExit = false;
            _refusedThisVisit = false;
            _pulsing = false;
            if (padVisual != null) padVisual.localScale = Vector3.one;
        }

        // IInteractable: en cada frame con el actor encima. Barata cuando no hay nada que pagar y sin escribir en la consola.
        public void Interact(IActor actor)
        {
            if (_progression == null || _waitingForExit || !IsPurchasable() || !EnsureDrip()) return;

            EconomyManager economy = EconomyManager.Instance;
            if (economy == null) return;

            ReadPrice(out long cost, out long paid);
            float now = Time.time;
            if (!_drip.IsActive) _visitStartedAt = now;

            long offer = _drip.Step(now, cost - paid, economy.Balance);
            if (offer <= 0L)
            {
                // Parado un rato sin una sola moneda: una sola vez por visita se avisa "no se puede" (el audio lo escucha).
                if (!_refusedThisVisit && economy.Balance <= 0L && cost > paid && now - _visitStartedAt >= _drip.StartDelaySeconds + RefusedAfterSeconds)
                {
                    _refusedThisVisit = true;
                    _progression.ReportRefused();
                }

                return;
            }

            _justCompleted = false;
            if (Pay(offer) > 0L) Pulse(payPulseScale);

            if (_justCompleted)
            {
                _justCompleted = false;
                _waitingForExit = true;
            }
        }

        // IInteractableExit: el jugador se fue; la próxima visita vuelve a esperar antes de pagar.
        public void ActorLeft(IActor actor)
        {
            _waitingForExit = false;
            _refusedThisVisit = false;
            if (_drip != null) _drip.Reset();
        }

        private bool EnsureDrip()
        {
            if (_drip != null) return true;

            if (config == null)
            {
                if (!_configErrorLogged)
                {
                    _configErrorLogged = true;
                    Debug.LogError("[FarmIdle] El pad de compra '" + name + "' no puede cobrar: falta el GameConfig.", this);
                }

                return false;
            }

            _drip = new PurchaseDrip(config.PurchaseSeconds, config.PurchaseStartDelaySeconds);
            return true;
        }

        // ---------- Progreso ----------

        private void OnProgressChanged(ProgressChange change)
        {
            if (IsCompletion(change))
            {
                _justCompleted = true;
                if (_drip != null) _drip.Reset();
                Pulse(completedPulseScale);
                RefreshAll();
                OnCompleted();
                PurchaseCompleted?.Invoke(transform.position);
                return;
            }

            if (change.Kind == ProgressChangeKind.Restored) RefreshAll();
            else RefreshAmount();
        }

        // ---------- Cartel y barra (cosmético) ----------

        private void RefreshAll()
        {
            _title = BuildTitle();
            _shownRemaining = -1;
            RefreshAmount();
        }

        private void RefreshAmount()
        {
            if (_progression == null) return;

            bool purchasable = IsPurchasable();
            ReadPrice(out long cost, out long paid);
            long remaining = purchasable ? System.Math.Max(0L, cost - paid) : 0L;

            if (fillBar != null)
            {
                float fraction = !purchasable ? 1f : (cost > 0L ? Mathf.Clamp01(paid / (float)cost) : 1f);
                fillBar.localScale = new Vector3(Mathf.Max(0.001f, fraction), 1f, 1f);
            }

            long key = purchasable ? remaining : -2L;   // -2: "MAX"; distingue el primer dibujo (-1) del cartel ya escrito
            if (label == null || key == _shownRemaining) return;

            _shownRemaining = key;
            _text.Clear();
            _text.Append("<size=60%>").Append(_title).Append("</size>\n");
            if (purchasable) _text.Append(remaining);
            else _text.Append(Loc.Get("pad.max"));
            label.text = _text.ToString();   // una cadena corta por cambio de monedas (unas decenas por compra): no pesa
        }

        private void Pulse(float scale)
        {
            _pulseAt = Time.unscaledTime;
            _pulseScale = scale;
        }

        // Billboard: el cartel mira siempre a la cámara (la del juego es fija, pero así no depende de su ángulo).
        private void LateUpdate()
        {
            if (label == null || !label.enabled) return;

            if (_camera == null) _camera = Camera.main;
            if (_camera != null) label.transform.rotation = _camera.transform.rotation;
        }

        private void Update()
        {
            if (padVisual == null) return;

            float k = (Time.unscaledTime - _pulseAt) / 0.2f;
            if (k >= 1f)
            {
                // En reposo no se toca el transform.
                if (_pulsing)
                {
                    padVisual.localScale = Vector3.one;
                    _pulsing = false;
                }

                return;
            }

            _pulsing = true;
            float scale = 1f + (_pulseScale - 1f) * Mathf.Sin(Mathf.Clamp01(k) * Mathf.PI);
            padVisual.localScale = new Vector3(scale, 1f, scale);   // solo a lo ancho, como el pad de venta
        }
    }
}
