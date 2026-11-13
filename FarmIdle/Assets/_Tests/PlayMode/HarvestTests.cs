using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace FarmIdle.Tests
{
    // La cosecha vista desde la parcela: campo de 4 parcelas con un cultivo de 0,6 s y un actor con mochila propia
    // (FakeFarmer), sin el jugador ni el BackpackManager. La pisada se prueba con física real (trigger de la parcela +
    // actor con Rigidbody); el resto llama a Interact directamente, que es lo que hace el InteractionTrigger.
    public class HarvestTests
    {
        private const int PlotCount = 4;
        private const float GrowSeconds = 0.6f;
        private const int Capacity = 3;
        private static readonly Vector3 FarAway = new Vector3(-50f, 1f, 0f);

        private FieldTestWorld _world;
        private FakeFarmer _farmer;
        private FakeActor _plainActor;   // un actor sin mochila, solo para una prueba; se destruye en TearDown
        private int _logs;
        private Application.LogCallback _logCounter;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _world = FieldTestWorld.Create(PlotCount, GrowSeconds);
            _farmer = FakeFarmer.Create(FarAway, Capacity);   // lejos de las parcelas: no pisa nada
            _logCounter = (message, stackTrace, type) => _logs++;
            yield return null;                                // el Start() del campo hace el Prewarm
        }

        [TearDown]
        public void TearDown()
        {
            Application.logMessageReceived -= _logCounter;
            if (_plainActor != null) UnityEngine.Object.DestroyImmediate(_plainActor.gameObject);
            if (_farmer != null) UnityEngine.Object.DestroyImmediate(_farmer.gameObject);
            _world.Destroy();
            Time.timeScale = 1f;
        }

        // Siembra la parcela y la deja lista sin esperar (lo mismo que hace el PlotField con el tiempo del juego).
        private void MakeReady(PlotView plot)
        {
            plot.Interact(_farmer);
            plot.Tick(GrowSeconds + 0.1f);
            Assert.AreEqual(PlotState.Ready, plot.State);
        }

        private void MoveFarmerOnto(int plotIndex)
        {
            _farmer.transform.position = _world.Plots[plotIndex].transform.position + Vector3.up;
        }

        private PoolStats PlantPool()
        {
            Assert.IsTrue(_world.Pools.TryGetStats(_world.PlantTemplate, out PoolStats stats), "No hay pool de plantas.");
            return stats;
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

        // Cuenta todo lo que llega a la consola mientras corre 'action'. Nada de try/finally en las pruebas iterador (ver
        // BackpackStackViewTests): si 'action' falla, TearDown deja de escuchar.
        private int CountLogs(Action action)
        {
            _logs = 0;
            Application.logMessageReceived += _logCounter;
            action();
            Application.logMessageReceived -= _logCounter;
            return _logs;
        }

        [UnityTest]
        public IEnumerator HarvestingAReadyPlot_IsInstantaneous_AndTheNextInteractSowsItAgain()
        {
            PlotView plot = _world.Plots[0];
            MakeReady(plot);
            GameObject plant = plot.Plant.gameObject;
            Assert.AreEqual(1, _world.Census.Ready);

            plot.Interact(_farmer);   // una sola llamada: cosecha

            Assert.AreEqual(PlotState.Empty, plot.State, "La parcela cosechada queda vacía en el mismo instante.");
            Assert.IsNull(plot.Plant);
            Assert.IsFalse(plant.activeSelf, "La planta cosechada vuelve al pool, desactivada.");
            Assert.AreEqual(1, _farmer.Backpack.Count, "El producto entró a la mochila.");
            Assert.AreSame(_world.Crop, _farmer.Backpack.Items[0]);
            Assert.AreEqual(1, _farmer.Offers);
            Assert.AreEqual(PlotCount, _world.Census.Empty);
            Assert.AreEqual(0, _world.Census.Ready);

            // El producto sale de la planta: del ancla de la parcela, un poco más arriba.
            Vector3 plantPoint = plot.transform.position + new Vector3(0f, 0.12f, 0f);
            Assert.AreEqual(plantPoint.x, _farmer.LastFrom.x, 0.01f);
            Assert.AreEqual(plantPoint.z, _farmer.LastFrom.z, 0.01f);
            Assert.Greater(_farmer.LastFrom.y, plantPoint.y, "El producto sale por encima de la tierra.");

            plot.Interact(_farmer);   // el actor sigue encima: el siguiente Interact la siembra sola

            Assert.AreEqual(PlotState.Growing, plot.State);
            Assert.AreSame(plant, plot.Plant.gameObject, "La resiembra reutiliza la planta del pool.");
            Assert.AreEqual(1, _farmer.Backpack.Count, "Sembrar no cosecha.");

            yield break;
        }

        [UnityTest]
        public IEnumerator SteppingOnAReadyPlot_HarvestsItByItself_AndSowsItAgainWhileTheActorStaysOnTop()
        {
            PlotView plot = _world.Plots[1];
            MakeReady(plot);

            MoveFarmerOnto(1);
            yield return WaitFor(() => _farmer.Backpack.Count == 1);
            yield return PhysicsSteps();

            Assert.AreEqual(1, _farmer.Backpack.Count, "Pisar la parcela lista tiene que cosecharla.");
            Assert.AreSame(_world.Crop, _farmer.Backpack.Items[0]);
            Assert.AreEqual(PlotState.Growing, plot.State, "Con el actor encima, la parcela se resiembra sola.");
            Assert.AreEqual(1, _world.Census.Growing);
            Assert.AreEqual(0, _world.Census.Ready);
            Assert.AreEqual(PlotState.Empty, _world.Plots[0].State, "Las otras parcelas no se tocan.");

            // Y mientras la nueva planta crece no cosecha nada más.
            yield return new WaitForSeconds(0.2f);
            Assert.AreEqual(1, _farmer.Backpack.Count);
        }

        [UnityTest]
        public IEnumerator WithAFullBackpack_ThePlotStaysReady_AndTheConsoleStaysQuiet_UntilThereIsRoom()
        {
            for (int i = 0; i < Capacity; i++)
            {
                MakeReady(_world.Plots[i]);
                _world.Plots[i].Interact(_farmer);
            }
            Assert.IsTrue(_farmer.Backpack.IsFull);

            PlotView plot = _world.Plots[3];
            MakeReady(plot);
            int offers = _farmer.Offers;

            int logs = CountLogs(() =>
            {
                for (int frame = 0; frame < 30; frame++) plot.Interact(_farmer);   // el InteractionTrigger lo haría en cada frame
            });

            Assert.AreEqual(PlotState.Ready, plot.State, "Con la mochila llena la parcela sigue lista.");
            Assert.IsNotNull(plot.Plant);
            Assert.IsTrue(plot.Plant.IsReady);
            Assert.AreEqual(Capacity, _farmer.Backpack.Count);
            Assert.AreEqual(1, _world.Census.Ready);
            Assert.Greater(_farmer.Offers, offers, "La parcela sí lo intentó.");
            Assert.AreEqual(0, logs, "Rechazar por mochila llena no tiene que escribir en la consola.");

            // Apenas hay lugar, el siguiente Interact cosecha, sin que el actor tenga que irse y volver.
            Assert.IsTrue(_farmer.Backpack.TryPop(out _));
            plot.Interact(_farmer);

            Assert.AreEqual(PlotState.Empty, plot.State);
            Assert.AreEqual(Capacity, _farmer.Backpack.Count);

            yield break;
        }

        [UnityTest]
        public IEnumerator AFullFarmerStandingOnAReadyPlot_Harvests_AsSoonAsItHasRoom()
        {
            for (int i = 0; i < Capacity; i++) Assert.IsTrue(_farmer.Backpack.TryAdd(_world.Crop));

            PlotView plot = _world.Plots[2];
            MakeReady(plot);
            MoveFarmerOnto(2);

            _logs = 0;
            Application.logMessageReceived += _logCounter;
            yield return PhysicsSteps();
            yield return new WaitForSeconds(0.2f);   // varios frames con el actor encima y la mochila llena
            Application.logMessageReceived -= _logCounter;

            Assert.AreEqual(PlotState.Ready, plot.State, "Llena: no cosecha aunque esté encima.");
            Assert.AreEqual(Capacity, _farmer.Backpack.Count);
            Assert.AreEqual(0, _logs, "Ni un mensaje en la consola.");

            _farmer.Backpack.TryPop(out _);   // por ejemplo, vende uno
            yield return WaitFor(() => _farmer.Backpack.IsFull);

            Assert.AreEqual(Capacity, _farmer.Backpack.Count, "Con lugar, cosecha sola.");
            Assert.AreNotEqual(PlotState.Ready, plot.State);
        }

        [UnityTest]
        public IEnumerator AnActorWithoutABackpack_CannotHarvest()
        {
            PlotView plot = _world.Plots[0];
            MakeReady(plot);

            _plainActor = FakeActor.Create(FarAway);   // un IActor que no es IBackpackOwner
            int logs = CountLogs(() =>
            {
                for (int frame = 0; frame < 10; frame++) plot.Interact(_plainActor);
            });

            Assert.AreEqual(PlotState.Ready, plot.State, "Sin mochila no se cosecha.");
            Assert.AreEqual(0, logs);

            yield break;
        }

        [UnityTest]
        public IEnumerator AnActorThatCannotInteract_DoesNotHarvest_UntilItCan()
        {
            PlotView plot = _world.Plots[0];
            MakeReady(plot);

            // Por ejemplo el jugador en pausa: está parado encima, pero la parcela no lo atiende.
            _farmer.CanAct = false;
            MoveFarmerOnto(0);
            yield return PhysicsSteps();
            yield return new WaitForSeconds(0.1f);

            Assert.AreEqual(PlotState.Ready, plot.State);
            Assert.AreEqual(0, _farmer.Backpack.Count);

            // Apenas puede actuar, la parcela lo atiende sin que tenga que salir y volver a entrar.
            _farmer.CanAct = true;
            yield return WaitFor(() => _farmer.Backpack.Count == 1);

            Assert.AreEqual(1, _farmer.Backpack.Count);
        }

        [UnityTest]
        public IEnumerator MixedCrops_GoIntoTheBackpackInHarvestOrder_AndComeOutLIFO()
        {
            CropData carrot = _world.CreateExtraCrop("carrot");
            CropData tomato = _world.CreateExtraCrop("tomato");
            TestReflection.Set(_world.Plots[1], "crop", carrot);
            TestReflection.Set(_world.Plots[2], "crop", tomato);

            // Se cosechan en el orden 2, 0, 1: trigo-tomate-zanahoria no es el orden de las parcelas.
            foreach (int index in new[] { 2, 0, 1 })
            {
                MakeReady(_world.Plots[index]);
                _world.Plots[index].Interact(_farmer);
            }

            CollectionAssert.AreEqual(new[] { tomato, _world.Crop, carrot }, _farmer.Backpack.Items);

            Assert.IsTrue(_farmer.Backpack.TryPop(out CropData first));
            Assert.IsTrue(_farmer.Backpack.TryPop(out CropData second));
            Assert.IsTrue(_farmer.Backpack.TryPop(out CropData third));
            Assert.AreSame(carrot, first, "Sale primero lo último que se cosechó.");
            Assert.AreSame(_world.Crop, second);
            Assert.AreSame(tomato, third);

            yield break;
        }

        [UnityTest]
        public IEnumerator HarvestAndResowCycles_NeverInstantiatePlants()
        {
            int awakes = AwakeCounter.Awakes;
            Assert.AreEqual(PlotCount, awakes, "Solo las plantas del Prewarm.");

            for (int round = 0; round < 8; round++)
            {
                foreach (PlotView plot in _world.Plots)
                {
                    MakeReady(plot);                   // siembra (planta del pool) y la deja lista
                    plot.Interact(_farmer);            // cosecha (la planta vuelve al pool)
                    Assert.AreEqual(PlotState.Empty, plot.State);
                    _farmer.Backpack.TryPop(out _);    // vacía la mochila para que siempre haya lugar
                }
            }

            PoolStats stats = PlantPool();
            Assert.AreEqual(PlotCount, stats.Created, "Se crearon plantas durante el juego.");
            Assert.AreEqual(0, stats.Misses);
            Assert.AreEqual(awakes, AwakeCounter.Awakes, "Hubo Instantiate después del Prewarm.");

            yield break;
        }
    }
}
