using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace FarmIdle.Tests
{
    // J-02: el estallido de trocitos al cosechar. Campo de 2 parcelas, un actor con mochila y un BurstFeedback con su pool propio.
    public class BurstFeedbackTests
    {
        private const float GrowSeconds = 0.6f;
        private static readonly Vector3 FarAway = new Vector3(-50f, 1f, 0f);

        private FieldTestWorld _world;
        private FakeFarmer _farmer;
        private GameObject _leaf;
        private GameObject _gold;
        private GameObject _burstObject;
        private BurstFeedback _burst;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _world = FieldTestWorld.Create(2, GrowSeconds);
            _farmer = FakeFarmer.Create(FarAway, 5);

            _leaf = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _leaf.SetActive(false);
            _gold = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _gold.SetActive(false);

            _burstObject = new GameObject("BurstFeedback");
            _burstObject.SetActive(false);
            _burst = _burstObject.AddComponent<BurstFeedback>();
            TestReflection.Set(_burst, "harvestChip", _leaf);
            TestReflection.Set(_burst, "purchaseChip", _gold);
            TestReflection.Set(_burst, "prewarm", 6);
            TestReflection.Set(_burst, "harvestPieces", 4);
            TestReflection.Set(_burst, "seconds", 0.2f);
            _burstObject.SetActive(true);
            yield return null;
        }

        [TearDown]
        public void TearDown()
        {
            if (_burstObject != null) Object.DestroyImmediate(_burstObject);
            if (_leaf != null) Object.DestroyImmediate(_leaf);
            if (_gold != null) Object.DestroyImmediate(_gold);
            if (_farmer != null) Object.DestroyImmediate(_farmer.gameObject);
            _world.Destroy();
        }

        private void Harvest(PlotView plot)
        {
            plot.Interact(_farmer);
            plot.Tick(GrowSeconds + 0.1f);
            plot.Interact(_farmer);
        }

        [UnityTest]
        public IEnumerator Harvesting_ShowsChips_AndTheyReturnToThePool()
        {
            Harvest(_world.Plots[0]);
            Assert.AreEqual(4, _burst.ActiveChips);

            yield return new WaitForSecondsRealtime(0.4f);

            Assert.AreEqual(0, _burst.ActiveChips);
            Assert.IsTrue(_world.Pools.TryGetStats(_leaf, out PoolStats stats));
            Assert.AreEqual(0, stats.Active);
            Assert.AreEqual(0, stats.Misses, "El Prewarm alcanzó: no debería haber instancias nuevas durante el juego.");
        }

        [UnityTest]
        public IEnumerator MoreBurstsThanSlots_DoNotCreateInstances()
        {
            PlotView plot = _world.Plots[0];
            for (int i = 0; i < 3; i++) Harvest(plot);   // 3 x 4 trocitos con 6 lugares

            Assert.AreEqual(6, _burst.ActiveChips);
            Assert.IsTrue(_world.Pools.TryGetStats(_leaf, out PoolStats stats));
            Assert.AreEqual(0, stats.Misses);

            yield return null;
        }

        [UnityTest]
        public IEnumerator DisablingTheBurst_ReturnsEverythingToThePool()
        {
            Harvest(_world.Plots[0]);
            _burstObject.SetActive(false);

            Assert.IsTrue(_world.Pools.TryGetStats(_leaf, out PoolStats stats));
            Assert.AreEqual(0, stats.Active);

            Harvest(_world.Plots[1]);   // apagado, cosechar no falla
            yield return null;
        }
    }
}
