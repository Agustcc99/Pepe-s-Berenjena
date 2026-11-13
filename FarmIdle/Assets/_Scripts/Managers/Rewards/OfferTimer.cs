using System;

namespace FarmIdle
{
    // El ritmo del símbolo de oferta (velocidad por video), sin Unity: espera, aparece, dura un rato y se va. Todo el tiempo es
    // tiempo de juego (se lo pasa OfferManager con Tick): no corre en pausa ni con un anuncio en pantalla.
    //   - Espera: cuenta hacia atrás SOLO si 'canOffer' (hay anuncio listo, el buff no está activo...). Si no se puede ofrecer, la
    //     cuenta se congela donde estaba, así que al terminar un buff la próxima oferta no llega de golpe.
    //   - Oferta: el símbolo vive 'lifetime' segundos. Se va solo (Ended con taken=false) o cuando se toma (Cancel con taken=true).
    //   - Después de cada oferta la espera siguiente se sortea entre 'minInterval' y 'maxInterval'. La primera es 'firstDelay'.
    //   - El azar viene de afuera (una función 0..1) para poder probarlo con números fijos.
    public sealed class OfferTimer
    {
        private readonly float _firstDelay;
        private readonly float _minInterval;
        private readonly float _maxInterval;
        private readonly float _lifetime;
        private readonly Func<float> _random01;

        public OfferTimer(float firstDelaySeconds, float minIntervalSeconds, float maxIntervalSeconds, float lifetimeSeconds, Func<float> random01)
        {
            if (!(lifetimeSeconds > 0f)) throw new ArgumentOutOfRangeException(nameof(lifetimeSeconds));
            if (firstDelaySeconds < 0f || minIntervalSeconds < 0f || maxIntervalSeconds < minIntervalSeconds)
            {
                throw new ArgumentOutOfRangeException(nameof(maxIntervalSeconds), "Los tiempos tienen que ser >= 0 y el máximo no puede ser menor que el mínimo.");
            }

            _firstDelay = firstDelaySeconds;
            _minInterval = minIntervalSeconds;
            _maxInterval = maxIntervalSeconds;
            _lifetime = lifetimeSeconds;
            _random01 = random01 ?? throw new ArgumentNullException(nameof(random01));

            Remaining = firstDelaySeconds;
        }

        // true mientras el símbolo está en el mundo.
        public bool IsOffering { get; private set; }

        // Segundos que faltan: para que aparezca (esperando) o para que se vaya (ofreciendo).
        public float Remaining { get; private set; }

        // Apareció el símbolo.
        public event Action Started;

        // Se fue el símbolo; el parámetro es true si el jugador lo tomó.
        public event Action<bool> Ended;

        public void Tick(float deltaSeconds, bool canOffer)
        {
            if (!(deltaSeconds > 0f)) return;

            if (IsOffering)
            {
                Remaining -= deltaSeconds;
                if (Remaining <= 0f) Finish(taken: false);
                return;
            }

            if (!canOffer) return;

            Remaining -= deltaSeconds;
            if (Remaining <= 0f)
            {
                IsOffering = true;
                Remaining = _lifetime;
                Started?.Invoke();
            }
        }

        // Termina la oferta en curso (el jugador la tomó, o ya no corresponde). Sin oferta no hace nada.
        public void Cancel(bool taken)
        {
            if (IsOffering) Finish(taken);
        }

        // Para la prueba manual del panel de debug: la próxima oferta aparece en el siguiente Tick.
        public void ForceNextOffer()
        {
            if (!IsOffering) Remaining = 0.0001f;
        }

        private void Finish(bool taken)
        {
            IsOffering = false;
            float t = Math.Max(0f, Math.Min(1f, _random01()));
            Remaining = _minInterval + (_maxInterval - _minInterval) * t;
            Ended?.Invoke(taken);
        }
    }
}
