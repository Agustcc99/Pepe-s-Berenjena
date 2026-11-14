using UnityEngine;

namespace FarmIdle
{
    // Un ayudante que se ve (F-19): un peón que da vueltas por un circuito fijo, carga una pila chica al borde del campo y la
    // deja junto al camión (o al puesto). Es puro adorno, como SellTruck: no cosecha ni vende, el ingreso sigue saliendo de la
    // fórmula de HelperManager. HelperCrew lo prende y lo apaga según el nivel comprado.
    //   - Ya está armado en la escena (sin Instantiate) y no tiene colliders: no pisa parcelas ni pads, ni frena al jugador.
    //   - Animado por matemática (como PlayerAnimator): rebota, da pasos con la distancia recorrida y gira hacia donde camina.
    //   - Tiempo de juego (con la pausa se queda quieto) y sin asignaciones por cuadro.
    public sealed class HelperWalker : MonoBehaviour
    {
        [SerializeField, Tooltip("El circuito cerrado, en posiciones locales al padre. El punto 0 es donde carga.")]
        private Vector3[] route = new Vector3[0];

        [SerializeField, Min(1), Tooltip("El punto del circuito donde deja la carga.")]
        private int unloadIndex = 2;

        [SerializeField, Tooltip("Hacia dónde mira mientras carga (el campo).")]
        private Vector3 loadFacing = Vector3.forward;

        [SerializeField, Tooltip("Hacia dónde mira mientras descarga (el camión).")]
        private Vector3 unloadFacing = Vector3.left;

        [SerializeField, Tooltip("El cuerpo: rebota y se inclina.")]
        private Transform body;

        [SerializeField] private Transform leftFoot;
        [SerializeField] private Transform rightFoot;

        [SerializeField, Tooltip("La pila que lleva en la espalda. Arranca escondida.")]
        private Transform pile;

        [SerializeField, Min(0.1f)] private float speed = 2.6f;
        [SerializeField, Min(0f), Tooltip("Cuánto se queda parado al cargar y al descargar.")]
        private float pauseSeconds = 0.8f;

        [SerializeField, Min(0.1f), Tooltip("Unidades recorridas por ciclo completo de dos pasos.")]
        private float strideLength = 1.5f;
        [SerializeField, Min(0f)] private float bobHeight = 0.08f;
        [SerializeField, Min(0f)] private float leanDegrees = 8f;
        [SerializeField, Min(0f)] private float footSwing = 0.2f;
        [SerializeField, Min(0f)] private float footLift = 0.14f;

        private const float BlendSeconds = 0.12f;
        private const float PileSeconds = 0.2f;
        private const float TurnDegreesPerSecond = 540f;

        private Vector3 _bodyRest, _leftFootRest, _rightFootRest;
        private bool _ready;
        private int _next;          // el punto hacia el que camina
        private float _pause;       // segundos que le quedan parado
        private bool _swapPending;  // a mitad de la pausa aparece o desaparece la pila
        private bool _carrying;
        private float _pileScale;
        private float _yaw;
        private float _pauseYaw;
        private float _run;         // 0 parado .. 1 caminando
        private float _phase;       // radianes del ciclo de pasos

        // Para pruebas y depuración.
        public bool Carrying => _carrying;
        public int RoutePoints => route.Length;
        public int UnloadIndex => unloadIndex;

        public Vector3 RoutePoint(int index)
        {
            return route[index];
        }

        private void Awake()
        {
            if (body != null) _bodyRest = body.localPosition;
            if (leftFoot != null) _leftFootRest = leftFoot.localPosition;
            if (rightFoot != null) _rightFootRest = rightFoot.localPosition;
        }

        // Cada vez que aparece arranca en el punto de carga, con las manos vacías.
        private void OnEnable()
        {
            _ready = route.Length >= 2 && unloadIndex < route.Length && body != null;
            if (!_ready) return;

            transform.localPosition = route[0];
            _yaw = Yaw(loadFacing, 0f);
            transform.localRotation = Quaternion.Euler(0f, _yaw, 0f);
            _carrying = false;
            _pileScale = 0f;
            if (pile != null) pile.localScale = Vector3.zero;
            _run = 0f;
            _phase = 0f;
            Arrive(0);
        }

