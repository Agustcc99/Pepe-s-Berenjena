using NUnit.Framework;

namespace FarmIdle.Tests
{
    // El ritmo del pago a goteo (C# puro, sin escena: el reloj lo pasa la prueba). Valores de la mayoría: tarda 1 s en pagar todo
    // y espera 0,25 s al llegar.
    public class PurchaseDripTests
    {
        private PurchaseDrip _drip;

        [SetUp]
        public void SetUp()
        {
            _drip = new PurchaseDrip(1f, 0.25f);
        }

        // Camina el reloj en pasos de 'step' desde 'from' hasta 'to' (inclusive) y suma lo que ofreció pagar.
        private long Run(PurchaseDrip drip, float from, float to, float step, long remaining, long balance)
        {
            long total = 0;
            for (float now = from; now <= to + 1e-4f; now += step)
            {
                long offer = drip.Step(now, remaining - total, balance - total);
                Assert.GreaterOrEqual(offer, 0L);
                total += offer;
            }

            return total;
        }

        [Test]
        public void TheFirstStep_OnlyRegistersTheArrival()
        {
            Assert.IsFalse(_drip.IsActive);
            Assert.AreEqual(0L, _drip.Step(10f, 300, 1000));
            Assert.IsTrue(_drip.IsActive);
        }

        [Test]
        public void DuringTheStartDelay_NothingIsPaid()
        {
            _drip.Step(0f, 300, 1000);

            Assert.AreEqual(0L, _drip.Step(0.1f, 300, 1000));
            Assert.AreEqual(0L, _drip.Step(0.2f, 300, 1000));
            Assert.AreEqual(0L, _drip.Step(0.24f, 300, 1000), "Todavía dentro de la espera: cruzar el pad no gasta monedas.");
        }

        [Test]
        public void AfterTheDelay_ItPaysEverythingInTheTotalSeconds_AtASteadyPace()
        {
            _drip.Step(0f, 300, 1000);   // llega
            long total = Run(_drip, 0.25f + 1f / 60f, 0.25f + 1f, 1f / 60f, 300, 1000);

            Assert.That(total, Is.InRange(295L, 300L), "Casi todo el costo en 1 s después de la espera (las monedas son enteras: sobra menos de una).");
            total += Run(_drip, 0.25f + 1f + 1f / 60f, 0.25f + 1.2f, 1f / 60f, 300 - total, 1000 - total);
            Assert.AreEqual(300L, total, "Y termina de pagar justo el costo, ni más ni menos.");
        }

        [Test]
        public void ThePaceDoesNotDependOnTheCost_ThePaymentAlwaysTakesTheSameTime()
        {
            foreach (long cost in new long[] { 50, 330, 5000 })
            {
                var drip = new PurchaseDrip(1.2f, 0.25f);
                drip.Step(0f, cost, 1_000_000);

                long paidAtHalf = Run(drip, 0.25f + 1f / 60f, 0.25f + 0.6f, 1f / 60f, cost, 1_000_000);
                Assert.That(paidAtHalf, Is.InRange(cost * 0.45, cost * 0.55), "A la mitad del tiempo va por la mitad (costo " + cost + ").");
            }
        }

        [Test]
        public void ItNeverOffersMoreThanWhatIsMissing()
        {
            _drip.Step(0f, 40, 1000);
            long total = Run(_drip, 0.25f, 3f, 1f / 60f, 40, 1000);

            Assert.AreEqual(40L, total);
        }

        [Test]
        public void ItNeverOffersMoreThanTheBalance_AndDoesNotOweTheRest()
        {
            _drip.Step(0f, 300, 100);
            long total = Run(_drip, 0.25f, 3f, 1f / 60f, 300, 100);

            Assert.AreEqual(100L, total, "Gotea todo lo que tiene.");

            // Con saldo 0 no ofrece nada ni junta deuda: si vuelve a haber saldo, sigue al ritmo de siempre.
            Assert.AreEqual(0L, _drip.Step(4f, 200, 0));
            Assert.AreEqual(0L, _drip.Step(10f, 200, 0));
            long next = _drip.Step(10f + 1f / 60f, 200, 1000);
            Assert.LessOrEqual(next, 5L, "No cae de golpe lo que no se pudo pagar mientras no había saldo.");
        }

        [Test]
        public void WithNothingLeftToPay_ItOffersNothing()
        {
            _drip.Step(0f, 0, 1000);

            Assert.AreEqual(0L, _drip.Step(1f, 0, 1000));
            Assert.AreEqual(0L, _drip.Step(2f, -5, 1000));
        }

        [Test]
        public void AnotherStepInTheSameInstant_DoesNotAddTime()
        {
            _drip.Step(0f, 300, 1000);
            _drip.Step(0.5f, 300, 1000);

            Assert.AreEqual(0L, _drip.Step(0.5f, 300, 1000), "Dos llamadas en el mismo instante: la segunda no suma tiempo.");
        }

        [Test]
        public void Reset_CutsTheVisit_AndTheNextOneWaitsAgain()
        {
            _drip.Step(0f, 300, 1000);
            Run(_drip, 0.25f, 0.6f, 1f / 60f, 300, 1000);   // empezó a pagar

            _drip.Reset();
            Assert.IsFalse(_drip.IsActive);

            Assert.AreEqual(0L, _drip.Step(5f, 300, 1000), "Vuelve a llegar.");
            Assert.AreEqual(0L, _drip.Step(5.2f, 300, 1000), "Y vuelve a esperar.");
            Assert.Greater(Run(_drip, 5.25f + 1f / 60f, 5.5f, 1f / 60f, 300, 1000), 0L, "Pasada la espera, vuelve a pagar.");
        }

        [Test]
        public void ThePaceIsFixedWhenItStartsPaying_NotRecomputedWithWhatIsLeft()
        {
            // Si el ritmo bajara con lo que falta (cada vez pagaría una fracción de lo restante), nunca terminaría de pagar.
            _drip.Step(0f, 1000, 100000);
            long paidInHalfASecond = Run(_drip, 0.25f + 1f / 60f, 0.75f, 1f / 60f, 1000, 100000);
            long paidInTheNextHalf = Run(_drip, 0.75f + 1f / 60f, 1.25f, 1f / 60f, 1000 - paidInHalfASecond, 100000 - paidInHalfASecond);

            Assert.That(paidInHalfASecond + paidInTheNextHalf, Is.InRange(990L, 1000L), "En total, el costo en 1 s.");
            Assert.AreEqual(paidInHalfASecond, paidInTheNextHalf, 40L, "Las dos mitades pagan casi lo mismo.");
        }

        [Test]
        public void TheConstructor_ClampsNonsenseValues()
        {
            var drip = new PurchaseDrip(0f, -3f);

            Assert.Greater(drip.TotalSeconds, 0f);
            Assert.AreEqual(0f, drip.StartDelaySeconds);
        }
    }
}
