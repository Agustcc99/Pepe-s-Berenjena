using System;
using UnityEngine;

namespace FarmIdle
{
    // Idioma elegido por el jugador. Auto = el del sistema/navegador (lo resuelve la localización, ROADMAP U-03; hasta entonces solo se guarda).
    public enum LanguagePreference
    {
        Auto = 0,
        Spanish = 1,
        English = 2
    }

    // Dueño de los ajustes del jugador (audio: volumen general, música, efectos y mute; además vibración, idioma y texto grande). Es el ISaveable "settings".
    //   - Va ANTES del SaveManager en el Bootstrapper (la partida se carga una sola vez, al final, y los ajustes tienen que estar
    //     registrados para recibirla); el AudioManager, que los usa, es el último servicio.
    //   - La UI (el panel de ajustes) cambia los valores acá; el AudioManager los lee y escucha Changed. Ninguno se conoce.
    //   - Restaurar no pide guardar. Cada cambio pide guardar (el SaveManager junta los avisos seguidos de un deslizador).
    //   - "Borrar partida" (solo desarrollo) reinicia todos los sistemas guardables, también este: vuelve a los valores de fábrica.
    public sealed class SettingsManager : MonoBehaviour, ISaveable
    {
        public static SettingsManager Instance { get; private set; }

        // v2: se agregan vibración e idioma. v3: texto grande (U-04). Un guardado viejo no trae esos campos y queda con los valores de
        // fábrica (JsonUtility respeta los valores por defecto de los campos que faltan).
        private const int SaveVersion = 3;

        // Cuánto se agranda el texto de la pantalla con "Texto grande" (ROADMAP U-04).
        public const float LargeTextScale = 1.3f;

        [Serializable]
        private sealed class SettingsSave
        {
            public int version = SaveVersion;
            public float master = AudioSettings.DefaultMaster;
            public float music = AudioSettings.DefaultMusic;
            public float sfx = AudioSettings.DefaultSfx;
            public bool muted;
            public bool vibration = true;
            public int language;
            public bool largeText;
            public bool fixedControls;   // U-11; un guardado anterior no lo trae y queda en flotante
        }

        private readonly AudioSettings _audio = new AudioSettings();
        private bool _restoring;

        public AudioSettings Audio => _audio;

        // La vibración corta del celular (la dispara U-06; acá solo vive el interruptor). Activada por defecto.
        public bool VibrationEnabled { get; private set; } = true;

        public LanguagePreference Language { get; private set; } = LanguagePreference.Auto;

        public void SetVibration(bool enabled)
        {
            if (enabled == VibrationEnabled) return;

            VibrationEnabled = enabled;
            OnAudioChanged();
        }

        // Texto grande (accesibilidad): la UI lo aplica con TextScaleApplier; acá solo vive el interruptor. Apagado por defecto.
        public bool LargeText { get; private set; }

        public float TextScale => LargeText ? LargeTextScale : 1f;

        public void SetLargeText(bool enabled)
        {
            if (enabled == LargeText) return;

            LargeText = enabled;
            OnAudioChanged();
        }

        // Control fijo (U-11): el joystick queda siempre a la vista en un lugar fijo, en vez de aparecer donde se toca. La UI lo aplica
        // (FloatingJoystick); acá solo vive el interruptor. Apagado por defecto.
        public bool FixedControls { get; private set; }

        public void SetFixedControls(bool enabled)
        {
            if (enabled == FixedControls) return;

            FixedControls = enabled;
            OnAudioChanged();
        }

        public void SetLanguage(LanguagePreference language)
        {
            LanguagePreference value = Sanitize(language);
            if (value == Language) return;

            Language = value;
            ApplyLanguage();
            OnAudioChanged();
        }

        // Un valor desconocido (un guardado de una versión futura) vuelve a Auto.
        private static LanguagePreference Sanitize(LanguagePreference language)
        {
            return language == LanguagePreference.Spanish || language == LanguagePreference.English ? language : LanguagePreference.Auto;
        }

        // Cambió algún ajuste (jugando o al restaurar).
        public event Action Changed;

        // Lo llama el Bootstrapper, antes del SaveManager; hasta entonces Instance es null.
        public void Initialize()
        {
            _audio.Changed += OnAudioChanged;
            Instance = this;
            ApplyLanguage();
        }

        // Lleva la preferencia a la tabla de textos (Automático = idioma del sistema o del navegador).
        private void ApplyLanguage()
        {
            switch (Language)
            {
                case LanguagePreference.Spanish: Loc.SetLanguage(GameLanguage.Spanish); break;
                case LanguagePreference.English: Loc.SetLanguage(GameLanguage.English); break;
                default: Loc.SetLanguage(Loc.ResolveAuto()); break;
            }
        }

        private void OnAudioChanged()
        {
            Changed?.Invoke();
            if (!_restoring) SaveRequested?.Invoke();
        }

        // ---------- Partida guardada ----------

        public string SaveId => "settings";

        public event Action SaveRequested;

        public string Capture()
        {
            return JsonUtility.ToJson(new SettingsSave
            {
                master = _audio.Master,
                music = _audio.Music,
                sfx = _audio.Sfx,
                muted = _audio.Muted,
                vibration = VibrationEnabled,
                language = (int)Language,
                largeText = LargeText,
                fixedControls = FixedControls
            });
        }

        // Con null o vacío (partida nueva, o un archivo de una fase anterior que no trae esta sección) quedan los valores de fábrica. Un
        // JSON ilegible se toma igual como "de fábrica".
        public void Restore(string json)
        {
            SettingsSave save = null;
            if (!string.IsNullOrWhiteSpace(json))
            {
                try
                {
                    save = JsonUtility.FromJson<SettingsSave>(json);
                }
                catch (ArgumentException)
                {
                    save = null;
                }
            }

            bool vibration = save == null || save.vibration;
            LanguagePreference language = save == null ? LanguagePreference.Auto : Sanitize((LanguagePreference)save.language);
            bool largeText = save != null && save.largeText;
            bool fixedControls = save != null && save.fixedControls;
            bool otherChanged = vibration != VibrationEnabled || language != Language || largeText != LargeText || fixedControls != FixedControls;
            FixedControls = fixedControls;
            VibrationEnabled = vibration;
            Language = language;
            LargeText = largeText;
            ApplyLanguage();

            _restoring = true;
            try
            {
                if (save == null) _audio.ResetToDefaults();
                else _audio.Load(save.master, save.music, save.sfx, save.muted);
            }
            finally
            {
                _restoring = false;
            }

            // El audio avisa por su cuenta; si solo cambió la vibración o el idioma, avisamos acá (sin pedir guardar).
            if (otherChanged) Changed?.Invoke();
        }

        private void OnDestroy()
        {
            _audio.Changed -= OnAudioChanged;
            if (Instance == this) Instance = null;
        }
    }
}
