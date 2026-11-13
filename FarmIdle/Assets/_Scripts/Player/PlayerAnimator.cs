using UnityEngine;

namespace FarmIdle
{
    // La "vida" del granjero, por matemática simple (sin Animator ni clips): al correr el cuerpo rebota, se estira, se inclina hacia
    // adelante y se ladea en los giros; los pies dan pasos y las manos se balancean al revés; quieto, respira. Todo es cosmético: mueve
    // solo los hijos visuales (nunca la raíz ni el CharacterController) y no cambia ninguna regla.
    //   - La velocidad se mide del movimiento real de la raíz, así que sirve con cualquier cosa que la mueva (joystick, teclado, pruebas).
    //   - El paso avanza con la DISTANCIA recorrida: con Turbo o mejoras de velocidad los pies no patinan.
    //   - Sin asignaciones por cuadro (solo estructuras). Con el juego en pausa (deltaTime 0) no hace nada.
    public sealed class PlayerAnimator : MonoBehaviour
    {
        [SerializeField, Tooltip("El cuerpo (la cápsula). Rebota, se estira y se inclina.")]
        private Transform body;

        [SerializeField] private Transform leftFoot;
        [SerializeField] private Transform rightFoot;
        [SerializeField] private Transform leftHand;
        [SerializeField] private Transform rightHand;

        [Header("Correr")]
        [SerializeField, Min(0.1f), Tooltip("Velocidad (u/s) a la que la animación llega al máximo.")]
        private float fullSpeed = 5f;
        [SerializeField, Min(0.1f), Tooltip("Unidades recorridas por ciclo completo de dos pasos.")]
        private float strideLength = 2.2f;
        [SerializeField, Min(0f)] private float bobHeight = 0.12f;
        [SerializeField, Min(0f)] private float stretch = 0.06f;
        [SerializeField, Min(0f)] private float leanDegrees = 10f;
        [SerializeField, Min(0f)] private float rollDegrees = 12f;
        [SerializeField, Min(0f)] private float footSwing = 0.32f;
        [SerializeField, Min(0f)] private float footLift = 0.22f;
        [SerializeField, Min(0f)] private float handSwing = 0.3f;

        [Header("Quieto")]
        [SerializeField, Min(0f)] private float breatheAmount = 0.02f;
        [SerializeField, Min(0f)] private float breatheSpeed = 2.4f;

        private const float BlendSeconds = 0.12f;
        private const float TurnForFullRoll = 360f;   // grados por segundo de giro que dan el ladeo máximo

        private Vector3 _bodyRestPosition;
        private Vector3 _bodyRestScale;
        private Vector3 _leftFootRest, _rightFootRest, _leftHandRest, _rightHandRest;
        private Vector3 _lastPosition;
        private float _lastYaw;
        private float _run;       // 0 quieto .. 1 corriendo a fullSpeed o más
        private float _roll;      // -1 .. 1, ladeo suavizado
        private float _phase;     // radianes del ciclo de pasos

        // Para pruebas y depuración.
        public float RunAmount => _run;
        public float Phase => _phase;

        private void Awake()
        {
            if (body != null)
            {
                _bodyRestPosition = body.localPosition;
                _bodyRestScale = body.localScale;
            }

            if (leftFoot != null) _leftFootRest = leftFoot.localPosition;
            if (rightFoot != null) _rightFootRest = rightFoot.localPosition;
            if (leftHand != null) _leftHandRest = leftHand.localPosition;
            if (rightHand != null) _rightHandRest = rightHand.localPosition;
        }

        private void OnEnable()
        {
            _lastPosition = transform.position;
            _lastYaw = transform.eulerAngles.y;
            _run = 0f;
            _roll = 0f;
        }

        private void OnDisable()
        {
            // Todo vuelve al reposo: quien mire la escena con el componente apagado ve el modelo tal cual lo armó el builder.
            if (body != null)
            {
                body.localPosition = _bodyRestPosition;
                body.localScale = _bodyRestScale;
                body.localRotation = Quaternion.identity;
            }

            if (leftFoot != null) leftFoot.localPosition = _leftFootRest;
            if (rightFoot != null) rightFoot.localPosition = _rightFootRest;
            if (leftHand != null) leftHand.localPosition = _leftHandRest;
            if (rightHand != null) rightHand.localPosition = _rightHandRest;
        }

        // Después del movimiento (PlayerMovement mueve en Update).
        private void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || body == null) return;

            Vector3 position = transform.position;
            float dx = position.x - _lastPosition.x;
            float dz = position.z - _lastPosition.z;
            _lastPosition = position;
            float distance = Mathf.Sqrt(dx * dx + dz * dz);
            if (distance > 3f) distance = 0f;   // un teletransporte (cargar partida, pruebas) no es correr

            float yaw = transform.eulerAngles.y;
            float turnRate = Mathf.DeltaAngle(_lastYaw, yaw) / dt;
            _lastYaw = yaw;

            float blend = 1f - Mathf.Exp(-dt / BlendSeconds);
            _run += (Mathf.Clamp01(distance / dt / fullSpeed) - _run) * blend;
            _roll += (Mathf.Clamp(turnRate / TurnForFullRoll, -1f, 1f) * _run - _roll) * blend;
            _phase = Mathf.Repeat(_phase + distance / strideLength * (Mathf.PI * 2f), Mathf.PI * 2f);

            float sin = Mathf.Sin(_phase);
            float cos = Mathf.Cos(_phase);

            // Cuerpo: dos rebotes por ciclo (uno por paso), estirado en el aire; quieto, respira.
            float hop = Mathf.Abs(sin) * _run;
            float breathe = Mathf.Sin(Time.time * breatheSpeed) * breatheAmount * (1f - _run);
            float tall = 1f + hop * stretch + breathe;
            float wide = 1f - (hop * stretch + breathe) * 0.5f;
            body.localPosition = _bodyRestPosition + new Vector3(0f, hop * bobHeight, 0f);
            body.localScale = new Vector3(_bodyRestScale.x * wide, _bodyRestScale.y * tall, _bodyRestScale.z * wide);
            body.localRotation = Quaternion.Euler(leanDegrees * _run, 0f, -rollDegrees * _roll);

            // Pies: uno adelante y el otro atrás; el que va hacia adelante se levanta.
            if (leftFoot != null) leftFoot.localPosition = _leftFootRest + new Vector3(0f, Mathf.Max(0f, cos) * footLift * _run, sin * footSwing * _run);
            if (rightFoot != null) rightFoot.localPosition = _rightFootRest + new Vector3(0f, Mathf.Max(0f, -cos) * footLift * _run, -sin * footSwing * _run);

            // Manos: al revés que el pie de su lado, y acompañan el rebote.
            float lift = hop * bobHeight;
            if (leftHand != null) leftHand.localPosition = _leftHandRest + new Vector3(0f, lift, -sin * handSwing * _run);
            if (rightHand != null) rightHand.localPosition = _rightHandRest + new Vector3(0f, lift, sin * handSwing * _run);
        }
    }
}
