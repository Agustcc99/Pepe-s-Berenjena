using System.Collections;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace FarmIdle.Tests
{
    // El campo de parcelas con servicios reales (GameManager, PoolManager, PlotCensus) pero un mundo armado por código:
    // 4 parcelas con un cultivo de 0,6 s, para no esperar. La pisada se prueba con física real (trigger de la parcela
    // + actor con Rigidbody); el resto llama a Interact directamente, que es lo que hace el InteractionTrigger.
    public class PlotFieldTests
    {
        private const int PlotCount = 4;
        private const float GrowSeconds = 0.6f;
        private static readonly Vector3 FarAway = new Vector3(-50f, 1f, 0f);

        private FieldTestWorld _world;
        private FakeActor _actor;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _world = FieldTestWorld.Create(PlotCount, GrowSeconds);
            _actor = FakeActor.Create(FarAway);   // lejos de las parcelas: no pisa nada
            yield return null;                    // el Start() del campo hace el Prewarm
        }

        [TearDown]
        public void TearDown()
        {
            if (_actor != null) Object.DestroyImmediate(_actor.gameObject);
            _world.Destroy();
            Time.timeScale = 1f;
        }

        private PoolStats PlantPool()
        {
            Assert.IsTrue(_world.Pools.TryGetStats(_world.PlantTemplate, out PoolStats stats), "No hay pool de plantas: el campo no hizo el Prewarm.");
            return stats;
        }

        private void MoveActorOnto(int plotIndex)
        {
            _actor.transform.position = _world.Plots[plotIndex].transform.position + Vector3.up;
        }

        private InteractionTrigger TriggerOf(int plotIndex)
        {
            return _world.Plots[plotIndex].GetComponent<InteractionTrigger>();
        }

        // Deja pasar un par de pasos de física (llegan OnTriggerEnter/Exit) y un frame (el Update llama a Interact).
        private static IEnumerator PhysicsSteps()
        {
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            yield return null;
        }

        [UnityTest]
        public IEnumerator Prewarm_DeclaresOnePlantPerPlot_AndSowingEveryPlotNeverInstantiates()
        {
            PoolStats afterPrewarm = PlantPool();
            Assert.AreEqual(PlotCount, afterPrewarm.Created);
            Assert.AreEqual(PlotCount, afterPrewarm.Inactive);
            Assert.AreEqual(0, afterPrewarm.Active);
            Assert.AreEqual(0, afterPrewarm.Misses);

            // Contador independiente del pool: cada Awake del molde clonado es un Instantiate real.
            int awakesAfterPrewarm = AwakeCounter.Awakes;
            Assert.AreEqual(PlotCount, awakesAfterPrewarm);

            foreach (PlotView plot in _world.Plots)
            {
                Assert.AreEqual(PlotState.Empty, plot.State);
                Assert.IsNull(plot.Plant, "Una parcela vacía no tiene planta.");
                plot.Interact(_actor);
            }

            PoolStats whileGrowing = PlantPool();
            Assert.AreEqual(afterPrewarm.Created, whileGrowing.Created, "Sembrar creó instancias nuevas.");
            Assert.AreEqual(PlotCount, whileGrowing.Active);
            Assert.AreEqual(0, whileGrowing.Misses);
            Assert.AreEqual(awakesAfterPrewarm, AwakeCounter.Awakes, "Hubo Instantiate después del Prewarm.");

            foreach (PlotView plot in _world.Plots)
            {
                Assert.AreEqual(PlotState.Growing, plot.State);
                Assert.IsNotNull(plot.Plant, "Una parcela sembrada tiene que mostrar su planta.");
                Assert.IsTrue(plot.Plant.gameObject.activeInHierarchy);
            }

            yield break;
        }

        [UnityTest]
        public IEnumerator SteppingOnAnEmptyPlot_SowsItByItself()
        {
            yield return PhysicsSteps();
            foreach (PlotView plot in _world.Plots) Assert.AreEqual(PlotState.Empty, plot.State, "Nadie pisó nada todavía.");

            MoveActorOnto(1);
            yield return PhysicsSteps();

            Assert.AreEqual(PlotState.Growing, _world.Plots[1].State, "Pisar la parcela vacía tiene que sembrarla.");
            Assert.AreEqual(PlotState.Empty, _world.Plots[0].State);
            Assert.AreEqual(PlotState.Empty, _world.Plots[2].State);
            Assert.AreEqual(PlotState.Empty, _world.Plots[3].State);

            Assert.AreEqual(PlotCount - 1, _world.Census.Empty);
            Assert.AreEqual(1, _world.Census.Growing);
            Assert.AreEqual(0, _world.Census.Ready);
        }

        [UnityTest]
        public IEnumerator AfterGrowSeconds_ThePlotIsReady_AndTheReadyPlantLooksDifferent()
        {
            PlotView plot = _world.Plots[0];
            plot.Interact(_actor);

            PlantView plant = plot.Plant;
            Assert.IsTrue(plant.GrowingModel.activeSelf, "Creciendo se ve la forma verde.");
            Assert.IsFalse(plant.ReadyModel.activeSelf);
            float seedSize = plant.transform.localScale.x;

            yield return new WaitForSeconds(GrowSeconds * 0.5f);

            // A mitad de camino: sigue creciendo y la planta se agrandó (crece escalando).
            Assert.AreEqual(PlotState.Growing, plot.State);
            Assert.That(plot.Progress, Is.InRange(0.2f, 0.95f));
            Assert.Greater(plant.transform.localScale.x, seedSize, "La planta tiene que agrandarse mientras crece.");
            Assert.IsFalse(plant.IsReady);

            yield return new WaitForSeconds(GrowSeconds);

            Assert.AreEqual(PlotState.Ready, plot.State);
            Assert.AreEqual(1f, plot.Progress);
            Assert.IsTrue(plant.IsReady);
            Assert.IsTrue(plant.ReadyModel.activeSelf, "Lista se ve la forma madura.");
            Assert.IsFalse(plant.GrowingModel.activeSelf);
            Assert.AreEqual(0, _world.Census.Growing);
            Assert.AreEqual(1, _world.Census.Ready);

            yield return new WaitForSecondsRealtime(0.6f);   // termina el "salto" de la planta madura
            Assert.AreEqual(1f, plant.transform.localScale.x, 0.001f, "La planta lista queda a tamaño completo.");
        }

        [UnityTest]
        public IEnumerator Growth_IsFrozenWhilePaused_AndWhileAnAdIsShowing()
        {
            // Acá no hace falta llegar a "lista": con 5 s de crecimiento un tirón de frames no puede adelantarse al test.
            TestReflection.Set(_world.Crop, "growSeconds", 5f);

            PlotView plot = _world.Plots[0];
            plot.Interact(_actor);

            yield return new WaitForSeconds(0.15f);
            Assert.AreEqual(PlotState.Growing, plot.State);

            Assert.IsTrue(_world.Game.Pause());
            float frozen = plot.Progress;
            Assert.Greater(frozen, 0f);

            yield return new WaitForSecondsRealtime(0.4f);
            Assert.AreEqual(frozen, plot.Progress, 1e-4f, "Creció durante la pausa.");

            Assert.IsTrue(_world.Game.Resume());
            Assert.IsTrue(_world.Game.BeginAd());

            yield return new WaitForSecondsRealtime(0.4f);
            Assert.AreEqual(frozen, plot.Progress, 1e-4f, "Creció durante el anuncio.");

            Assert.IsTrue(_world.Game.EndAd());
            yield return new WaitForSeconds(0.2f);
            Assert.Greater(plot.Progress, frozen, "Al volver a Playing tiene que seguir creciendo.");
        }

        [UnityTest]
        public IEnumerator AnActorThatCannotInteract_DoesNotSow_UntilItCan()
        {
            // Por ejemplo el jugador en el menú o en pausa: está parado encima, pero la parcela no lo atiende.
            _actor.CanAct = false;
            MoveActorOnto(0);
            yield return PhysicsSteps();

            Assert.AreEqual(1, TriggerOf(0).ActorCount, "La parcela tiene que haber detectado al actor.");
            Assert.AreEqual(PlotState.Empty, _world.Plots[0].State, "Un actor que no puede actuar no siembra.");

            // Apenas puede actuar, la parcela lo atiende sin que tenga que salir y volver a entrar.
            _actor.CanAct = true;
            yield return null;
            yield return null;

            Assert.AreEqual(PlotState.Growing, _world.Plots[0].State);
        }

        [UnityTest]
        public IEnumerator WhenTheActorLeaves_ThePlotForgetsIt()
        {
            _actor.CanAct = false;
            MoveActorOnto(2);
            yield return PhysicsSteps();
            Assert.AreEqual(1, TriggerOf(2).ActorCount);

            _actor.transform.position = FarAway;
            yield return PhysicsSteps();
            Assert.AreEqual(0, TriggerOf(2).ActorCount, "Al irse, la parcela tiene que olvidarlo.");

            _actor.CanAct = true;
            yield return PhysicsSteps();
            Assert.AreEqual(PlotState.Empty, _world.Plots[2].State, "Un actor que ya no está encima no siembra.");
        }

        [UnityTest]
        public IEnumerator AnActorWhoseColliderIsDisabled_IsForgotten()
        {
            // OnTriggerExit no siempre llega si el collider se apaga o se destruye: la parcela no se queda con un fantasma.
            _actor.CanAct = false;
            MoveActorOnto(3);
            yield return PhysicsSteps();
            Assert.AreEqual(1, TriggerOf(3).ActorCount);

            _actor.GetComponent<Collider>().enabled = false;
            yield return null;

            Assert.AreEqual(0, TriggerOf(3).ActorCount);
        }

        [UnityTest]
        public IEnumerator Harvest_ReturnsThePlantToThePool_AndTheNextSowReusesIt()
        {
            PlotView plot = _world.Plots[0];
            plot.Interact(_actor);
            GameObject firstPlant = plot.Plant.gameObject;

            // Se fuerza el paso del tiempo con Tick (lo mismo que hace el PlotField en cada frame).
            plot.Tick(GrowSeconds + 0.1f);
            Assert.AreEqual(PlotState.Ready, plot.State);
            Assert.IsTrue(plot.Plant.IsReady);

            int awakes = AwakeCounter.Awakes;
            Assert.IsTrue(plot.TryHarvest());

            Assert.AreEqual(PlotState.Empty, plot.State);
            Assert.IsNull(plot.Plant);
            Assert.IsFalse(firstPlant.activeSelf, "La planta cosechada vuelve al pool, desactivada.");
            Assert.AreEqual(0, PlantPool().Active);
            Assert.AreEqual(PlotCount, PlantPool().Inactive);

            plot.Interact(_actor);

            Assert.AreSame(firstPlant, plot.Plant.gameObject, "La siguiente siembra reutiliza la instancia del pool.");
            Assert.IsFalse(plot.Plant.IsReady, "La planta reciclada vuelve a empezar desde la forma verde.");
            Assert.IsTrue(plot.Plant.GrowingModel.activeSelf);
            Assert.IsFalse(plot.Plant.ReadyModel.activeSelf);
            Assert.AreEqual(PlotCount, PlantPool().Created);
            Assert.AreEqual(0, PlantPool().Misses);
            Assert.AreEqual(awakes, AwakeCounter.Awakes, "Reutilizar la planta no hace Instantiate.");

            yield break;
        }

        [UnityTest]
        public IEnumerator DisablingAPlot_ReturnsItsPlantToThePool_AndEnablingItRestoresTheLook()
        {
            PlotView plot = _world.Plots[0];
            plot.Interact(_actor);
            Assert.AreEqual(1, PlantPool().Active);

            plot.gameObject.SetActive(false);

            Assert.AreEqual(0, PlantPool().Active, "Una parcela apagada no se queda con su planta.");
            Assert.AreEqual(PlotCount - 1, _world.Census.Total, "Una parcela apagada se da de baja del censo.");

            plot.gameObject.SetActive(true);

            Assert.AreEqual(PlotState.Growing, plot.State, "El crecimiento en curso se conserva.");
            Assert.IsNotNull(plot.Plant, "Al reactivarse recupera su planta.");
            Assert.AreEqual(1, PlantPool().Active);
            Assert.AreEqual(PlotCount, _world.Census.Total);
            Assert.AreEqual(1, _world.Census.Growing);

            yield break;
        }

        [UnityTest]
        public IEnumerator ReadyPlot_ThatGetsReactivated_ShowsTheReadyPlantRightAway()
        {
            PlotView plot = _world.Plots[0];
            plot.Interact(_actor);
            plot.Tick(GrowSeconds + 0.1f);

            plot.gameObject.SetActive(false);
            plot.gameObject.SetActive(true);

            Assert.AreEqual(PlotState.Ready, plot.State);
            Assert.IsTrue(plot.Plant.IsReady);
            Assert.IsTrue(plot.Plant.ReadyModel.activeSelf);
            Assert.AreEqual(1, _world.Census.Ready);

            yield break;
        }

        [UnityTest]
        public IEnumerator MisconfiguredPlot_StaysEmpty_AndLogsTheErrorOnlyOnce()
        {
            PlotView plot = _world.Plots[0];
            CropData withoutPlant = TestReflection.CreateCrop("sin-planta", 1f, null);
            TestReflection.Set(plot, "crop", withoutPlant);

            LogAssert.Expect(LogType.Error, new Regex("no puede sembrar"));
            for (int i = 0; i < 5; i++) plot.Interact(_actor);   // el InteractionTrigger lo haría en cada frame

            Assert.AreEqual(PlotState.Empty, plot.State);
            Object.DestroyImmediate(withoutPlant);

            yield break;
        }
    }
}
