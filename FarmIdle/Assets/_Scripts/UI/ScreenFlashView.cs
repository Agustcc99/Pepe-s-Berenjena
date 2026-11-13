using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace FarmIdle
{
    // Un destello suave en los bordes de la pantalla para los momentos que merecen un "¡ah!": verde al usar el fertilizante, dorado al
    // subir de nivel, celeste al arrancar el Turbo y crema al comprar una zona o una mejora. Es una imagen a pantalla completa con un viñeteado (borde de color, centro
    // transparente) que se enciende y se apaga con una corrutina de tiempo sin escala. Solo escucha eventos de Managers y no decide nada.
    // No captura toques (Raycast Target apagado) y no hace nada sin la imagen.
    public sealed class ScreenFlashView : MonoBehaviour
    {
        [SerializeField, Tooltip("Imagen a pantalla completa con el viñeteado. Arranca transparente.")]
        private Image image;

        [SerializeField] private Color fertilizerColor = new Color(0.45f, 0.95f, 0.35f);
        [SerializeField] private Color levelUpColor = new Color(1f, 0.85f, 0.25f);
        [SerializeField] private Color turboColor = new Color(0.35f, 0.75f, 1f);
        [SerializeField] private Color priceColor = new Color(1f, 0.85f, 0.25f);
        [SerializeField] private Color purchaseColor = new Color(1f, 0.97f, 0.75f);

        [SerializeField, Range(0.05f, 1f)] private float peakAlpha = 0.55f;
        [SerializeField, Min(0.1f)] private float seconds = 0.55f;

        private FertilizerManager _fertilizer;
        private ProgressionManager _progression;
        private BuffManager _buffs;
        private Coroutine _routine;

        private void OnEnable()
        {
            SetAlpha(0f);

            _fertilizer = FertilizerManager.Instance;
            if (_fertilizer != null) _fertilizer.Used += OnFertilizerUsed;

            _progression = ProgressionManager.Instance;
            if (_progression != null)
            {
                _progression.LevelChanged += OnLevelChanged;
                _progression.Changed += OnProgressChanged;
            }

            _buffs = BuffManager.Instance;
            if (_buffs != null) _buffs.BuffStarted += OnBuffStarted;
        }

        private void OnDisable()
        {
            if (_fertilizer != null)
            {
                _fertilizer.Used -= OnFertilizerUsed;
                _fertilizer = null;
            }

            if (_progression != null)
            {
                _progression.LevelChanged -= OnLevelChanged;
                _progression.Changed -= OnProgressChanged;
                _progression = null;
            }

            if (_buffs != null)
            {
                _buffs.BuffStarted -= OnBuffStarted;
                _buffs = null;
            }

            _routine = null;
            SetAlpha(0f);
        }

        private void OnFertilizerUsed(int grown)
        {
            Flash(fertilizerColor);
        }

        private void OnLevelChanged(int level)
        {
            Flash(levelUpColor);
        }

        // Abrir una zona o subir una mejora: un destello claro (el de subir de nivel, que es dorado, lo reemplaza si llegan juntos).
        private void OnProgressChanged(ProgressChange change)
        {
            if (change.Kind == ProgressChangeKind.ZoneUnlocked || change.Kind == ProgressChangeKind.UpgradeLevelChanged) Flash(purchaseColor);
        }

        private void OnBuffStarted(RewardData reward)
        {
            if (reward == null) return;

            Flash(reward.Kind == RewardKind.MoveSpeedBuff ? turboColor : priceColor);
        }

        // Dispara un destello del color dado (uno nuevo reemplaza al que esté en curso).
        public void Flash(Color color)
        {
            if (image == null || !isActiveAndEnabled) return;

            if (_routine != null) StopCoroutine(_routine);
            _routine = StartCoroutine(Run(color));
        }

        private IEnumerator Run(Color color)
        {
            float elapsed = 0f;
            while (elapsed < seconds)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / seconds);
                SetColor(color, peakAlpha * (1f - t) * (1f - t));   // arranca fuerte y se apaga suave
                yield return null;
            }

            SetAlpha(0f);
            _routine = null;
        }

        private void SetColor(Color color, float alpha)
        {
            color.a = alpha;
            image.color = color;
        }

        private void SetAlpha(float alpha)
        {
            if (image == null) return;

            Color color = image.color;
            color.a = alpha;
            image.color = color;
        }
    }
}
