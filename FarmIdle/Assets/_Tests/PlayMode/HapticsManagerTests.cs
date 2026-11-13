using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace FarmIdle.Tests
{
    // Un IVibrator que anota los pulsos: "ms/amplitud".
    internal sealed class RecordingVibrator : IVibrator
    {
        public readonly List<string> Pulses = new List<string>();

        public string Name => "Recording";

        public void Pulse(int milliseconds, int amplitude) { Pulses.Add(milliseconds + "/" + amplitude); }
    }

    // La vibración (U-06) con servicios reales armados por código: cosechar, vender, comprar y subir de nivel, y el interruptor de ajustes.
    public class HapticsManagerTests
    {
        private FieldTestWorld _world;
        private SaveRig _rig;
        private SettingsManager _settings;
        private HapticsManager _haptics;
        private RecordingVibrator _vibrator;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _world = FieldTestWorld.Create(1, 5f);
            _rig = SaveRig.Create(_world);
            _rig.Start();

            var settingsObject = new GameObject("SettingsManager");
            settingsObject.transform.SetParent(_world.Root.transform, false);
            _settings = settingsObject.AddComponent<SettingsManager>();
            _settings.Initialize();

            _vibrator = new RecordingVibrator();
            var go = new GameObject("HapticsManager");
            go.transform.SetParent(_world.Root.transform, false);
            _haptics = go.AddComponent<HapticsManager>();
            _haptics.Initialize(_vibrator);
            yield return null;
        }

        [TearDown]
        public void TearDown()
        {
            _rig.Destroy();
            _world.Destroy();
            Time.timeScale = 1f;
        }

        // Los pulsos están separados por más que el límite de frecuencia: se espera entre una acción y la siguiente.
        private static IEnumerator WaitBetweenPulses()
        {
            yield return new WaitForSecondsRealtime(HapticsManager.MinSecondsBetweenPulses + 0.05f);
        }

        [UnityTest]
        public IEnumerator Harvesting_GivesALightPulse()
        {
            Assert.IsTrue(_rig.Backpack.TryAddOrReject(_rig.Wheat));

            CollectionAssert.AreEqual(new[] { "12/60" }, _vibrator.Pulses);
            yield break;
        }

        [UnityTest]
        public IEnumerator Selling_GivesALightPulse()
        {
            _rig.Economy.SellProduct(_rig.Tomato);

            CollectionAssert.AreEqual(new[] { "15/80" }, _vibrator.Pulses);
            yield break;
        }

        [UnityTest]
        public IEnumerator Buying_GivesAMediumPulse_OnlyWhenThePurchaseIsComplete()
        {
            _rig.Economy.AddCoins(2000);

            _rig.Progression.TryPay(_rig.Zones[1], 100);   // a medias
            CollectionAssert.IsEmpty(_vibrator.Pulses);

            _rig.Progression.TryPay(_rig.Zones[0], 300);
            CollectionAssert.AreEqual(new[] { "40/180" }, _vibrator.Pulses);
            yield break;
        }

        [UnityTest]
        public IEnumerator LevelingUp_GivesTheStrongestPulse_AndItCoversTheSalePulse()
        {
            for (int i = 0; i < 12; i++)
            {
                _rig.Economy.SellProduct(_rig.Tomato);   // 120 de XP: todavía nivel 1
                yield return WaitBetweenPulses();
            }

            _vibrator.Pulses.Clear();
            _rig.Economy.SellProduct(_rig.Tomato);   // 130: nivel 2 en la misma venta

            // La progresión se entera de la venta antes que la vibración: el nivel (fuerte) sale primero y la venta (suave) queda tapada.
            CollectionAssert.AreEqual(new[] { "70/230" }, _vibrator.Pulses);
        }

        [UnityTest]
        public IEnumerator ARunOfSales_IsNotABuzz()
        {
            for (int i = 0; i < 5; i++) _rig.Economy.SellProduct(_rig.Wheat);   // todas en el mismo cuadro

            Assert.AreEqual(1, _vibrator.Pulses.Count);
            Assert.AreEqual(1, _haptics.PulseCount);
            yield break;
        }

        [UnityTest]
        public IEnumerator TheSettingsSwitch_TurnsItOff_AndOnAgain()
        {
            _settings.SetVibration(false);
            _rig.Economy.SellProduct(_rig.Tomato);
            CollectionAssert.IsEmpty(_vibrator.Pulses, "Con la vibración apagada no hay pulsos.");

            _settings.SetVibration(true);
            yield return WaitBetweenPulses();
            _rig.Economy.SellProduct(_rig.Tomato);
            Assert.AreEqual(1, _vibrator.Pulses.Count);
        }

        [UnityTest]
        public IEnumerator InTheEditor_TheDefaultVibratorDoesNothing()
        {
            var go = new GameObject("Default");
            go.transform.SetParent(_world.Root.transform, false);
            HapticsManager defaults = go.AddComponent<HapticsManager>();
            defaults.Initialize();

            Assert.AreEqual("Null", defaults.Vibrator.Name);
            yield break;
        }
    }
}
