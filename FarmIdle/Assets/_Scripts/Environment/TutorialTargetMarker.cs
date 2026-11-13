using UnityEngine;

namespace FarmIdle
{
    // Un lugar al que la guía de los primeros minutos puede llevar al jugador (el campo, el pad de venta, la primera mejora). Se
    // registra en el TutorialManager al activarse y se da de baja al apagarse; solo el lugar ACTUAL del paso muestra su flecha: un cono
    // dorado que flota y gira sobre el lugar. Es un ITutorialTarget (de Core): el servicio y la flecha del borde de la pantalla (UI) no
    // saben que existe este componente. La flecha es cosmética y usa el reloj sin escala; sin TutorialManager (una escena suelta) no
    // hace nada.
    public sealed class TutorialTargetMarker : MonoBehaviour, ITutorialTarget
    {
        [SerializeField, Tooltip("En qué paso de la guía hay que ir a este lugar (el campo sirve para Sow y también para Harvest).")]
        private TutorialStep step = TutorialStep.Sow;

        [SerializeField, Tooltip("La flecha que flota sobre el lugar. Arranca apagada.")]
        private Transform arrow;

        [SerializeField, Min(0f)] private float bobAmount = 0.35f;
        [SerializeField, Min(0.1f)] private float bobSpeed = 4f;
        [SerializeField] private float spinDegreesPerSecond = 120f;

        private TutorialManager _tutorial;
        private Vector3 _arrowRest;
        private bool _isCurrent;

        public TutorialStep Step => step;
        public Vector3 Position => transform.position;
        public bool IsCurrent => _isCurrent;

        private void Awake()
        {
            if (arrow != null)
            {
                _arrowRest = arrow.localPosition;
                arrow.gameObject.SetActive(false);
            }
        }

        private void OnEnable()
        {
            _tutorial = TutorialManager.Instance;
            if (_tutorial != null) _tutorial.RegisterTarget(this);
        }

        private void OnDisable()
        {
            if (_tutorial != null)
            {
                _tutorial.UnregisterTarget(this);
                _tutorial = null;
            }

            SetCurrent(false);
        }

        public void SetCurrent(bool isCurrent)
        {
            _isCurrent = isCurrent;
            if (arrow != null && arrow.gameObject.activeSelf != isCurrent) arrow.gameObject.SetActive(isCurrent);
        }

        private void Update()
        {
            if (!_isCurrent || arrow == null) return;

            float t = Time.unscaledTime;
            arrow.localPosition = _arrowRest + new Vector3(0f, bobAmount * Mathf.Sin(t * bobSpeed), 0f);
            arrow.localRotation = Quaternion.Euler(0f, t * spinDegreesPerSecond, 0f);
        }
    }
}
