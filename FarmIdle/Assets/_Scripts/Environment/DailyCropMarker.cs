using UnityEngine;

namespace FarmIdle
{
    // IN-02: el ícono (una estrella dorada) que flota sobre la fila de un campo cuando su cultivo es el cultivo del día. Está en la escena
    // desde el principio (hijo del campo, así solo se ve con la zona abierta) y se prende o apaga; sin Instantiate.
    //   - El cultivo de la fila es el de su parcela de referencia.
    //   - Prendido, sube y baja y gira con tiempo sin escala (un solo Update, sin asignaciones). Apagado no toca ningún transform.
    public sealed class DailyCropMarker : MonoBehaviour
    {
        [SerializeField, Tooltip("Una parcela de la fila: de ahí sale el cultivo.")]
        private PlotView referencePlot;

        [SerializeField, Tooltip("El dibujo del ícono. Arranca apagado.")]
        private Transform icon;

        [SerializeField, Min(0f)] private float bobHeight = 0.18f;
        [SerializeField, Min(0f)] private float spinDegreesPerSecond = 90f;

        private DailyCropManager _daily;
        private Vector3 _rest;
        private bool _on;

        public bool IsShown => _on;

        private void OnEnable()
        {
            if (icon == null || referencePlot == null) return;

            _rest = icon.localPosition;
            _daily = DailyCropManager.Instance;
            if (_daily != null) _daily.Changed += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            if (_daily != null)
            {
                _daily.Changed -= Refresh;
                _daily = null;
            }

            _on = false;
            if (icon != null) icon.gameObject.SetActive(false);
        }

        private void Refresh()
        {
            bool shown = _daily != null && _daily.IsDaily(referencePlot.Crop);
            if (shown == _on) return;

            _on = shown;
            icon.localPosition = _rest;
            icon.gameObject.SetActive(shown);
        }

        private void Update()
        {
            if (!_on) return;

            float t = Time.unscaledTime;
            icon.localPosition = _rest + Vector3.up * (Mathf.Sin(t * 2.4f) * bobHeight);
            icon.localRotation = Quaternion.Euler(0f, t * spinDegreesPerSecond, 0f);
        }
    }
}
