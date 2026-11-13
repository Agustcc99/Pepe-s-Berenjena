using System;
using NUnit.Framework;
using UnityEngine;

namespace FarmIdle.Tests
{
    // El efecto total de las mejoras compradas (C# puro sobre los datos): lugares extra que suman y multiplicadores que se componen.
    public class ProgressionModifiersTests
    {
        private PlayerProgress _progress;
        private UpgradeData _capacity;
        private UpgradeData _speed;
        private UpgradeData _price;
        private UpgradeData _growth;
        private UpgradeData[] _all;

        [SetUp]
        public void SetUp()
        {
            _progress = new PlayerProgress();
            _capacity = ProgressionTestData.Upgrade("capacity", UpgradeKind.BackpackCapacity, (100L, 4f), (200L, 8f), (400L, 12f));
            _speed = ProgressionTestData.Upgrade("speed", UpgradeKind.MoveSpeed, (100L, 1.15f), (200L, 1.3f));
            _price = ProgressionTestData.Upgrade("price", UpgradeKind.SellPrice, (100L, 1.5f));
            _growth = ProgressionTestData.Upgrade("growth", UpgradeKind.GrowthSpeed, (100L, 1.25f), (200L, 2f));
            _all = new[] { _capacity, _speed, _price, _growth };
        }

        [TearDown]
        public void TearDown()
        {
            foreach (UpgradeData upgrade in _all) UnityEngine.Object.DestroyImmediate(upgrade);
        }

        [Test]
        public void WithoutUpgrades_EverythingIsNeutral()
        {
            ProgressionModifiers none = ProgressionModifiers.None;
            Assert.AreEqual(0, none.BackpackBonus);
            Assert.AreEqual(1f, none.MoveSpeed);
            Assert.AreEqual(1f, none.SellPrice);
            Assert.AreEqual(1f, none.GrowthSpeed);

            ProgressionModifiers computed = ProgressionModifiers.Compute(_progress, _all);
            Assert.AreEqual(0, computed.BackpackBonus);
            Assert.AreEqual(1f, computed.MoveSpeed);
            Assert.AreEqual(1f, computed.SellPrice);
            Assert.AreEqual(1f, computed.GrowthSpeed);
        }

        [Test]
        public void EachUpgrade_FeedsItsOwnModifier()
        {
            _progress.SetLevel("capacity", 2);
            _progress.SetLevel("speed", 1);
            _progress.SetLevel("price", 1);
            _progress.SetLevel("growth", 2);

            ProgressionModifiers m = ProgressionModifiers.Compute(_progress, _all);

            Assert.AreEqual(8, m.BackpackBonus, "El nivel 2 de capacidad da +8 en total.");
            Assert.AreEqual(1.15f, m.MoveSpeed, 1e-5f);
            Assert.AreEqual(1.5f, m.SellPrice, 1e-5f);
            Assert.AreEqual(2f, m.GrowthSpeed, 1e-5f);
        }

        [Test]
        public void ALevelAboveTheMaximum_CountsAsTheMaximum()
        {
            _progress.SetLevel("speed", 9);

            Assert.AreEqual(1.3f, ProgressionModifiers.Compute(_progress, _all).MoveSpeed, 1e-5f);
        }

        [Test]
        public void TwoUpgradesOfTheSameKind_Accumulate()
        {
            UpgradeData boots = ProgressionTestData.Upgrade("boots", UpgradeKind.MoveSpeed, (100L, 1.1f));
            UpgradeData bag = ProgressionTestData.Upgrade("bag", UpgradeKind.BackpackCapacity, (100L, 3f));
            try
            {
                _progress.SetLevel("speed", 2);
                _progress.SetLevel("boots", 1);
                _progress.SetLevel("capacity", 1);
                _progress.SetLevel("bag", 1);

                ProgressionModifiers m = ProgressionModifiers.Compute(_progress, new[] { _speed, boots, _capacity, bag });

                Assert.AreEqual(1.3f * 1.1f, m.MoveSpeed, 1e-5f, "Las velocidades se multiplican.");
                Assert.AreEqual(7, m.BackpackBonus, "Los lugares extra se suman.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(boots);
                UnityEngine.Object.DestroyImmediate(bag);
            }
        }

        [Test]
        public void UpgradesNotInTheTable_AreIgnored_AndNullEntriesAreSkipped()
        {
            _progress.SetLevel("unknown-upgrade", 3);

            ProgressionModifiers m = ProgressionModifiers.Compute(_progress, new UpgradeData[] { null, _price, null });

            Assert.AreEqual(1f, m.SellPrice);
            Assert.AreEqual(0, m.BackpackBonus);
        }

        [Test]
        public void ANullTable_IsNeutral_ANullProgress_Throws()
        {
            ProgressionModifiers m = ProgressionModifiers.Compute(_progress, null);
            Assert.AreEqual(1f, m.MoveSpeed);

            Assert.Throws<ArgumentNullException>(() => ProgressionModifiers.Compute(null, _all));
        }

        // El multiplicador de precio entra tal cual en PriceCalculator: con 1,33 / 1,67 / 2 los precios base del juego
        // (3, 6, 12 y 24) dan siempre monedas enteras exactas, sin que el redondeo cambie el efecto de la mejora.
        [TestCase(3, 1.33f, 4L)]
        [TestCase(3, 1.67f, 5L)]
        [TestCase(3, 2f, 6L)]
        [TestCase(6, 1.33f, 8L)]
        [TestCase(6, 1.67f, 10L)]
        [TestCase(12, 1.33f, 16L)]
        [TestCase(12, 1.67f, 20L)]
        [TestCase(24, 1.33f, 32L)]
        [TestCase(24, 1.67f, 40L)]
        public void ThePriceMultipliersOfTheGame_GiveExactWholeCoins(int basePrice, float multiplier, long expected)
        {
            Assert.AreEqual(expected, PriceCalculator.FinalPrice(basePrice, multiplier, 1f));
        }
    }
}
