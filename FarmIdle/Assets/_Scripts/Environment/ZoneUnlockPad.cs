using System.Collections;
using UnityEngine;

namespace FarmIdle
{
    // El pad que desbloquea una zona: el "portón" entre el campo actual y la zona cerrada. Se paga parado encima (ver
    // PurchasePad). Cuando la zona se abre, el pad se achica y desaparece; el campo nuevo lo muestra el ZoneController.
    public sealed class ZoneUnlockPad : PurchasePad
    {
        [SerializeField, Tooltip("La zona que desbloquea este pad.")]
        private ZoneData zone;

        [SerializeField, Min(0.05f)] private float dismissSeconds = 0.35f;

        private Vector3 _restScale;
        private bool _restScaleKnown;

        public ZoneData Zone => zone;

        protected override bool IsPurchasable()
        {
            return zone != null && Progression != null && !Progression.IsZoneUnlocked(zone);
        }

        protected override void ReadPrice(out long cost, out long paid)
        {
            cost = Progression.CostOf(zone);
            paid = Progression.PaidFor(zone);
        }

        protected override long Pay(long coins)
        {
            return Progression.TryPay(zone, coins);
        }

        protected override string BuildTitle()
        {
            return zone != null ? zone.DisplayName : Loc.Get("pad.zone");
        }

        protected override bool IsCompletion(ProgressChange change)
        {
            return zone != null && change.Kind == ProgressChangeKind.ZoneUnlocked && change.Id == zone.Id;
        }

        protected override void OnCompleted()
        {
            if (isActiveAndEnabled) StartCoroutine(Dismiss());
        }

        private void Start()
        {
            // Una zona que ya estaba abierta al cargar (partida guardada, Fase 7) no necesita su pad.
            if (zone != null && Progression != null && Progression.IsZoneUnlocked(zone)) gameObject.SetActive(false);
        }

        private IEnumerator Dismiss()
        {
            if (!_restScaleKnown)
            {
                _restScale = transform.localScale;
                _restScaleKnown = true;
            }

            float elapsed = 0f;
            while (elapsed < dismissSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(elapsed / dismissSeconds);
                transform.localScale = _restScale * Mathf.Max(0.01f, 1f - k * k);
                yield return null;
            }

            transform.localScale = _restScale;
            gameObject.SetActive(false);
        }
    }
}
