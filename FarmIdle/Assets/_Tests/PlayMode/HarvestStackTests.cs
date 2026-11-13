using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace FarmIdle.Tests
{
    // La cadena completa de la Fase 4 con un mundo armado por código: parcelas reales con física, el PlayerActor
    // (el mismo que usa el jugador), el BackpackManager y la pila visible. 3 parcelas de 3 cultivos distintos (cada
    // uno con su producto) y una mochila de 2, para llenarla enseguida.
    public class HarvestStackTests
    {
        private const int PlotCount = 3;
        private const float GrowSeconds = 0.6f;
        private const int Capacity = 2;
        private static readonly Vector3 FarAway = new Vector3(-50f, 1f, 0f);

        private FieldTestWorld _world;
        private BackpackRig _rig;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _world = FieldTestWorld.Create(PlotCount, GrowSeconds);
            _rig = BackpackRig.AddTo(_world, FarAway, Capacity, PlotCount);

            // Cada parcela con su cultivo (y cada cultivo con su producto); las tres comparten la planta molde.
            for (int i = 0; i < PlotCount; i++)
            {
                TestReflection.Set(_world.Plots[i], "crop", _rig.Crops[i]);
            }

            yield return null;   // el Start() del campo hace el Prewarm de plantas
        }

        [TearDown]
        public void TearDown()
        {
            _rig.Destroy();
            _world.Destroy();
            Time.timeScale = 1f;
        }

        private void MakeReady(PlotView plot)
        {
            plot.Interact(_rig.Actor);
            plot.Tick(GrowSeconds + 0.1f);
            Assert.AreEqual(PlotState.Ready, plot.State);
        }

        private void MoveFarmerOnto(int plotIndex)
        {
            _rig.Farmer.transform.position = _world.Plots[plotIndex].transform.position + Vector3.up;
        }

        private static IEnumerator PhysicsSteps()
        {
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            yield return null;
        }

        private static IEnumerator WaitFor(Func<bool> condition, float timeoutSeconds = 3f)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
        }

        private static IEnumerator Settle()
        {
            yield return new WaitForSecondsRealtime(0.7f);
        }

        [UnityTest]
        public IEnumerator SteppingOnReadyPlots_PilesTheProductsOnTheBack_FlyingFromEachPlot_UntilTheBackpackIsFull()
        {
            foreach (PlotView plot in _world.Plots) MakeReady(plot);
            Assert.AreEqual(PlotCount, _world.Census.Ready);

            // Dónde está cada producto justo cuando entra (el evento llega con la pila ya actualizada).
            var arrivals = new List<(Vector3 from, Vector3 itemPosition)>();
            _rig.Actor.ProductReceived += (crop, from) =>
                arrivals.Add((from, _rig.View.ItemObject(_rig.View.ItemCount - 1).transform.position));

            MoveFarmerOnto(0);
            yield return WaitFor(() => _rig.Backpack.Count == 1);

            Assert.AreEqual(1, arrivals.Count);
            Vector3 plotPoint = _world.Plots[0].transform.position + new Vector3(0f, 0.12f + 0.35f, 0f);
            Assert.AreEqual(0f, Vector3.Distance(plotPoint, arrivals[0].from), 0.01f, "El producto sale de la parcela cosechada.");
            Assert.AreEqual(0f, Vector3.Distance(arrivals[0].from, arrivals[0].itemPosition), 0.01f,
                "Aparece donde estaba la planta, no directo en la espalda.");

            MoveFarmerOnto(1);
            yield return WaitFor(() => _rig.Backpack.Count == 2);

            CollectionAssert.AreEqual(new[] { _rig.Crops[0], _rig.Crops[1] }, _rig.Backpack.Items);
            Assert.IsTrue(_rig.Backpack.IsFull);
            Assert.AreEqual(2, _rig.View.ItemCount);

            // La tercera parcela está lista, pero con la mochila llena no se cosecha.
            MoveFarmerOnto(2);
            yield return PhysicsSteps();
            yield return new WaitForSeconds(0.2f);
            Assert.AreEqual(PlotState.Ready, _world.Plots[2].State);
            Assert.AreEqual(2, _rig.Backpack.Count);

            yield return Settle();
            Assert.AreEqual(_rig.ProductTemplates[0].name, _rig.View.ItemObject(0).name);
            Assert.AreEqual(_rig.ProductTemplates[1].name, _rig.View.ItemObject(1).name, "Arriba queda lo último que se cosechó.");

            // Sale primero el de arriba... y como el jugador sigue sobre la parcela lista, apenas hay lugar cosecha sola.
            Assert.IsTrue(_rig.Manager.TryPop(out CropData sold));
            Assert.AreSame(_rig.Crops[1], sold);
            Assert.AreEqual(1, _rig.View.ItemCount);

            yield return WaitFor(() => _rig.Backpack.Count == 2);
            CollectionAssert.AreEqual(new[] { _rig.Crops[0], _rig.Crops[2] }, _rig.Backpack.Items);
            Assert.AreNotEqual(PlotState.Ready, _world.Plots[2].State, "La parcela se vació (y se resiembra sola).");

            yield return Settle();
            Assert.AreEqual(_rig.ProductTemplates[2].name, _rig.View.ItemObject(1).name);
        }

        [UnityTest]
        public IEnumerator WhilePausedOrShowingAnAd_StandingOnAReadyPlotDoesNotHarvest_UntilThePlayerCanAct()
        {
            // Mochila llena y jugador parado sobre una parcela lista: no cosecha.
            Assert.IsTrue(_rig.Actor.TryAccept(_rig.Crops[1], FarAway));
            Assert.IsTrue(_rig.Actor.TryAccept(_rig.Crops[2], FarAway));
            MakeReady(_world.Plots[0]);
            MoveFarmerOnto(0);
            yield return PhysicsSteps();
            yield return new WaitForSeconds(0.1f);
            Assert.AreEqual(PlotState.Ready, _world.Plots[0].State, "Llena: no cosecha.");

            // En pausa (y con un anuncio en pantalla) aparece lugar, pero el jugador no puede actuar.
            // La física también está congelada: solo se espera tiempo real, no pasos de física.
            Assert.IsTrue(_world.Game.Pause());
            Assert.IsTrue(_rig.Manager.TryPop(out _));
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.AreEqual(1, _rig.Backpack.Count);
            Assert.AreEqual(PlotState.Ready, _world.Plots[0].State, "En pausa no cosecha.");

            Assert.IsTrue(_world.Game.Resume());
            yield return WaitFor(() => _rig.Backpack.Count == 2);
            Assert.AreEqual(2, _rig.Backpack.Count, "Al reanudar cosecha sin tener que irse y volver.");

            // Lo mismo con un anuncio en pantalla.
            MakeReady(_world.Plots[0]);
            Assert.IsTrue(_world.Game.BeginAd());
            Assert.IsTrue(_rig.Manager.TryPop(out _));
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.AreEqual(1, _rig.Backpack.Count);
            Assert.AreEqual(PlotState.Ready, _world.Plots[0].State, "Con un anuncio no cosecha.");

            Assert.IsTrue(_world.Game.EndAd());
            yield return WaitFor(() => _rig.Backpack.Count == 2);
            Assert.AreEqual(2, _rig.Backpack.Count);
        }

        [UnityTest]
        public IEnumerator HarvestCycles_NeverInstantiatePlantsNorProducts()
        {
            int awakes = AwakeCounter.Awakes;

            for (int round = 0; round < 6; round++)
            {
                foreach (PlotView plot in _world.Plots)
                {
                    MakeReady(plot);                    // siembra (planta del pool) y la deja lista
                    plot.Interact(_rig.Actor);          // cosecha: el producto sale del pool y vuela a la pila
                    Assert.AreEqual(PlotState.Empty, plot.State);
                    _rig.Manager.TryPop(out _);         // vacía la mochila (por ejemplo, la venta): el producto vuelve al pool
                }
            }

            Assert.AreEqual(awakes, AwakeCounter.Awakes, "Hubo Instantiate durante el juego.");

            Assert.IsTrue(_world.Pools.TryGetStats(_world.PlantTemplate, out PoolStats plants));
            Assert.AreEqual(PlotCount, plants.Created);
            Assert.AreEqual(0, plants.Misses);

            foreach (GameObject template in _rig.ProductTemplates)
            {
                Assert.IsTrue(_world.Pools.TryGetStats(template, out PoolStats products));
                Assert.AreEqual(_rig.ProductPrewarm, products.Created, "Se crearon productos fuera del Prewarm: " + template.name);
                Assert.AreEqual(0, products.Misses);
                Assert.AreEqual(0, products.Active);
            }

            yield break;
        }
    }
}
