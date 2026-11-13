using UnityEngine;

namespace FarmIdle
{
    // Mueve al granjero con un CharacterController.
    // El movimiento es relativo a la cámara: "arriba" en el joystick = hacia el fondo de la pantalla.
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(PlayerInputReader))]
    public class PlayerMovement : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 5f;     // unidades por segundo
        [SerializeField] private float turnSpeed = 720f;   // grados por segundo
        [SerializeField] private float gravity = -20f;

        private CharacterController _controller;
        private PlayerInputReader _input;
        private Transform _cameraTransform;
        private float _verticalVelocity;
        private ProgressionManager _progression;
        private BuffManager _buffs;
        private float _speedMultiplier = 1f;   // lo que dan las mejoras y los perks de velocidad, permanente (1 = sin mejoras)

        // La velocidad con que camina ahora: la base por el efecto permanente de las mejoras (que llega por ProgressionManager) y por
        // el buff temporal de velocidad (BuffManager), con el tope final del GameConfig. Sin BuffManager (una escena de prueba suelta)
        // no hay buff ni tope.
        public float EffectiveMoveSpeed => _buffs != null ? _buffs.FinalMoveSpeed(moveSpeed, _speedMultiplier) : moveSpeed * _speedMultiplier;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _input = GetComponent<PlayerInputReader>();
        }

        private void OnEnable()
        {
            // Opcional: sin BuffManager (una escena de prueba suelta) no hay buff de velocidad. Se consulta en cada frame, sin eventos.
            _buffs = BuffManager.Instance;

            // Opcional: sin ProgressionManager (una escena de prueba suelta) camina a la velocidad base.
            _progression = ProgressionManager.Instance;
            if (_progression == null) return;

            _progression.ModifiersChanged += OnModifiersChanged;
            _speedMultiplier = _progression.Modifiers.MoveSpeed;
        }

        private void OnDisable()
        {
            if (_progression != null)
            {
                _progression.ModifiersChanged -= OnModifiersChanged;
                _progression = null;
            }

            _buffs = null;
            _speedMultiplier = 1f;
        }

        private void OnModifiersChanged()
        {
            _speedMultiplier = _progression.Modifiers.MoveSpeed;
        }

        private void Start()
        {
            // La cámara de Cinemachine es la Main Camera. Se guarda una vez para no buscarla cada frame.
            Camera mainCamera = Camera.main;
            if (mainCamera != null) _cameraTransform = mainCamera.transform;
        }

        private void Update()
        {
            Vector3 direction = ToWorldDirection(_input.Move);

            if (_controller.isGrounded && _verticalVelocity < 0f)
            {
                _verticalVelocity = -2f;   // pegado al suelo
            }
            _verticalVelocity += gravity * Time.deltaTime;

            Vector3 velocity = direction * EffectiveMoveSpeed;
            velocity.y = _verticalVelocity;
            _controller.Move(velocity * Time.deltaTime);

            if (direction.sqrMagnitude > 0.0001f)
            {
                Quaternion target = Quaternion.LookRotation(direction, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, target, turnSpeed * Time.deltaTime);
            }
        }

        // Convierte el Vector2 del input en una dirección sobre el suelo, según hacia dónde mira la cámara.
        private Vector3 ToWorldDirection(Vector2 input)
        {
            if (_cameraTransform == null)
            {
                return new Vector3(input.x, 0f, input.y);
            }

            Vector3 forward = _cameraTransform.forward;
            forward.y = 0f;
            forward.Normalize();

            Vector3 right = _cameraTransform.right;
            right.y = 0f;
            right.Normalize();

            return forward * input.y + right * input.x;
        }
    }
}
