using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FarmIdle.Tests
{
    // F-17 tanda 1: las decoraciones de la granja. Datos del GameConfig real (8, precios crecientes, ~500.000 en total) y el
    // DecorationManager (se compran en orden con TrySpend, solo con monedas, se guardan y una partida vieja arranca en 0).
    public class DecorationTests
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

        private DecorationManager CreateServices(out EconomyManager economy)
        {
            economy = Create<EconomyManager>();
            economy.Initialize(_config);
            DecorationManager decorations = Create<DecorationManager>();
            decorations.Initialize(_config);
            return decorations;
        }

        [Test]
        public void TheConfig_HasEightDecorations_WithRisingPrices_AddingUpToHalfAMillion()
        {
            Assert.AreEqual(8, _config.Decorations.Count);

            long total = 0;
            var ids = new HashSet<string>();
            for (int i = 0; i < _config.Decorations.Count; i++)
            {
                DecorationData data = _config.Decorations[i];
                Assert.IsNotNull(data, "Decoración " + i + " sin asset.");
                Assert.IsTrue(ids.Add(data.Id), "Id repetido: " + data.Id);
                if (i > 0) Assert.Greater(data.Cost, _config.Decorations[i - 1].Cost, "Los precios crecen: " + data.Id);
                total += data.Cost;
            }

            Assert.AreEqual(500000L, total, "F-17: ~1,4 h de juego con el ingreso del final (ECONOMIA_V1.md).");
        }

        [Test]
        public void TheBuyOrder_FollowsTheList_AndEachOneCharges()
        {
            DecorationManager decorations = CreateServices(out EconomyManager economy);
            economy.AddCoins(1000000L);
            long before = economy.Wallet.Balance;

            Assert.AreEqual(_config.Decorations[0], decorations.Next);
            Assert.IsTrue(decorations.TryBuyNext());
            Assert.AreEqual(1, decorations.Owned);
            Assert.AreEqual(before - _config.Decorations[0].Cost, economy.Wallet.Balance);
            Assert.IsTrue(decorations.IsOwned(0));
            Assert.IsFalse(decorations.IsOwned(1));
            Assert.AreEqual(_config.Decorations[1], decorations.Next);
        }

        [Test]
        public void EachDecoration_AddsCharm_ToTheSellPrice_AndASavedGameRestoresIt()
        {
            DecorationManager decorations = CreateServices(out EconomyManager economy);
            economy.AddCoins(2000000L);

            Assert.AreEqual(1f, economy.Pricing.CharmMultiplier, 1e-4f, "Sin decoraciones no hay encanto.");
            Assert.AreEqual(100L, economy.Pricing.FinalPrice(100));

            Assert.IsTrue(decorations.TryBuyNext());
            Assert.AreEqual(1f + _config.Decorations[0].Charm, economy.Pricing.CharmMultiplier, 1e-4f);
            Assert.Greater(economy.Pricing.FinalPrice(100), 100L, "La primera decoración ya sube el precio.");

            while (decorations.TryBuyNext()) { }

            float expected = 1f;
            foreach (DecorationData data in _config.Decorations) expected += data.Charm;
            Assert.AreEqual(expected, economy.Pricing.CharmMultiplier, 1e-4f);
            Assert.AreEqual(1.16f, economy.Pricing.CharmMultiplier, 1e-3f, "F-21: +2 % cada una, +16 % las ocho.");

            // Una partida con 3 decoraciones recupera el encanto de esas 3 (restaurar no cobra ni suma de más).
            string save = decorations.Capture();
            DecorationManager other = Create<DecorationManager>();
            other.Initialize(_config);
            economy.Pricing.SetCharmMultiplier(1f);
            other.Restore("{\"version\":1,\"owned\":3}");
            Assert.AreEqual(1f + _config.Decorations[0].Charm + _config.Decorations[1].Charm + _config.Decorations[2].Charm, economy.Pricing.CharmMultiplier, 1e-4f);
            Assert.IsNotEmpty(save);

            other.Restore(null);
            Assert.AreEqual(1f, economy.Pricing.CharmMultiplier, 1e-4f, "Sin sección guardada vuelve a 1.");
        }

        [Test]
        public void WithoutCoins_NothingIsBoughtAndNothingIsCharged()
        {
            DecorationManager decorations = CreateServices(out EconomyManager economy);
            economy.AddCoins(_config.Decorations[0].Cost - 1);

            Assert.IsFalse(decorations.CanBuyNext);
            Assert.IsFalse(decorations.TryBuyNext());
            Assert.AreEqual(0, decorations.Owned);
            Assert.AreEqual(_config.Decorations[0].Cost - 1, economy.Wallet.Balance);
        }

        [Test]
        public void WhenAllAreOwned_ThereIsNothingLeftToBuy()
        {
            DecorationManager decorations = CreateServices(out EconomyManager economy);
            economy.AddCoins(2000000L);

            for (int i = 0; i < _config.Decorations.Count; i++) Assert.IsTrue(decorations.TryBuyNext(), "Compra " + i);

            Assert.IsTrue(decorations.AllOwned);
            Assert.IsNull(decorations.Next);
            Assert.IsFalse(decorations.TryBuyNext());
            Assert.AreEqual(2000000L - 500000L, economy.Wallet.Balance);
        }

        [Test]
        public void TheGame_Saves_Restores_AndAnOldSaveWithoutTheSectionStartsAtZero()
        {
            DecorationManager decorations = CreateServices(out EconomyManager economy);
            economy.AddCoins(1000000L);
            decorations.TryBuyNext();
            decorations.TryBuyNext();
            string json = decorations.Capture();

            DecorationManager other = CreateServices(out _);
            other.Restore(json);
            Assert.AreEqual(2, other.Owned);

            other.Restore(null);
            Assert.AreEqual(0, other.Owned, "Partida vieja: sin la sección arranca en 0.");

            other.Restore("{\"version\":1,\"owned\":99}");
            Assert.AreEqual(_config.Decorations.Count, other.Owned, "Se acota a las que existen.");

            other.Restore("esto no es json");
            Assert.AreEqual(0, other.Owned);
        }

        [Test]
        public void Buying_RaisesTheEvents_ButRestoringDoesNotCharge()
        {
            DecorationManager decorations = CreateServices(out EconomyManager economy);
            economy.AddCoins(100000L);
            int bought = -1;
            decorations.Bought += index => bought = index;

            decorations.TryBuyNext();
            Assert.AreEqual(0, bought);

            long balance = economy.Wallet.Balance;
            decorations.Restore("{\"version\":1,\"owned\":3}");
            Assert.AreEqual(balance, economy.Wallet.Balance, "Restaurar no cobra.");
        }
    }
}
