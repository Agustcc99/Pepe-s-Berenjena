using NUnit.Framework;
using UnityEngine;

namespace FarmIdle.Tests
{
    // Los ajustes guardados (ROADMAP U-02 y U-04): audio + vibración + idioma + texto grande, en el ISaveable "settings" (versión 3). Se prueba el manager
    // suelto, sin escena: Capture/Restore son JSON puro y los avisos son eventos de C#.
    public class SettingsManagerTests
    {
        private GameObject _host;
        private SettingsManager _settings;
        private int _saveRequests;
        private int _changes;

        [SetUp]
        public void SetUp()
        {
            Loc.SystemLanguageOverride = SystemLanguage.Spanish;
            Loc.SetLanguage(GameLanguage.Spanish);
            _host = new GameObject("SettingsManagerTests");
            _settings = _host.AddComponent<SettingsManager>();
            _settings.Initialize();
            _saveRequests = 0;
            _changes = 0;
            _settings.SaveRequested += () => _saveRequests++;
            _settings.Changed += () => _changes++;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_host);
            Loc.SetLanguage(GameLanguage.Spanish);
            Loc.SystemLanguageOverride = null;
        }

        [Test]
        public void FactoryValues_VibrationOn_LanguageAuto()
        {
            Assert.IsTrue(_settings.VibrationEnabled);
            Assert.AreEqual(LanguagePreference.Auto, _settings.Language);
        }

        [Test]
        public void VibrationAndLanguage_RequestASave_OnlyWhenTheyReallyChange()
        {
            _settings.SetVibration(true);
            _settings.SetLanguage(LanguagePreference.Auto);
            Assert.AreEqual(0, _saveRequests, "Poner el mismo valor no guarda.");

            _settings.SetVibration(false);
            _settings.SetLanguage(LanguagePreference.English);
            Assert.AreEqual(2, _saveRequests);
            Assert.AreEqual(2, _changes);
        }

        [Test]
        public void CaptureThenRestore_BringsBackEverything_WithoutRequestingASave()
        {
            _settings.Audio.SetMusic(0.25f);
            _settings.Audio.SetMuted(true);
            _settings.SetVibration(false);
            _settings.SetLanguage(LanguagePreference.Spanish);
            string json = _settings.Capture();

            _settings.Restore(null);
            Assert.IsTrue(_settings.VibrationEnabled);
            Assert.AreEqual(LanguagePreference.Auto, _settings.Language);

            _saveRequests = 0;
            _settings.Restore(json);
            Assert.AreEqual(0.25f, _settings.Audio.Music, 1e-5f);
            Assert.IsTrue(_settings.Audio.Muted);
            Assert.IsFalse(_settings.VibrationEnabled);
            Assert.AreEqual(LanguagePreference.Spanish, _settings.Language);
            Assert.AreEqual(0, _saveRequests, "Restaurar no pide guardar.");
        }

        [Test]
        public void AVersion1Save_KeepsItsAudio_AndStartsWithFactoryVibrationAndLanguage()
        {
            _settings.SetVibration(false);
            _settings.SetLanguage(LanguagePreference.English);

            _settings.Restore("{\"version\":1,\"master\":1.0,\"music\":0.3,\"sfx\":0.5,\"muted\":true}");

            Assert.AreEqual(0.3f, _settings.Audio.Music, 1e-5f);
            Assert.AreEqual(0.5f, _settings.Audio.Sfx, 1e-5f);
            Assert.IsTrue(_settings.Audio.Muted);
            Assert.IsTrue(_settings.VibrationEnabled, "El v1 no traía vibración: queda en el valor de fábrica.");
            Assert.AreEqual(LanguagePreference.Auto, _settings.Language);
        }

        [Test]
        public void AnUnknownLanguageValue_FallsBackToAuto()
        {
            _settings.Restore("{\"version\":2,\"language\":9}");
            Assert.AreEqual(LanguagePreference.Auto, _settings.Language);
        }

        [Test]
        public void RestoringOnlyAChangedVibration_StillNotifiesTheListeners()
        {
            _changes = 0;
            _settings.Restore("{\"version\":2,\"vibration\":false}");
            Assert.IsFalse(_settings.VibrationEnabled);
            Assert.GreaterOrEqual(_changes, 1, "La tarjeta tiene que refrescarse.");
        }

        [Test]
        public void LargeText_IsOffByDefault_ScalesByOnePointThree_AndSurvivesASaveRoundTrip()
        {
            Assert.IsFalse(_settings.LargeText);
            Assert.AreEqual(1f, _settings.TextScale, 1e-5f);

            _saveRequests = 0;
            _settings.SetLargeText(true);
            _settings.SetLargeText(true);
            Assert.AreEqual(1, _saveRequests, "Cambiar guarda una vez; repetir el mismo valor no.");
            Assert.AreEqual(1.3f, _settings.TextScale, 1e-5f);

            string json = _settings.Capture();
            _settings.Restore(null);
            Assert.IsFalse(_settings.LargeText, "Sin partida vuelve al valor de fábrica.");

            _settings.Restore(json);
            Assert.IsTrue(_settings.LargeText);
        }

        [Test]
        public void AVersion2Save_LoadsWithNormalText()
        {
            _settings.SetLargeText(true);
            _settings.Restore("{\"version\":2,\"vibration\":true,\"language\":1}");
            Assert.IsFalse(_settings.LargeText, "El v2 no traía texto grande.");
            Assert.AreEqual(LanguagePreference.Spanish, _settings.Language);
        }

        [Test]
        public void ARestoreWithGarbage_GoesBackToFactoryValues()
        {
            _settings.SetVibration(false);
            _settings.Restore("esto no es json");
            Assert.IsTrue(_settings.VibrationEnabled);
        }
    }
}
