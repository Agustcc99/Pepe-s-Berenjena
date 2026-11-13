using Unity.Cinemachine;
using UnityEngine;

namespace FarmIdle
{
    // El "jugo" de la cámara, con la DISTANCIA del Position Composer (el campo de visión es del Turbo: TurboCameraEffect), así los dos
    // efectos se suman sin pisarse:
    //   - al abrir una zona la cámara se aleja un momento para mostrar lo nuevo y vuelve;
    //   - al subir de nivel da un golpecito hacia adentro;
    //   - corriendo se aleja apenas, para ver más adelante.
    // Solo mira a ProgressionManager (Managers) y al transform que sigue la cámara. Cosmético: tiempo sin escala, y en reposo no escribe.
    [RequireComponent(typeof(CinemachinePositionComposer))]
    public sealed class CameraJuice : MonoBehaviour
    {
        [SerializeField, Min(0f), Tooltip("Unidades que se aleja al abrir una zona.")]
        private float zoneZoomOut = 5f;
        [SerializeField, Min(0f), Tooltip("Segundos que se queda alejada antes de volver.")]
        private float zoneHoldSeconds = 1.4f;
        [SerializeField, Min(0f), Tooltip("Unidades que se acerca de golpe al subir de nivel.")]
        private float levelPunch = 1.1f;
        [SerializeField, Min(0f), Tooltip("Unidades que se aleja corriendo.")]
        private float runZoomOut = 1.2f;
        [SerializeField, Min(0.01f)] private float smoothSeconds = 0.45f;
        [SerializeField, Min(0.01f)] private float punchSeconds = 0.35f;

        private CinemachinePositionComposer _composer;
        private CinemachineCamera _camera;
        private ProgressionManager _progression;
        private float _baseDistance;
        private float _extra;
        private float _velocity;
        private float _holdUntil = -1f;
        private float _punchAt = -10f;
        private Vector3 _lastTargetPosition;
        private bool _hasTarget;
        private float _applied;

        // Para pruebas y depuración.
        public float ExtraDistance => _applied;

        private void Awake()
        {
            _composer = GetComponent<CinemachinePositionComposer>();
            _camera = GetComponent<CinemachineCamera>();
            _baseDistance = _composer.CameraDistance;
        }

        private void OnEnable()
        {
            _progression = ProgressionManager.Instance;   // opcional: sin él (una escena suelta) solo queda el efecto de correr
            if (_progression == null) return;

            _progression.Changed += OnProgressChanged;
            _progression.LevelChanged += OnLevelChanged;
        }

        private void OnDisable()
        {
            if (_progression != null)
            {
                _progression.Changed -= OnProgressChanged;
                _progression.LevelChanged -= OnLevelChanged;
                _progression = null;
            }

            _extra = 0f;
            _velocity = 0f;
            _applied = 0f;
            _holdUntil = -1f;
            _punchAt = -10f;
            _hasTarget = false;
            if (_composer != null) _composer.CameraDistance = _baseDistance;
        }

        private void OnProgressChanged(ProgressChange change)
        {
            if (change.Kind == ProgressChangeKind.ZoneUnlocked) _holdUntil = Time.unscaledTime + zoneHoldSeconds;
        }

        private void OnLevelChanged(int level)
        {
            _punchAt = Time.unscaledTime;
        }

        private void LateUpdate()
        {
            float now = Time.unscaledTime;
            float dt = Time.unscaledDeltaTime;
            if (dt <= 0f) return;

            // Corriendo: se mide del objetivo que sigue la cámara (el jugador), sin conocer su componente.
            float target = 0f;
            Transform followed = _camera != null ? _camera.Target.TrackingTarget : null;
            if (followed != null)
            {
                Vector3 position = followed.position;
                if (_hasTarget && Time.deltaTime > 0f)
                {
                    float dx = position.x - _lastTargetPosition.x;
                    float dz = position.z - _lastTargetPosition.z;
                    if (dx * dx + dz * dz > 0.000001f) target = runZoomOut;
                }

                _lastTargetPosition = position;
                _hasTarget = true;
            }

            if (now < _holdUntil) target = zoneZoomOut;

            _extra = Mathf.SmoothDamp(_extra, target, ref _velocity, smoothSeconds, float.PositiveInfinity, dt);
            if (Mathf.Abs(_extra - target) < 0.005f && Mathf.Abs(_velocity) < 0.005f)
            {
                _extra = target;
                _velocity = 0f;
            }

            float punch = 0f;
            float k = (now - _punchAt) / punchSeconds;
            if (k >= 0f && k < 1f) punch = -levelPunch * Mathf.Sin(k * Mathf.PI) * (1f - k);

            float total = _extra + punch;
            if (Mathf.Approximately(total, _applied)) return;   // en reposo no se toca el composer

            _applied = total;
            _composer.CameraDistance = _baseDistance + total;
        }
    }
}
