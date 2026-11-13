// SOLO existe en el Editor y en los builds de desarrollo (define DEVELOPMENT_BUILD): un build final no lo compila, así que no puede
// regalar recompensas por un "video" simulado. Un build final sin SDK usa NoAdService (nunca hay anuncio).
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;

namespace FarmIdle
{
    // Anuncio simulado. Es C# puro, sin UnityEngine: el tiempo se lo pasa AdsManager con Tick (tiempo sin escala), así que se
    // prueba sin escena. Sirve para el Editor, las pruebas y los builds de desarrollo sin SDK.
    //   - ShowRewarded "dura" DurationSeconds y después entrega NextResult (Rewarded, Closed o Failed). Con duración 0 termina
    //     en el siguiente Tick.
    //   - Available = false simula "sin anuncio": IsReady devuelve false y, si igual se pide, responde NoFill en el acto.
    //   - HoldOpen = true lo deja "colgado" (nunca termina solo) para probar el corte por tiempo; CompleteNow lo cierra a mano
    //     (y sirve para probar un resultado tardío).
    public sealed class MockAdService : IAdService
    {
        private Action<AdResult> _callback;
        private float _remaining;

        public string Name => "Mock";

        // Lo que entrega el próximo anuncio que se muestre.
        public AdResult NextResult { get; set; } = AdResult.Rewarded;

        // Cuánto dura el anuncio simulado, en segundos sin escala.
        public float DurationSeconds { get; set; } = 3f;

        // false = no hay anuncio (IsReady false).
        public bool Available { get; set; } = true;

        public bool HoldOpen { get; set; }

        public int ShowCount { get; private set; }
        public string LastPlacement { get; private set; } = string.Empty;

        public bool IsShowing => _callback != null;

        // Segundos que le faltan al anuncio en pantalla (0 si no hay).
        public float RemainingSeconds => _callback != null ? _remaining : 0f;

        public bool IsReady(string placement)
        {
            return Available && _callback == null;
        }

        public void ShowRewarded(string placement, Action<AdResult> onFinished)
        {
            if (onFinished == null) throw new ArgumentNullException(nameof(onFinished));

            if (_callback != null)
            {
                onFinished(AdResult.Failed);   // ya hay uno en pantalla
                return;
            }

            if (!Available)
            {
                onFinished(AdResult.NoFill);
                return;
            }

            ShowCount++;
            LastPlacement = placement ?? string.Empty;
            _callback = onFinished;
            _remaining = Math.Max(0f, DurationSeconds);
        }

        public void Tick(float unscaledDeltaSeconds)
        {
            if (_callback == null || HoldOpen || !(unscaledDeltaSeconds > 0f)) return;

            _remaining -= unscaledDeltaSeconds;
            if (_remaining <= 0f) Finish(NextResult);
        }

        // Cierra el anuncio en pantalla con 'result' (sin esperar la duración). Devuelve false si no había ninguno.
        public bool CompleteNow(AdResult result)
        {
            if (_callback == null) return false;

            Finish(result);
            return true;
        }

        private void Finish(AdResult result)
        {
            Action<AdResult> callback = _callback;
            _callback = null;
            _remaining = 0f;
            callback(result);
        }
    }
}
#endif
