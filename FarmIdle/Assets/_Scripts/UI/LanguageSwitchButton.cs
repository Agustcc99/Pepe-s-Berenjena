using UnityEngine;
using UnityEngine.UI;

namespace FarmIdle
{
    // El botón de idioma del menú inicial: alterna entre inglés y español sin entrar a Ajustes. Su rótulo (clave menu.language_switch)
    // muestra el OTRO idioma, y se reescribe solo al cambiar porque es un texto de la tabla. Guarda la elección como preferencia fija.
    [RequireComponent(typeof(Button))]
    public sealed class LanguageSwitchButton : MonoBehaviour
    {
        private Button _button;

        private void Awake()
        {
            _button = GetComponent<Button>();
        }

        private void OnEnable()
        {
            _button.onClick.AddListener(OnClicked);
        }

        private void OnDisable()
        {
            _button.onClick.RemoveListener(OnClicked);
        }

        private void OnClicked()
        {
            SettingsManager settings = SettingsManager.Instance;
            if (settings == null) return;

            settings.SetLanguage(Loc.Current == GameLanguage.English ? LanguagePreference.Spanish : LanguagePreference.English);
        }
    }
}
