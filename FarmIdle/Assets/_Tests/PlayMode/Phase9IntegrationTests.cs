using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace FarmIdle.Tests
{
    // La Fase 9 en el MainLevel real, arrancando desde Bootstrap como el juego (con un almacenamiento en memoria: nunca tocan la
    // partida del dispositivo): el audio (qué escucha, cuándo se calla, los ajustes que se guardan), la tarjeta de sonido del HUD y la guía
    // de los primeros minutos de punta a punta, caminando.
    public class Phase9IntegrationTests
    {
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return GameBoot.CleanUp();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return GameBoot.CleanUp();
        }

        private static T[] All<T>() where T : Object
        {
            return Object.FindObjectsByType<T>(FindObjectsInactive.Include);
        }

        private static IEnumerator WaitFor(Func<bool> condition, float timeoutSeconds = 6f)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
        }

        private static GameObject Player => GameObject.FindWithTag("Player");

        private static PlotView[] InitialPlots()
        {
            // El campo inicial: el activo que está en el centro (las zonas cerradas tienen su campo apagado).
            return All<PlotField>().First(field => field.gameObject.activeInHierarchy && Mathf.Abs(field.transform.position.x) < 0.1f && Mathf.Abs(field.transform.position.z - 7f) < 0.1f).Plots.ToArray();
        }

        private static IEnumerator StartPlaying()
        {
            yield return GameBoot.BootToReady();
            yield return null;
            Assert.IsTrue(GameManager.Instance.StartPlaying());
            yield return null;
        }

        // ---------- Servicios ----------

        [UnityTest]
        public IEnumerator TheGame_CreatesTheNewServices_AndTheAudioIsWiredToItsBank()
        {
            yield return GameBoot.BootToReady();
            yield return null;

            Assert.IsNotNull(SettingsManager.Instance);
            Assert.IsNotNull(TutorialManager.Instance);
            AudioManager audio = AudioManager.Instance;
            Assert.IsNotNull(audio);
            Assert.IsTrue(audio.HasBank, "El GameConfig trae el banco de audio.");
            Assert.AreEqual(10, audio.VoiceCount, "Las voces se crean una sola vez, al arrancar.");
            Assert.IsFalse(audio.IsMusicPlaying, "La música no suena en el menú: arranca con Tap to Play.");
        }

        // ---------- Audio ----------

        [UnityTest]
        public IEnumerator TheAudio_ListensToTheGame_OneCuePerEvent()
        {
            yield return StartPlaying();
            AudioManager audio = AudioManager.Instance;
            EconomyManager economy = EconomyManager.Instance;
            CropData wheat = InitialPlots()[0].Crop;

            // Venta: cada unidad es un disparo, y el tono sube mientras se vende seguido.
            economy.SellProduct(wheat);
            Assert.AreEqual(1, audio.TriggerCount(SoundCue.Sell));
            Assert.AreEqual(SoundCue.Sell, audio.LastCue);

            // Cosecha: entra un producto a la mochila.
            Assert.IsTrue(BackpackManager.Instance.TryAdd(wheat));
            Assert.AreEqual(1, audio.TriggerCount(SoundCue.Harvest));

            // Compra: una mejora al nivel 1 por la puerta de siempre.
            UpgradeData upgrade = ProgressionManager.Instance.Upgrades[0];
            economy.AddCoins(10000);
            ProgressionManager.Instance.TryPay(upgrade, upgrade.CostOfLevel(1));
            Assert.AreEqual(1, audio.TriggerCount(SoundCue.Purchase));

            // Fertilizante: se suma stock y se usa con una planta creciendo.
            InitialPlots()[0].TrySow();
            FertilizerManager.Instance.DebugGive(1);
            Assert.Greater(FertilizerManager.Instance.TryUse(), 0);
            Assert.AreEqual(1, audio.TriggerCount(SoundCue.Fertilize));

            // Subir de nivel: mucha XP de golpe por ventas.
            int before = audio.TriggerCount(SoundCue.LevelUp);
            for (int i = 0; i < RealXp.UnitsToReachLevel(2, wheat.SellPrice); i++) economy.SellProduct(wheat);
            Assert.Greater(audio.TriggerCount(SoundCue.LevelUp), before, "Pasó al nivel 2.");

            // "No se puede": el pad avisa a Managers, el audio lo oye.
            int denied = audio.TriggerCount(SoundCue.Denied);
            ProgressionManager.Instance.ReportRefused();
            Assert.AreEqual(denied + 1, audio.TriggerCount(SoundCue.Denied));
        }

        [UnityTest]
        public IEnumerator TheAudio_StartsTheMusicOnTapToPlay_AndGoesSilentOnPauseAdsAndFocusLoss()
        {
            yield return GameBoot.BootToReady();
            yield return null;
            AudioManager audio = AudioManager.Instance;
            Assert.IsFalse(audio.ListenerPaused);

            GameManager.Instance.StartPlaying();
            yield return null;
            Assert.IsTrue(audio.IsMusicPlaying, "Tap to Play arranca la música (y en la web desbloquea el audio).");
            Assert.IsFalse(audio.ListenerPaused);

            GameManager.Instance.Pause();
            Assert.IsTrue(audio.ListenerPaused, "En pausa se calla todo.");
            GameManager.Instance.Resume();
            Assert.IsFalse(audio.ListenerPaused);

            GameManager.Instance.BeginAd();
            Assert.IsTrue(audio.ListenerPaused, "Con un anuncio en pantalla se calla todo.");
            GameManager.Instance.EndAd();
            Assert.IsFalse(audio.ListenerPaused);

            audio.SendMessage("OnApplicationFocus", false);
            Assert.IsTrue(audio.ListenerPaused, "Al perder el foco se calla todo.");
            audio.SendMessage("OnApplicationFocus", true);
            Assert.IsFalse(audio.ListenerPaused);

            // Un anuncio y perder el foco a la vez: recuperar el foco no puede romper el silencio del anuncio.
            GameManager.Instance.BeginAd();
            audio.SendMessage("OnApplicationFocus", false);
            audio.SendMessage("OnApplicationFocus", true);
            Assert.IsTrue(audio.ListenerPaused, "El anuncio sigue en pantalla.");
            GameManager.Instance.EndAd();
            Assert.IsFalse(audio.ListenerPaused);
        }

        [UnityTest]
        public IEnumerator Mute_SilencesMusicAndEffects_AndTheVolumesReachTheMixer()
        {
            yield return StartPlaying();
            AudioManager audio = AudioManager.Instance;
            AudioSettings settings = SettingsManager.Instance.Audio;
            float musicBefore = audio.MusicVolume;
            Assert.Greater(musicBefore, 0f);

            settings.SetMuted(true);
            Assert.AreEqual(0f, audio.MusicVolume, "Mute: la música queda en 0.");
            Assert.IsFalse(audio.Play(SoundCue.Click), "Mute: ningún efecto sale por una voz.");

            settings.SetMuted(false);
            Assert.AreEqual(musicBefore, audio.MusicVolume, 1e-5f);

            settings.SetMusic(0.5f);
            Assert.AreEqual(musicBefore * AudioSettings.Curve(0.5f) / AudioSettings.Curve(AudioSettings.DefaultMusic), audio.MusicVolume, 1e-4f);
        }

        // ---------- Ajustes guardados ----------

        [UnityTest]
        public IEnumerator TheSoundSettings_AreSaved_AndComeBackOnTheNextStart()
        {
            yield return GameBoot.BootToReady();
            yield return null;

            AudioSettings settings = SettingsManager.Instance.Audio;
            settings.SetMusic(0.25f);
            settings.SetSfx(0.4f);
            settings.SetMuted(true);
            Assert.IsTrue(SaveManager.Instance.IsDirty, "Cambiar un ajuste pide guardar.");
            Assert.IsTrue(SaveManager.Instance.SaveNow(force: true));

            MemorySaveStorage storage = GameBoot.Storage;
            yield return GameBoot.CleanUp();
            yield return GameBoot.BootToReady(storage);
            yield return null;

            AudioSettings restored = SettingsManager.Instance.Audio;
            Assert.AreEqual(0.25f, restored.Music, 1e-5f);
            Assert.AreEqual(0.4f, restored.Sfx, 1e-5f);
            Assert.IsTrue(restored.Muted);
            Assert.AreEqual(0f, AudioManager.Instance.MusicVolume, "Y el audio arranca ya en mute.");
            Assert.IsFalse(SaveManager.Instance.IsDirty, "Restaurar no pide guardar.");
        }

        [UnityTest]
        public IEnumerator ASaveWithoutTheNewSections_StartsWithFactorySettings_AndKeepsTheRest()
        {
            var data = new SaveData();
            data.Set("economy", "{\"version\":1,\"coins\":321}");
            var storage = new MemorySaveStorage();
            storage.Put(SaveFile.MainSlot, SaveSerializer.Serialize(data));

            yield return GameBoot.BootToReady(storage);
            yield return null;

            Assert.AreEqual(321L, EconomyManager.Instance.Balance);
            Assert.AreEqual(AudioSettings.DefaultMusic, SettingsManager.Instance.Audio.Music, 1e-5f);
            Assert.IsFalse(SettingsManager.Instance.Audio.Muted);
        }

        // ---------- La tarjeta de sonido del HUD ----------

        [UnityTest]
        public IEnumerator TheSoundCard_OpensFromTheHud_ChangesTheSettings_AndCloses()
        {
            yield return StartPlaying();
            SettingsPanelView view = Object.FindAnyObjectByType<SettingsPanelView>();
            Assert.IsNotNull(view, "El HUD tiene el ícono de sonido.");
            Assert.IsFalse(view.IsOpen);

            view.GetComponent<Button>().onClick.Invoke();
            Assert.IsTrue(view.IsOpen);

            Slider music = All<Slider>().First(slider => slider.name == "Slider0");
            Slider sfx = All<Slider>().First(slider => slider.name == "Slider1");
            AudioSettings settings = SettingsManager.Instance.Audio;
            Assert.AreEqual(settings.Music, music.value, 1e-5f, "La tarjeta muestra lo que dicen los ajustes.");

            music.value = 0.2f;
            sfx.value = 0.6f;
            Assert.AreEqual(0.2f, settings.Music, 1e-5f);
            Assert.AreEqual(0.6f, settings.Sfx, 1e-5f);

            Button mute = All<Button>().First(button => button.name == "MuteButton");
            mute.onClick.Invoke();
            Assert.IsTrue(settings.Muted);
            mute.onClick.Invoke();
            Assert.IsFalse(settings.Muted);

            Button close = All<Button>().First(button => button.name == "CloseButton");
            close.onClick.Invoke();
            Assert.IsFalse(view.IsOpen);
            Assert.AreEqual(GameState.Playing, GameManager.Instance.State, "Abrir la tarjeta no pausa el juego.");
        }

        [UnityTest]
        public IEnumerator TheSettingsCard_HasVibrationLanguageCreditsAndAConfirmedDelete()
        {
            yield return StartPlaying();
            SettingsPanelView view = Object.FindAnyObjectByType<SettingsPanelView>();
            SettingsManager settings = SettingsManager.Instance;
            view.Open();

            // Vibración e idioma: se cambian desde la tarjeta y se guardan.
            Button vibration = All<Button>().First(button => button.name == "VibrationButton");
            vibration.onClick.Invoke();
            Assert.IsFalse(settings.VibrationEnabled);
            Assert.AreEqual(Loc.Get("settings.vibration_off"), vibration.GetComponentInChildren<TMPro.TMP_Text>().text);

            Button language = All<Button>().First(button => button.name == "LanguageButton");
            language.onClick.Invoke();
            Assert.AreEqual(LanguagePreference.Spanish, settings.Language);
            language.onClick.Invoke();
            Assert.AreEqual(LanguagePreference.English, settings.Language);
            language.onClick.Invoke();
            Assert.AreEqual(LanguagePreference.Auto, settings.Language, "El tercer toque vuelve a Automático.");

            // Créditos: abre y cierra sin tocar nada más.
            All<Button>().First(button => button.name == "CreditsButton").onClick.Invoke();
            Assert.IsTrue(view.IsCreditsOpen);
            All<Button>().First(button => button.name == "CreditsCloseButton").onClick.Invoke();
            Assert.IsFalse(view.IsCreditsOpen);

            // La política de privacidad queda oculta mientras no haya dirección publicada (M-04).
            Assert.IsFalse(All<Button>().First(button => button.name == "PrivacyPolicyButton").gameObject.activeSelf);

            // Borrar partida: pide confirmación; cancelar no borra, confirmar sí (y deja los ajustes de fábrica).
            settings.Audio.SetMusic(0.2f);
            Button delete = All<Button>().First(button => button.name == "DeleteSaveButton");
            delete.onClick.Invoke();
            Assert.IsTrue(view.IsDeleteConfirmOpen);
            Assert.AreEqual(0.2f, settings.Audio.Music, 1e-5f, "Abrir la confirmación no borra nada.");

            All<Button>().First(button => button.name == "DeleteConfirmNoButton").onClick.Invoke();
            Assert.IsFalse(view.IsDeleteConfirmOpen);
            Assert.AreEqual(0.2f, settings.Audio.Music, 1e-5f, "Cancelar no borra nada.");

            delete.onClick.Invoke();
            All<Button>().First(button => button.name == "DeleteConfirmYesButton").onClick.Invoke();
            Assert.AreEqual(AudioSettings.DefaultMusic, settings.Audio.Music, 1e-5f, "Confirmar deja todo como en una partida nueva.");
            Assert.IsTrue(settings.VibrationEnabled);
            Assert.IsFalse(view.IsOpen, "Y cierra la tarjeta.");
            Assert.IsFalse(view.IsDeleteConfirmOpen);
        }

        [UnityTest]
        public IEnumerator TheLanguage_ChangesLive_OnTheStaticTexts_TheHud_TheSignsAndTheSettingsLabels_AndIsSaved()
        {
            yield return StartPlaying();
            SettingsManager settings = SettingsManager.Instance;
            Assert.AreEqual(GameLanguage.Spanish, Loc.Current, "Las pruebas arrancan en español.");
            Object.FindAnyObjectByType<SettingsPanelView>().Open();   // un texto de una tarjeta cerrada se actualiza al abrirla

            LocalizedText title = All<LocalizedText>().First(text => text.Key == "settings.title");
            TMPro.TMP_Text levelLabel = All<TMPro.TMP_Text>().First(text => text.name == "Level");
            UpgradePad upgradePad = All<UpgradePad>().First();
            StringAssert.Contains(upgradePad.Upgrade.DisplayName, upgradePad.Label.text);
            Assert.AreEqual("Ajustes", title.GetComponent<TMPro.TMP_Text>().text);
            StringAssert.StartsWith("Nv ", levelLabel.text);

            settings.SetLanguage(LanguagePreference.English);
            Assert.AreEqual(GameLanguage.English, Loc.Current);
            Assert.AreEqual("Settings", title.GetComponent<TMPro.TMP_Text>().text, "El texto fijo cambia solo.");
            StringAssert.StartsWith("Lv ", levelLabel.text, "El HUD se reescribe.");
            Assert.AreEqual(Loc.Get("settings.language_en"), All<Button>().First(button => button.name == "LanguageButton").GetComponentInChildren<TMPro.TMP_Text>().text,
                "Y las etiquetas que arma la tarjeta.");
            StringAssert.Contains(upgradePad.Upgrade.DisplayName, upgradePad.Label.text, "Los carteles del mundo también.");
            Assert.AreEqual(Loc.Get("data." + upgradePad.Upgrade.Id, GameLanguage.English), upgradePad.Upgrade.DisplayName);

            // Se guarda y vuelve en el próximo arranque.
            Assert.IsTrue(SaveManager.Instance.SaveNow(force: true));
            MemorySaveStorage storage = GameBoot.Storage;
            yield return GameBoot.CleanUp();
            yield return GameBoot.BootToReady(storage);
            yield return null;
            Assert.AreEqual(LanguagePreference.English, SettingsManager.Instance.Language);
            Assert.AreEqual(GameLanguage.English, Loc.Current, "Restaurar la partida deja el idioma elegido.");

            SettingsManager.Instance.SetLanguage(LanguagePreference.Auto);   // vuelve a Automático (las pruebas fijan el sistema en español)
            Assert.AreEqual(GameLanguage.Spanish, Loc.Current);
        }

        // ---------- La guía de los primeros minutos ----------

        [UnityTest]
        public IEnumerator TheGuide_WalksThePlayerThroughSowHarvestSellAndBuy_ThenNeverComesBack()
        {
            yield return StartPlaying();
            TutorialManager tutorial = TutorialManager.Instance;
            GameObject player = Player;
            PlotView[] plots = InitialPlots();
            TutorialTargetMarker[] markers = All<TutorialTargetMarker>();
            Assert.AreEqual(3, markers.Length, "Tres lugares: el campo, el pad de venta y la primera mejora.");

            // 1) Sembrar: la flecha está sobre el campo.
            Assert.AreEqual(TutorialStep.Sow, tutorial.Step);
            Assert.IsNotNull(tutorial.CurrentTarget);
            Assert.AreEqual(TutorialStep.Sow, tutorial.CurrentTarget.Step);
            TutorialTargetMarker field = markers.First(marker => marker.Step == TutorialStep.Sow);
            Assert.IsTrue(field.IsCurrent, "La flecha del campo se ve.");
            Assert.IsFalse(markers.First(marker => marker.Step == TutorialStep.Sell).IsCurrent, "Las demás están apagadas.");

            foreach (int index in new[] { 1, 2, 0 }) yield return FieldIntegrationTests.WalkTo(player, plots[index].transform.position);
            Assert.AreEqual(TutorialStep.Harvest, tutorial.Step, "Tres parcelas creciendo: se sembró.");
            Assert.IsTrue(field.IsCurrent, "Cosechar sigue siendo en el campo.");

            // 2) Cosechar tres.
            foreach (PlotView plot in plots) plot.Tick(1000f);
            foreach (int index in new[] { 0, 1, 2 }) yield return FieldIntegrationTests.WalkTo(player, plots[index].transform.position);
            yield return WaitFor(() => tutorial.Step == TutorialStep.Sell, 4f);
            Assert.AreEqual(TutorialStep.Sell, tutorial.Step, "Tres cosechas.");
            Assert.IsFalse(field.IsCurrent);
            Assert.IsTrue(markers.First(marker => marker.Step == TutorialStep.Sell).IsCurrent, "Ahora la flecha está sobre el pad de venta.");

            // 3) Vender.
            SellZone sellZone = Object.FindAnyObjectByType<SellZone>();
            yield return FieldIntegrationTests.WalkTo(player, sellZone.transform.position);
            yield return WaitFor(() => tutorial.Step == TutorialStep.Buy, 4f);
            Assert.AreEqual(TutorialStep.Buy, tutorial.Step, "La primera venta.");

            // 4) Comprar la primera mejora (con monedas por la vía de siempre, como el botón +1000).
            EconomyManager.Instance.AddCoins(1000);
            UpgradePad growth = All<UpgradePad>().First(pad => pad.Upgrade.Kind == UpgradeKind.GrowthSpeed);
            Assert.IsTrue(markers.First(marker => marker.Step == TutorialStep.Buy).IsCurrent, "La flecha de la compra está sobre la mejora.");
            yield return FieldIntegrationTests.WalkTo(player, growth.transform.position);
            yield return WaitFor(() => tutorial.Step == TutorialStep.Done);
            Assert.AreEqual(TutorialStep.Done, tutorial.Step, "La primera compra termina la guía.");
            Assert.IsFalse(tutorial.IsActive);
            Assert.IsNull(tutorial.CurrentTarget);
            Assert.IsFalse(markers.Any(marker => marker.IsCurrent), "Ninguna flecha queda a la vista.");

            // 5) Guardada: cerrar y volver a abrir, y no reaparece.
            Assert.IsTrue(SaveManager.Instance.SaveNow(force: true));
            MemorySaveStorage storage = GameBoot.Storage;
            yield return GameBoot.CleanUp();
            yield return GameBoot.BootToReady(storage);
            yield return null;

            Assert.AreEqual(TutorialStep.Done, TutorialManager.Instance.Step);
            Assert.IsNull(TutorialManager.Instance.CurrentTarget);
            Assert.IsFalse(All<TutorialTargetMarker>().Any(marker => marker.IsCurrent));
        }

        [UnityTest]
        public IEnumerator TheGuide_SkipsItselfForAGameThatAlreadyHasProgress_ButNotForANewOne()
        {
            // Una partida de la Fase 8: tiene progreso (una zona abierta) pero no la sección "tutorial".
            var data = new SaveData();
            data.Set("economy", "{\"version\":1,\"coins\":5}");
            data.Set("progress", JsonUtility.ToJson(new ProgressData { unlockedZones = new[] { "zone_east" } }));
            var storage = new MemorySaveStorage();
            storage.Put(SaveFile.MainSlot, SaveSerializer.Serialize(data));

            yield return GameBoot.BootToReady(storage);
            yield return null;

            Assert.IsFalse(TutorialManager.Instance.IsActive, "Quien ya tiene una granja armada no necesita la guía.");
            Assert.IsFalse(All<TutorialTargetMarker>().Any(marker => marker.IsCurrent));

            // Una partida nueva sí la tiene.
            yield return GameBoot.CleanUp();
            yield return GameBoot.BootToReady();
            yield return null;
            Assert.IsTrue(TutorialManager.Instance.IsActive);
            Assert.AreEqual(TutorialStep.Sow, TutorialManager.Instance.Step);
        }

        [UnityTest]
        public IEnumerator TheEdgeArrow_PointsOffscreenTargets_AndHidesWhenTheyAreInView()
        {
            yield return StartPlaying();
            TutorialEdgeArrow edge = Object.FindAnyObjectByType<TutorialEdgeArrow>();
            Assert.IsNotNull(edge, "El HUD tiene la flecha del borde.");

            // Se apagan los marcadores reales y se registra un lugar de prueba, para decidir nosotros dónde está.
            foreach (TutorialTargetMarker marker in All<TutorialTargetMarker>()) marker.gameObject.SetActive(false);
            var target = new GameObject("TestTarget").AddComponent<FarTarget>();
            TutorialManager.Instance.RegisterTarget(target);
            Assert.AreSame(target, TutorialManager.Instance.CurrentTarget);

            // Lejos de la vista: la flecha del borde aparece.
            target.transform.position = new Vector3(300f, 0f, 300f);
            yield return null;
            yield return null;
            Assert.IsTrue(edge.IsShowing, "Un lugar fuera de la pantalla se señala con la flecha del borde.");

            // Y la flecha se pega al borde del lado correcto, dentro del área del HUD: a la derecha, a la izquierda, arriba y abajo del jugador.
            Vector3 origin = Player.transform.position;
            (Vector3 offset, Func<Vector2, bool> side)[] cases =
            {
                (new Vector3(200f, 0f, 0f), p => p.x > edge.Area.center.x + edge.Area.width * 0.3f),
                (new Vector3(-200f, 0f, 0f), p => p.x < edge.Area.center.x - edge.Area.width * 0.3f),
                (new Vector3(0f, 0f, 200f), p => p.y > edge.Area.center.y + edge.Area.height * 0.3f),
                (new Vector3(0f, 0f, -200f), p => p.y < edge.Area.center.y - edge.Area.height * 0.3f),
            };
            foreach ((Vector3 offset, Func<Vector2, bool> side) in cases)
            {
                target.transform.position = origin + offset;
                yield return null;
                yield return null;
                Assert.IsTrue(edge.IsShowing, "Con el lugar a " + offset);
                Assert.IsTrue(side(edge.ArrowPosition), "La flecha apunta del lado de " + offset + " (está en " + edge.ArrowPosition + ")");
                Assert.IsTrue(edge.Area.Contains(edge.ArrowPosition), "Y queda dentro del área del HUD.");
            }

            target.transform.position = new Vector3(300f, 0f, 300f);
            yield return null;

            // A la vista (justo adelante del jugador): se apaga, porque ya lo marca la flecha del mundo.
            target.transform.position = Player.transform.position + new Vector3(0f, 0f, 2f);
            yield return null;
            yield return null;
            Assert.IsFalse(edge.IsShowing, "Un lugar a la vista no necesita la flecha del borde.");

            // Sin guía no hay flecha.
            target.transform.position = new Vector3(300f, 0f, 300f);
            yield return null;
            TutorialManager.Instance.Skip();
            yield return null;
            Assert.IsFalse(edge.IsShowing);

            TutorialManager.Instance.UnregisterTarget(target);
            Object.Destroy(target.gameObject);
        }

        // Un lugar de prueba para la flecha del borde.
        private sealed class FarTarget : MonoBehaviour, ITutorialTarget
        {
            public TutorialStep Step => TutorialStep.Sow;   // el paso con el que arranca la guía
            public Vector3 Position => transform.position;
            public void SetCurrent(bool isCurrent) { }
        }

        // ---------- El feedback del Turbo y los destellos ----------

        [UnityTest]
        public IEnumerator Turbo_OpensTheCameraAndLeavesATrail_ThenEverythingGoesBack()
        {
            yield return StartPlaying();
            TurboTrail trail = Object.FindAnyObjectByType<TurboTrail>();
            Assert.IsNotNull(Object.FindAnyObjectByType<TurboCameraEffect>(), "La cámara trae el efecto del Turbo.");
            Assert.IsNotNull(trail, "El jugador trae la estela.");

            yield return new WaitForSecondsRealtime(0.3f);
            float baseFov = Camera.main.fieldOfView;
            Assert.IsFalse(trail.IsEmitting, "Sin Turbo no hay estela.");

            Assert.IsTrue(BuffManager.Instance.Activate(OfferManager.Instance.Reward));
            yield return new WaitForSecondsRealtime(1.2f);
            Assert.Greater(Camera.main.fieldOfView, baseFov + 7f, "Con el Turbo el campo de visión se abre.");
            Assert.IsTrue(trail.IsEmitting, "Y el jugador deja estela.");

            BuffManager.Instance.ClearAll();
            yield return new WaitForSecondsRealtime(1.5f);
            Assert.AreEqual(baseFov, Camera.main.fieldOfView, 0.1f, "Sin Turbo vuelve el campo de visión de siempre.");
            Assert.IsFalse(trail.IsEmitting);
        }

        [UnityTest]
        public IEnumerator TheScreenFlash_LightsUpOnFertilizerAndLevelUp_AndFadesByItself()
        {
            yield return StartPlaying();
            ScreenFlashView flash = Object.FindAnyObjectByType<ScreenFlashView>();
            Assert.IsNotNull(flash, "El HUD trae el destello.");
            Image image = flash.GetComponent<Image>();
            Assert.AreEqual(0f, image.color.a, "En reposo es transparente.");
            Assert.IsFalse(image.raycastTarget, "No captura toques.");

            InitialPlots()[0].TrySow();
            FertilizerManager.Instance.DebugGive(1);
            Assert.Greater(FertilizerManager.Instance.TryUse(), 0);
            yield return null;
            Assert.Greater(image.color.a, 0.2f, "El fertilizante enciende el destello.");
            Assert.Greater(image.color.g, image.color.r, "Y es verde.");

            yield return new WaitForSecondsRealtime(0.9f);
            Assert.AreEqual(0f, image.color.a, 1e-3f, "Se apaga solo.");

            CropData wheat = InitialPlots()[0].Crop;
            for (int i = 0; i < RealXp.UnitsToReachLevel(2, wheat.SellPrice); i++) EconomyManager.Instance.SellProduct(wheat);   // pasa al nivel 2
            yield return null;
            Assert.Greater(image.color.a, 0.2f, "Subir de nivel también.");
        }

        // ---------- "No se puede" desde los pads ----------

        [UnityTest]
        public IEnumerator StandingOnAPurchasePadWithoutCoins_SaysNoOnce_AndNotBeforeTheWait()
        {
            yield return StartPlaying();
            AudioManager audio = AudioManager.Instance;
            EconomyManager.Instance.TrySpend(EconomyManager.Instance.Balance);   // sin monedas

            UpgradePad pad = All<UpgradePad>().First(candidate => candidate.Upgrade.Kind == UpgradeKind.GrowthSpeed);
            int before = audio.TriggerCount(SoundCue.Denied);

            yield return FieldIntegrationTests.WalkTo(Player, pad.transform.position);
            Assert.AreEqual(before, audio.TriggerCount(SoundCue.Denied), "Recién llegó: todavía no se avisa.");

            yield return new WaitForSecondsRealtime(1.4f);
            Assert.AreEqual(before + 1, audio.TriggerCount(SoundCue.Denied), "Parado un rato sin monedas: se avisa, una sola vez.");

            yield return new WaitForSecondsRealtime(1.0f);
            Assert.AreEqual(before + 1, audio.TriggerCount(SoundCue.Denied), "Y no se repite mientras sigue parado.");
        }
    }
}
