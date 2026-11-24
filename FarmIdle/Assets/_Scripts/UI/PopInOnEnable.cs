using UnityEngine;
using UnityEngine.UI;

namespace FarmIdle
{
    // La tarjeta de un panel "salta" al abrirse: arranca un poco más chica, se pasa apenas y queda en 1. Mientras salta, la tarjeta
    // (si tiene CanvasGroup) y el fondo oscuro de atrás (el Image del padre) hacen fundido de 0 a su opacidad. Cosmético, con tiempo
    // sin escala (los paneles se abren con el juego en pausa) y solo corre Update mientras dura.
    public sealed class PopInOnEnable : MonoBehaviour
    {
        [SerializeField, Range(0.5f, 1f)] private float fromScale = 0.86f;
        [SerializeField, Min(1f)] private float overshootScale = 1.03f;
        [SerializeField, Min(0.01f)] private float seconds = 0.2f;

        private CanvasGroup _group;       // opcional: fundido de la tarjeta
        private Image _backdrop;          // opcional: fundido del fondo oscuro (el padre)
        private float _backdropAlpha = 1f;
        private float _startedAt;
        private bool _playing;

        // Para pruebas y depuración.
        public bool IsPlaying => _playing;

        private void Awake()
        {
            TryGetComponent(out _group);
            if (transform.parent != null && transform.parent.TryGetComponent(out _backdrop)) _backdropAlpha = _backdrop.color.a;
        }

        private void OnEnable()
        {
            _startedAt = Time.unscaledTime;
            _playing = true;
            transform.localScale = new Vector3(fromScale, fromScale, 1f);
            SetFade(0f);
        }

        private void OnDisable()
        {
            _playing = false;
            transform.localScale = Vector3.one;
            SetFade(1f);
        }

        private void Update()
        {
            if (!_playing) return;

            float k = (Time.unscaledTime - _startedAt) / seconds;
            if (k >= 1f)
            {
                transform.localScale = Vector3.one;
                SetFade(1f);
                _playing = false;
                return;
            }

            float scale = k < 0.7f
                ? Mathf.Lerp(fromScale, overshootScale, k / 0.7f)
                : Mathf.Lerp(overshootScale, 1f, (k - 0.7f) / 0.3f);
            transform.localScale = new Vector3(scale, scale, 1f);
            SetFade(Mathf.SmoothStep(0f, 1f, k / 0.7f));   // llega a opaco antes de que termine el salto
        }

        // t = 0 todo transparente, t = 1 opacidad normal (la del fondo es la que tenía armada).
        private void SetFade(float t)
        {
            if (_group != null) _group.alpha = t;
            if (_backdrop != null)
            {
                Color c = _backdrop.color;
                c.a = _backdropAlpha * t;
                _backdrop.color = c;
            }
        }
    }
}
