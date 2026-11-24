using System.Text;
using TMPro;
using UnityEngine;

namespace FarmIdle
{
    // El pad de un perk (Fuerza, Velocidad): parado encima se gasta un punto de nivel en ese perk, sin botones. Es un
    // IInteractable como los demás pads, así que no sabe quién lo pisa: ve un IActor, y el InteractionTrigger del mismo objeto le
    // llama a Interact en cada frame mientras el actor esté encima y pueda actuar (el jugador, solo en Playing).
    //   - Cuesta un punto, no monedas: no hay goteo. Tras una espera corta al llegar (para no gastar un punto solo por cruzar
    //     el pad) elige UN perk, y si hay más puntos sigue con uno cada 'intervalSeconds' mientras el jugador se quede.
    //   - Quien decide es ProgressionManager.TryChoosePerk (solo en Playing, solo con puntos y si el perk no se agotó); acá no
    //     se aplica ningún efecto. El pad solo ofrece la elección y la muestra.
    //   - Lo cosmético (el cartel con el nombre, lo que da cada elección, cuántas lleva y los puntos libres, y el "latido" del
    //     pad al elegir) usa tiempo sin escala y un solo Update; si falla o se apaga, la elección sigue igual.
    public sealed class PerkPad : MonoBehaviour, IInteractable, IInteractableExit
    {
        [SerializeField, Tooltip("El perk que da este pad.")]
        private PerkData perk;

        [SerializeField, Tooltip("Cartel en el mundo con el nombre del perk, cuántas veces se eligió y los puntos libres.")]
        private TMP_Text label;

        [SerializeField, Tooltip("Lo que \"late\" al elegir (el modelo del pad). Puede faltar.")]
        private Transform padVisual;

        [SerializeField, Min(0f), Tooltip("Segundos que hay que estar parado antes de gastar el primer punto. Mientras tanto el pad \"se carga\" (crece de a poco). 1,6 s para poder cruzar los pads (están juntos) sin gastar un punto de casualidad.")]
        private float startDelaySeconds = 1.6f;

        [SerializeField, Min(1f)] private float chargeScale = 1.12f;

        [SerializeField, Min(0.05f), Tooltip("Segundos entre un punto y el siguiente si el jugador se queda y hay más puntos (usa el tiempo del juego).")]
        private float intervalSeconds = 0.45f;

        [SerializeField, Min(1f)] private float pickPulseScale = 1.18f;

        private ProgressionManager _progression;
        private SalePacer _pacer;
        private readonly StringBuilder _text = new StringBuilder(80);
        private Camera _camera;
        private float _arrivedAt = -1f;
        private float _blockedSince = -1f;       // desde cuándo está parado sin poder elegir
        private bool _refusedThisVisit;
        private const float RefusedAfterSeconds = 0.6f;
        private float _pulseAt = -10f;
        private bool _pulsing;
        private int _shownKey = -1;

        public PerkData Perk => perk;
        public TMP_Text Label => label;

        private void OnEnable()
        {
            _progression = ProgressionManager.Instance;
            if (_progression == null || perk == null)
            {
                Debug.LogError("[FarmIdle] PerkPad '" + name + "': falta el ProgressionManager o el perk.", this);
                _progression = null;
                return;
            }

            _pacer = new SalePacer(intervalSeconds);
            _progression.Changed += OnProgressChanged;
            Loc.Changed += OnLanguageChanged;
            RefreshLabel(force: true);
        }

        private void OnLanguageChanged()
        {
            RefreshLabel(force: true);
        }

        private void OnDisable()
        {
            if (_progression != null)
            {
                Loc.Changed -= OnLanguageChanged;
                _progression.Changed -= OnProgressChanged;
                _progression = null;
            }

            _arrivedAt = -1f;
            _blockedSince = -1f;
            _refusedThisVisit = false;
            _pulsing = false;
            if (padVisual != null) padVisual.localScale = Vector3.one;
        }

