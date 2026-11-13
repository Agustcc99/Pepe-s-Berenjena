using System;

namespace FarmIdle
{
    public enum AdRejection
    {
        None,
        Busy,       // ya hay un anuncio en pantalla
        Cooldown    // pasó muy poco desde el anterior
    }

    // Las reglas de UN anuncio a la vez, sin Unity: ¿se puede mostrar otro ahora?, ¿ya terminó este?, ¿se colgó?
    // El tiempo lo pone quien la usa (AdsManager, con el reloj sin escala), así se prueba con números.
    //   - Cada pedido tiene un número. Solo el resultado del pedido en curso cuenta, y una sola vez: un resultado repetido o
    //     tardío (de un pedido que ya se dio por cortado) se ignora. Eso es lo que evita otorgar una recompensa dos veces.
    //   - Enfriamiento mínimo entre anuncios: se cuenta desde que terminó el anterior, pero solo si ese anuncio llegó a mostrarse
    //     (un NoFill o un fallo al cargar no penaliza al jugador).
    //   - Corte por tiempo: si pasa 'timeoutSeconds' sin resultado, Abort lo da por terminado sin otorgar nada.
    public sealed class AdSession
    {
        private readonly float _minIntervalSeconds;
        private readonly float _timeoutSeconds;
        private int _lastId;
        private float _startedAt;
        private float _lastEndedAt = float.NegativeInfinity;

        public AdSession(float minIntervalSeconds, float timeoutSeconds)
        {
            if (!(minIntervalSeconds >= 0f)) throw new ArgumentOutOfRangeException(nameof(minIntervalSeconds));
            if (!(timeoutSeconds > 0f)) throw new ArgumentOutOfRangeException(nameof(timeoutSeconds));

            _minIntervalSeconds = minIntervalSeconds;
            _timeoutSeconds = timeoutSeconds;
        }

        public bool IsShowing { get; private set; }

        // Número del pedido en curso (0 si no hay).
        public int CurrentId => IsShowing ? _lastId : 0;

        // Segundos que faltan para poder mostrar otro anuncio (0 = ya se puede).
        public float CooldownRemaining(float now)
        {
            if (IsShowing) return 0f;

            return Math.Max(0f, _lastEndedAt + _minIntervalSeconds - now);
        }

        // Empieza un pedido. Si devuelve None, 'requestId' es el número que tiene que traer el resultado.
        public AdRejection TryBegin(float now, out int requestId)
        {
            requestId = 0;
            if (IsShowing) return AdRejection.Busy;
            if (CooldownRemaining(now) > 0f) return AdRejection.Cooldown;

            _lastId++;
            requestId = _lastId;
            _startedAt = now;
            IsShowing = true;
            return AdRejection.None;
        }

        // El resultado del pedido 'requestId'. Devuelve true UNA sola vez para el pedido en curso; cualquier otro (repetido,
        // tardío, de un pedido anterior) devuelve false y no cambia nada. 'wasShown' false (NoFill, error al cargar) no activa
        // el enfriamiento.
        public bool TryComplete(int requestId, float now, bool wasShown)
        {
            if (!IsShowing || requestId != _lastId) return false;

            IsShowing = false;
            if (wasShown) _lastEndedAt = now;
            return true;
        }

        // true si el anuncio en curso lleva más de 'timeoutSeconds' sin responder.
        public bool HasTimedOut(float now)
        {
            return IsShowing && now - _startedAt >= _timeoutSeconds;
        }

        // Da por cortado el anuncio en curso (sin recompensa). Devuelve su número, o 0 si no había ninguno. Los resultados que
        // lleguen después serán ignorados por TryComplete.
        public int Abort(float now)
        {
            if (!IsShowing) return 0;

            int id = _lastId;
            IsShowing = false;
            _lastEndedAt = now;
            return id;
        }
    }
}
