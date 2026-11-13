using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace FarmIdle
{
    // "Texto grande" (ROADMAP U-04): agranda ×1,3 los textos del Canvas cuando el jugador lo pide en Ajustes. Va en el objeto Canvas.
    //   - Toma una foto de cada TMP_Text del Canvas (tamaño y autoajuste originales) al activarse y la usa para aplicar o deshacer;
    //     sin trabajo por cuadro: solo recalcula cuando cambia el interruptor.
    //   - Para que lo agrandado no se corte, cada texto pasa a autoajuste con tope en ×1,3 y piso en su tamaño original: crece hasta
    //     donde entra en su caja y nunca queda más chico que sin la opción. Los que ya tenían autoajuste conservan su piso y suben el tope.
    //   - Solo conoce a SettingsManager (lee LargeText y escucha Changed); los textos en el mundo (carteles de los pads) no se tocan.
    [DisallowMultipleComponent]
    public sealed class TextScaleApplier : MonoBehaviour
    {
        private struct Original
        {
            public TMP_Text Text;
            public float FontSize;
            public bool AutoSizing;
            public float Min;
            public float Max;
        }

        private readonly List<Original> _originals = new List<Original>(128);
        private SettingsManager _settings;
        private float _appliedScale = 1f;

        public float AppliedScale => _appliedScale;

        private void OnEnable()
        {
            _settings = SettingsManager.Instance;
            if (_settings == null) return;

            Capture();
            _settings.Changed += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            if (_settings == null) return;

            _settings.Changed -= Refresh;
            Apply(1f);
            _originals.Clear();
            _settings = null;
        }

        private void Capture()
        {
            _originals.Clear();
            foreach (TMP_Text text in GetComponentsInChildren<TMP_Text>(true))
            {
                _originals.Add(new Original
                {
                    Text = text,
                    FontSize = text.fontSize,
                    AutoSizing = text.enableAutoSizing,
                    Min = text.fontSizeMin,
                    Max = text.fontSizeMax
                });
            }

            _appliedScale = 1f;
        }

        private void Refresh()
        {
            float scale = _settings.TextScale;
            if (Mathf.Approximately(scale, _appliedScale)) return;

            Apply(scale);
        }

        private void Apply(float scale)
        {
            _appliedScale = scale;
            bool scaled = scale > 1f;

            for (int i = 0; i < _originals.Count; i++)
            {
                Original original = _originals[i];
                TMP_Text text = original.Text;
                if (text == null) continue;

                if (!scaled)
                {
                    text.enableAutoSizing = original.AutoSizing;
                    text.fontSizeMin = original.Min;
                    text.fontSizeMax = original.Max;
                    text.fontSize = original.FontSize;
                    continue;
                }

                float floor = original.AutoSizing ? original.Min : original.FontSize;
                float ceiling = (original.AutoSizing ? original.Max : original.FontSize) * scale;
                text.enableAutoSizing = true;
                text.fontSizeMin = floor;
                text.fontSizeMax = ceiling;
            }
        }
    }
}
