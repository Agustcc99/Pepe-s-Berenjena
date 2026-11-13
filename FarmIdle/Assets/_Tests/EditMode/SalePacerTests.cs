using NUnit.Framework;

namespace FarmIdle.Tests
{
    public class SalePacerTests
    {
        private const float Interval = 0.12f;

        [Test]
        public void TheFirstSale_IsReadyAtOnce()
        {
            var pacer = new SalePacer(Interval);

            Assert.IsTrue(pacer.IsReady(0f));
            Assert.IsTrue(pacer.IsReady(37.5f));
        }

        [Test]
        public void AfterASale_TheNextOneWaitsTheInterval()
        {
            var pacer = new SalePacer(Interval);

            pacer.Commit(10f);

            Assert.IsFalse(pacer.IsReady(10f));
            Assert.IsFalse(pacer.IsReady(10.119f));
            Assert.IsTrue(pacer.IsReady(10.1201f));
            Assert.IsTrue(pacer.IsReady(10.5f));
        }

        [Test]
        public void AskingWithoutCommitting_DoesNotConsumeTheTurn()
        {
            var pacer = new SalePacer(Interval);

            for (int i = 0; i < 100; i++) Assert.IsTrue(pacer.IsReady(5f + i * 0.01f), "Mochila vacía: se pregunta pero no se anota.");
        }

        [Test]
        public void TheRhythmDoesNotDriftWithFrameJitter()
        {
            var pacer = new SalePacer(Interval);

            // Se vende en el primer frame que llega (frames de 50 ms): la hora de cada venta es la del frame, pero el
            // ritmo promedio sigue siendo el del intervalo.
            float now = 0f;
            int sold = 0;
            float firstSale = -1f;
            float lastSale = 0f;
            while (now < 10f)
            {
                if (pacer.IsReady(now))
                {
                    pacer.Commit(now);
                    sold++;
                    if (firstSale < 0f) firstSale = now;
                    lastSale = now;
                }

                now += 0.05f;
            }

            // En 10 s a 0,12 s por venta caben ~83 ventas: con un retraso acumulado por frame serían menos.
            Assert.GreaterOrEqual(sold, 82, "El ritmo no tiene que perder tiempo por el redondeo de los frames.");
            Assert.LessOrEqual(sold, 84);
            Assert.GreaterOrEqual(lastSale - firstSale, (sold - 1) * Interval - 1e-3f, "Nunca más rápido que el intervalo.");
        }

        [Test]
        public void AfterALongPause_TheRhythmRestartsFromNow_WithoutABurst()
        {
            var pacer = new SalePacer(Interval);
            pacer.Commit(1f);

            // Pasan 20 s sin vender (el jugador se fue y volvió): la primera venta es al instante...
            Assert.IsTrue(pacer.IsReady(21f));
            pacer.Commit(21f);

            // ...y la siguiente espera el intervalo, sin "ráfaga" para recuperar lo atrasado.
            Assert.IsFalse(pacer.IsReady(21.05f));
            Assert.IsFalse(pacer.IsReady(21.119f));
            Assert.IsTrue(pacer.IsReady(21.1201f));
        }

        [Test]
        public void Reset_MakesTheNextSaleImmediate()
        {
            var pacer = new SalePacer(Interval);
            pacer.Commit(5f);
            Assert.IsFalse(pacer.IsReady(5.01f));

            pacer.Reset();

            Assert.IsTrue(pacer.IsReady(5.01f));
        }

        [Test]
        public void ANonPositiveInterval_MeansNoLimit()
        {
            var pacer = new SalePacer(0f);

            pacer.Commit(3f);
            Assert.IsTrue(pacer.IsReady(3f));
            Assert.AreEqual(0f, pacer.Interval);
        }
    }
}
