using System;
using NUnit.Framework;

namespace FarmIdle.Tests
{
    public class WalletTests
    {
        [Test]
        public void NewWallet_UsesInitialBalance_AndNeverStartsNegative()
        {
            Assert.AreEqual(0L, new Wallet().Balance);
            Assert.AreEqual(25L, new Wallet(25).Balance);
            Assert.AreEqual(0L, new Wallet(-5).Balance);
        }

        [Test]
        public void Add_IncreasesBalance_AndRaisesEventWithNewBalanceAndDelta()
        {
            var wallet = new Wallet(5);
            int calls = 0;
            long eventBalance = -1;
            long eventDelta = -1;
            wallet.BalanceChanged += (balance, delta) => { calls++; eventBalance = balance; eventDelta = delta; };

            wallet.Add(10);

            Assert.AreEqual(15L, wallet.Balance);
            Assert.AreEqual(1, calls);
            Assert.AreEqual(15L, eventBalance);
            Assert.AreEqual(10L, eventDelta);
        }

        [Test]
        public void Add_Zero_DoesNothing()
        {
            var wallet = new Wallet(5);
            int calls = 0;
            wallet.BalanceChanged += (balance, delta) => calls++;

            wallet.Add(0);

            Assert.AreEqual(5L, wallet.Balance);
            Assert.AreEqual(0, calls);
        }

        [Test]
        public void Add_Negative_Throws()
        {
            var wallet = new Wallet(5);

            Assert.Throws<ArgumentOutOfRangeException>(() => wallet.Add(-1));
            Assert.AreEqual(5L, wallet.Balance);
        }

        [Test]
        public void Add_SaturatesAtMaxInsteadOfWrappingAround()
        {
            var wallet = new Wallet(long.MaxValue - 3);

            wallet.Add(10);

            Assert.AreEqual(long.MaxValue, wallet.Balance);
        }

        [Test]
        public void TrySpend_WithEnoughCoins_SubtractsAndRaisesEventWithNegativeDelta()
        {
            var wallet = new Wallet(50);
            long eventBalance = -1;
            long eventDelta = 0;
            wallet.BalanceChanged += (balance, delta) => { eventBalance = balance; eventDelta = delta; };

            bool spent = wallet.TrySpend(20);

            Assert.IsTrue(spent);
            Assert.AreEqual(30L, wallet.Balance);
            Assert.AreEqual(30L, eventBalance);
            Assert.AreEqual(-20L, eventDelta);
        }

        [Test]
        public void TrySpend_WithoutEnoughCoins_FailsKeepsBalanceAndRaisesNoEvent()
        {
            var wallet = new Wallet(10);
            int calls = 0;
            wallet.BalanceChanged += (balance, delta) => calls++;

            bool spent = wallet.TrySpend(11);

            Assert.IsFalse(spent);
            Assert.AreEqual(10L, wallet.Balance);
            Assert.AreEqual(0, calls);
        }

        [Test]
        public void TrySpend_ExactBalance_LeavesZero()
        {
            var wallet = new Wallet(10);

            Assert.IsTrue(wallet.TrySpend(10));
            Assert.AreEqual(0L, wallet.Balance);
        }

        [Test]
        public void TrySpend_Negative_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new Wallet(10).TrySpend(-1));
        }

        [Test]
        public void CanAfford_ComparesAgainstBalance()
        {
            var wallet = new Wallet(10);

            Assert.IsTrue(wallet.CanAfford(10));
            Assert.IsFalse(wallet.CanAfford(11));
            Assert.IsFalse(wallet.CanAfford(-1));
        }
    }
}
