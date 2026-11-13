using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace FarmIdle
{
    // El menú de ajustes del HUD: un ícono de parlante chico (no es un botón de gameplay: no hace nada en el mundo) que abre una
    // tarjeta con el volumen de la música y el de los efectos, el mute, la vibración, el idioma, los créditos, la política de
    // privacidad y "Borrar partida" (con confirmación). Solo habla con SettingsManager (y pide el borrado al SaveManager): lee los
    // valores y los cambia; el AudioManager los escucha por su cuenta, así que la UI no conoce el audio.
    //   - El juego sigue corriendo con la tarjeta abierta (en pausa el audio se silencia y no se oiría el cambio); la tarjeta tapa el
    //     joystick, así que el granjero se queda quieto mientras se ajusta.
    //   - Se cierra con la X o tocando afuera de la tarjeta (Esc no hace nada a propósito: la guía de CrazyGames recomienda no usarla). Créditos y la confirmación de borrado son paneles encima de la tarjeta.
    //   - Sin SettingsManager (una escena suelta) el ícono queda apagado.
    //   - Los textos fijos de la tarjeta los pone LocalizedText; acá solo se reescriben los que dependen del estado (mute, vibración, idioma).
    public sealed class SettingsPanelView : MonoBehaviour
    {
        [Header("Ícono del HUD")]
        [SerializeField] private Button openButton;
        [SerializeField] private Image openIcon;
        [SerializeField] private Sprite soundOnSprite;
        [SerializeField] private Sprite soundOffSprite;

        [Header("Tarjeta")]
        [SerializeField] private GameObject panel;
        [SerializeField] private Slider musicSlider;
        [SerializeField] private Slider sfxSlider;
        [SerializeField] private Button muteButton;
        [SerializeField] private TMP_Text muteLabel;
        [SerializeField, Tooltip("La X de la esquina y el fondo oscuro de afuera: los dos cierran.")]
        private Button closeButton;
        [SerializeField] private Button backdropButton;
        [SerializeField, Tooltip("\"Privacidad\": abre el formulario de consentimiento de anuncios. Solo se ve si el jugador está en una región que lo exige (UE...). Puede faltar.")]
        private Button privacyButton;

        [Header("Vibración e idioma")]
        [SerializeField] private Button vibrationButton;
        [SerializeField] private TMP_Text vibrationLabel;
        [SerializeField] private Button languageButton;
        [SerializeField] private TMP_Text languageLabel;
        [SerializeField, Tooltip("Texto grande (U-04): alterna entre tamaño normal y ×1,3. Puede faltar.")] private Button textSizeButton;
        [SerializeField] private TMP_Text textSizeLabel;

        [Header("Créditos y política")]
        [SerializeField] private Button creditsButton;
        [SerializeField] private GameObject creditsPanel;
        [SerializeField] private Button creditsCloseButton;
        [SerializeField, Tooltip("Dirección de la política de privacidad (ROADMAP M-04). Vacía: el botón queda oculto.")]
        private string privacyPolicyUrl = string.Empty;
        [SerializeField] private Button privacyPolicyButton;

        [Header("Borrar partida")]
        [SerializeField] private Button deleteButton;
        [SerializeField] private GameObject deleteConfirmPanel;
        [SerializeField] private Button deleteConfirmYesButton;
        [SerializeField] private Button deleteConfirmNoButton;

        private SettingsManager _settings;

        public bool IsOpen => panel != null && panel.activeSelf;

        public bool IsCreditsOpen => creditsPanel != null && creditsPanel.activeSelf;

        public bool IsDeleteConfirmOpen => deleteConfirmPanel != null && deleteConfirmPanel.activeSelf;

        private void OnEnable()
        {
            _settings = SettingsManager.Instance;
            if (_settings == null)
            {
                if (openButton != null) openButton.gameObject.SetActive(false);
                if (panel != null) panel.SetActive(false);
                return;
            }

            openButton.onClick.AddListener(Open);
            closeButton.onClick.AddListener(Close);
            if (backdropButton != null) backdropButton.onClick.AddListener(Close);
            muteButton.onClick.AddListener(OnMuteClicked);
            if (privacyButton != null) privacyButton.onClick.AddListener(OnPrivacyClicked);
            musicSlider.onValueChanged.AddListener(OnMusicChanged);
            sfxSlider.onValueChanged.AddListener(OnSfxChanged);
            Hook(vibrationButton, OnVibrationClicked);
            Hook(languageButton, OnLanguageClicked);
            Hook(textSizeButton, OnTextSizeClicked);
            Hook(creditsButton, OpenCredits);
            Hook(creditsCloseButton, CloseCredits);
            Hook(privacyPolicyButton, OnPrivacyPolicyClicked);
            Hook(deleteButton, OpenDeleteConfirm);
            Hook(deleteConfirmYesButton, ConfirmDelete);
            Hook(deleteConfirmNoButton, CloseDeleteConfirm);
            _settings.Changed += Refresh;
            Loc.Changed += Refresh;

            panel.SetActive(false);
            Refresh();
        }

        private void OnDisable()
        {
            if (_settings == null) return;

            openButton.onClick.RemoveListener(Open);
            closeButton.onClick.RemoveListener(Close);
            if (backdropButton != null) backdropButton.onClick.RemoveListener(Close);
            muteButton.onClick.RemoveListener(OnMuteClicked);
            if (privacyButton != null) privacyButton.onClick.RemoveListener(OnPrivacyClicked);
            musicSlider.onValueChanged.RemoveListener(OnMusicChanged);
            sfxSlider.onValueChanged.RemoveListener(OnSfxChanged);
            Unhook(vibrationButton, OnVibrationClicked);
            Unhook(languageButton, OnLanguageClicked);
            Unhook(textSizeButton, OnTextSizeClicked);
            Unhook(creditsButton, OpenCredits);
            Unhook(creditsCloseButton, CloseCredits);
            Unhook(privacyPolicyButton, OnPrivacyPolicyClicked);
            Unhook(deleteButton, OpenDeleteConfirm);
            Unhook(deleteConfirmYesButton, ConfirmDelete);
            Unhook(deleteConfirmNoButton, CloseDeleteConfirm);
            _settings.Changed -= Refresh;
            Loc.Changed -= Refresh;
            _settings = null;
        }

        private static void Hook(Button button, UnityAction action)
        {
            if (button != null) button.onClick.AddListener(action);
        }

        private static void Unhook(Button button, UnityAction action)
        {
            if (button != null) button.onClick.RemoveListener(action);
        }

        public void Open()
        {
            panel.SetActive(true);
            Refresh();
        }

        // Cerrar la tarjeta cierra también los paneles de encima, para que se reabra siempre limpia.
        public void Close()
        {
            CloseCredits();
            CloseDeleteConfirm();
            panel.SetActive(false);
        }

        public void OpenCredits()
        {
            if (creditsPanel != null) creditsPanel.SetActive(true);
        }

        public void CloseCredits()
        {
            if (creditsPanel != null) creditsPanel.SetActive(false);
        }

        public void OpenDeleteConfirm()
        {
            if (deleteConfirmPanel != null) deleteConfirmPanel.SetActive(true);
        }

        public void CloseDeleteConfirm()
        {
            if (deleteConfirmPanel != null) deleteConfirmPanel.SetActive(false);
        }

        // Solo el "Sí, borrar" llega acá. El SaveManager deja todos los sistemas como en una partida nueva (también estos ajustes).
        private void ConfirmDelete()
        {
            SaveManager save = SaveManager.Instance;
            if (save != null) save.DeleteSave();
            Close();
        }

        private void OnMuteClicked()
        {
            _settings.Audio.SetMuted(!_settings.Audio.Muted);
        }

        private void OnVibrationClicked()
        {
            _settings.SetVibration(!_settings.VibrationEnabled);
        }

        private void OnTextSizeClicked()
        {
            _settings.SetLargeText(!_settings.LargeText);
        }

        // Automático → Español → English → Automático.
        private void OnLanguageClicked()
        {
            LanguagePreference next;
            switch (_settings.Language)
            {
                case LanguagePreference.Auto: next = LanguagePreference.Spanish; break;
                case LanguagePreference.Spanish: next = LanguagePreference.English; break;
                default: next = LanguagePreference.Auto; break;
            }

            _settings.SetLanguage(next);
        }

        // Abre el formulario de consentimiento (lo maneja el servicio de anuncios; la UI solo lo pide).
        private void OnPrivacyClicked()
        {
            AdsManager ads = AdsManager.Instance;
            if (ads != null) ads.ShowPrivacyOptions();
        }

        private void OnPrivacyPolicyClicked()
        {
            if (!string.IsNullOrWhiteSpace(privacyPolicyUrl)) Application.OpenURL(privacyPolicyUrl);
        }

        private void OnMusicChanged(float value)
        {
            _settings.Audio.SetMusic(value);
        }

        private void OnSfxChanged(float value)
        {
            _settings.Audio.SetSfx(value);
        }

        // Pone en la tarjeta y en el ícono lo que dicen los ajustes, sin disparar los eventos de los deslizadores.
        private void Refresh()
        {
            AudioSettings audio = _settings.Audio;

            musicSlider.SetValueWithoutNotify(audio.Music);
            sfxSlider.SetValueWithoutNotify(audio.Sfx);
            if (muteLabel != null) muteLabel.text = Loc.Get(audio.Muted ? "settings.unmute" : "settings.mute");
            if (openIcon != null) openIcon.sprite = audio.Muted ? soundOffSprite : soundOnSprite;
            if (vibrationLabel != null) vibrationLabel.text = Loc.Get(_settings.VibrationEnabled ? "settings.vibration_on" : "settings.vibration_off");
            if (languageLabel != null) languageLabel.text = LanguageText(_settings.Language);
            if (textSizeLabel != null) textSizeLabel.text = Loc.Get(_settings.LargeText ? "settings.text_size_large" : "settings.text_size_normal");

            // "Privacidad" solo aparece si la región del jugador lo exige.
            if (privacyButton != null)
            {
                AdsManager ads = AdsManager.Instance;
                privacyButton.gameObject.SetActive(ads != null && ads.PrivacyOptionsRequired);
            }

            // La política solo aparece cuando ya hay una dirección publicada.
            if (privacyPolicyButton != null) privacyPolicyButton.gameObject.SetActive(!string.IsNullOrWhiteSpace(privacyPolicyUrl));
        }

        private static string LanguageText(LanguagePreference language)
        {
            switch (language)
            {
                case LanguagePreference.Spanish: return Loc.Get("settings.language_es");
                case LanguagePreference.English: return Loc.Get("settings.language_en");
                default: return Loc.Get("settings.language_auto");
            }
        }
    }
}
