using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.OnScreen;

namespace FarmIdle
{
    // Joystick flotante: aparece donde el jugador toca y escribe en <Gamepad>/leftStick,
    // así que la acción "Move" lo recibe igual que a un mando.
    // Va en un Image transparente que cubre toda la pantalla (TouchArea).
    // Requiere que el Canvas esté en "Screen Space - Overlay" (el valor por defecto).
    [AddComponentMenu("Input/Floating Joystick")]
    public class FloatingJoystick : OnScreenControl, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [Header("Visual")]
        [SerializeField] private RectTransform background;   // círculo grande (base)
        [SerializeField] private RectTransform handle;       // círculo pequeño (palanca), hijo de background

        [Header("Ajustes")]
        [SerializeField] private float radius = 100f;        // recorrido máximo, en unidades de Canvas
        [SerializeField, Range(0f, 0.5f)] private float deadZone = 0.1f;

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

        private void Start()
        {
            if (background == null || handle == null)
            {
                Debug.LogError("FloatingJoystick: asigna Background y Handle en el Inspector.", this);
                enabled = false;
                return;
            }

            background.gameObject.SetActive(false);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_activePointerId != NoPointer) return;   // ya hay un dedo manejando el joystick

            _activePointerId = eventData.pointerId;
            _originScreen = eventData.position;

            background.gameObject.SetActive(true);
            background.position = _originScreen;
            handle.position = _originScreen;

            SendValueToControl(Vector2.zero);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.pointerId != _activePointerId) return;

            float radiusInPixels = radius * CanvasScale();
            Vector2 offset = Vector2.ClampMagnitude(eventData.position - _originScreen, radiusInPixels);
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
            background.gameObject.SetActive(false);
        }

        // En un Canvas con Canvas Scaler, 1 unidad de Canvas = scaleFactor píxeles de pantalla.
        private float CanvasScale()
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            return canvas != null ? canvas.rootCanvas.scaleFactor : 1f;
        }
    }
}
