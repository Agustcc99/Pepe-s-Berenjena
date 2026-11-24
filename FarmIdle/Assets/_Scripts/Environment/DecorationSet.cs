using System.Collections;
using UnityEngine;

namespace FarmIdle
{
    // Las decoraciones de la granja (F-17) ya armadas en la escena y apagadas: prende las primeras 'Owned' (en el orden del
    // GameConfig) y apaga el resto. Escucha a DecorationManager, así que cargar la partida y comprar usan el mismo camino.
    // Sin Instantiate: solo SetActive sobre objetos que ya existen.
    //
    // Que se note la compra (F-20): la decoración recién comprada aparece con un salto (crece desde chica, se pasa y vuelve) y, por
    // unos segundos, es el lugar al que apunta la guía (IGuidePoint con la prioridad más alta): la flecha del borde y su cartel le
    // muestran al jugador dónde apareció. Cargar la partida no dispara nada de esto.
    public sealed class DecorationSet : MonoBehaviour, IGuidePoint
    {
        [SerializeField, Tooltip("Los objetos en el mismo orden que GameConfig.Decorations.")]
        private GameObject[] items = new GameObject[0];

        [SerializeField, Min(0f), Tooltip("Segundos que la guía señala la decoración recién comprada.")]
        private float guideSeconds = 12f;

        [SerializeField, Min(0.05f), Tooltip("Cuánto dura el salto con el que aparece.")]
        private float popSeconds = 0.6f;

        private DecorationManager _decorations;
        private int _newIndex = -1;       // la recién comprada (-1 = ninguna)
        private float _guideUntil;        // reloj sin escala
        private Vector3 _restScale = Vector3.one;
        private Coroutine _popRoutine;

        // F-22: aviso de festejo (posición de la decoración recién comprada, su encanto en %). Lo escuchan BurstFeedback (estallido
        // dorado) y HarvestFeedback (cartel "+2 % ventas"); Environment no necesita conocerlos.
        public static event System.Action<Vector3, int> Placed;

        // Para pruebas y depuración.
        public int NewIndex => _newIndex;

        // ---------- IGuidePoint ----------

        public Vector3 Position => _newIndex >= 0 && _newIndex < items.Length && items[_newIndex] != null ? items[_newIndex].transform.position : transform.position;
        public int GuidePriority => GuidePoints.NewItemPriority;
        public bool WantsGuide => _newIndex >= 0 && Time.unscaledTime < _guideUntil;

        private void OnEnable()
        {
            _decorations = DecorationManager.Instance;
            if (_decorations == null) return;

            _decorations.Changed += Refresh;
            _decorations.Bought += OnBought;
            GuidePoints.Register(this);
            Refresh();
        }

        private void OnDisable()
        {
            GuidePoints.Unregister(this);
            FinishPop();
            _newIndex = -1;

            if (_decorations != null)
            {
                _decorations.Changed -= Refresh;
                _decorations.Bought -= OnBought;
                _decorations = null;
            }
        }

        private void Refresh()
        {
            for (int i = 0; i < items.Length; i++)
            {
                if (items[i] != null) items[i].SetActive(_decorations.IsOwned(i));
            }
        }

        // Se compró la decoración 'index' (Changed ya la prendió): salto de aparición y la guía la señala un rato.
        private void OnBought(int index)
        {
            if (index < 0 || index >= items.Length || items[index] == null) return;

            FinishPop();
            _newIndex = index;
            _guideUntil = Time.unscaledTime + guideSeconds;
            _restScale = items[index].transform.localScale;
            _popRoutine = StartCoroutine(Pop(items[index].transform));

            DecorationData data = _decorations.At(index);
            Placed?.Invoke(items[index].transform.position, data != null ? Mathf.RoundToInt(data.Charm * 100f) : 0);
        }

        private IEnumerator Pop(Transform item)
        {
            float elapsed = 0f;
            while (elapsed < popSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / popSeconds);

                // De 0,2 a 1 pasándose un 25 % cerca del final (un rebote, por matemática).
                float overshoot = Mathf.Sin(t * Mathf.PI) * 0.25f;
                float scale = Mathf.Lerp(0.2f, 1f, t) + overshoot;
                item.localScale = _restScale * scale;
                yield return null;
            }

            item.localScale = _restScale;
            _popRoutine = null;
        }

        // Corta el salto en curso dejando la decoración en su tamaño.
        private void FinishPop()
        {
            if (_popRoutine == null) return;

            StopCoroutine(_popRoutine);
            _popRoutine = null;
            if (_newIndex >= 0 && _newIndex < items.Length && items[_newIndex] != null) items[_newIndex].transform.localScale = _restScale;
        }
    }
}
