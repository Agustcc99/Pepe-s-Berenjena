using NUnit.Framework;
using UnityEngine;

namespace FarmIdle.Tests
{
    // J-03: la curva de rebote (squash & stretch) es matemática pura.
    public class SquashStretchTests
    {
        [Test]
        public void StartsAndEndsAtFullSize()
        {
            Assert.AreEqual(Vector3.one, SquashStretch.Evaluate(0f, 0.3f));
            Assert.AreEqual(Vector3.one, SquashStretch.Evaluate(1f, 0.3f));
            Assert.AreEqual(Vector3.one, SquashStretch.Evaluate(5f, 0.3f));
        }

        [Test]
        public void Deforms_StretchingOneAxisAndSquashingTheOther_AndFadesOut()
        {
            float maxEarly = 0f;
            float maxLate = 0f;
            bool stretched = false;
            bool squashed = false;

            for (int i = 1; i < 100; i++)
            {
                float t = i / 100f;
                Vector3 s = SquashStretch.Evaluate(t, 0.3f);
                Assert.Greater(s.y, 0f);
                Assert.AreEqual(s.x, s.z, 1e-6f);
                Assert.IsTrue((s.y - 1f) * (s.x - 1f) <= 0f, "Si estira en Y, achata en X/Z (y al revés).");
                if (s.y > 1.01f) stretched = true;
                if (s.y < 0.99f) squashed = true;

                float d = Mathf.Abs(s.y - 1f);
                if (t < 0.5f) maxEarly = Mathf.Max(maxEarly, d); else maxLate = Mathf.Max(maxLate, d);
            }

            Assert.IsTrue(stretched && squashed, "Rebota para los dos lados.");
            Assert.Greater(maxEarly, maxLate, "Se amortigua.");
        }
    }
}
