using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FarmIdle.Tests
{
    // F-17 tanda 3: las skins del jugador. Datos del GameConfig real (6 colores distintos, ~700.000 en total) y el SkinManager
    // (compra en orden y se pone sola, cambia entre las que tiene, se guarda y una partida vieja arranca sin skins).
    public class SkinTests
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

        private SkinManager CreateServices(out EconomyManager economy)
        {
            economy = Create<EconomyManager>();
            economy.Initialize(_config);
            SkinManager skins = Create<SkinManager>();
            skins.Initialize(_config);
            return skins;
        }

        [Test]
        public void TheConfig_HasSixDistinctSkins_WithRisingPrices_AddingUpTo700000()
        {
            Assert.AreEqual(6, _config.Skins.Count);

            long total = 0;
            var colors = new HashSet<Color>();
            for (int i = 0; i < _config.Skins.Count; i++)
            {
                SkinData skin = _config.Skins[i];
                Assert.IsNotNull(skin, "Skin " + i + " sin asset.");
                Assert.IsTrue(colors.Add(skin.BodyColor), "Color repetido: " + skin.Id);
                if (i > 0) Assert.Greater(skin.Cost, _config.Skins[i - 1].Cost, "Los precios crecen: " + skin.Id);
                total += skin.Cost;
            }

            Assert.AreEqual(700000L, total, "F-17: ~2 h con el ingreso del final (ECONOMIA_V1.md).");
        }

        [Test]
        public void Buying_FollowsTheList_ChargesEach_AndPutsTheNewSkinOn()
        {
            SkinManager skins = CreateServices(out EconomyManager economy);
            economy.AddCoins(1000000L);
            long before = economy.Wallet.Balance;

            Assert.IsTrue(skins.TryBuyNext());
            Assert.AreEqual(before - _config.Skins[0].Cost, economy.Wallet.Balance);
            Assert.AreEqual(1, skins.Owned);
            Assert.AreEqual(_config.Skins[0], skins.EquippedSkin);

            Assert.IsTrue(skins.TryBuyNext());
            Assert.AreEqual(_config.Skins[1], skins.EquippedSkin, "La recién comprada se pone sola.");
        }

        [Test]
        public void WithoutCoins_NothingIsBought_AndWithoutSkinsThereIsNothingToDo()
        {
            SkinManager skins = CreateServices(out EconomyManager economy);
            economy.AddCoins(_config.Skins[0].Cost - 1);

            Assert.IsFalse(skins.CanBuyNext);
            Assert.IsFalse(skins.CanAct);
            Assert.IsFalse(skins.TryBuyNext());
            Assert.IsFalse(skins.Cycle());
            Assert.AreEqual(0, skins.Owned);
            Assert.IsNull(skins.EquippedSkin);
        }

        [Test]
        public void Cycle_GoesThroughTheDefaultAndEveryOwnedSkin()
        {
            SkinManager skins = CreateServices(out EconomyManager economy);
            economy.AddCoins(1000000L);
            skins.TryBuyNext();
            skins.TryBuyNext();
            Assert.AreEqual(1, skins.Equipped);

            Assert.IsTrue(skins.Cycle());
            Assert.AreEqual(-1, skins.Equipped, "Después de la última vuelve al color de fábrica.");
            Assert.IsNull(skins.EquippedSkin);

            skins.Cycle();
            Assert.AreEqual(0, skins.Equipped);
            skins.Cycle();
            Assert.AreEqual(1, skins.Equipped);
        }

        [Test]
        public void TheGame_Saves_Restores_AndAnOldSaveStartsWithoutSkins()
        {
            SkinManager skins = CreateServices(out EconomyManager economy);
            economy.AddCoins(1000000L);
            skins.TryBuyNext();
            skins.TryBuyNext();
            skins.Cycle();
            string json = skins.Capture();

            SkinManager other = CreateServices(out _);
            other.Restore(json);
            Assert.AreEqual(2, other.Owned);
            Assert.AreEqual(-1, other.Equipped);

            other.Restore(null);
            Assert.AreEqual(0, other.Owned, "Partida vieja: sin la sección arranca sin skins.");
            Assert.AreEqual(-1, other.Equipped);

            other.Restore("{\"version\":1,\"owned\":99,\"equipped\":50}");
            Assert.AreEqual(_config.Skins.Count, other.Owned, "Se acota a las que existen.");
            Assert.AreEqual(_config.Skins.Count - 1, other.Equipped);

            other.Restore("basura");
            Assert.AreEqual(0, other.Owned);
        }

        [Test]
        public void Restoring_DoesNotCharge()
        {
            SkinManager skins = CreateServices(out EconomyManager economy);
            economy.AddCoins(50000L);
            skins.Restore("{\"version\":1,\"owned\":3,\"equipped\":2}");
            Assert.AreEqual(50000L, economy.Wallet.Balance);
        }
    }
}
