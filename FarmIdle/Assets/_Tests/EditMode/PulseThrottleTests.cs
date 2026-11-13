using NUnit.Framework;

namespace FarmIdle.Tests
{
    // La regla de frecuencia de la vibración (U-06): sin zumbido continuo, pero un pulso más fuerte siempre pasa.
    public class PulseThrottleTests
    {
        [Test]
        public void TheFirstPulse_AlwaysPasses()
        {
            var throttle = new PulseThrottle(0.1);
            Assert.IsTrue(throttle.TryPass(0d, 50));
        }

        [Test]
        public void AWeakerOrEqualPulse_InsideTheInterval_IsDropped()
        {
            var throttle = new PulseThrottle(0.1);
            Assert.IsTrue(throttle.TryPass(1d, 100));

            Assert.IsFalse(throttle.TryPass(1.05d, 100), "Igual de fuerte y muy pegado.");
            Assert.IsFalse(throttle.TryPass(1.09d, 40), "Más suave y muy pegado.");
        }

        [Test]
        public void AStrongerPulse_InsideTheInterval_Passes()
        {
            var throttle = new PulseThrottle(0.1);
            Assert.IsTrue(throttle.TryPass(1d, 80));

            Assert.IsTrue(throttle.TryPass(1.02d, 230), "Subir de nivel justo después de vender se tiene que sentir.");
            Assert.IsFalse(throttle.TryPass(1.04d, 200), "Pero el siguiente, más suave que ese, no.");
        }

        [Test]
        public void AfterTheInterval_AnyPulsePasses()
        {
            var throttle = new PulseThrottle(0.1);
            Assert.IsTrue(throttle.TryPass(1d, 200));

            Assert.IsTrue(throttle.TryPass(1.1d, 20));
        }

        [Test]
        public void Reset_ForgetsThePreviousPulse()
        {
            var throttle = new PulseThrottle(0.1);
            throttle.TryPass(1d, 200);
            throttle.Reset();

            Assert.IsTrue(throttle.TryPass(1.01d, 10));
        }
    }
}
