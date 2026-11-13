using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FarmIdle
{
    // El botón "se hunde" al apretarlo y rebota al soltarlo. Cosmético: no decide nada (el click lo sigue manejando el Button), usa
    // tiempo sin escala y en reposo su Update sale en la primera línea sin tocar el transform.
    public sealed class ButtonPressFeedback : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        [SerializeField, Range(0.5f, 1f)] private float pressedScale = 0.93f;
        [SerializeField, Min(1f)] private float overshootScale = 1.06f;
        [SerializeField, Min(0.01f)] private float releaseSeconds = 0.18f;

        private Selectable _selectable;
        private bool _pressed;
        private bool _releasing;
        private float _releasedAt;

        // Para pruebas y depuración.
        public bool IsPressed => _pressed;
        public bool IsAnimating => _releasing;

        private void Awake()
        {
            _selectable = GetComponent<Selectable>();
        }

        private void OnDisable()
        {
            // Al apagarse el objeto (un panel que se cierra con el botón apretado) queda en reposo.
            if (_pressed || _releasing) transform.localScale = Vector3.one;
            _pressed = false;
            _releasing = false;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_selectable != null && !_selectable.IsInteractable()) return;

            _pressed = true;
            _releasing = false;
            transform.localScale = new Vector3(pressedScale, pressedScale, 1f);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            Release();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            Release();
        }

        private void Release()
        {
            if (!_pressed) return;

            _pressed = false;
            _releasing = true;
            _releasedAt = Time.unscaledTime;
        }

        private void Update()
        {
            if (!_releasing) return;

            float k = (Time.unscaledTime - _releasedAt) / releaseSeconds;
            if (k >= 1f)
            {
                transform.localScale = Vector3.one;
                _releasing = false;
                return;
            }

            // De hundido a un poco más grande y a 1.
            float scale = k < 0.5f
                ? Mathf.Lerp(pressedScale, overshootScale, k * 2f)
                : Mathf.Lerp(overshootScale, 1f, (k - 0.5f) * 2f);
            transform.localScale = new Vector3(scale, scale, 1f);
        }
    }
}
