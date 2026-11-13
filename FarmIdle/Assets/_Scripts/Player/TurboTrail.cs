using UnityEngine;

namespace FarmIdle
{
    // La estela del Turbo: un TrailRenderer colgado del jugador que solo deja rastro mientras el buff de velocidad está activo. Junto con
    // el campo de visión de la cámara (TurboCameraEffect) hace que se note la velocidad aunque el tope final sea 11 u/s. Solo mira a
    // BuffManager: no cambia ninguna regla. Sin BuffManager (una escena suelta) no emite nada.
    [RequireComponent(typeof(TrailRenderer))]
    public sealed class TurboTrail : MonoBehaviour
    {
        private TrailRenderer _trail;
        private BuffManager _buffs;

        // Para pruebas y depuración.
        public bool IsEmitting => _trail != null && _trail.emitting;

        private void Awake()
        {
            _trail = GetComponent<TrailRenderer>();
            _trail.emitting = false;
        }

        private void OnEnable()
        {
            _buffs = BuffManager.Instance;
            if (_buffs != null) _buffs.Changed += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            if (_buffs != null)
            {
                _buffs.Changed -= Refresh;
                _buffs = null;
            }

            _trail.emitting = false;
            _trail.Clear();
        }

        private void Refresh()
        {
            bool turbo = _buffs != null && _buffs.MoveSpeedMultiplier > 1.001f;
            if (turbo == _trail.emitting) return;

            if (!turbo) _trail.Clear();   // sin rastro colgando al terminar
            _trail.emitting = turbo;
        }
    }
}
