using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace FarmIdle.Tests
{
    // La lógica pura de la Fase 9: ajustes de audio, guía de los primeros minutos y los límites del sonido.
    public class AudioSettingsTests
    {
        [Test]
        public void Defaults_AreAudibleAndNotMuted()
        {
            var settings = new AudioSettings();

            Assert.AreEqual(AudioSettings.DefaultMaster, settings.Master);
            Assert.AreEqual(AudioSettings.DefaultMusic, settings.Music);
            Assert.AreEqual(AudioSettings.DefaultSfx, settings.Sfx);
            Assert.IsFalse(settings.Muted);
            Assert.Greater(settings.MusicGain, 0f);
            Assert.Greater(settings.SfxGain, 0f);
        }

        [Test]
        public void Gain_IsMasterTimesCategory_SquaredForAnEvenerEar()
        {
            var settings = new AudioSettings();
            settings.SetMaster(0.5f);
            settings.SetSfx(0.5f);
            settings.SetMusic(1f);

            Assert.AreEqual(0.25f * 0.25f, settings.SfxGain, 1e-6f);
            Assert.AreEqual(0.25f * 1f, settings.MusicGain, 1e-6f);
        }

        [Test]
        public void Mute_SilencesBothCategories_WithoutLosingTheVolumes()
        {
            var settings = new AudioSettings();
            settings.SetSfx(0.8f);
            settings.SetMuted(true);

            Assert.AreEqual(0f, settings.SfxGain);
            Assert.AreEqual(0f, settings.MusicGain);
            Assert.AreEqual(0.8f, settings.Sfx, 1e-6f);

            settings.SetMuted(false);
            Assert.Greater(settings.SfxGain, 0f);
        }

        [Test]
        public void StrangeValues_AreClamped_NeverBreakTheMixer()
        {
            var settings = new AudioSettings();

            settings.SetMaster(5f);
            settings.SetMusic(-3f);
            settings.SetSfx(float.NaN);

            Assert.AreEqual(1f, settings.Master);
            Assert.AreEqual(0f, settings.Music);
            Assert.AreEqual(0f, settings.Sfx);
            Assert.AreEqual(0f, AudioSettings.Curve(float.PositiveInfinity) - 1f);
        }

        [Test]
        public void Changed_FiresOnlyWhenSomethingReallyChanged()
        {
            var settings = new AudioSettings();
            int fired = 0;
            settings.Changed += () => fired++;

            settings.SetMaster(AudioSettings.DefaultMaster);
            settings.SetMuted(false);
            Assert.AreEqual(0, fired, "Poner el mismo valor no avisa.");

            settings.SetMaster(0.4f);
            settings.SetMuted(true);
            Assert.AreEqual(2, fired);

            settings.Load(0.4f, AudioSettings.DefaultMusic, AudioSettings.DefaultSfx, true);
            Assert.AreEqual(2, fired, "Cargar lo mismo no avisa.");

            settings.ResetToDefaults();
            Assert.AreEqual(3, fired, "Volver a los valores de fábrica avisa una sola vez.");
            Assert.IsFalse(settings.Muted);
        }
    }

    public class TutorialProgressTests
    {
        [Test]
        public void TheGuide_StartsAtSowing_AndAdvancesStepByStep()
        {
            var tutorial = new TutorialProgress();
            var steps = new List<TutorialStep>();
            tutorial.StepChanged += steps.Add;
            Assert.AreEqual(TutorialStep.Sow, tutorial.Step);

            tutorial.ReportGrowing(2);
            Assert.AreEqual(TutorialStep.Sow, tutorial.Step, "Dos parcelas no alcanzan.");

            tutorial.ReportGrowing(TutorialProgress.SowTarget);
            Assert.AreEqual(TutorialStep.Harvest, tutorial.Step);

            for (int i = 0; i < TutorialProgress.HarvestTarget; i++) tutorial.ReportHarvest();
            Assert.AreEqual(TutorialStep.Sell, tutorial.Step);

            tutorial.ReportSold();
            Assert.AreEqual(TutorialStep.Buy, tutorial.Step);

            tutorial.ReportPurchased();
            Assert.AreEqual(TutorialStep.Done, tutorial.Step);
            Assert.IsTrue(tutorial.IsDone);

            CollectionAssert.AreEqual(new[] { TutorialStep.Harvest, TutorialStep.Sell, TutorialStep.Buy, TutorialStep.Done }, steps);
        }

        [Test]
        public void ALaterStep_CompletesTheEarlierOnes()
        {
            var tutorial = new TutorialProgress();
            tutorial.ReportSold();   // vendió sin que la guía lo viera sembrar ni cosechar

            Assert.AreEqual(TutorialStep.Buy, tutorial.Step);
        }

        [Test]
        public void Steps_NeverGoBack_AndDoneIsFinal()
        {
            var tutorial = new TutorialProgress();
            tutorial.ReportSold();
            tutorial.ReportGrowing(9);
            tutorial.ReportHarvest();
            Assert.AreEqual(TutorialStep.Buy, tutorial.Step);

            tutorial.ReportPurchased();
            int fired = 0;
            tutorial.StepChanged += step => fired++;
            tutorial.ReportSold();
            tutorial.ReportPurchased();
            tutorial.ReportHarvest();

            Assert.AreEqual(TutorialStep.Done, tutorial.Step);
            Assert.AreEqual(0, fired);
        }

        [Test]
        public void Restore_SetsTheStepSilently_AndRejectsInvalidValues()
        {
            var tutorial = new TutorialProgress();
            int fired = 0;
            tutorial.StepChanged += step => fired++;

            tutorial.Restore((int)TutorialStep.Sell);
            Assert.AreEqual(TutorialStep.Sell, tutorial.Step);

            tutorial.Restore(99);
            Assert.AreEqual(TutorialStep.Sow, tutorial.Step);

            tutorial.Restore(-4);
            Assert.AreEqual(TutorialStep.Sow, tutorial.Step);

            tutorial.Restore((int)TutorialStep.Done);
            Assert.IsTrue(tutorial.IsDone);
            Assert.AreEqual(0, fired, "Restaurar no avisa ningún paso.");
        }

        [Test]
        public void Finish_SkipsTheWholeGuide()
        {
            var tutorial = new TutorialProgress();
            tutorial.Finish();

            Assert.IsTrue(tutorial.IsDone);
        }
    }

    public class SoundThrottleTests
    {
        [Test]
        public void TheFirstShot_AlwaysPlays_AndTheNextOneWaitsItsInterval()
        {
            var throttle = new SoundThrottle(4);

            Assert.IsTrue(throttle.TryPlay(1, 10.0, 0.5));
            Assert.IsFalse(throttle.TryPlay(1, 10.3, 0.5));
            Assert.IsTrue(throttle.TryPlay(1, 10.5, 0.5));
        }

        [Test]
        public void EachSound_HasItsOwnClock()
        {
            var throttle = new SoundThrottle(4);

            Assert.IsTrue(throttle.TryPlay(0, 5.0, 1.0));
            Assert.IsTrue(throttle.TryPlay(2, 5.0, 1.0), "Otro sonido no se frena por el primero.");
            Assert.IsFalse(throttle.TryPlay(0, 5.2, 1.0));
        }

        [Test]
        public void AnUnknownSound_NeverPlays_AndNeverThrows()
        {
            var throttle = new SoundThrottle(2);

            Assert.IsFalse(throttle.TryPlay(-1, 1.0, 0.0));
            Assert.IsFalse(throttle.TryPlay(2, 1.0, 0.0));
        }

        [Test]
        public void TheSellCombo_RisesStepByStep_ToItsCap_AndRestartsAfterAGap()
        {
            var combo = new ComboCounter(0.5, 3);

            Assert.AreEqual(0, combo.Next(1.0));
            Assert.AreEqual(1, combo.Next(1.2));
            Assert.AreEqual(2, combo.Next(1.4));
            Assert.AreEqual(3, combo.Next(1.6));
            Assert.AreEqual(3, combo.Next(1.8), "Tope.");

            Assert.AreEqual(0, combo.Next(3.0), "Pasó la ventana: vuelve a empezar.");
            Assert.AreEqual(1, combo.Next(3.1));

            combo.Reset();
            Assert.AreEqual(0, combo.Next(3.2));
        }

        // J-08: el combo de cosecha sube por una escala (siempre más agudo, nunca fuera de lo que una voz puede sonar).
        [Test]
        public void TheHarvestScale_StartsAtOne_AlwaysRises_AndStopsAtItsTop()
        {
            Assert.AreEqual(1f, HarvestScale.PitchFor(0), 1e-6f);
            Assert.AreEqual(1f, HarvestScale.PitchFor(-3), 1e-6f);

            for (int step = 1; step <= HarvestScale.MaxStep; step++)
            {
                Assert.Greater(HarvestScale.PitchFor(step), HarvestScale.PitchFor(step - 1), "Paso " + step);
            }

            Assert.AreEqual(2f, HarvestScale.PitchFor(5), 1e-5f, "El sexto paso es la octava.");
            Assert.LessOrEqual(HarvestScale.PitchFor(HarvestScale.MaxStep), 3f);
            Assert.AreEqual(HarvestScale.PitchFor(HarvestScale.MaxStep), HarvestScale.PitchFor(HarvestScale.MaxStep + 10), 1e-6f, "Tope.");
        }

        [Test]
        public void ABigStreak_IsReportedOnce_WhenItEnds_AndASmallOneNever()
        {
            var run = new StreakTracker(0.5, 3);

            Assert.IsFalse(run.PollBigStreakEnded(0.0), "Sin eventos no hay nada que avisar.");

            Assert.AreEqual(1, run.Add(1.0));
            Assert.AreEqual(2, run.Add(1.2));
            Assert.IsFalse(run.PollBigStreakEnded(2.0), "Dos no llegan al umbral.");
            Assert.AreEqual(0, run.Count);

            run.Add(3.0);
            run.Add(3.2);
            Assert.AreEqual(3, run.Add(3.4));
            Assert.IsFalse(run.PollBigStreakEnded(3.6), "Todavía puede seguir.");
            Assert.IsTrue(run.PollBigStreakEnded(4.0));
            Assert.IsFalse(run.PollBigStreakEnded(4.1), "Una sola vez.");

            Assert.AreEqual(1, run.Add(10.0));
            Assert.AreEqual(1, run.Add(11.0), "Pasó la ventana: tanda nueva.");
        }
    }
}
