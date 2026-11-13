using System.Collections;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;

namespace FarmIdle.Tests
{
    // J-01: el "+1" que sube al cosechar. Campo de 2 parcelas, un actor con mochila y un HarvestFeedback con su pool propio.
    public class HarvestFeedbackTests
    {
        private const float GrowSeconds = 0.6f;
        private static readonly Vector3 FarAway = new Vector3(-50f, 1f, 0f);

        private FieldTestWorld _world;
        private FakeFarmer _farmer;
        private GameObject _template;
        private GameObject _feedbackObject;
        private HarvestFeedback _feedback;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _world = FieldTestWorld.Create(2, GrowSeconds);
            _farmer = FakeFarmer.Create(FarAway, 5);

            _template = new GameObject("PopupTemplate", typeof(TextMeshPro));
            _template.SetActive(false);

            _feedbackObject = new GameObject("HarvestFeedback");
            _feedbackObject.SetActive(false);
            _feedback = _feedbackObject.AddComponent<HarvestFeedback>();
            TestReflection.Set(_feedback, "popupPrefab", _template);
            TestReflection.Set(_feedback, "prewarm", 2);
            TestReflection.Set(_feedback, "seconds", 0.2f);
            _feedbackObject.SetActive(true);
            yield return null;
        }

        [TearDown]
        public void TearDown()
        {
            if (_feedbackObject != null) Object.DestroyImmediate(_feedbackObject);
            if (_template != null) Object.DestroyImmediate(_template);
            if (_farmer != null) Object.DestroyImmediate(_farmer.gameObject);
            _world.Destroy();
        }

        private void Harvest(PlotView plot)
        {
            plot.Interact(_farmer);              // siembra
            plot.Tick(GrowSeconds + 0.1f);       // crece
            plot.Interact(_farmer);              // cosecha
        }

        [UnityTest]
        public IEnumerator Harvesting_ShowsAPopup_AndItReturnsToThePool()
        {
            Harvest(_world.Plots[0]);
            Assert.AreEqual(1, _feedback.ActivePopups);

            yield return new WaitForSecondsRealtime(0.4f);

            Assert.AreEqual(0, _feedback.ActivePopups);
            Assert.IsTrue(_world.Pools.TryGetStats(_template, out PoolStats stats));
            Assert.AreEqual(0, stats.Active);
            Assert.AreEqual(0, stats.Misses, "El Prewarm alcanzó: no debería haber instancias nuevas durante el juego.");
        }

        [UnityTest]
        public IEnumerator MoreHarvestsThanSlots_DoNotCreateInstances_NorBreakTheHarvest()
        {
            PlotView plot = _world.Plots[0];
            for (int i = 0; i < 4; i++) Harvest(plot);   // 4 cosechas seguidas con 2 lugares

            Assert.AreEqual(2, _feedback.ActivePopups);
            Assert.IsTrue(_world.Pools.TryGetStats(_template, out PoolStats stats));
            Assert.AreEqual(0, stats.Misses);

            yield return null;
        }

        [UnityTest]
        public IEnumerator DisablingTheFeedback_ReturnsEverythingToThePool()
        {
            Harvest(_world.Plots[0]);
            _feedbackObject.SetActive(false);

            Assert.IsTrue(_world.Pools.TryGetStats(_template, out PoolStats stats));
            Assert.AreEqual(0, stats.Active);

            // Con el feedback apagado, cosechar no falla.
            Harvest(_world.Plots[1]);
            yield return null;
        }
    }
}
