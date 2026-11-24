using System.Collections;
using UnityEngine;

namespace FarmIdle
{
    // El cierre y la apertura de una zona en el mundo. Una zona tiene dos mitades que ya existen en la escena:
    //   - 'lockedRoot': lo que se ve mientras está cerrada (el terreno vallado y apagado),
    //   - 'unlockedRoot': lo que aparece al abrirse (el campo de parcelas con su PlotField).
    // Este componente solo las alterna según el progreso: no hace Instantiate ni Destroy, activa objetos que ya estaban en el
    // nivel (las plantas de las parcelas nuevas salen del pool, que se llenó al cargar el nivel: ver PlotField). El que
    // cobra y abre la zona es ProgressionManager; el que se paga parado es el ZoneUnlockPad.
    //   - Si la zona ya estaba abierta al cargar (partida guardada, Fase 7) aparece de una, sin efecto.
    //   - Si se abre jugando, el campo crece con un rebote y el terreno cerrado se hunde. Es una corrutina con tiempo sin
    //     escala, igual que las demás animaciones cosméticas: la zona ya está abierta desde el primer instante.
    public sealed class ZoneController : MonoBehaviour
    {
        [SerializeField, Tooltip("La zona de datos que controla (ProgressionManager dice si está abierta).")]
        private ZoneData zone;

        [SerializeField, Tooltip("Lo que se ve con la zona cerrada.")]
        private GameObject lockedRoot;

        [SerializeField, Tooltip("Lo que aparece al abrirla: el campo y sus parcelas. En la escena arranca desactivado.")]
        private GameObject unlockedRoot;

        [SerializeField, Min(0.1f)] private float revealSeconds = 0.7f;
        [SerializeField, Min(0f), Tooltip("Cuánto se pasa el campo de su tamaño antes de asentarse (1,7 = un 10 % más).")]
        private float backStrength = 1.7f;

        private ProgressionManager _progression;
        private PrestigeManager _prestige;
        private Coroutine _reveal;

        public ZoneData Zone => zone;
        public GameObject LockedRoot => lockedRoot;
        public GameObject UnlockedRoot => unlockedRoot;

        // true mientras corre el efecto de apertura (para pruebas).
        public bool IsRevealing => _reveal != null;

        private void OnEnable()
        {
            _progression = ProgressionManager.Instance;
            if (_progression == null || zone == null || lockedRoot == null || unlockedRoot == null)
            {
                Debug.LogError("[FarmIdle] ZoneController '" + name + "': falta el ProgressionManager, la zona o sus dos mitades.", this);
                _progression = null;
                return;
            }

            _progression.Changed += OnProgressChanged;
            _prestige = PrestigeManager.Instance;
            if (_prestige != null) _prestige.Changed += OnSeasonChanged;
            ApplyState(_progression.IsZoneUnlocked(zone));
        }

        private void OnDisable()
        {
            if (_progression != null)
            {
                _progression.Changed -= OnProgressChanged;
                if (_prestige != null) _prestige.Changed -= OnSeasonChanged;
                _prestige = null;

                // Al desactivarse Unity detiene la corrutina: la zona queda en su estado final, sin efecto a medias.
                _reveal = null;
                ApplyState(_progression.IsZoneUnlocked(zone));
                _progression = null;
            }
        }

        private void OnProgressChanged(ProgressChange change)
        {
            bool unlocked = _progression.IsZoneUnlocked(zone);

            if (change.Kind == ProgressChangeKind.ZoneUnlocked && change.Id == zone.Id)
            {
                if (_reveal != null) StopCoroutine(_reveal);
                _reveal = StartCoroutine(Reveal());
            }
            else if (change.Kind == ProgressChangeKind.Restored)
            {
                if (_reveal != null)
                {
                    StopCoroutine(_reveal);
                    _reveal = null;
                }

                ApplyState(unlocked);
                if (!unlocked) ReopenGate();
            }
        }

        // F-24: cambió la temporada (se empezó una o se cargó la partida): una granja de temporada puede haber aparecido o
        // desaparecido. Si justo se está abriendo, el efecto sigue su curso.
        private void OnSeasonChanged()
        {
            if (_progression == null || _reveal != null) return;

            bool unlocked = _progression.IsZoneUnlocked(zone);
            ApplyState(unlocked);
            if (!unlocked) SetGates(_progression.IsZoneAvailable(zone));
        }

        // Una zona que vuelve a cerrarse (nueva temporada, F-03) necesita su portón otra vez: el pad se apagó al abrirse y apagado no
        // escucha al progreso, así que lo reactiva este componente, que sigue activo. Solo pasa al restaurar el progreso.
        private void ReopenGate()
        {
            SetGates(_progression.IsZoneAvailable(zone));
        }

        // Prende o apaga el portón de esta zona (una granja que todavía no apareció no tiene portón).
        private void SetGates(bool active)
        {
            ZoneUnlockPad[] gates = GetComponentsInChildren<ZoneUnlockPad>(true);
            for (int i = 0; i < gates.Length; i++)
            {
                if (gates[i].Zone == zone && gates[i].gameObject.activeSelf != active) gates[i].gameObject.SetActive(active);
            }
        }

        // El estado final de cada mitad, sin efectos. Una granja de temporada que todavía no apareció no muestra ni el terreno ni el portón.
        private void ApplyState(bool unlocked)
        {
            bool available = _progression == null || _progression.IsZoneAvailable(zone);
            lockedRoot.SetActive(!unlocked && available);
            if (!available) SetGates(false);
            lockedRoot.transform.localScale = Vector3.one;

            unlockedRoot.SetActive(unlocked);
            unlockedRoot.transform.localScale = Vector3.one;
        }

        private IEnumerator Reveal()
        {
            unlockedRoot.SetActive(true);
            Transform field = unlockedRoot.transform;
            Transform locked = lockedRoot.transform;

            float elapsed = 0f;
            while (elapsed < revealSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(elapsed / revealSeconds);

                // El campo crece pasándose un poco de su tamaño y vuelve (ease-out-back); el terreno cerrado se hunde y se achica.
                float grow = Mathf.Max(0.01f, EaseOutBack(k, backStrength));
                field.localScale = new Vector3(grow, grow, grow);

                float sink = Mathf.Clamp01(k * 1.6f);
                locked.localScale = new Vector3(1f - sink, Mathf.Max(0.01f, 1f - sink), 1f - sink);

                yield return null;
            }

            ApplyState(true);
            _reveal = null;
        }

        private static float EaseOutBack(float t, float c1)
        {
            float c3 = c1 + 1f;
            float p = t - 1f;
            return 1f + c3 * p * p * p + c1 * p * p;
        }
    }
}
