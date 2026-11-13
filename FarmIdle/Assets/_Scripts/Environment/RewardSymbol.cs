using UnityEngine;

namespace FarmIdle
{
    // El símbolo de oferta (Turbo por video): un objeto pequeño que flota en el mundo un rato. Es un RewardPad: parado al lado un
    // momento (la barra se llena), pide el video por AdsManager; no hay toques ni botones, y nunca se pide un video por rozarlo.
    // Sale del PoolManager (OfferSpawner): OnEnable lo deja en reposo y SetReward le dice qué ofrece. Flota y gira con tiempo sin
    // escala (solo cosmético).
    public sealed class RewardSymbol : RewardPad
    {
        [SerializeField, Tooltip("Lo que flota y gira (el modelo).")]
        private Transform floater;

        [SerializeField, Min(0f)] private float bobHeight = 0.18f;
        [SerializeField, Min(0f)] private float spinDegreesPerSecond = 90f;

        private Vector3 _floaterStart;
        private bool _hasStart;
        private OfferManager _offers;

        protected override void OnEnable()
        {
            base.OnEnable();
            _offers = OfferManager.Instance;

            if (floater != null)
            {
                if (!_hasStart)
                {
                    _floaterStart = floater.localPosition;
                    _hasStart = true;
                }

                floater.localPosition = _floaterStart;
            }
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            _offers = null;
        }

        protected override string Title()
        {
            string title = Reward != null ? Reward.DisplayName : Loc.Get("pad.turbo");
            if (_offers != null && _offers.HasOffer) title += " (" + Mathf.CeilToInt(_offers.OfferRemaining) + " s)";
            return title;
        }

        // Los segundos que le quedan al símbolo entran en la clave del cartel: así se reescribe cada vez que baja un segundo.
        protected override int TitleKey()
        {
            return _offers != null && _offers.HasOffer ? Mathf.CeilToInt(_offers.OfferRemaining) : 0;
        }

        protected override void LateUpdate()
        {
            base.LateUpdate();   // el cartel mira a la cámara
            if (floater == null) return;

            float t = Time.unscaledTime;
            floater.localPosition = _floaterStart + new Vector3(0f, Mathf.Sin(t * 3f) * bobHeight, 0f);
            floater.localRotation = Quaternion.Euler(0f, t * spinDegreesPerSecond, 0f);
        }
    }
}
