using System.Collections;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;

namespace FarmIdle.Tests
{
    // J-04: la cosecha rara. Azar fijo (RareRoll) para que la prueba no dependa de la suerte.
    public class RareHarvestTests
    {
        private const float GrowSeconds = 0.6f;
        private const int SellPrice = 10;
        private static readonly Vector3 FarAway = new Vector3(-50f, 1f, 0f);

        private FieldTestWorld _world;
        private FakeFarmer _farmer;
        private GameConfig _config;
        private EconomyManager _economy;
        private GameObject _template;
        private GameObject _feedbackObject;
        private HarvestFeedback _feedback;
        private long _rareBonus;
        private int _rareEvents;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _world = FieldTestWorld.Create(1, GrowSeconds);
            _farmer = FakeFarmer.Create(FarAway, 5);
            TestReflection.Set(_world.Crop, "sellPrice", SellPrice);

            _config = ScriptableObject.CreateInstance<GameConfig>();
            TestReflection.Set(_config, "rareHarvestChance", 0.5f);
            TestReflection.Set(_config, "rareHarvestMultiplier", 5f);

            var economyObject = new GameObject("EconomyManager");
            economyObject.transform.SetParent(_world.Root.transform, false);
            _economy = economyObject.AddComponent<EconomyManager>();
            _economy.Initialize(_config);

            _template = new GameObject("PopupTemplate", typeof(TextMeshPro));
            _template.GetComponent<TextMeshPro>().text = "+1";
            _template.SetActive(false);

            _feedbackObject = new GameObject("HarvestFeedback");
            _feedbackObject.SetActive(false);
            _feedback = _feedbackObject.AddComponent<HarvestFeedback>();
            TestReflection.Set(_feedback, "popupPrefab", _template);
            TestReflection.Set(_feedback, "prewarm", 4);
            TestReflection.Set(_feedback, "seconds", 0.2f);
            _feedbackObject.SetActive(true);

            _rareBonus = 0L;
            _rareEvents = 0;
            PlotView.RareHarvested += OnRare;
            yield return null;
        }

        [TearDown]
        public void TearDown()
        {
            PlotView.RareHarvested -= OnRare;
            if (_feedbackObject != null) Object.DestroyImmediate(_feedbackObject);
            if (_template != null) Object.DestroyImmediate(_template);
            if (_farmer != null) Object.DestroyImmediate(_farmer.gameObject);
            _world.Destroy();
            if (_config != null) Object.DestroyImmediate(_config);
        }

        private void OnRare(CropData crop, Vector3 position, long bonus)
        {
            _rareEvents++;
            _rareBonus = bonus;
        }

        private void Harvest()
        {
            PlotView plot = _world.Plots[0];
            plot.Interact(_farmer);              // siembra
            plot.Tick(GrowSeconds + 0.1f);       // crece
            plot.Interact(_farmer);              // cosecha
        }

        [UnityTest]
        public IEnumerator ARareRoll_PaysFiveTimesTheSellPrice_AndAnnouncesIt()
        {
            _economy.RareRoll = () => 0.1f;   // menor que la chance: sale rara
            long before = _economy.Balance;

            Harvest();

            Assert.AreEqual(1, _rareEvents);
            Assert.AreEqual(SellPrice * 5L, _rareBonus);
            Assert.AreEqual(before + SellPrice * 5L, _economy.Balance);
            Assert.AreEqual(0, _economy.SoldUnits, "El premio no cuenta como venta.");
            Assert.AreEqual(2, _feedback.ActivePopups, "El '+1' normal y el texto dorado del premio.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator ANormalRoll_PaysNothingExtra_AndDoesNotAnnounce()
        {
            _economy.RareRoll = () => 0.9f;   // mayor que la chance: cosecha común
            long before = _economy.Balance;

            Harvest();

            Assert.AreEqual(0, _rareEvents);
            Assert.AreEqual(before, _economy.Balance);
            Assert.AreEqual(1, _feedback.ActivePopups);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ChanceZero_NeverGivesARareHarvest()
        {
            TestReflection.Set(_config, "rareHarvestChance", 0f);
            _economy.RareRoll = () => 0f;

            Harvest();

            Assert.AreEqual(0, _rareEvents);
            Assert.AreEqual(0L, _economy.TryRareHarvest(_world.Crop));
            yield return null;
        }

        [UnityTest]
        public IEnumerator PopupsGoBackToThePool_WithoutNewInstances()
        {
            _economy.RareRoll = () => 0.1f;
            Harvest();
            yield return new WaitForSecondsRealtime(0.4f);

            Assert.AreEqual(0, _feedback.ActivePopups);
            Assert.IsTrue(_world.Pools.TryGetStats(_template, out PoolStats stats));
            Assert.AreEqual(0, stats.Active);
            Assert.AreEqual(0, stats.Misses, "El Prewarm alcanzó: sin instancias nuevas durante el juego.");
        }

        [UnityTest]
        public IEnumerator ANormalPopup_AfterARareOne_GetsItsNormalTextBack()
        {
            _economy.RareRoll = () => 0.1f;
            Harvest();
            yield return new WaitForSecondsRealtime(0.4f);   // vuelven al pool con el texto dorado

            _economy.RareRoll = () => 0.9f;
            Harvest();

            var texts = _feedbackObject.activeInHierarchy ? Object.FindObjectsByType<TextMeshPro>(FindObjectsInactive.Exclude) : new TextMeshPro[0];
            foreach (TextMeshPro text in texts)
            {
                Assert.AreEqual("+1", text.text, "Ningún texto activo debe quedar con el premio de la cosecha anterior.");
            }
        }
    }
}
