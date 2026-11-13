using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FarmIdle.Tests
{
    // La tienda de semillas doradas (F-11): el catálogo del GameConfig, las semillas por temporada, las compras, el guardado v2 (con
    // migración desde v1) y que los efectos lleguen a quien los usa.
    public class PrestigeShopTests
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

        private PrestigeManager CreateServices(out EconomyManager economy, out ProgressionManager progression)
        {
            economy = Create<EconomyManager>();
            economy.Initialize(_config);
            Create<BackpackManager>().Initialize(_config);
            progression = Create<ProgressionManager>();
            progression.Initialize(_config);
            PrestigeManager prestige = Create<PrestigeManager>();
            prestige.Initialize(_config);
            return prestige;
        }

        private ProgressData EverythingBought()
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

            return new ProgressData { unlockedZones = zones.ToArray(), upgradeLevels = levels.ToArray() };
        }

        private int IndexOf(PrestigeManager prestige, PrestigeShopKind kind)
        {
            for (int i = 0; i < prestige.ShopCount; i++)
            {
                if (prestige.ShopItem(i).Kind == kind) return i;
            }

            Assert.Fail("No hay una mejora " + kind);
            return -1;
        }

        // ---------- El catálogo ----------

        [Test]
        public void TheCatalog_HasSixToEightItems_WithUniqueIds_AndCosts()
        {
            Assert.That(_config.PrestigeShop.Count, Is.InRange(6, 8));

            var ids = new HashSet<string>();
            for (int i = 0; i < _config.PrestigeShop.Count; i++)
            {
                PrestigeShopItem item = _config.PrestigeShop[i];
                Assert.IsFalse(string.IsNullOrEmpty(item.Id));
                Assert.IsTrue(ids.Add(item.Id), "Id repetido: " + item.Id);
                Assert.Greater(item.MaxLevel, 0, item.Id);

                float previous = PrestigeShopItem.Neutral(item.Kind);
                for (int level = 1; level <= item.MaxLevel; level++)
                {
                    Assert.Greater(item.CostOf(level - 1), 0, item.Id + " nivel " + level);
                    Assert.Greater(item.ValueAt(level), previous, item.Id + " nivel " + level + " tiene que mejorar al anterior");
                    previous = item.ValueAt(level);
                }
            }
        }

        [Test]
        public void EveryItem_HasLocalizedNameAndDescription()
        {
            for (int i = 0; i < _config.PrestigeShop.Count; i++)
            {
                string id = _config.PrestigeShop[i].Id;
                Assert.AreNotEqual("shop.name." + id, Loc.Get("shop.name." + id), "Falta el nombre de " + id);
                Assert.AreNotEqual("shop.desc." + id, Loc.Get("shop.desc." + id), "Falta la descripción de " + id);
            }
        }

        [Test]
        public void SeedsPerSeason_GrowSlowly_AndNeverZero()
        {
            Assert.AreEqual(1, _config.SeedsForSeason(1));
            Assert.AreEqual(2, _config.SeedsForSeason(2));
            Assert.AreEqual(2, _config.SeedsForSeason(3));
            Assert.AreEqual(3, _config.SeedsForSeason(4));
        }

        // Balance (F-11): comprarlo todo no es instantáneo ni inalcanzable, y la potencia junta de venta y crecimiento tiene techo.
        [Test]
        public void BuyingEverything_TakesBetweenFourAndTwelveSeasons_AndPowerHasACap()
        {
            int total = 0;
            float sell = 1f;
            float growth = 1f;
            for (int i = 0; i < _config.PrestigeShop.Count; i++)
            {
                PrestigeShopItem item = _config.PrestigeShop[i];
                for (int level = 0; level < item.MaxLevel; level++) total += item.CostOf(level);
                if (item.Kind == PrestigeShopKind.SellPrice) sell = item.ValueAt(item.MaxLevel);
                if (item.Kind == PrestigeShopKind.GrowthSpeed) growth = item.ValueAt(item.MaxLevel);
            }

            int seeds = 0;
            int seasons = 0;
            while (seeds < total && seasons < 100) seeds += _config.SeedsForSeason(++seasons);

            Assert.That(seasons, Is.InRange(4, 12), "Con " + total + " semillas en total.");
            Assert.LessOrEqual(sell * growth, 2f, "Venta y crecimiento al máximo no pueden más que duplicar la ganancia.");
        }

        // ---------- Semillas y compras ----------

        [Test]
        public void StartingASeason_GivesSeeds()
        {
            PrestigeManager prestige = CreateServices(out _, out ProgressionManager progression);
            progression.Restore(EverythingBought());

            Assert.AreEqual(0, prestige.Seeds);
            Assert.IsTrue(prestige.TryStartSeason());
            Assert.AreEqual(_config.SeedsForSeason(1), prestige.Seeds);
        }

        [Test]
        public void Buying_SpendsSeeds_RaisesTheLevel_AndStopsAtMax()
        {
            PrestigeManager prestige = CreateServices(out _, out _);
            prestige.Restore("{\"version\":2,\"seasons\":1,\"seeds\":100}");
            int index = IndexOf(prestige, PrestigeShopKind.GrowthSpeed);
            PrestigeShopItem item = prestige.ShopItem(index);

            Assert.AreEqual(1f, prestige.GrowthMultiplier);
            int expectedSeeds = 100;
            for (int level = 1; level <= item.MaxLevel; level++)
            {
                expectedSeeds -= prestige.ShopNextCost(index);
                Assert.IsTrue(prestige.TryBuyShop(index));
                Assert.AreEqual(level, prestige.ShopLevel(index));
                Assert.AreEqual(expectedSeeds, prestige.Seeds);
                Assert.AreEqual(item.ValueAt(level), prestige.GrowthMultiplier, 1e-6f);
            }

            Assert.IsFalse(prestige.CanBuyShop(index), "Al máximo no se compra más.");
            Assert.AreEqual(expectedSeeds, prestige.Seeds);
        }

        [Test]
        public void Buying_WithoutEnoughSeeds_ChangesNothing()
        {
            PrestigeManager prestige = CreateServices(out _, out _);
            prestige.Restore("{\"version\":2,\"seasons\":1,\"seeds\":0}");

            Assert.IsFalse(prestige.TryBuyShop(0));
            Assert.AreEqual(0, prestige.ShopLevel(0));
            Assert.AreEqual(0, prestige.Seeds);
        }

        // ---------- Efectos ----------

        [Test]
        public void TheSellUpgrade_RaisesThePriceMultiplier()
        {
            PrestigeManager prestige = CreateServices(out EconomyManager economy, out _);
            prestige.Restore("{\"version\":2,\"seasons\":2,\"seeds\":50}");
            float before = prestige.Multiplier;

            Assert.IsTrue(prestige.TryBuyShop(IndexOf(prestige, PrestigeShopKind.SellPrice)));

            Assert.Greater(prestige.Multiplier, before);
            Assert.AreEqual(prestige.Multiplier, economy.Pricing.PrestigeMultiplier, 1e-6f);
        }

        [Test]
        public void TheStartCoinsUpgrade_PaysOnTheNextSeason()
        {
            PrestigeManager prestige = CreateServices(out EconomyManager economy, out ProgressionManager progression);
            prestige.Restore("{\"version\":2,\"seasons\":0,\"seeds\":10}");
            Assert.IsTrue(prestige.TryBuyShop(IndexOf(prestige, PrestigeShopKind.StartCoins)));
            long bonus = prestige.StartCoinsBonus;
            Assert.Greater(bonus, 0L);

            progression.Restore(EverythingBought());
            Assert.IsTrue(prestige.TryStartSeason());

            Assert.AreEqual(_config.StartingCoins + bonus, economy.Balance);
        }

        [Test]
        public void TheStatics_FollowTheUpgrades_AndAreNeutralWithNone()
        {
            PrestigeManager prestige = CreateServices(out _, out _);
            prestige.Restore("{\"version\":2,\"seasons\":1,\"seeds\":100}");
            Assert.AreEqual(1f, PrestigeManager.GrowthFactor);
            Assert.AreEqual(1f, PrestigeManager.XpFactor);
            Assert.AreEqual(0f, PrestigeManager.OfflineCapBonus);

            prestige.TryBuyShop(IndexOf(prestige, PrestigeShopKind.GrowthSpeed));
            prestige.TryBuyShop(IndexOf(prestige, PrestigeShopKind.XpGain));
            prestige.TryBuyShop(IndexOf(prestige, PrestigeShopKind.OfflineCap));
            Assert.Greater(PrestigeManager.GrowthFactor, 1f);
            Assert.Greater(PrestigeManager.XpFactor, 1f);
            Assert.Greater(PrestigeManager.OfflineCapBonus, 0f);
        }

        // ---------- Partida guardada ----------

        [Test]
        public void TheSave_RoundTrips_SeedsAndLevels()
        {
            PrestigeManager prestige = CreateServices(out _, out _);
            prestige.Restore("{\"version\":2,\"seasons\":3,\"seeds\":20}");
            int index = IndexOf(prestige, PrestigeShopKind.OfflineCap);
            prestige.TryBuyShop(index);
            prestige.TryBuyShop(index);
            string json = prestige.Capture();
            int seeds = prestige.Seeds;

            prestige.Restore(null);
            Assert.AreEqual(0, prestige.Seeds);
            Assert.AreEqual(0, prestige.ShopLevel(index));

            prestige.Restore(json);
            Assert.AreEqual(seeds, prestige.Seeds);
            Assert.AreEqual(2, prestige.ShopLevel(index));
            Assert.AreEqual(3, prestige.Seasons);
        }

        [Test]
        public void ASaveFromV1_LoadsWithNoSeedsAndNoUpgrades()
        {
            PrestigeManager prestige = CreateServices(out _, out _);
            prestige.Restore("{\"version\":1,\"seasons\":4}");

            Assert.AreEqual(4, prestige.Seasons);
            Assert.AreEqual(0, prestige.Seeds);
            for (int i = 0; i < prestige.ShopCount; i++) Assert.AreEqual(0, prestige.ShopLevel(i));
        }

        [Test]
        public void ALevelAboveTheMax_OrAnUnknownId_IsIgnored()
        {
            PrestigeManager prestige = CreateServices(out _, out _);
            string id = prestige.ShopItem(0).Id;
            int max = prestige.ShopItem(0).MaxLevel;
            prestige.Restore("{\"version\":2,\"seasons\":1,\"seeds\":-9,\"shopIds\":[\"" + id + "\",\"no_existe\"],\"shopLevels\":[99,2]}");

            Assert.AreEqual(max, prestige.ShopLevel(0));
            Assert.AreEqual(0, prestige.Seeds, "Las semillas no bajan de 0.");
        }
    }
}
