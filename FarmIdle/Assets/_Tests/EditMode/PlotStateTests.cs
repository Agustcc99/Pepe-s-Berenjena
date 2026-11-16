using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FarmIdle.Tests
{
    // B-11: lo sembrado se guarda. Al volver crece con el tiempo que se estuvo fuera; pasadas 24 h (GameConfig.OfflineRotSeconds)
    // se pudre y las parcelas vuelven vacías. Se prueba el PlotStateManager con un campo falso (el de verdad se prueba en PlayMode).
    public class PlotStateTests
    {
        private const int Empty = (int)PlotState.Empty, Growing = (int)PlotState.Growing, Ready = (int)PlotState.Ready;

        private sealed class FakeField : IPlotStateHolder
        {
            public string Id = "Zone/Field";
            public int[] States = new int[3];
            public float[] Elapsed = new float[3];
            public float GrowSeconds = 100f;

            public string StateId => Id;
            public int PlotCount => States.Length;

            public void CaptureState(int[] states, float[] elapsed)
            {
                Array.Copy(States, states, States.Length);
                Array.Copy(Elapsed, elapsed, Elapsed.Length);
            }

            public int RestoreState(int[] states, float[] elapsed, double awaySeconds)
            {
                Array.Copy(states, States, States.Length);
                Array.Copy(elapsed, Elapsed, Elapsed.Length);
                AdvanceAway(awaySeconds);
                return Planted;
            }

            public void AdvanceAway(double awaySeconds)
            {
                for (int i = 0; i < States.Length; i++)
                {
                    if (States[i] != Growing) continue;

                    Elapsed[i] += (float)awaySeconds;
                    if (Elapsed[i] >= GrowSeconds)
                    {
                        Elapsed[i] = GrowSeconds;
                        States[i] = Ready;
                    }
                }
            }

            public int ClearAll()
            {
                int cleared = Planted;
                States = new int[States.Length];
                Elapsed = new float[Elapsed.Length];
                return cleared;
            }

            public int Planted
            {
                get
                {
                    int planted = 0;
                    foreach (int state in States) if (state != Empty) planted++;
                    return planted;
                }
            }
        }

        private static readonly DateTime Noon = new DateTime(2026, 11, 16, 12, 0, 0, DateTimeKind.Utc);

        private GameConfig _config;
        private readonly List<GameObject> _objects = new List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            string[] guids = AssetDatabase.FindAssets("t:GameConfig");
            Assert.AreEqual(1, guids.Length);
            _config = AssetDatabase.LoadAssetAtPath<GameConfig>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _objects)
            {
                if (go != null) Object.DestroyImmediate(go);
            }

            _objects.Clear();
        }

        private PlotStateManager CreateManager(DateTime now)
        {
            var go = new GameObject("PlotStateManager");
            _objects.Add(go);
            var manager = go.AddComponent<PlotStateManager>();
            manager.Initialize(_config);
            manager.Clock = () => now;
            return manager;
        }

        // Una partida guardada al mediodía con una parcela creciendo (10 de 100 s), una lista y una vacía.
        private string SavedAtNoon()
        {
            PlotStateManager manager = CreateManager(Noon);
            var field = new FakeField { States = new[] { Growing, Ready, Empty }, Elapsed = new[] { 10f, 100f, 0f } };
            manager.Register(field);
            manager.Apply(field);   // el nivel arrancó
            return manager.Capture();
        }

        private FakeField LoadAfter(string json, TimeSpan away, out PlotStateManager manager)
        {
            manager = CreateManager(Noon + away);
            manager.Restore(json);

            var field = new FakeField();
            manager.Register(field);
            manager.Apply(field);
            return field;
        }

        [Test]
        public void TheConfig_RotsTheCropsAfterADay()
        {
            Assert.AreEqual(86400f, _config.OfflineRotSeconds, "Pedido del usuario: pasadas 24 h, lo sembrado se pudre.");
        }

        [Test]
        public void AfterAShortBreak_ThePlotsComeBack_AndKeepGrowing()
        {
            FakeField field = LoadAfter(SavedAtNoon(), TimeSpan.FromSeconds(30), out PlotStateManager manager);

            CollectionAssert.AreEqual(new[] { Growing, Ready, Empty }, field.States, "Lo sembrado no desaparece.");
            Assert.AreEqual(40f, field.Elapsed[0], 1e-3f, "Y siguió creciendo los 30 s que se estuvo fuera.");
            Assert.AreEqual(0, manager.GrownPlots, "Por una ausencia corta no hay aviso.");
            Assert.AreEqual(0, manager.RottedPlots);
        }

        [Test]
        public void AfterAnHour_WhatWasSown_IsReadyToHarvest()
        {
            FakeField field = LoadAfter(SavedAtNoon(), TimeSpan.FromHours(1), out PlotStateManager manager);

            CollectionAssert.AreEqual(new[] { Ready, Ready, Empty }, field.States);
            Assert.AreEqual(2, manager.GrownPlots, "El aviso cuenta las parcelas que volvieron sembradas.");
            Assert.AreEqual(0, manager.RottedPlots);

            manager.ClearNotice();
            Assert.AreEqual(0, manager.GrownPlots);
        }

        [Test]
        public void JustBeforeADay_ItStillGrows_AndAfterADay_EverythingRots()
        {
            string json = SavedAtNoon();

            FakeField almost = LoadAfter(json, TimeSpan.FromHours(23.9), out PlotStateManager before);
            Assert.AreEqual(2, almost.Planted);
            Assert.AreEqual(0, before.RottedPlots);

            FakeField late = LoadAfter(json, TimeSpan.FromHours(24), out PlotStateManager after);
            Assert.AreEqual(0, late.Planted, "Pasadas 24 h las parcelas vuelven vacías.");
            Assert.AreEqual(2, after.RottedPlots, "Y el aviso dice cuántas se pudrieron.");
            Assert.AreEqual(0, after.GrownPlots);
            Assert.AreEqual(24, after.RotHours);
        }

        [Test]
        public void IfTheClockWentBack_ThePlotsComeBackAsTheyWere()
        {
            FakeField field = LoadAfter(SavedAtNoon(), TimeSpan.FromHours(-5), out PlotStateManager manager);

            CollectionAssert.AreEqual(new[] { Growing, Ready, Empty }, field.States);
            Assert.AreEqual(10f, field.Elapsed[0], 1e-3f);
            Assert.AreEqual(0, manager.RottedPlots);
        }

        [Test]
        public void ASaveBeforeTheLevelStarts_KeepsTheLoadedPlots_AndTheirTime()
        {
            string json = SavedAtNoon();
            PlotStateManager manager = CreateManager(Noon + TimeSpan.FromHours(2));
            manager.Restore(json);

            Assert.AreEqual(json, manager.Capture(), "Sin llegar a jugar, lo cargado se vuelve a escribir tal cual (con su hora).");
        }

        [Test]
        public void EachField_GetsItsOwnPlots_AndAFieldThatIsNotActive_KeepsItsSave()
        {
            PlotStateManager first = CreateManager(Noon);
            var east = new FakeField { Id = "Zone_East/Field", States = new[] { Ready, Ready, Ready }, Elapsed = new[] { 100f, 100f, 100f } };
            var west = new FakeField { Id = "Zone_West/Field", States = new[] { Growing, Empty, Empty }, Elapsed = new[] { 5f, 0f, 0f } };
            first.Register(east);
            first.Register(west);
            first.Apply(east);
            string json = first.Capture();

            PlotStateManager second = CreateManager(Noon + TimeSpan.FromSeconds(10));
            second.Restore(json);
            var westAgain = new FakeField { Id = "Zone_West/Field" };
            second.Register(westAgain);
            second.Apply(westAgain);
            CollectionAssert.AreEqual(new[] { Growing, Empty, Empty }, westAgain.States, "Cada campo recibe lo suyo.");

            // El campo del este no se activó en esta sesión: su foto sigue en la partida.
            StringAssert.Contains("Zone_East/Field", second.Capture());
        }

        [Test]
        public void ComingBackFromTheBackground_GrowsThePlots_OrRotsThemAfterADay()
        {
            DateTime now = Noon;
            PlotStateManager manager = CreateManager(Noon);
            manager.Clock = () => now;
            var field = new FakeField { States = new[] { Growing, Growing, Empty }, Elapsed = new[] { 10f, 80f, 0f } };
            manager.Register(field);

            manager.OnApplicationPause(true);
            now = Noon + TimeSpan.FromSeconds(50);
            manager.OnApplicationPause(false);
            CollectionAssert.AreEqual(new[] { Growing, Ready, Empty }, field.States, "En segundo plano lo sembrado sigue creciendo.");
            Assert.AreEqual(60f, field.Elapsed[0], 1e-3f);

            manager.OnApplicationPause(true);
            now += TimeSpan.FromHours(25);
            manager.OnApplicationPause(false);
            Assert.AreEqual(0, field.Planted);
            Assert.AreEqual(2, manager.RottedPlots);
        }

        [Test]
        public void ASaveWithoutThePlotsSection_OrGarbage_StartsWithEmptyPlots()
        {
            foreach (string json in new[] { null, "", "esto no es json", "{\"version\":1}" })
            {
                PlotStateManager manager = CreateManager(Noon);
                manager.Restore(json);

                var field = new FakeField();
                manager.Register(field);
                manager.Apply(field);
                Assert.AreEqual(0, field.Planted, "Con: " + json);
                Assert.AreEqual(0, manager.GrownPlots + manager.RottedPlots);
            }
        }

        [Test]
        public void LoadingWithTheLevelRunning_ReplacesWhatIsOnTheFields()
        {
            string json = SavedAtNoon();
            PlotStateManager manager = CreateManager(Noon + TimeSpan.FromSeconds(5));
            var field = new FakeField { States = new[] { Ready, Ready, Ready }, Elapsed = new[] { 100f, 100f, 100f } };
            manager.Register(field);

            manager.Restore(json);
            CollectionAssert.AreEqual(new[] { Growing, Ready, Empty }, field.States, "Cargar a mitad del juego deja las parcelas como dice la partida.");

            manager.Restore(null);
            Assert.AreEqual(0, field.Planted, "Y borrar la partida las vacía.");
        }
    }
}
