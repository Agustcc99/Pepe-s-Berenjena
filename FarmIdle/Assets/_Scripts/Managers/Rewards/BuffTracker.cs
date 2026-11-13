using System;
using System.Collections.Generic;

namespace FarmIdle
{
    public enum BuffKind
    {
        MoveSpeed,   // multiplica la velocidad del jugador
        SellPrice    // multiplica el precio de venta
    }

    // Un buff activo: de qué es, cuánto multiplica y cuántos segundos de TIEMPO DE JUEGO le quedan.
    public sealed class ActiveBuff
    {
        public string Id;
        public BuffKind Kind;
        public float Magnitude;
        public float Duration;    // lo que dura entero (para mostrar una barra)
        public float Remaining;
    }

    // Los buffs temporales, sin Unity. Quien la usa (BuffManager) le pasa el tiempo del juego con Tick (Time.deltaTime, que vale 0
    // en pausa y con un anuncio en pantalla): un buff solo envejece mientras se juega, y no hay reloj de pared, así que cambiar la
    // hora del dispositivo no lo estira ni lo acorta.
    //   - Reglas al volver a activar un buff que ya está: se RENUEVA (vuelve a tener su duración completa, no se suman).
    //     Sumar permitiría acumular horas de buff mirando videos seguidos; renovar acota el abuso a una duración por video.
    //   - Varios buffs del mismo tipo (ids distintos) se multiplican entre sí. Hoy no hay dos del mismo tipo.
    //   - Vencer: al llegar a 0 sale de la lista y avisa Expired. Restaurar una partida pone la lista como estaba y avisa solo
    //     Changed: nunca Activated (no es un buff "recién activado": no hay efectos de aparición).
    public sealed class BuffTracker
    {
        private readonly List<ActiveBuff> _buffs = new List<ActiveBuff>(4);

        public IReadOnlyList<ActiveBuff> Active => _buffs;
        public int Count => _buffs.Count;

        // Se activó (o renovó) un buff jugando.
        public event Action<ActiveBuff> Activated;

        // Un buff se terminó solo.
        public event Action<ActiveBuff> Expired;

        // Cambió algo de la lista (activó, renovó, venció, se restauró, se limpió). Después de Activated/Expired.
        public event Action Changed;

        // Activa el buff, o lo renueva si ya estaba. Devuelve false (sin tocar nada) si los datos no sirven.
        public bool Activate(string id, BuffKind kind, float magnitude, float durationSeconds)
        {
            if (string.IsNullOrEmpty(id) || float.IsNaN(magnitude) || float.IsInfinity(magnitude) || magnitude < 0f) return false;
            if (!(durationSeconds > 0f) || float.IsInfinity(durationSeconds)) return false;

            ActiveBuff buff = Find(id);
            if (buff == null)
            {
                buff = new ActiveBuff { Id = id };
                _buffs.Add(buff);
            }

            buff.Kind = kind;
            buff.Magnitude = magnitude;
            buff.Duration = durationSeconds;
            buff.Remaining = durationSeconds;

            Activated?.Invoke(buff);
            Changed?.Invoke();
            return true;
        }

        // Hace pasar 'deltaSeconds' de tiempo de juego. Los pasos de 0, negativos o NaN se ignoran.
        public void Tick(float deltaSeconds)
        {
            if (_buffs.Count == 0 || !(deltaSeconds > 0f)) return;

            for (int i = _buffs.Count - 1; i >= 0; i--)
            {
                ActiveBuff buff = _buffs[i];
                buff.Remaining -= deltaSeconds;
                if (buff.Remaining > 0f) continue;

                _buffs.RemoveAt(i);
                Expired?.Invoke(buff);
                Changed?.Invoke();
            }
        }

        public bool IsActive(string id)
        {
            return Find(id) != null;
        }

        // Segundos que le quedan al buff (0 si no está activo).
        public float RemainingOf(string id)
        {
            ActiveBuff buff = Find(id);
            return buff != null ? buff.Remaining : 0f;
        }

        // El multiplicador total de ese tipo de buff (1 = ninguno activo).
        public float MultiplierOf(BuffKind kind)
        {
            float multiplier = 1f;
            for (int i = 0; i < _buffs.Count; i++)
            {
                if (_buffs[i].Kind == kind) multiplier *= _buffs[i].Magnitude;
            }

            return multiplier;
        }

        // Reemplaza la lista (partida guardada). Silencioso: solo Changed. Los datos inválidos se saltean y el tiempo restante se
        // acota a la duración (un archivo editado no puede dar más de lo que dura el buff).
        public void Restore(IEnumerable<ActiveBuff> buffs)
        {
            _buffs.Clear();

            if (buffs != null)
            {
                foreach (ActiveBuff source in buffs)
                {
                    if (source == null || string.IsNullOrEmpty(source.Id) || Find(source.Id) != null) continue;
                    if (!(source.Duration > 0f) || !(source.Remaining > 0f) || float.IsNaN(source.Magnitude) || source.Magnitude < 0f) continue;

                    _buffs.Add(new ActiveBuff
                    {
                        Id = source.Id,
                        Kind = source.Kind,
                        Magnitude = source.Magnitude,
                        Duration = source.Duration,
                        Remaining = Math.Min(source.Remaining, source.Duration)
                    });
                }
            }

            Changed?.Invoke();
        }

        public void Clear()
        {
            if (_buffs.Count == 0) return;

            _buffs.Clear();
            Changed?.Invoke();
        }

        private ActiveBuff Find(string id)
        {
            for (int i = 0; i < _buffs.Count; i++)
            {
                if (_buffs[i].Id == id) return _buffs[i];
            }

            return null;
        }
    }
}
