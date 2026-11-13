using TMPro;
using UnityEngine;

namespace FarmIdle
{
    // El texto del cartel de un sector de la plaza (Videos, Atributos...). Sale de la tabla de textos con su clave y se reescribe al
    // cambiar el idioma. NO se orienta a la cámara: el tablón es fijo y un texto inclinado se hundía en él (se veía cortado).
    [RequireComponent(typeof(TextMeshPro))]
    public sealed class PlazaSignLabel : MonoBehaviour
    {
        [SerializeField, Tooltip("Clave de la tabla de textos (plaza.*).")]
        private string key;

        private TextMeshPro _text;

        private void Awake()
        {
            _text = GetComponent<TextMeshPro>();
        }

        private void OnEnable()
        {
            Loc.Changed += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            Loc.Changed -= Refresh;
        }

        private void Refresh()
        {
            if (_text != null && !string.IsNullOrEmpty(key)) _text.text = Loc.Get(key);
        }
    }
}
