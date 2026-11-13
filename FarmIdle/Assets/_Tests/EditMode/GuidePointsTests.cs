using NUnit.Framework;
using UnityEngine;

namespace FarmIdle.Tests
{
    // La elección del lugar al que apunta la flecha de "conviene ir": prioridad primero, cercanía después, y solo los que quieren.
    public sealed class GuidePointsTests
    {
        private sealed class FakePoint : IGuidePoint
        {
            public Vector3 Position { get; set; }
            public int GuidePriority { get; set; }
            public bool WantsGuide { get; set; }
        }

        [SetUp]
        public void SetUp()
        {
            GuidePoints.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            GuidePoints.Clear();
        }

        [Test]
        public void Best_IsNull_WhenNobodyWantsGuide()
        {
            GuidePoints.Register(new FakePoint { GuidePriority = GuidePoints.BuyPriority, WantsGuide = false });

            Assert.IsNull(GuidePoints.Best(Vector3.zero));
        }

        [Test]
        public void Best_PicksTheNearest_AmongEqualPriority()
        {
            var far = new FakePoint { Position = new Vector3(30f, 0f, 0f), GuidePriority = GuidePoints.BuyPriority, WantsGuide = true };
            var near = new FakePoint { Position = new Vector3(0f, 0f, 4f), GuidePriority = GuidePoints.BuyPriority, WantsGuide = true };
            GuidePoints.Register(far);
            GuidePoints.Register(near);

            Assert.AreSame(near, GuidePoints.Best(Vector3.zero));
            Assert.AreSame(far, GuidePoints.Best(new Vector3(28f, 0f, 0f)));
        }

        [Test]
        public void Best_PrefersSelling_OverANearerPurchase()
        {
            var buy = new FakePoint { Position = new Vector3(1f, 0f, 0f), GuidePriority = GuidePoints.BuyPriority, WantsGuide = true };
            var sell = new FakePoint { Position = new Vector3(40f, 0f, 0f), GuidePriority = GuidePoints.SellPriority, WantsGuide = true };
            GuidePoints.Register(buy);
            GuidePoints.Register(sell);

            Assert.AreSame(sell, GuidePoints.Best(Vector3.zero));

            sell.WantsGuide = false;
            Assert.AreSame(buy, GuidePoints.Best(Vector3.zero));
        }

        [Test]
        public void Register_IgnoresDuplicates_AndUnregisterRemoves()
        {
            var point = new FakePoint { WantsGuide = true };
            GuidePoints.Register(point);
            GuidePoints.Register(point);
            Assert.AreEqual(1, GuidePoints.Count);

            GuidePoints.Unregister(point);
            Assert.AreEqual(0, GuidePoints.Count);
            Assert.IsNull(GuidePoints.Best(Vector3.zero));
        }
    }
}
