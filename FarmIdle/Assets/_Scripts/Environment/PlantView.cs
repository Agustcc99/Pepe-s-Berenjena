using System.Collections;
using UnityEngine;

namespace FarmIdle
{
    // Vista de una planta: va en el prefab de cada cultivo (CropData.PlantPrefab) y sale del PoolManager.
    // No tiene reglas de juego: la parcela le dice cuánto lleva crecido y cuándo está lista.
    //   - Mientras crece muestra la forma verde ("Growing") y la agranda de 'seedScale' a 'grownScale' según el progreso.
    //   - Cuando está lista cambia a la forma madura ("Ready"), ya a tamaño completo, con un "salto". El salto es una
    //     corrutina con tiempo sin escala: es solo cosmético, así que no depende de que el juego esté corriendo.
    public sealed class PlantView : MonoBehaviour
    {
        [SerializeField, Tooltip("Forma verde mientras crece.")]
        private GameObject growingModel;

        [SerializeField, Tooltip("Forma madura, lista para cosechar.")]
        private GameObject readyModel;

        [SerializeField, Range(0.05f, 1f), Tooltip("Tamaño recién sembrada (1 = tamaño completo).")]
        private float seedScale = 0.2f;

        [SerializeField, Range(0.1f, 1f), Tooltip("Tamaño justo antes de madurar.")]
        private float grownScale = 0.85f;

        [SerializeField, Min(0.05f)] private float popSeconds = 0.35f;
        [SerializeField, Min(1f)] private float popScale = 1.3f;

        [SerializeField, Min(0.05f), Tooltip("Cuánto dura el rebote al brotar (J-03).")]
        private float sproutSeconds = 0.4f;

        [SerializeField, Range(0f, 0.6f), Tooltip("Cuánto se estira/achata al brotar y al madurar (0,3 = 30 %).")]
        private float squashAmount = 0.3f;

        private Coroutine _popRoutine;
        private Coroutine _sproutRoutine;
        private Vector3 _squash = Vector3.one;   // deformación de rebote vigente; se multiplica por la escala de crecimiento

        public GameObject GrowingModel => growingModel;
        public GameObject ReadyModel => readyModel;

        public bool IsReady { get; private set; }

        // Último progreso mostrado (0..1).
        public float Growth { get; private set; }

        private void Awake()
        {
            if (growingModel == null || readyModel == null)
            {
                Debug.LogError("[FarmIdle] PlantView '" + name + "': faltan las formas Growing y Ready.", this);
            }
        }

        // Deja la planta recién sembrada: brote chiquito y con la forma verde.
        public void BeginGrowing()
        {
            StopPop();
            IsReady = false;
            SetModels(growing: true);
            SetGrowth(0f);

            if (isActiveAndEnabled) _sproutRoutine = StartCoroutine(Sprout());
        }

        public void SetGrowth(float progress)
        {
            if (IsReady) return;

            Growth = Mathf.Clamp01(progress);
            ApplyGrowthScale();
        }

        private void ApplyGrowthScale()
        {
            float scale = Mathf.Lerp(seedScale, grownScale, Growth);
            transform.localScale = _squash * scale;
        }

        // Pasa a la forma madura. 'animate' agrega el salto; sin él (por ejemplo al reactivar la parcela) aparece directo.
        public void ShowReady(bool animate)
        {
            StopPop();
            IsReady = true;
            Growth = 1f;
            SetModels(growing: false);
            transform.localScale = Vector3.one;

            if (animate && isActiveAndEnabled)
            {
                _popRoutine = StartCoroutine(Pop());
            }
        }

        private void OnDisable()
        {
            // Al volver al pool Unity detiene las corrutinas; la planta queda en reposo para la próxima siembra.
            _popRoutine = null;
            _sproutRoutine = null;
            _squash = Vector3.one;
            IsReady = false;
            Growth = 0f;
            transform.localScale = Vector3.one;
        }

        private void SetModels(bool growing)
        {
            if (growingModel != null) growingModel.SetActive(growing);
            if (readyModel != null) readyModel.SetActive(!growing);
        }

        private void StopPop()
        {
            _squash = Vector3.one;

            if (_sproutRoutine != null)
            {
                StopCoroutine(_sproutRoutine);
                _sproutRoutine = null;
            }

            if (_popRoutine == null) return;

            StopCoroutine(_popRoutine);
            _popRoutine = null;
        }

        // Rebote al brotar: squash & stretch sobre la escala de crecimiento (que sigue creciendo por debajo).
        private IEnumerator Sprout()
        {
            float elapsed = 0f;

            while (elapsed < sproutSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                _squash = SquashStretch.Evaluate(elapsed / sproutSeconds, squashAmount);
                if (!IsReady) ApplyGrowthScale();
                yield return null;
            }

            _squash = Vector3.one;
            if (!IsReady) ApplyGrowthScale();
            _sproutRoutine = null;
        }

        // Madurar: el salto de siempre (sube y vuelve a 1) más el rebote de estirar/achatar.
        private IEnumerator Pop()
        {
            float elapsed = 0f;

            while (elapsed < popSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / popSeconds);
                float scale = 1f + (popScale - 1f) * Mathf.Sin(t * Mathf.PI);   // sube y vuelve a 1
                transform.localScale = SquashStretch.Evaluate(t, squashAmount) * scale;
                yield return null;
            }

            transform.localScale = Vector3.one;
            _popRoutine = null;
        }
    }
}
