using UnityEngine;

namespace FarmIdle
{
    public enum DecorationMotionKind
    {
        Spin,    // gira sin parar (aspas del molino): 'amount' = grados por segundo
        Sway,    // se mece como con el viento (bandera, espantapájaros): 'amount' = grados de cada lado
        Orbit,   // da vueltas alrededor de su lugar (patos, luciérnagas, mariposas): 'amount' = radio, 'bob' = subida y bajada
        Pulse,   // se achica y se agranda sobre los ejes de 'axis' (aleteo, brillo): 'amount' = cuánto se achica (0 a 1)
        Hop,     // saltitos (pajarito, gallina): 'amount' = altura
    }

    // Vida de las decoraciones (F-21): cada pieza que se mueve lleva uno de estos, armado por DecorationBuilder. Es solo cosmético.
    //   - Cuelgan de la decoración, así que se apagan con ella: una decoración sin comprar no gasta nada.
    //   - Sin asignaciones y sin Instantiate: solo Vector3/Quaternion (structs) sobre el transform. Reloj de juego (Time.time):
    //     con el juego en pausa, quieto.
    public sealed class DecorationMotion : MonoBehaviour
    {
        [SerializeField] private DecorationMotionKind kind = DecorationMotionKind.Sway;
        [SerializeField, Tooltip("Spin/Sway: eje local. Pulse: qué ejes se achican (0 a 1 cada uno).")] private Vector3 axis = Vector3.up;
        [SerializeField, Tooltip("Spin: grados/s. Sway: grados. Orbit: radio. Pulse: cuánto se achica. Hop: altura.")] private float amount = 10f;
        [SerializeField, Min(0f), Tooltip("Ciclos (Sway, Pulse, Hop) o vueltas (Orbit) por segundo.")] private float speed = 0.5f;
        [SerializeField, Tooltip("Orbit: cuánto sube y baja mientras da vueltas.")] private float bob;
        [SerializeField, Tooltip("Desfase en radianes, para que no se muevan todas parejas.")] private float phase;

        private Vector3 _basePosition;
        private Quaternion _baseRotation;
        private Vector3 _baseScale;

        public DecorationMotionKind Kind => kind;

        // Para pruebas: lo arma el builder por SerializedObject, pero una prueba puede armarlo a mano.
        public void Configure(DecorationMotionKind newKind, Vector3 newAxis, float newAmount, float newSpeed, float newBob, float newPhase)
        {
            kind = newKind;
            axis = newAxis;
            amount = newAmount;
            speed = newSpeed;
            bob = newBob;
            phase = newPhase;
        }

        private void Awake()
        {
            _basePosition = transform.localPosition;
            _baseRotation = transform.localRotation;
            _baseScale = transform.localScale;
        }

        private void Update()
        {
            float t = Time.time;
            float angle = (t * speed) * (2f * Mathf.PI) + phase;
            Transform tr = transform;

            switch (kind)
            {
                case DecorationMotionKind.Spin:
                    tr.localRotation = _baseRotation * Quaternion.AngleAxis(Mathf.Repeat(t * amount, 360f), axis);
                    break;

                case DecorationMotionKind.Sway:
                    tr.localRotation = _baseRotation * Quaternion.AngleAxis(Mathf.Sin(angle) * amount, axis);
                    break;

                case DecorationMotionKind.Orbit:
                    tr.localPosition = _basePosition + new Vector3(Mathf.Cos(angle) * amount, Mathf.Sin(angle * 2.3f) * bob, Mathf.Sin(angle) * amount);
                    tr.localRotation = _baseRotation * Quaternion.Euler(0f, -angle * Mathf.Rad2Deg, 0f);   // mira hacia donde va
                    break;

                case DecorationMotionKind.Pulse:
                    float k = 1f - amount * (0.5f + 0.5f * Mathf.Sin(angle));
                    tr.localScale = new Vector3(_baseScale.x * Mathf.Lerp(1f, k, axis.x), _baseScale.y * Mathf.Lerp(1f, k, axis.y), _baseScale.z * Mathf.Lerp(1f, k, axis.z));
                    break;

                case DecorationMotionKind.Hop:
                    tr.localPosition = _basePosition + Vector3.up * (Mathf.Abs(Mathf.Sin(angle * 0.5f)) * amount);
                    break;
            }
        }
    }
}
