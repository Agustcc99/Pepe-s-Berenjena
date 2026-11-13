using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace FarmIdle.Tests
{
    // La lógica de la parcela es C# puro: se prueba sin escena, sin física y sin esperar tiempo real
    // (el tiempo se simula pasando deltas a Tick).
    public class PlotLogicTests
    {
        private PlotLogic _plot;
        private List<(PlotState previous, PlotState current)> _changes;

        [SetUp]
        public void SetUp()
        {
            _plot = new PlotLogic();
            _changes = new List<(PlotState previous, PlotState current)>();
            _plot.StateChanged += (previous, current) => _changes.Add((previous, current));
        }

        [Test]
        public void NewPlot_IsEmpty_WithoutProgress()
        {
            Assert.AreEqual(PlotState.Empty, _plot.State);
            Assert.AreEqual(0f, _plot.Progress);
            Assert.AreEqual(0f, _plot.GrowSeconds);
            Assert.AreEqual(0, _changes.Count);
        }

        [Test]
        public void TryPlant_OnAnEmptyPlot_StartsGrowing_AndRaisesTheEventOnce()
        {
            Assert.IsTrue(_plot.TryPlant(6f));

            Assert.AreEqual(PlotState.Growing, _plot.State);
            Assert.AreEqual(6f, _plot.GrowSeconds);
            Assert.AreEqual(0f, _plot.Elapsed);
            Assert.AreEqual(0f, _plot.Progress);
            Assert.AreEqual(1, _changes.Count);
            Assert.AreEqual((PlotState.Empty, PlotState.Growing), _changes[0]);
        }

        [Test]
        public void TryPlant_OnAPlotThatIsNotEmpty_ReturnsFalse_AndKeepsTheTimer()
        {
            _plot.TryPlant(10f);
            _plot.Tick(4f);

            Assert.IsFalse(_plot.TryPlant(2f), "No se puede sembrar encima de lo que ya crece.");
            Assert.AreEqual(10f, _plot.GrowSeconds);
            Assert.AreEqual(4f, _plot.Elapsed, 1e-5f);
            Assert.AreEqual(1, _changes.Count, "Un intento rechazado no cambia el estado.");

            _plot.Tick(6f);
            Assert.AreEqual(PlotState.Ready, _plot.State);
            Assert.IsFalse(_plot.TryPlant(2f), "Tampoco sobre una parcela lista.");
            Assert.AreEqual(PlotState.Ready, _plot.State);
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void TryPlant_WithAnInvalidDuration_Throws_AndLeavesThePlotEmpty(float seconds)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _plot.TryPlant(seconds));

            Assert.AreEqual(PlotState.Empty, _plot.State);
            Assert.AreEqual(0, _changes.Count);
        }

        [Test]
        public void Tick_AdvancesTheProgress_InProportionToTheElapsedTime()
        {
            _plot.TryPlant(8f);

            _plot.Tick(2f);
            Assert.AreEqual(0.25f, _plot.Progress, 1e-5f);

            _plot.Tick(2f);
            Assert.AreEqual(0.5f, _plot.Progress, 1e-5f);
            Assert.AreEqual(PlotState.Growing, _plot.State);
            Assert.AreEqual(1, _changes.Count, "Mientras crece no hay más eventos.");
        }

        [Test]
        public void Tick_ReachingGrowSeconds_MakesThePlotReady_AndRaisesTheEventOnce()
        {
            _plot.TryPlant(3f);

            _plot.Tick(1f);
            _plot.Tick(1f);
            Assert.AreEqual(PlotState.Growing, _plot.State);

            _plot.Tick(1f);

            Assert.AreEqual(PlotState.Ready, _plot.State);
            Assert.AreEqual(1f, _plot.Progress);
            Assert.AreEqual(2, _changes.Count);
            Assert.AreEqual((PlotState.Growing, PlotState.Ready), _changes[1]);
        }

        [Test]
        public void Tick_WithAStepBiggerThanWhatIsLeft_ClampsToReady()
        {
            _plot.TryPlant(2f);

            _plot.Tick(50f);   // por ejemplo un tirón de frames largos

            Assert.AreEqual(PlotState.Ready, _plot.State);
            Assert.AreEqual(2f, _plot.Elapsed, "El tiempo no se pasa de GrowSeconds.");
            Assert.AreEqual(1f, _plot.Progress);
        }

        [Test]
        public void Tick_OnAnEmptyOrReadyPlot_DoesNothing()
        {
            _plot.Tick(5f);
            Assert.AreEqual(PlotState.Empty, _plot.State);
            Assert.AreEqual(0f, _plot.Elapsed);

            _plot.TryPlant(1f);
            _plot.Tick(1f);
            int changes = _changes.Count;

            _plot.Tick(5f);

            Assert.AreEqual(PlotState.Ready, _plot.State);
            Assert.AreEqual(1f, _plot.Elapsed);
            Assert.AreEqual(changes, _changes.Count);
        }

        [TestCase(0f)]
        [TestCase(-0.5f)]
        [TestCase(float.NaN)]
        public void Tick_IgnoresStepsThatAreZeroNegativeOrNaN(float delta)
        {
            // El tiempo del juego vale 0 en pausa y con un anuncio: la parcela no tiene que moverse.
            _plot.TryPlant(4f);
            _plot.Tick(1f);

            _plot.Tick(delta);

            Assert.AreEqual(1f, _plot.Elapsed, 1e-5f);
            Assert.AreEqual(PlotState.Growing, _plot.State);
        }

        [Test]
        public void TryHarvest_OnlyWorksWhenReady_AndEmptiesThePlot()
        {
            Assert.IsFalse(_plot.TryHarvest(), "Vacía: no hay nada que cosechar.");

            _plot.TryPlant(2f);
            _plot.Tick(1f);
            Assert.IsFalse(_plot.TryHarvest(), "Creciendo: todavía no.");
            Assert.AreEqual(PlotState.Growing, _plot.State);

            _plot.Tick(1f);
            Assert.IsTrue(_plot.TryHarvest());

            Assert.AreEqual(PlotState.Empty, _plot.State);
            Assert.AreEqual(0f, _plot.Progress);
            Assert.AreEqual(0f, _plot.Elapsed);
            Assert.AreEqual((PlotState.Ready, PlotState.Empty), _changes[_changes.Count - 1]);
        }

        [Test]
        public void TheCycle_CanBeRepeated_WithADifferentDurationEachTime()
        {
            _plot.TryPlant(1f);
            _plot.Tick(1f);
            _plot.TryHarvest();

            Assert.IsTrue(_plot.TryPlant(5f));
            _plot.Tick(2.5f);

            Assert.AreEqual(PlotState.Growing, _plot.State);
            Assert.AreEqual(5f, _plot.GrowSeconds);
            Assert.AreEqual(0.5f, _plot.Progress, 1e-5f);
        }
    }
}
