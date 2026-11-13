using Unity.Cinemachine;
using UnityEngine;

namespace FarmIdle
{
    // El "se nota la velocidad" del Turbo: mientras el buff de velocidad está activo la cámara abre un poco el campo de visión y al
    // terminar vuelve. Con el tope final de 11 u/s el granjero no puede ir mucho más rápido, pero el efecto de cámara hace que se SIENTA
    // la diferencia. Va en el objeto de la Cinemachine Camera (que es quien manda el campo de visión: el Brain pisa el de la cámara).
    //   - Solo mira a BuffManager (Managers): no conoce la UI ni el entorno, y no cambia ninguna regla del juego.
    //   - Usa tiempo sin escala (la transición no se congela si el juego se pausa por un anuncio) y no escribe el lente si el campo de
    //     visión no cambió: en reposo no hace nada.
    [RequireComponent(typeof(CinemachineCamera))]
    public sealed class TurboCameraEffect : MonoBehaviour
    {
        [SerializeField, Min(0f), Tooltip("Grados que se abre el campo de visión con el Turbo activo.")]
        private float extraFieldOfView = 9f;

        [SerializeField, Min(0.01f), Tooltip("Segundos que tarda en abrirse o cerrarse.")]
        private float smoothSeconds = 0.35f;

        private CinemachineCamera _camera;
        private BuffManager _buffs;
        private float _baseFov;
        private float _extra;
        private float _velocity;

        // Para pruebas y depuración.
        public float ExtraFieldOfView => _extra;

        private void Awake()
        {
            _camera = GetComponent<CinemachineCamera>();
            _baseFov = _camera.Lens.FieldOfView;
        }

        private void OnEnable()
        {
            _buffs = BuffManager.Instance;   // opcional: sin él (una escena suelta) no hay Turbo y no hace nada
        }

        private void OnDisable()
        {
            _buffs = null;
            _extra = 0f;
            _velocity = 0f;
            if (_camera != null) SetFieldOfView(_baseFov);
        }

        private void LateUpdate()
        {
            float target = _buffs != null && _buffs.MoveSpeedMultiplier > 1.001f ? extraFieldOfView : 0f;
            if (Mathf.Approximately(_extra, target) && Mathf.Approximately(_velocity, 0f)) return;

            _extra = Mathf.SmoothDamp(_extra, target, ref _velocity, smoothSeconds, float.PositiveInfinity, Time.unscaledDeltaTime);
            if (Mathf.Abs(_extra - target) < 0.01f && Mathf.Abs(_velocity) < 0.01f)
            {
                _extra = target;
                _velocity = 0f;
            }

            SetFieldOfView(_baseFov + _extra);
        }

        private void SetFieldOfView(float fieldOfView)
        {
            LensSettings lens = _camera.Lens;
            lens.FieldOfView = fieldOfView;
            _camera.Lens = lens;
        }
    }
}
