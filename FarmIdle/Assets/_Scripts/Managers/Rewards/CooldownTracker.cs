using System;
using System.Collections.Generic;

namespace FarmIdle
{
    // Enfriamientos por clave (la recompensa de video, por ejemplo), sin Unity. Se le pasa el tiempo del juego con Tick: no corre
    // en pausa ni con un anuncio en pantalla. Las claves que llegan a 0 se olvidan solas (la lista nunca crece).
    public sealed class CooldownTracker
    {
        private readonly Dictionary<string, float> _remaining = new Dictionary<string, float>();
        private readonly List<string> _scratch = new List<string>(4);

        // Empieza (o reinicia) el enfriamiento de 'key'. Con 0 o menos no hay nada que esperar.
        public void Start(string key, float seconds)
        {
            if (string.IsNullOrEmpty(key)) throw new ArgumentException("La clave no puede estar vacía.", nameof(key));

            if (seconds > 0f && !float.IsNaN(seconds)) _remaining[key] = seconds;
            else _remaining.Remove(key);
        }

        public float Remaining(string key)
        {
            return key != null && _remaining.TryGetValue(key, out float seconds) ? seconds : 0f;
        }

        public bool IsReady(string key)
        {
            return Remaining(key) <= 0f;
        }

        public void Tick(float deltaSeconds)
        {
            if (_remaining.Count == 0 || !(deltaSeconds > 0f)) return;

            _scratch.Clear();
            foreach (KeyValuePair<string, float> entry in _remaining) _scratch.Add(entry.Key);

            for (int i = 0; i < _scratch.Count; i++)
            {
                string key = _scratch[i];
                float left = _remaining[key] - deltaSeconds;
                if (left > 0f) _remaining[key] = left;
                else _remaining.Remove(key);
            }
        }

        public void Clear()
        {
            _remaining.Clear();
        }
    }
}
