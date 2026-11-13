using UnityEngine;

namespace FarmIdle
{
    // El camión de reparto estacionado junto al pad de venta: puro adorno. Escucha las ventas de SellZone y, cada 'salesPerCrate'
    // ventas, aparece un cajón en la caja; cuando se llena se va (sale de pantalla hacia la cámara), se queda afuera un rato y vuelve
    // vacío. La venta ya está hecha cuando esto se entera: si falta o se apaga, nada cambia.
    //   - Tiempo sin escala y un solo Update; sin Instantiate (los cajones son hijos de la escena, escondidos con escala 0).
    //   - Cuando está quieto y sin cajones animándose no toca ningún transform.
    public sealed class SellTruck : MonoBehaviour
    {
        private enum Phase { Parked, Leaving, Away, Returning }

        [SerializeField, Tooltip("La zona de venta que avisa de cada venta.")]
        private SellZone zone;

        [SerializeField, Tooltip("El camión entero (lo que se mueve al irse y volver).")]
        private Transform body;

        [SerializeField, Tooltip("Los cajones de la caja, en el orden en que se llenan. Arrancan escondidos.")]
        private Transform[] crates = new Transform[0];

        [SerializeField, Min(1), Tooltip("Ventas que hacen falta para que aparezca un cajón.")]
        private int salesPerCrate = 5;

        [SerializeField, Tooltip("Hacia dónde se va, relativo a donde está estacionado (fuera de la pantalla).")]
        private Vector3 exitOffset = new Vector3(0f, 0f, -9f);

        [SerializeField, Min(0.2f)] private float driveSeconds = 1.2f;
        [SerializeField, Min(0f), Tooltip("Cuánto tarda en volver después de irse.")]
        private float awaySeconds = 1.6f;

        [SerializeField, Min(0f), Tooltip("Cuánto tarda en irse una vez lleno (para que se vea el último cajón).")]
        private float departDelaySeconds = 0.5f;

        private const float PopSeconds = 0.25f;

        private float[] _popAt;
        private Vector3 _parked;
        private Phase _phase;
        private float _phaseAt;
        private int _sales;
        private int _shown;
        private bool _wired;
        private bool _popping;

        // Para pruebas y depuración.
        public int ShownCrates => _shown;
        public bool IsParked => _phase == Phase.Parked;

        private void OnEnable()
        {
            _wired = false;
            if (zone == null || body == null) return;

            _parked = body.localPosition;
            _popAt = new float[crates.Length];
            ResetCrates();
            _phase = Phase.Parked;
            _sales = 0;
            _shown = 0;

            zone.Sold += OnSold;
            _wired = true;
        }

        private void OnDisable()
        {
            if (!_wired) return;

            zone.Sold -= OnSold;
            _wired = false;
            body.localPosition = _parked;
        }

        private void ResetCrates()
        {
            for (int i = 0; i < crates.Length; i++)
            {
                if (crates[i] != null) crates[i].localScale = Vector3.zero;
                _popAt[i] = -10f;
            }

            _popping = false;
        }

        private void OnSold(CropData crop, long coins, float arrivesInSeconds)
        {
            if (_phase != Phase.Parked || _shown >= crates.Length) return;

            _sales++;
            if (_sales < salesPerCrate) return;

            _sales = 0;
            _popAt[_shown] = Time.unscaledTime;
            _shown++;
            _popping = true;
            if (_shown == crates.Length) _phaseAt = Time.unscaledTime + departDelaySeconds;   // sale cuando pasa este momento
        }

        private void Update()
        {
            if (!_wired) return;

            float now = Time.unscaledTime;
            if (_popping) UpdatePops(now);

            switch (_phase)
            {
                case Phase.Parked:
                    if (_shown >= crates.Length && crates.Length > 0 && now >= _phaseAt) SetPhase(Phase.Leaving, now);
                    break;

                case Phase.Leaving:
                {
                    float k = Mathf.Clamp01((now - _phaseAt) / driveSeconds);
                    body.localPosition = _parked + exitOffset * (k * k);   // arranca despacio y acelera
                    if (k >= 1f) SetPhase(Phase.Away, now);
                    break;
                }

                case Phase.Away:
                    if (now - _phaseAt >= awaySeconds)
                    {
                        ResetCrates();
                        _shown = 0;
                        _sales = 0;
                        SetPhase(Phase.Returning, now);
                    }

                    break;

                case Phase.Returning:
                {
                    float k = Mathf.Clamp01((now - _phaseAt) / driveSeconds);
                    float left = 1f - k;
                    body.localPosition = _parked + exitOffset * (left * left);   // llega rápido y frena
                    if (k >= 1f) SetPhase(Phase.Parked, now);
                    break;
                }
            }
        }

        private void SetPhase(Phase phase, float now)
        {
            _phase = phase;
            _phaseAt = now;
            if (phase == Phase.Parked) body.localPosition = _parked;
        }

        // Cada cajón nuevo "salta": crece un poco de más y se asienta.
        private void UpdatePops(float now)
        {
            bool any = false;
            for (int i = 0; i < _shown; i++)
            {
                float k = (now - _popAt[i]) / PopSeconds;
                if (k >= 1f)
                {
                    if (k < 1.5f) crates[i].localScale = Vector3.one;   // una escritura final y listo
                    continue;
                }

                any = true;
                float scale = k < 0.6f ? Mathf.Lerp(0f, 1.2f, k / 0.6f) : Mathf.Lerp(1.2f, 1f, (k - 0.6f) / 0.4f);
                crates[i].localScale = Vector3.one * scale;
            }

            if (!any && now - _popAt[Mathf.Max(0, _shown - 1)] > 0.5f) _popping = false;
        }
    }
}