        // IInteractable: llega en cada frame con el actor encima. Barata cuando no hay nada que elegir y sin escribir en la consola.
        public void Interact(IActor actor)
        {
            if (_progression == null) return;

            if (!_progression.CanChoose(perk))
            {
                _arrivedAt = -1f;   // sin puntos (o perk agotado): la próxima visita vuelve a esperar

                // Parado un rato sin poder elegir: una sola vez por visita se avisa "no se puede" (el audio lo escucha).
                float blockedNow = Time.time;
                if (_blockedSince < 0f) _blockedSince = blockedNow;
                if (!_refusedThisVisit && blockedNow - _blockedSince >= RefusedAfterSeconds)
                {
                    _refusedThisVisit = true;
                    _progression.ReportRefused();
                }

                return;
            }

            _blockedSince = -1f;

            float now = Time.time;
            if (_arrivedAt < 0f) _arrivedAt = now;
            if (now - _arrivedAt < startDelaySeconds || !_pacer.IsReady(now)) return;

            if (_progression.TryChoosePerk(perk))
            {
                _pacer.Commit(now);
                _pulseAt = Time.unscaledTime;
                _refusedThisVisit = true;   // eligió en esta visita: quedarse sin puntos después no es un "no se puede"
            }
        }

        // IInteractableExit: el jugador se fue; la próxima visita vuelve a esperar antes de gastar un punto.
        public void ActorLeft(IActor actor)
        {
            _arrivedAt = -1f;
            _blockedSince = -1f;
            _refusedThisVisit = false;
            if (_pacer != null) _pacer.Reset();
        }

        private void OnProgressChanged(ProgressChange change)
        {
            // La XP cambia con cada venta, pero el cartel solo cambia cuando cambian los puntos libres o las elecciones.
            RefreshLabel(force: change.Kind == ProgressChangeKind.Restored);
        }

        private void RefreshLabel(bool force)
        {
            if (label == null || _progression == null) return;

            int picks = _progression.PicksOf(perk);
            int points = _progression.PerkPoints;
            int key = picks * 1000 + Mathf.Min(points, 999);
            if (!force && key == _shownKey) return;

            _shownKey = key;
            char newLine = (char)10;
            _text.Clear();
            _text.Append(perk.DisplayName).Append(newLine);

            if (_progression.IsMaxed(perk))
            {
                _text.Append("<size=60%>").Append(Loc.Get("pad.max")).Append(" (").Append(picks).Append('/').Append(perk.MaxPicks).Append(")</size>");
            }
            else
            {
                _text.Append("<size=60%>").Append(perk.DescribePick()).Append(" (").Append(picks).Append('/').Append(perk.MaxPicks).Append(")</size>").Append(newLine);
                _text.Append(points > 0 ? "<color=#FFFFFF>" : "<color=#DCDCC8>").Append(Loc.Get("pad.points")).Append(' ').Append(points).Append("</color>");
            }

            label.text = _text.ToString();   // una cadena corta por cambio (unas decenas por partida): no pesa
        }

        // Billboard: el cartel mira siempre a la cámara.
        private void LateUpdate()
        {
            if (label == null || !label.enabled) return;

            if (_camera == null) _camera = Camera.main;
            if (_camera != null) label.transform.rotation = _camera.transform.rotation;
        }

        private void Update()
        {
            if (padVisual == null) return;

            float k = (Time.unscaledTime - _pulseAt) / 0.22f;
            if (k >= 1f)
            {
                // Parado encima el pad "se carga": crece de a poco hasta gastar el punto (y queda así mientras el jugador se quede).
                float charge = _arrivedAt >= 0f && _progression != null && startDelaySeconds > 0f
                    ? Mathf.Clamp01((Time.time - _arrivedAt) / startDelaySeconds)
                    : 0f;
                if (charge > 0f)
                {
                    float grow = 1f + (chargeScale - 1f) * charge;
                    padVisual.localScale = new Vector3(grow, 1f, grow);
                    _pulsing = true;
                    return;
                }

                // En reposo no se toca el transform.
                if (_pulsing)
                {
                    padVisual.localScale = Vector3.one;
                    _pulsing = false;
                }

                return;
            }

            _pulsing = true;
            float scale = 1f + (pickPulseScale - 1f) * Mathf.Sin(Mathf.Clamp01(k) * Mathf.PI);
            padVisual.localScale = new Vector3(scale, 1f, scale);   // solo a lo ancho, como los demás pads
        }
    }
}
