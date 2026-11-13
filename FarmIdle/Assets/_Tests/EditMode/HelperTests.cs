using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FarmIdle.Tests
{
    // F-17 tanda 2: los ayudantes automáticos. Datos del GameConfig real (3 niveles, precios y efecto crecientes, 850.000 en total) y el
    // HelperManager (compra en orden con TrySpend, acredita monedas según la producción, se guarda, se reinicia con la temporada).
    public class HelperTests
    {
        private sealed class FakeSource : IProductionSource
        {
            public float Rate;
            public float BaseCoinsPerSecond => Rate;
        }

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

        private HelperManager CreateServices(float productionRate, out EconomyManager economy)
        {
            economy = Create<EconomyManager>();
            economy.Initialize(_config);

            OfflineManager offline = Create<OfflineManager>();
            offline.Initialize(_config);
            offline.Register(new FakeSource { Rate = productionRate });

            HelperManager helpers = Create<HelperManager>();
            helpers.Initialize(_config);
            return helpers;
        }

        [Test]
        public void TheConfig_HasThreeHelperLevels_WithRisingPriceAndEffect_AddingUpTo850000()
        {
            Assert.AreEqual(3, _config.Helpers.Count);

            long total = 0;
            for (int i = 0; i < _config.Helpers.Count; i++)
            {
                HelperData data = _config.Helpers[i];
                Assert.IsNotNull(data, "Ayudante " + i + " sin asset.");
                if (i > 0)
                {
                    Assert.Greater(data.Cost, _config.Helpers[i - 1].Cost, "El precio crece: " + data.Id);
                    Assert.Greater(data.Share, _config.Helpers[i - 1].Share, "El efecto crece: " + data.Id);
                }

                total += data.Cost;
            }

            Assert.AreEqual(850000L, total, "F-17: ~2,4 h con el ingreso del final (ECONOMIA_V1.md).");
            Assert.LessOrEqual(_config.Helpers[2].Share, 0.5f, "Tope de diseño: los ayudantes no pasan de +50 % de la producción.");
        }

        [Test]
        public void Buying_FollowsTheLevels_ChargesEach_AndStopsAtTheMax()
        {
            HelperManager helpers = CreateServices(1f, out EconomyManager economy);
            economy.AddCoins(2000000L);

            for (int i = 0; i < 3; i++)
            {
                long before = economy.Wallet.Balance;
                Assert.IsTrue(helpers.TryBuyNext(), "Compra " + i);
                Assert.AreEqual(before - _config.Helpers[i].Cost, economy.Wallet.Balance);
                Assert.AreEqual(i + 1, helpers.Level);
            }

            Assert.IsTrue(helpers.IsMaxed);
            Assert.IsNull(helpers.Next);
            Assert.IsFalse(helpers.TryBuyNext());
            Assert.AreEqual(2000000L - 850000L, economy.Wallet.Balance);
        }

        [Test]
        public void WithoutCoins_NothingIsBought()
        {
            HelperManager helpers = CreateServices(1f, out EconomyManager economy);
            economy.AddCoins(_config.Helpers[0].Cost - 1);

            Assert.IsFalse(helpers.CanBuyNext);
            Assert.IsFalse(helpers.TryBuyNext());
            Assert.AreEqual(0, helpers.Level);
            Assert.AreEqual(_config.Helpers[0].Cost - 1, economy.Wallet.Balance);
        }

        [Test]
        public void TheHelpers_EarnTheirShareOfTheProduction()
        {
            HelperManager helpers = CreateServices(10f, out EconomyManager economy);

            helpers.Tick(10f);
            Assert.AreEqual(0L, economy.Wallet.Balance, "Sin ayudantes no suman nada.");

            economy.AddCoins(1000000L);
            helpers.TryBuyNext();
            long before = economy.Wallet.Balance;

            helpers.Tick(10f);   // 10 monedas/s de producción x 10 s x Share del nivel 1
            long expected = (long)(10f * 10f * _config.Helpers[0].Share);
            Assert.AreEqual(before + expected, economy.Wallet.Balance, 1d, "Suma la fracción de la producción.");
        }

        [Test]
        public void TheCarry_AccumulatesFractionsOfACoin()
        {
            HelperManager helpers = CreateServices(1f, out EconomyManager economy);
            economy.AddCoins(1000000L);
            helpers.TryBuyNext();
            long before = economy.Wallet.Balance;

            for (int i = 0; i < 100; i++) helpers.Tick(0.1f);   // 10 s de juego, a 0,1 monedas/s: 1 moneda

            Assert.AreEqual(before + 1L, economy.Wallet.Balance, 1d, "Las fracciones de moneda se acumulan en vez de perderse.");
        }

        [Test]
        public void TheLevel_Saves_Restores_AndAnOldSaveStartsAtZero()
        {
            HelperManager helpers = CreateServices(1f, out EconomyManager economy);
            economy.AddCoins(1000000L);
            helpers.TryBuyNext();
            helpers.TryBuyNext();
            string json = helpers.Capture();

            HelperManager other = CreateServices(1f, out _);
            other.Restore(json);
            Assert.AreEqual(2, other.Level);

            other.Restore(null);
            Assert.AreEqual(0, other.Level, "Partida vieja: sin la sección arranca en 0.");

            other.Restore("{\"version\":1,\"level\":99}");
            Assert.AreEqual(_config.Helpers.Count, other.Level, "Se acota a los que existen.");

            other.Restore("no es json");
            Assert.AreEqual(0, other.Level);
        }

        [Test]
        public void ANewSeason_ResetsTheHelpers()
        {
            HelperManager helpers = CreateServices(1f, out EconomyManager economy);
            economy.AddCoins(1000000L);
            helpers.TryBuyNext();
            Assert.AreEqual(1, helpers.Level);

            helpers.ResetSeason();
            Assert.AreEqual(0, helpers.Level);
            Assert.AreEqual(0f, helpers.Share);
        }
    }
}
