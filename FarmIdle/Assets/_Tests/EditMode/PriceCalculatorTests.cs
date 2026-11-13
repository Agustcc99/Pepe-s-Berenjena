using System;
using NUnit.Framework;

namespace FarmIdle.Tests
{
    public class PriceCalculatorTests
    {
        [Test]
        public void ByDefault_TheFinalPriceIsTheBasePrice()
        {
            var calculator = new PriceCalculator();

            Assert.AreEqual(1f, calculator.UpgradeMultiplier);
            Assert.AreEqual(1f, calculator.BuffMultiplier);
            Assert.AreEqual(0L, calculator.FinalPrice(0));
            Assert.AreEqual(2L, calculator.FinalPrice(2));
            Assert.AreEqual(11L, calculator.FinalPrice(11));
        }

        [Test]
        public void FinalPrice_IsBaseTimesUpgradesTimesBuffs()
        {
            var calculator = new PriceCalculator();
            calculator.SetUpgradeMultiplier(1.5f);
            calculator.SetBuffMultiplier(2f);

            Assert.AreEqual(30L, calculator.FinalPrice(10), "10 x 1,5 x 2");

            calculator.SetBuffMultiplier(1f);   // el buff se acaba: vuelven solo las mejoras
            Assert.AreEqual(15L, calculator.FinalPrice(10));

            calculator.SetUpgradeMultiplier(1f);
            Assert.AreEqual(10L, calculator.FinalPrice(10));
        }

        [Test]
        public void TheStaticOverload_DoesTheSameMath_WithoutTouchingTheState()
        {
            var calculator = new PriceCalculator();

            Assert.AreEqual(24L, PriceCalculator.FinalPrice(4, 3f, 2f));
            Assert.AreEqual(1f, calculator.UpgradeMultiplier);
            Assert.AreEqual(4L, calculator.FinalPrice(4));
        }

        [TestCase(5, 1.5f, 1f, 8L)]       // 7,5 sube
        [TestCase(5, 1.3f, 1f, 7L)]       // 6,5 sube
        [TestCase(3, 1.1f, 1f, 3L)]       // 3,3 baja
        [TestCase(3, 1.2f, 1f, 4L)]       // 3,6 sube
        [TestCase(1, 0.4f, 1f, 0L)]       // 0,4 baja a 0: con multiplicadores chicos una unidad puede no pagar
        [TestCase(1, 0.5f, 1f, 1L)]       // 0,5 sube (la mitad va hacia arriba)
        public void FinalPrice_RoundsToTheNearestWholeCoin_HalvesGoUp(int basePrice, float upgrades, float buffs, long expected)
        {
            Assert.AreEqual(expected, PriceCalculator.FinalPrice(basePrice, upgrades, buffs));
        }

        [Test]
        public void AZeroMultiplier_IsAllowed_AndPaysNothing()
        {
            Assert.AreEqual(0L, PriceCalculator.FinalPrice(10, 0f, 1f));
        }

        [Test]
        public void HugeValues_SaturateInsteadOfWrappingAround()
        {
            Assert.AreEqual(long.MaxValue, PriceCalculator.FinalPrice(int.MaxValue, float.MaxValue, float.MaxValue));
            Assert.AreEqual((long)int.MaxValue * 2L, PriceCalculator.FinalPrice(int.MaxValue, 2f, 1f), "Sin perder los enteros grandes.");
        }

        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void InvalidMultipliers_AreRejected(float multiplier)
        {
            var calculator = new PriceCalculator();

            Assert.Throws<ArgumentOutOfRangeException>(() => calculator.SetUpgradeMultiplier(multiplier));
            Assert.Throws<ArgumentOutOfRangeException>(() => calculator.SetBuffMultiplier(multiplier));
            Assert.Throws<ArgumentOutOfRangeException>(() => PriceCalculator.FinalPrice(5, multiplier, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => PriceCalculator.FinalPrice(5, 1f, multiplier));
            Assert.AreEqual(1f, calculator.UpgradeMultiplier, "Un valor inválido no cambia nada.");
            Assert.AreEqual(1f, calculator.BuffMultiplier);
        }

        [Test]
        public void ANegativeBasePrice_IsRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new PriceCalculator().FinalPrice(-1));
        }
    }
}
