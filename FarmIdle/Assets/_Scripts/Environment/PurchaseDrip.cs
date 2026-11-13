using System;

namespace FarmIdle
{
    // El ritmo del pago "a goteo": mientras el jugador está parado en un pad de compra, las monedas pasan de a poco de su
    // billetera a la compra, en vez de cobrarse de golpe. Es C# puro, sin UnityEngine: el reloj se lo pasa quien lo usa (el
    // pad le da el tiempo del juego, que no corre en pausa ni con un anuncio).
    //   - Al llegar espera 'startDelaySeconds' sin cobrar nada: cruzar un pad de camino a otro lado no gasta monedas.
    //   - Después paga lo que faltaba en ese momento en 'totalSeconds', a ritmo parejo (sin importar cuánto cueste).
    //   - Reparte monedas enteras: lo que sobra de cada paso se acumula para el siguiente.
    //   - Nunca ofrece más que lo que falta ni más que el saldo. Con saldo 0 espera sin acumular.
    //   - Reset() corta la visita: al volver a llegar vuelve a esperar y recalcula el ritmo.
    // Solo decide cuánto ofrecer; el cobro real (y que sea autoritativo) lo hace ProgressionManager.TryPay.
    public sealed class PurchaseDrip
    {
        private readonly float _totalSeconds;
        private readonly float _startDelaySeconds;

        private bool _active;
        private float _arrivedAt;
        private float _lastAt;
        private double _rate;    // monedas por segundo de esta visita (0 hasta que empieza a pagar)
        private double _carry;   // fracción de moneda acumulada que todavía no se pagó

        public PurchaseDrip(float totalSeconds, float startDelaySeconds)
        {
            _totalSeconds = totalSeconds > 0.01f ? totalSeconds : 0.01f;
            _startDelaySeconds = startDelaySeconds > 0f ? startDelaySeconds : 0f;
        }

        public float TotalSeconds => _totalSeconds;
        public float StartDelaySeconds => _startDelaySeconds;

        // true entre la llegada y el Reset.
        public bool IsActive => _active;

        // Un paso del goteo, con el reloj del juego en 'now'. 'remaining' es lo que falta pagar y 'balance' lo que tiene el
        // jugador. Devuelve las monedas que ofrece pagar ahora (0 casi siempre en el primer frame, durante la espera
        // o sin saldo). Si se llama dos veces en el mismo instante, la segunda no suma tiempo.
        public long Step(float now, long remaining, long balance)
        {
            if (!_active)
            {
                _active = true;
                _arrivedAt = now;
                _lastAt = now;
                _rate = 0d;
                _carry = 0d;
                return 0L;
            }

            // El tiempo que cuenta para pagar empieza al terminar la espera: lo que pasó esperando no se cobra después.
            float startsAt = _arrivedAt + _startDelaySeconds;
            double elapsed = Math.Max(0f, now - Math.Max(_lastAt, startsAt));
            _lastAt = now;

            if (remaining <= 0L || now < startsAt) return 0L;

            // El ritmo se fija al empezar a pagar (con lo que faltaba entonces): pagar siempre tarda 'totalSeconds'.
            if (_rate <= 0d) _rate = Math.Max(1d, remaining / (double)_totalSeconds);

            if (balance <= 0L)
            {
                _carry = 0d;   // sin saldo no se acumula: al volver a tenerlo no cae todo junto
                return 0L;
            }

            _carry += _rate * elapsed;
            long whole = (long)_carry;
            if (whole <= 0L) return 0L;

            long offer = Math.Min(whole, Math.Min(remaining, balance));
            _carry = Math.Min(_carry - offer, 1d);   // lo que no se pudo pagar (saldo corto) no queda como deuda
            return offer;
        }

        // El jugador se fue (o la compra se completó): la próxima visita empieza de cero.
        public void Reset()
        {
            _active = false;
            _rate = 0d;
            _carry = 0d;
        }
    }
}
