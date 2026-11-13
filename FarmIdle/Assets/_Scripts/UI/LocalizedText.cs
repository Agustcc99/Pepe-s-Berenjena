using TMPro;
using UnityEngine;

namespace FarmIdle
{
    // Un texto fijo de la pantalla (un título, una etiqueta de botón...) que sale de la tabla de textos por su clave (ROADMAP U-03)
    // y se reescribe solo al cambiar el idioma. Lo agrega el builder de escenas: los textos que arma una vista según el estado
    // (mute, nivel, pads...) NO llevan este componente, se encargan ellas.
    [RequireComponent(typeof(TMP_Text))]
    public sealed class LocalizedText : MonoBehaviour
    {
        [SerializeField, Tooltip("Clave en Resources/Localization.csv.")]
        private string key = string.Empty;

        private TMP_Text _text;

        public string Key => key;

        private void OnEnable()
        {
            Loc.Changed += Apply;
            Apply();
        }

        private void OnDisable()
        {
            Loc.Changed -= Apply;
        }

        private void Apply()
        {
            if (_text == null) _text = GetComponent<TMP_Text>();
            if (_text != null && !string.IsNullOrEmpty(key)) _text.text = Loc.Get(key);
        }
    }
}
