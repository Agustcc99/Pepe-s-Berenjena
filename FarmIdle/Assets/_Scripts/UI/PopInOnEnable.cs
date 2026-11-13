using UnityEngine;

namespace FarmIdle
{
    // La tarjeta de un panel "salta" al abrirse: arranca un poco más chica, se pasa apenas y queda en 1. Cosmético, con tiempo sin
    // escala (los paneles se abren con el juego en pausa) y solo corre Update mientras dura.
    public sealed class PopInOnEnable : MonoBehaviour
    {
        [SerializeField, Range(0.5f, 1f)] private float fromScale = 0.86f;
        [SerializeField, Min(1f)] private float overshootScale = 1.03f;
        [SerializeField, Min(0.01f)] private float seconds = 0.2f;

        private float _startedAt;
        private bool _playing;

        // Para pruebas y depuración.
        public bool IsPlaying => _playing;

        private void OnEnable()
        {
            _startedAt = Time.unscaledTime;
            _playing = true;
            transform.localScale = new Vector3(fromScale, fromScale, 1f);
        }

        private void OnDisable()
        {
            _playing = false;
            transform.localScale = Vector3.one;
        }

        private void Update()
        {
            if (!_playing) return;

            float k = (Time.unscaledTime - _startedAt) / seconds;
            if (k >= 1f)
            {
                transform.localScale = Vector3.one;
                _playing = false;
                return;
            }

            float scale = k < 0.7f
                ? Mathf.Lerp(fromScale, overshootScale, k / 0.7f)
                : Mathf.Lerp(overshootScale, 1f, (k - 0.7f) / 0.3f);
            transform.localScale = new Vector3(scale, scale, 1f);
        }
    }
}
