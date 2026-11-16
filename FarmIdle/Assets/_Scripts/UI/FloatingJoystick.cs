using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.OnScreen;

namespace FarmIdle
{
    // Joystick en pantalla: escribe en <Gamepad>/leftStick, así que la acción "Move" lo recibe igual que a un mando.
    // Va en un Image transparente que cubre toda la pantalla (TouchArea).
    // Requiere que el Canvas esté en "Screen Space - Overlay" (el valor por defecto).
    //   - Flotante (de fábrica): aparece donde el jugador toca y desaparece al soltar.
    //   - Fijo (U-11, SettingsManager.FixedControls): queda siempre a la vista abajo, con cuatro flechas; se toca en cualquier lugar y
    //     la dirección se mide desde el centro del joystick, así que también sirve "tocar hacia donde quiero ir".
    [AddComponentMenu("Input/Floating Joystick")]
    public class FloatingJoystick : OnScreenControl, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [Header("Visual")]
        [SerializeField] private RectTransform background;   // círculo grande (base)
        [SerializeField] private RectTransform handle;       // círculo pequeño (palanca), hijo de background

        [Header("Ajustes")]
        [SerializeField] private float radius = 100f;        // recorrido máximo, en unidades de Canvas
        [SerializeField, Range(0f, 0.5f)] private float deadZone = 0.1f;

        [Header("Modo fijo")]
        [SerializeField, Tooltip("Dónde queda el joystick fijo: unidades de Canvas desde el centro del borde de abajo de la pantalla.")]
        private Vector2 fixedOffset = new Vector2(0f, 330f);

        [SerializeField, Tooltip("Las flechas que se ven solo en modo fijo. Puede faltar.")]
        private GameObject fixedArrows;

        // Campo y atributo tal como los pide el manual de Input System para controles personalizados.
        [InputControl(layout = "Vector2")]
        [SerializeField] private string m_ControlPath = "<Gamepad>/leftStick";

        protected override string controlPathInternal
        {
            get => m_ControlPath;
            set => m_ControlPath = value;
        }

        private const int NoPointer = int.MinValue;

        private int _activePointerId = NoPointer;
        private Vector2 _originScreen;
        private bool _ready;
        private bool _fixed;
        private int _screenWidth, _screenHeight;

        // Para pruebas y depuración.
        public bool IsFixed => _fixed;
        public bool IsBackgroundShown => background != null && background.gameObject.activeSelf;

        private void Start()
        {
            if (background == null || handle == null)
            {
                Debug.LogError("FloatingJoystick: asigna Background y Handle en el Inspector.", this);
                enabled = false;
                return;
            }

            _ready = true;
            ApplyMode(WantsFixed());
        }

        // El modo se lee del ajuste en cada cuadro (una comparación, sin eventos que desenganchar); el joystick fijo además se
        // recoloca si cambió el tamaño de la pantalla (girar el celular, redimensionar la ventana del navegador).
        private void Update()
        {
            if (!_ready) return;

            bool wanted = WantsFixed();
            if (wanted != _fixed) ApplyMode(wanted);
            else if (_fixed && (Screen.width != _screenWidth || Screen.height != _screenHeight)) PlaceFixed();
        }

        private static bool WantsFixed()
        {
            SettingsManager settings = SettingsManager.Instance;
            return settings != null && settings.FixedControls;
        }

        // Cambia de modo soltando lo que hubiera: el jugador no queda caminando solo.
        private void ApplyMode(bool fixedMode)
        {
            _fixed = fixedMode;
            if (_activePointerId != NoPointer)
            {
                _activePointerId = NoPointer;
                SendValueToControl(Vector2.zero);
            }

            if (fixedArrows != null && fixedArrows.activeSelf != fixedMode) fixedArrows.SetActive(fixedMode);
            background.gameObject.SetActive(fixedMode);
            if (fixedMode) PlaceFixed();
        }

        private void PlaceFixed()
        {
            _screenWidth = Screen.width;
            _screenHeight = Screen.height;

            float scale = CanvasScale();
            _originScreen = new Vector2(_screenWidth * 0.5f + fixedOffset.x * scale, fixedOffset.y * scale);
            background.position = _originScreen;
            handle.position = _originScreen;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!_ready || _activePointerId != NoPointer) return;   // ya hay un dedo manejando el joystick

            _activePointerId = eventData.pointerId;

            if (_fixed)
            {
                Steer(eventData.position);   // el centro no se mueve: tocar ya marca la dirección
                return;
            }

            _originScreen = eventData.position;

            background.gameObject.SetActive(true);
            background.position = _originScreen;
            handle.position = _originScreen;

            SendValueToControl(Vector2.zero);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.pointerId != _activePointerId) return;

            Steer(eventData.position);
        }

        private void Steer(Vector2 pointer)
        {
            float radiusInPixels = radius * CanvasScale();
            Vector2 offset = Vector2.ClampMagnitude(pointer - _originScreen, radiusInPixels);
            handle.position = _originScreen + offset;

            Vector2 value = offset / radiusInPixels;
            if (value.magnitude < deadZone) value = Vector2.zero;

            SendValueToControl(value);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId != _activePointerId) return;

            _activePointerId = NoPointer;
            SendValueToControl(Vector2.zero);

            if (_fixed) handle.position = _originScreen;
            else background.gameObject.SetActive(false);
        }

        // En un Canvas con Canvas Scaler, 1 unidad de Canvas = scaleFactor píxeles de pantalla.
        private float CanvasScale()
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            return canvas != null ? canvas.rootCanvas.scaleFactor : 1f;
        }
    }
}
