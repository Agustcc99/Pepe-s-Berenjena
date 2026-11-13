using UnityEngine;

namespace FarmIdle
{
    // Rebote "squash & stretch" por matemática pura (sin Animator ni asignaciones): una oscilación amortiguada que
    // estira en Y y achata en X/Z (y al revés) conservando más o menos el volumen. Es solo cosmético.
    public static class SquashStretch
    {
        // Cuántas oscilaciones completas hace durante el rebote.
        private const float Cycles = 1.5f;

        // 't' en 0..1 (progreso del rebote), 'amount' cuánto se deforma al principio (0,25 = 25 %).
        // Con t >= 1 devuelve Vector3.one: el rebote terminó y no queda deformación.
        public static Vector3 Evaluate(float t, float amount)
        {
            if (t >= 1f) return Vector3.one;
            if (t < 0f) t = 0f;

            float wave = Mathf.Sin(t * Cycles * 2f * Mathf.PI) * (1f - t) * (1f - t) * amount;
            float horizontal = 1f - wave * 0.5f;
            return new Vector3(horizontal, 1f + wave, horizontal);
        }
    }
}
