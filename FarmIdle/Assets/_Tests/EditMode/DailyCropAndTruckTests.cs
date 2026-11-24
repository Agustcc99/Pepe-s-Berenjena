using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FarmIdle.Tests
{
    // IN-02 (cultivo del día) y las mejoras visuales del camión. Reglas puras (fecha -> cultivo), el extra en PriceCalculator y los dos
    // managers con el GameConfig real.
    public class DailyCropAndTruckTests
    {
        private static readonly string[] Ids = { "wheat", "carrot", "tomato", "pumpkin", "corn", "eggplant", "watermelon", "sunflower" };

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
                if (go != null) UnityEngine.Object.DestroyImmediate(go);
            }

            _objects.Clear();
        }

        private T Create<T>() where T : Component
        {
            var go = new GameObject(typeof(T).Name);
            _objects.Add(go);
            return go.AddComponent<T>();
        }

        // ---------- Reglas ----------

        [Test]
        public void TheSameDay_GivesTheSameCrop_AndItDoesNotDependOnTheListOrder()
        {
            string first = DailyCropRules.Pick(Ids, id => true, 20261124);
            Assert.IsNotNull(first);
            Assert.AreEqual(first, DailyCropRules.Pick(Ids, id => true, 20261124));

            var reversed = new List<string>(Ids);
            reversed.Reverse();
            Assert.AreEqual(first, DailyCropRules.Pick(reversed, id => true, 20261124));
        }

        [Test]
        public void OnlyOpenCrops_CanBePicked_AndNoneOpenGivesNull()
        {
            var open = new HashSet<string> { "wheat", "carrot", "tomato" };
            for (int day = 0; day < 60; day++)
            {
                string crop = DailyCropRules.Pick(Ids, open.Contains, 20261101 + day);
                Assert.IsTrue(open.Contains(crop), "Salió un cultivo cerrado: " + crop);
            }

            Assert.IsNull(DailyCropRules.Pick(Ids, id => false, 20261124));
            Assert.IsNull(DailyCropRules.Pick(new string[0], id => true, 20261124));
        }

        [Test]
        public void OverAYear_EveryCropComesOut_AndNoneDominates()
        {
            var counts = new Dictionary<string, int>();
            DateTime day = new DateTime(2026, 1, 1);
            for (int i = 0; i < 365; i++, day = day.AddDays(1))
            {
                string crop = DailyCropRules.Pick(Ids, id => true, MissionRules.DayKey(day));
                counts[crop] = counts.TryGetValue(crop, out int n) ? n + 1 : 1;
            }

            Assert.AreEqual(Ids.Length, counts.Count, "Algún cultivo no salió nunca.");
            foreach (KeyValuePair<string, int> pair in counts)
            {
                Assert.That(pair.Value, Is.InRange(20, 75), "Reparto muy desparejo: " + pair.Key + " = " + pair.Value);
            }
        }

        // ---------- Precio ----------

        [Test]
        public void TheDailyCrop_PaysTheBonus_AndTheOthersDoNot()
        {
            var pricing = new PriceCalculator();
            Assert.AreEqual(10L, pricing.FinalPrice(10, "pumpkin"));

            pricing.SetDailyCrop("pumpkin", 0.5f);
            Assert.AreEqual(15L, pricing.FinalPrice(10, "pumpkin"));
            Assert.AreEqual(10L, pricing.FinalPrice(10, "wheat"));
            Assert.AreEqual(10L, pricing.FinalPrice(10), "Sin id no hay extra.");

            pricing.SetDailyCrop("", 0.5f);
            Assert.AreEqual(10L, pricing.FinalPrice(10, "pumpkin"));
            pricing.SetDailyCrop("pumpkin", 0f);
            Assert.AreEqual(10L, pricing.FinalPrice(10, "pumpkin"));
        }

        // ---------- DailyCropManager ----------

        [Test]
        public void TheManager_PicksOneOpenCrop_TellsTheEconomy_AndFollowsTheClock()
        {
            EconomyManager economy = Create<EconomyManager>();
            economy.Initialize(_config);

            DailyCropManager daily = Create<DailyCropManager>();
            DateTime now = new DateTime(2026, 11, 24, 10, 0, 0);
            daily.Clock = () => now;
            daily.Initialize(_config);   // sin progresión: solo está abierta la granja inicial

            Assert.IsTrue(daily.IsActive);
            Assert.That(new[] { "wheat", "carrot", "tomato" }, Contains.Item(daily.CropId));
            Assert.AreEqual(daily.CropId, economy.Pricing.DailyCropId);
            Assert.AreEqual(_config.DailyCropBonus, economy.Pricing.DailyBonus, 1e-5f);
            Assert.IsTrue(daily.IsDaily(daily.Crop));

            string today = daily.CropId;
            int changes = 0;
            daily.Changed += () => changes++;

            daily.Refresh();
            Assert.AreEqual(0, changes, "El mismo día no cambia nada.");

            // Otro día: puede salir el mismo (1 de 3) pero a lo largo de una semana tiene que cambiar alguna vez.
            bool changed = false;
            for (int i = 1; i <= 7 && !changed; i++)
            {
                now = now.AddDays(1);
                daily.Refresh();
                changed = daily.CropId != today;
            }

            Assert.IsTrue(changed);
            Assert.Greater(changes, 0);
        }

        [Test]
        public void ASale_OfTheDailyCrop_PaysMore()
        {
            EconomyManager economy = Create<EconomyManager>();
            economy.Initialize(_config);
            DailyCropManager daily = Create<DailyCropManager>();
            daily.Clock = () => new DateTime(2026, 11, 24);
            daily.Initialize(_config);

            CropData dailyCrop = daily.Crop;
            Assert.IsNotNull(dailyCrop);
            long expected = PriceCalculator.FinalPrice(dailyCrop.SellPrice, 1f, 1f, 1f + _config.DailyCropBonus);

            Assert.AreEqual(expected, economy.SellProduct(dailyCrop));
        }

        // ---------- Camión ----------

        [Test]
        public void TheTruckSkins_AreThreeWithRisingPrices_AndUniqueIds()
        {
            Assert.AreEqual(3, _config.TruckSkins.Count);

            var ids = new HashSet<string>();
            for (int i = 0; i < _config.TruckSkins.Count; i++)
            {
                Assert.IsTrue(ids.Add(_config.TruckSkins[i].Id), "Id repetido: " + _config.TruckSkins[i].Id);
                if (i > 0) Assert.Greater(_config.TruckSkins[i].Cost, _config.TruckSkins[i - 1].Cost);
                Assert.AreNotEqual(_config.TruckSkins[i].NameKey, Loc.Get(_config.TruckSkins[i].NameKey), "Falta el texto del camión: " + _config.TruckSkins[i].Id);
            }
        }

        [Test]
        public void TheTruck_IsBoughtInOrder_OnlyWithCoins_AndSavesTheCount()
        {
            EconomyManager economy = Create<EconomyManager>();
            economy.Initialize(_config);
            TruckManager truck = Create<TruckManager>();
            truck.Initialize(_config);

            Assert.AreEqual(0, truck.Owned);
            Assert.IsFalse(truck.CanBuyNext);
            Assert.IsFalse(truck.TryBuyNext(), "Sin monedas no compra.");

            economy.AddCoins(1000000L);
            long before = economy.Balance;
            int levels = 0;
            truck.Bought += level => levels = level;

            Assert.IsTrue(truck.TryBuyNext());
            Assert.AreEqual(1, truck.Owned);
            Assert.AreEqual(1, levels);
            Assert.AreEqual(before - _config.TruckSkins[0].Cost, economy.Balance);

            string json = truck.Capture();
            Assert.IsTrue(truck.TryBuyNext());
            Assert.IsTrue(truck.TryBuyNext());
            Assert.IsTrue(truck.AllOwned);
            Assert.IsFalse(truck.TryBuyNext(), "Con todas compradas no cobra de nuevo.");

            truck.Restore(json);
            Assert.AreEqual(1, truck.Owned);
            truck.Restore(null);
            Assert.AreEqual(0, truck.Owned, "Una partida sin la sección arranca en 0.");
            truck.Restore("{\"version\":1,\"owned\":99}");
            Assert.AreEqual(truck.Total, truck.Owned, "La cantidad se acota a las que existen.");
        }
    }
}