        private void OnDisable()
        {
            if (!_ready) return;

            body.localPosition = _bodyRest;
            body.localRotation = Quaternion.identity;
            if (leftFoot != null) leftFoot.localPosition = _leftFootRest;
            if (rightFoot != null) rightFoot.localPosition = _rightFootRest;
            if (pile != null) pile.localScale = Vector3.zero;
            _carrying = false;
            _pileScale = 0f;
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        // Avanza 'dt' segundos de juego. Público para las pruebas (como PlotView.Tick).
        public void Tick(float dt)
        {
            if (!_ready || dt <= 0f) return;

            float moved = 0f;
            float targetYaw = _pauseYaw;

            if (_pause > 0f)
            {
                _pause -= dt;
                if (_swapPending && _pause <= pauseSeconds * 0.5f)
                {
                    _carrying = !_carrying;
                    _swapPending = false;
                }
            }
            else
            {
                if (_swapPending)   // con una pausa de 0 segundos el cambio se hace al arrancar
                {
                    _carrying = !_carrying;
                    _swapPending = false;
                }

                Vector3 position = transform.localPosition;
                Vector3 goal = route[_next];
                float dx = goal.x - position.x;
                float dz = goal.z - position.z;
                float distance = Mathf.Sqrt(dx * dx + dz * dz);
                float step = speed * dt;

                if (distance > 0.0001f) targetYaw = Mathf.Atan2(dx, dz) * Mathf.Rad2Deg;

                if (step >= distance)
                {
                    moved = distance;
                    transform.localPosition = goal;
                    Arrive(_next);
                }
                else
                {
                    moved = step;
                    float k = step / distance;
                    transform.localPosition = new Vector3(position.x + dx * k, goal.y, position.z + dz * k);
                }
            }

            _yaw = Mathf.MoveTowardsAngle(_yaw, targetYaw, TurnDegreesPerSecond * dt);
            transform.localRotation = Quaternion.Euler(0f, _yaw, 0f);

            Animate(dt, moved);
        }

        // Llegó al punto 'index': en el de carga y en el de descarga se queda un momento; en los demás sigue de largo.
        private void Arrive(int index)
        {
            _next = (index + 1) % route.Length;

            bool load = index == 0;
            if (!load && index != unloadIndex) return;

            _pause = pauseSeconds;
            _pauseYaw = Yaw(load ? loadFacing : unloadFacing, _yaw);
            _swapPending = _carrying != load;   // carga si venía vacío, descarga si venía cargado
        }

        private static float Yaw(Vector3 direction, float fallback)
        {
            return direction.x * direction.x + direction.z * direction.z > 0.0001f ? Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg : fallback;
        }

        private void Animate(float dt, float moved)
        {
            float blend = 1f - Mathf.Exp(-dt / BlendSeconds);
            _run += ((moved > 0f ? 1f : 0f) - _run) * blend;
            _phase = Mathf.Repeat(_phase + moved / strideLength * (Mathf.PI * 2f), Mathf.PI * 2f);

            float sin = Mathf.Sin(_phase);
            float cos = Mathf.Cos(_phase);
            float hop = Mathf.Abs(sin) * _run;

            body.localPosition = _bodyRest + new Vector3(0f, hop * bobHeight, 0f);
            body.localRotation = Quaternion.Euler(leanDegrees * _run, 0f, 0f);

            if (leftFoot != null) leftFoot.localPosition = _leftFootRest + new Vector3(0f, Mathf.Max(0f, cos) * footLift * _run, sin * footSwing * _run);
            if (rightFoot != null) rightFoot.localPosition = _rightFootRest + new Vector3(0f, Mathf.Max(0f, -cos) * footLift * _run, -sin * footSwing * _run);

            if (pile == null) return;

            float pileTarget = _carrying ? 1f : 0f;
            if (_pileScale == pileTarget) return;

            _pileScale = Mathf.MoveTowards(_pileScale, pileTarget, dt / PileSeconds);
            pile.localScale = new Vector3(_pileScale, _pileScale, _pileScale);
        }
    }
}
