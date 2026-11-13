using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace FarmIdle.Tests
{
    public class PlotCensusTests
    {
        private GameObject _gameObject;
        private PlotCensus _census;
        private int _changed;

        [SetUp]
        public void SetUp()
        {
            _gameObject = new GameObject("PlotCensus (test)");
            _census = _gameObject.AddComponent<PlotCensus>();
            _changed = 0;
            _census.Changed += () => _changed++;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_gameObject);
        }

        [Test]
        public void StartsAtZero()
        {
            Assert.AreEqual(0, _census.Empty);
            Assert.AreEqual(0, _census.Growing);
            Assert.AreEqual(0, _census.Ready);
            Assert.AreEqual(0, _census.Total);
        }

        [Test]
        public void Register_CountsEachPlotInItsState_AndRaisesChanged()
        {
            _census.Register(PlotState.Empty);
            _census.Register(PlotState.Empty);
            _census.Register(PlotState.Growing);
            _census.Register(PlotState.Ready);

            Assert.AreEqual(2, _census.Empty);
            Assert.AreEqual(1, _census.Growing);
            Assert.AreEqual(1, _census.Ready);
            Assert.AreEqual(4, _census.Total);
            Assert.AreEqual(4, _changed);
        }

        [Test]
        public void Move_ShiftsOnePlotBetweenStates_AndKeepsTheTotal()
        {
            _census.Register(PlotState.Empty);
            _census.Register(PlotState.Empty);
            _changed = 0;

            _census.Move(PlotState.Empty, PlotState.Growing);
            Assert.AreEqual(1, _census.Empty);
            Assert.AreEqual(1, _census.Growing);

            _census.Move(PlotState.Growing, PlotState.Ready);
            Assert.AreEqual(1, _census.Empty);
            Assert.AreEqual(0, _census.Growing);
            Assert.AreEqual(1, _census.Ready);
            Assert.AreEqual(2, _census.Total);
            Assert.AreEqual(2, _changed);
        }

        [Test]
        public void Move_ToTheSameState_DoesNothing()
        {
            _census.Register(PlotState.Growing);
            _changed = 0;

            _census.Move(PlotState.Growing, PlotState.Growing);

            Assert.AreEqual(1, _census.Growing);
            Assert.AreEqual(0, _changed);
        }

        [Test]
        public void Unregister_RemovesThePlotFromItsState()
        {
            _census.Register(PlotState.Ready);
            _census.Register(PlotState.Empty);

            _census.Unregister(PlotState.Ready);

            Assert.AreEqual(0, _census.Ready);
            Assert.AreEqual(1, _census.Empty);
            Assert.AreEqual(1, _census.Total);
        }

        [Test]
        public void Unregister_OfAPlotThatWasNeverRegistered_WarnsAndNeverGoesNegative()
        {
            LogAssert.Expect(LogType.Warning, new Regex("negativo"));

            _census.Unregister(PlotState.Growing);

            Assert.AreEqual(0, _census.Growing);
        }

        [Test]
        public void Initialize_PublishesTheInstance()
        {
            _census.Initialize();

            Assert.AreSame(_census, PlotCensus.Instance);
        }
    }
}
