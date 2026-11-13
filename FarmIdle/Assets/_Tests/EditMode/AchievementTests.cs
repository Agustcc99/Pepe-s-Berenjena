using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FarmIdle.Tests
{
    // Los logros (F-06): los datos de las plantillas y el AchievementManager sobre el GameConfig real (contadores de por vida, desbloqueo
    // una sola vez, premio que se reclama una sola vez, guardado y partidas viejas).
    public class AchievementTests
    {
        private GameConfig _config;
        private readonly List<GameObject> _objects = new List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            string[] guids = AssetDatabase.FindAssets("t:GameConfig");
            Assert.AreEqual(1, guids.Length);
            _config = AssetDatabase.LoadAssetAtPath<GameConfig>(AssetDatabase.GUIDToAssetPath(guids[0]));
            Loc.SystemLanguageOverride = SystemLanguage.Spanish;
            Loc.SetLanguage(GameLanguage.Spanish);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _objects)
            {
                if (go != null) UnityEngine.Object.DestroyImmediate(go);
            }

            _objects.Clear();
            Loc.SetLanguage(GameLanguage.Spanish);
            Loc.SystemLanguageOverride = null;
        }

        private T Create<T>() where T : Component
        {
            var go = new GameObject(typeof(T).Name);
            _objects.Add(go);
            return go.AddComponent<T>();
        }

        // Los servicios que los logros escuchan, en el orden del Bootstrapper, y el AchievementManager.
        private AchievementManager CreateServices(out EconomyManager economy, out BackpackManager backpack, out ProgressionManager progression, out FertilizerManager fertilizer)
        {
            economy = Create<EconomyManager>();
            economy.Initialize(_config);
            backpack = Create<BackpackManager>();
            backpack.Initialize(_config);
            progression = Create<ProgressionManager>();
            progression.Initialize(_config);
            fertilizer = Create<FertilizerManager>();
            fertilizer.Initialize(_config);

            AchievementManager achievements = Create<AchievementManager>();
            achievements.Initialize(_config);
            return achievements;
        }

        private int IndexOf(string id)
        {
            for (int i = 0; i < _config.Achievements.Count; i++)
            {
                if (_config.Achievements[i].Id == id) return i;
            }

            return -1;
        }

        private static string SaveWith(Dictionary<AchievementKind, long> counters, params string[] claimed)
        {
            int kinds = Enum.GetValues(typeof(AchievementKind)).Length;
            var values = new long[kinds];
            foreach (KeyValuePair<AchievementKind, long> pair in counters) values[(int)pair.Key] = pair.Value;
            string ids = string.Join(",", claimed.Select(id => "\"" + id + "\""));
            return "{\"version\":1,\"counters\":[" + string.Join(",", values) + "],\"claimed\":[" + ids + "]}";
        }

        // ---------- Los datos ----------

        [Test]
        public void TheConfig_HasEighteenAchievements_WithUniqueIds_AndGrowingTargetsPerKind()
        {
            IReadOnlyList<AchievementData> all = _config.Achievements;
            Assert.AreEqual(18, all.Count, "El roadmap pide 15 a 20 logros.");
            CollectionAssert.AllItemsAreUnique(all.Select(a => a.Id));
            CollectionAssert.AllItemsAreNotNull(all);

            foreach (IGrouping<AchievementKind, AchievementData> group in all.GroupBy(a => a.Kind))
            {
                int previous = 0;
                foreach (AchievementData data in group)
                {
                    Assert.Greater(data.Target, previous, data.Id + ": dentro de cada tipo la meta crece.");
                    previous = data.Target;
                    Assert.Greater(data.RewardMinutes, 0f, data.Id);
                }
            }

            Assert.AreEqual(Enum.GetValues(typeof(AchievementKind)).Length, all.Select(a => a.Kind).Distinct().Count(), "Hay al menos un logro de cada tipo.");
        }

        [Test]
        public void EveryAchievement_HasItsTextInSpanishAndEnglish_WithTheTargetFilledIn()
        {
            foreach (AchievementData data in _config.Achievements)
            {
                foreach (GameLanguage language in new[] { GameLanguage.Spanish, GameLanguage.English })
                {
                    string key = "data." + data.Id;
                    string text = Loc.Get(key, language);
                    Assert.AreNotEqual(key, text, data.Id + " no tiene texto en " + language);
                    StringAssert.Contains("{0}", text, data.Id + " tiene que llevar la meta en {0}");
                }

                Loc.SetLanguage(GameLanguage.English);
                StringAssert.Contains(AchievementData.FormatNumber(data.Target), data.Describe());
                Loc.SetLanguage(GameLanguage.Spanish);
            }
        }

        [Test]
        public void Numbers_UseThePointInSpanish_AndTheCommaInEnglish()
        {
            Assert.AreEqual("12.345", AchievementData.FormatNumber(12345));
            Loc.SetLanguage(GameLanguage.English);
            Assert.AreEqual("12,345", AchievementData.FormatNumber(12345));
        }

        // ---------- El manager ----------

        [Test]
        public void HarvestsAndSales_Count_Unlock_AndAnnounceOnlyOnce()
        {
            AchievementManager achievements = CreateServices(out EconomyManager economy, out BackpackManager backpack, out _, out _);
            var unlocked = new List<string>();
            achievements.Unlocked += data => unlocked.Add(data.Id);

            CropData crop = _config.Crops[0];
            for (int i = 0; i < 60; i++)
            {
                Assert.IsTrue(backpack.TryAdd(crop));
                backpack.TryPop(out _);
            }

            int harvest1 = IndexOf("ach_harvest_1");
            Assert.AreEqual(60L, achievements.CounterOf(AchievementKind.Harvest));
            Assert.AreEqual(50L, achievements.ProgressAt(harvest1), "El avance que se muestra se acota a la meta.");
            Assert.IsTrue(achievements.IsCompleteAt(harvest1));

            long coins = 0;
            economy.ProductSold += (c, value) => coins += value;
            for (int i = 0; i < 100; i++) economy.SellProduct(crop);

            Assert.AreEqual(100L, achievements.CounterOf(AchievementKind.Sell));
            Assert.AreEqual(coins, achievements.CounterOf(AchievementKind.CoinsEarned));
            Assert.IsTrue(achievements.IsCompleteAt(IndexOf("ach_sell_1")));

            CollectionAssert.AreEqual(new[] { "ach_harvest_1", "ach_sell_1" }, unlocked, "Cada logro avisa una sola vez.");
            Assert.AreEqual(2, achievements.ClaimableCount);
        }

        [Test]
        public void UpgradesAndZones_Count_WhenBought()
        {
            AchievementManager achievements = CreateServices(out EconomyManager economy, out _, out ProgressionManager progression, out _);

            UpgradeData growth = _config.Upgrades.First(u => u.Kind == UpgradeKind.GrowthSpeed);
            economy.AddCoins(growth.CostOfLevel(1));
            Assert.Greater(progression.TryPay(growth, growth.CostOfLevel(1)), 0L);

            Assert.AreEqual(1L, achievements.CounterOf(AchievementKind.UpgradesBought));
            Assert.AreEqual(0L, achievements.CounterOf(AchievementKind.ZonesOpened));
        }

        [Test]
        public void LoadingAGame_DoesNotCountAsPlaying_NorAnnounceAnything()
        {
            AchievementManager achievements = CreateServices(out _, out BackpackManager backpack, out ProgressionManager progression, out _);
            int announced = 0;
            achievements.Unlocked += data => announced++;

            achievements.Restore(SaveWith(new Dictionary<AchievementKind, long> { { AchievementKind.Harvest, 49 } }));
            backpack.Restore("{\"version\":1,\"crops\":[\"" + _config.Crops[0].Id + "\",\"" + _config.Crops[0].Id + "\"]}");
            progression.Restore(new ProgressData { unlockedZones = new[] { _config.Zones[0].Id } });

            Assert.AreEqual(49L, achievements.CounterOf(AchievementKind.Harvest), "Restaurar la mochila y el progreso no suma.");
            Assert.AreEqual(0L, achievements.CounterOf(AchievementKind.ZonesOpened));

            achievements.Restore(SaveWith(new Dictionary<AchievementKind, long> { { AchievementKind.Harvest, 5000 } }));
            Assert.AreEqual(0, announced, "Una partida cargada con logros cumplidos no avisa de nuevo.");
            Assert.IsTrue(achievements.IsCompleteAt(IndexOf("ach_harvest_3")));
        }

        [Test]
        public void Claiming_PaysOnce_AndOnlyWhenComplete()
        {
            AchievementManager achievements = CreateServices(out EconomyManager economy, out _, out _, out _);
            int harvest = IndexOf("ach_harvest_1");
            var claims = new List<string>();
            achievements.Claimed += (data, coins, fertilizer) => claims.Add(data.Id + ":" + coins + ":" + fertilizer);

            Assert.IsFalse(achievements.TryClaim(harvest), "Todavía no está cumplido.");
            Assert.IsFalse(achievements.TryClaim(-1));
            Assert.IsFalse(achievements.TryClaim(99));

            achievements.Restore(SaveWith(new Dictionary<AchievementKind, long> { { AchievementKind.Harvest, 50 } }));
            long before = economy.Balance;
            Assert.Greater(achievements.RewardCoinsAt(harvest), 0L, "El premio en monedas nunca es cero (mínimo del GameConfig).");

            Assert.IsTrue(achievements.TryClaim(harvest));
            Assert.AreEqual(before + _config.MissionMinCoins, economy.Balance, "Sin producción todavía, paga el mínimo.");
            Assert.IsTrue(achievements.IsClaimedAt(harvest));
            Assert.IsFalse(achievements.TryClaim(harvest), "No se reclama dos veces.");
            Assert.AreEqual(before + _config.MissionMinCoins, economy.Balance);
            Assert.AreEqual(1, claims.Count);
            Assert.AreEqual(0, achievements.ClaimableCount);
        }

        [Test]
        public void AFertilizerReward_GoesToTheStock_AndFallsBackToCoinsWhenItIsFull()
        {
            AchievementManager achievements = CreateServices(out EconomyManager economy, out _, out _, out FertilizerManager fertilizer);
            int big = IndexOf("ach_harvest_3");
            Assert.Greater(_config.Achievements[big].RewardFertilizer, 0);
            achievements.Restore(SaveWith(new Dictionary<AchievementKind, long> { { AchievementKind.Harvest, 5000 } }));

            int stock = fertilizer.Stock;
            Assert.Greater(achievements.RewardFertilizerAt(big), 0);
            Assert.IsTrue(achievements.TryClaim(big));
            Assert.Greater(fertilizer.Stock, stock, "El premio entra al stock de fertilizante.");

            fertilizer.DebugGive(99);
            Assert.IsTrue(fertilizer.IsFull);
            achievements.Restore(SaveWith(new Dictionary<AchievementKind, long> { { AchievementKind.Harvest, 5000 } }));   // se vuelve a poder reclamar
            long coins = economy.Balance;
            Assert.AreEqual(0, achievements.RewardFertilizerAt(big), "Con el stock lleno el premio cae a monedas.");
            Assert.IsTrue(achievements.TryClaim(big));
            Assert.Greater(economy.Balance, coins);
        }

        [Test]
        public void CaptureThenRestore_BringsBackTheCountersAndTheClaims()
        {
            AchievementManager achievements = CreateServices(out _, out _, out _, out _);
            achievements.Restore(SaveWith(new Dictionary<AchievementKind, long> { { AchievementKind.Harvest, 700 }, { AchievementKind.Level, 12 } }));
            Assert.IsTrue(achievements.TryClaim(IndexOf("ach_harvest_1")));
            string json = achievements.Capture();

            achievements.Restore(null);
            Assert.AreEqual(0L, achievements.CounterOf(AchievementKind.Harvest));
            Assert.IsFalse(achievements.IsClaimedAt(IndexOf("ach_harvest_1")));

            achievements.Restore(json);
            Assert.AreEqual(700L, achievements.CounterOf(AchievementKind.Harvest));
            Assert.AreEqual(12L, achievements.CounterOf(AchievementKind.Level));
            Assert.IsTrue(achievements.IsClaimedAt(IndexOf("ach_harvest_1")));
            Assert.IsFalse(achievements.IsClaimedAt(IndexOf("ach_harvest_2")));
            Assert.AreEqual("achievements", achievements.SaveId);
        }

        [Test]
        public void ABadSave_NeverThrows_AndUnknownOrNegativeValuesAreFixed()
        {
            AchievementManager achievements = CreateServices(out _, out _, out _, out _);

            Assert.DoesNotThrow(() => achievements.Restore("esto no es json"));
            Assert.AreEqual(0L, achievements.CounterOf(AchievementKind.Harvest));

            Assert.DoesNotThrow(() => achievements.Restore("{\"version\":1,\"counters\":[-5],\"claimed\":[\"ya_no_existe\",\"ach_turbo\"]}"));
            Assert.AreEqual(0L, achievements.CounterOf(AchievementKind.Harvest), "Un contador negativo vuelve a cero.");
            Assert.IsTrue(achievements.IsClaimedAt(IndexOf("ach_turbo")), "Se conservan los reclamados que existen.");
            Assert.AreEqual(0L, achievements.CounterOf(AchievementKind.Seasons), "Una lista de contadores más corta deja el resto en cero.");

            Assert.DoesNotThrow(() => achievements.Restore(string.Empty));
            Assert.IsFalse(achievements.IsClaimedAt(IndexOf("ach_turbo")));
        }
    }
}
