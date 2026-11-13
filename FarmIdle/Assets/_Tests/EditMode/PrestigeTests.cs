using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FarmIdle.Tests
{
    // El prestigio "Nueva temporada" (F-03): la cuenta del multiplicador, su lugar en el precio, la sección de la partida y el reinicio
    // (qué se borra y qué se conserva) con el GameConfig real.
    public class PrestigeTests
    {
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

        private T Create<T>() where T : Component
        {
            var go = new GameObject(typeof(T).Name);
            _objects.Add(go);
            return go.AddComponent<T>();
        }

        // La economía, la mochila, la progresión y el prestigio, en el orden en que los crea el Bootstrapper.
        private void CreateServices(out EconomyManager economy, out BackpackManager backpack, out ProgressionManager progression, out PrestigeManager prestige)
        {
            economy = Create<EconomyManager>();
            economy.Initialize(_config);
            backpack = Create<BackpackManager>();
            backpack.Initialize(_config);
            progression = Create<ProgressionManager>();
            progression.Initialize(_config);
            prestige = Create<PrestigeManager>();
            prestige.Initialize(_config);
        }

        // Un progreso con todas las zonas abiertas, todas las mejoras al máximo, XP y un perk elegido.
        private ProgressData EverythingBought(long xp)
        {
            var zones = new List<string>();
            foreach (ZoneData zone in _config.Zones)
            {
                if (zone != null) zones.Add(zone.Id);
            }

            var levels = new List<UpgradeLevelEntry>();
            foreach (UpgradeData upgrade in _config.Upgrades)
            {
                if (upgrade != null) levels.Add(new UpgradeLevelEntry { id = upgrade.Id, level = upgrade.MaxLevel });
            }

            return new ProgressData
            {
                unlockedZones = zones.ToArray(),
                upgradeLevels = levels.ToArray(),
                xp = xp,
                perkPicks = new[] { new PerkPickEntry { id = _config.Perks[0].Id, picks = 1 } }
            };
        }

        // ---------- La cuenta y el precio ----------

        [Test]
        public void TheMultiplier_IsLinear_AndNeverBelowOne()
        {
            Assert.AreEqual(1f, PrestigeManager.MultiplierFor(0, 0.25f));
            Assert.AreEqual(1.25f, PrestigeManager.MultiplierFor(1, 0.25f), 1e-6f);
            Assert.AreEqual(1.5f, PrestigeManager.MultiplierFor(2, 0.25f), 1e-6f);
            Assert.AreEqual(2f, PrestigeManager.MultiplierFor(4, 0.25f), 1e-6f);
            Assert.AreEqual(1f, PrestigeManager.MultiplierFor(-3, 0.25f), "Una temporada negativa no baja el precio.");
            Assert.AreEqual(1f, PrestigeManager.MultiplierFor(5, -1f), "Un paso negativo tampoco.");
        }

        [Test]
        public void TheConfig_HasAPositiveStep()
        {
            Assert.Greater(_config.PrestigeStep, 0f);
        }

        [Test]
        public void ThePrestigeMultiplier_IsAThirdFactor_OfThePrice()
        {
            Assert.AreEqual(15L, PriceCalculator.FinalPrice(4, 1.5f, 2f, 1.25f), "4 x 1,5 x 2 x 1,25.");
            Assert.AreEqual(13L, PriceCalculator.FinalPrice(10, 1f, 1f, 1.25f), "12,5 redondea hacia arriba.");
            Assert.AreEqual(PriceCalculator.FinalPrice(7, 1.3f, 2f), PriceCalculator.FinalPrice(7, 1.3f, 2f, 1f), "Sin prestigio, la cuenta de siempre.");

            var calculator = new PriceCalculator();
            Assert.AreEqual(1f, calculator.PrestigeMultiplier);
            calculator.SetUpgradeMultiplier(1.5f);
            calculator.SetBuffMultiplier(2f);
            calculator.SetPrestigeMultiplier(1.25f);
            Assert.AreEqual(15L, calculator.FinalPrice(4));

            Assert.Throws<System.ArgumentOutOfRangeException>(() => calculator.SetPrestigeMultiplier(-1f));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => PriceCalculator.FinalPrice(5, 1f, 1f, float.NaN));
        }

        // ---------- La partida ----------

        [Test]
        public void TheSeasonsSection_RoundTrips_AndAppliesThePrice()
        {
            CreateServices(out EconomyManager economy, out _, out _, out PrestigeManager first);
            Assert.AreEqual("prestige", first.SaveId);
            Assert.AreEqual(0, first.Seasons);
            Assert.AreEqual(1f, economy.Pricing.PrestigeMultiplier);

            first.Restore("{\"version\":1,\"seasons\":3}");
            Assert.AreEqual(3, first.Seasons);
            Assert.AreEqual(PrestigeManager.MultiplierFor(3, _config.PrestigeStep), economy.Pricing.PrestigeMultiplier, 1e-6f, "Cargar la partida pone el precio de la temporada.");

            string json = first.Capture();
            first.Restore(null);
            Assert.AreEqual(0, first.Seasons);
            Assert.AreEqual(1f, economy.Pricing.PrestigeMultiplier);

            first.Restore(json);
            Assert.AreEqual(3, first.Seasons, "Lo capturado vuelve igual.");
        }

        [Test]
        public void TheSeasonsSection_IsTolerant_WithOldOrBrokenFiles()
        {
            CreateServices(out _, out _, out _, out PrestigeManager prestige);

            prestige.Restore("{\"version\":1,\"seasons\":2}");
            prestige.Restore("");
            Assert.AreEqual(0, prestige.Seasons, "Un archivo anterior a F-03 no trae la sección: x1.");

            prestige.Restore("basura {");
            Assert.AreEqual(0, prestige.Seasons);

            prestige.Restore("{\"version\":1,\"seasons\":-5}");
            Assert.AreEqual(0, prestige.Seasons, "Un archivo editado no puede dar temporadas negativas.");
        }

        // ---------- El reinicio ----------

        [Test]
        public void ASeason_CannotStart_WhileSomethingIsLeftToBuy()
        {
            CreateServices(out EconomyManager economy, out _, out ProgressionManager progression, out PrestigeManager prestige);
            economy.AddCoins(500);
            var started = new List<int>();
            prestige.SeasonStarted += started.Add;

            Assert.IsFalse(progression.IsEverythingBought);
            Assert.IsFalse(prestige.IsReady);
            Assert.IsFalse(prestige.CanStartSeason);
            Assert.IsFalse(prestige.TryStartSeason());

            Assert.AreEqual(0, prestige.Seasons);
            Assert.AreEqual(_config.StartingCoins + 500, economy.Balance, "No se tocó nada.");
            Assert.IsEmpty(started);

            // Con todo comprado menos una mejora, tampoco.
            ProgressData almost = EverythingBought(0);
            almost.upgradeLevels = new UpgradeLevelEntry[0];
            progression.Restore(almost);
            Assert.IsFalse(prestige.IsReady, "Falta subir las mejoras.");
        }

        [Test]
        public void ASeason_ResetsTheRun_AndKeepsLevelAndPerks()
        {
            CreateServices(out EconomyManager economy, out BackpackManager backpack, out ProgressionManager progression, out PrestigeManager prestige);

            progression.Restore(EverythingBought(xp: 12345));
            economy.AddCoins(9999);
            Assert.IsTrue(backpack.TryAdd(_config.Crops[0]));
            int fullCapacity = backpack.Backpack.Capacity;
            int levelBefore = progression.Level;
            int pointsBefore = progression.PerkPoints;
            Assert.Greater(fullCapacity, _config.BackpackCapacity, "Con las mejoras al máximo la mochila es más grande.");
            Assert.IsTrue(prestige.IsReady);
            Assert.IsTrue(progression.IsEverythingBought);

            var started = new List<int>();
            var changes = 0;
            prestige.SeasonStarted += started.Add;
            prestige.Changed += () => changes++;

            Assert.IsTrue(prestige.TryStartSeason());

            Assert.AreEqual(1, prestige.Seasons);
            CollectionAssert.AreEqual(new[] { 1 }, started);
            Assert.AreEqual(1, changes);

            // Se reinicia: monedas, zonas, mejoras, mochila (y su capacidad).
            Assert.AreEqual(_config.StartingCoins, economy.Balance);
            Assert.AreEqual(0, progression.UnlockedZoneCount);
            foreach (UpgradeData upgrade in _config.Upgrades)
            {
                if (upgrade != null) Assert.AreEqual(0, progression.LevelOf(upgrade), upgrade.Id);
            }

            Assert.AreEqual(0, backpack.Backpack.Count);
            Assert.Less(backpack.Backpack.Capacity, fullCapacity);
            Assert.IsFalse(progression.IsEverythingBought);
            Assert.IsFalse(prestige.IsReady);

            // Se conserva: XP, nivel, perks (y los puntos que quedaban libres).
            Assert.AreEqual(12345L, progression.Xp);
            Assert.AreEqual(levelBefore, progression.Level);
            Assert.AreEqual(1, progression.PicksOf(_config.Perks[0]));
            Assert.AreEqual(pointsBefore, progression.PerkPoints);

            // Y el precio de venta ya paga la temporada.
            float expected = PrestigeManager.MultiplierFor(1, _config.PrestigeStep);
            Assert.AreEqual(expected, economy.Pricing.PrestigeMultiplier, 1e-6f);
            Assert.AreEqual(expected, prestige.Multiplier, 1e-6f);
            Assert.AreEqual(PrestigeManager.MultiplierFor(2, _config.PrestigeStep), prestige.NextMultiplier, 1e-6f);
            Assert.AreEqual(PriceCalculator.FinalPrice(10, economy.Pricing.UpgradeMultiplier, 1f, expected), economy.Pricing.FinalPrice(10));
        }

        [Test]
        public void ASecondSeason_NeedsEverythingBoughtAgain_AndStacksTheMultiplier()
        {
            CreateServices(out _, out _, out ProgressionManager progression, out PrestigeManager prestige);

            progression.Restore(EverythingBought(100));
            Assert.IsTrue(prestige.TryStartSeason());
            Assert.IsFalse(prestige.TryStartSeason(), "Recién reiniciada, no queda nada comprado.");

            progression.Restore(EverythingBought(100));
            Assert.IsTrue(prestige.TryStartSeason());

            Assert.AreEqual(2, prestige.Seasons);
            Assert.AreEqual(PrestigeManager.MultiplierFor(2, _config.PrestigeStep), prestige.Multiplier, 1e-6f);
        }

        [Test]
        public void TheSeasons_SurviveASaveAndLoad_OfTheWholeGame()
        {
            CreateServices(out _, out _, out ProgressionManager progression, out PrestigeManager prestige);
            progression.Restore(EverythingBought(100));
            Assert.IsTrue(prestige.TryStartSeason());

            string json = prestige.Capture();
            PrestigeManager reloaded = Create<PrestigeManager>();
            reloaded.Initialize(_config);
            reloaded.Restore(json);

            Assert.AreEqual(1, reloaded.Seasons);
        }
    }
}
