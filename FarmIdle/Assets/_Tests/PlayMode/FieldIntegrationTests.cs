using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;

namespace FarmIdle.Tests
{
    // El campo real de MainLevel (el que genera el builder), arrancando desde Bootstrap como el juego:
    // 9 parcelas de 3 cultivos, plantas del pool, jugador real con su CharacterController y el panel de debug.
    public class FieldIntegrationTests
    {
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return GameBoot.CleanUp();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return GameBoot.CleanUp();
        }

        private static int CountPlantObjects()
        {
            // Todas las plantas que existen (activas o guardadas en el pool): sube solo si alguien hace Instantiate.
            return Object.FindObjectsByType<PlantView>(FindObjectsInactive.Include).Length;
        }

        internal static TMP_Text FindDebugText()
        {
            var panel = Object.FindAnyObjectByType<DebugPanel>();
            Assert.IsNotNull(panel, "No está el panel de debug.");

            foreach (TMP_Text label in panel.GetComponentsInChildren<TMP_Text>(true))
            {
                if (label.text.StartsWith("Estado:")) return label;
            }

            Assert.Fail("El panel de debug no tiene el texto de estado.");
            return null;
        }

        // Mueve al jugador real con el mismo CharacterController que usa para caminar, en pasos cortos (como
        // un movimiento de verdad: la física detecta cada trigger por el que pasa). Deja correr unos pasos de
        // física al llegar para que lleguen los eventos y el Update atienda al jugador.
        internal static IEnumerator WalkTo(GameObject player, Vector3 target)
        {
            var controller = player.GetComponent<CharacterController>();
            float deadline = Time.realtimeSinceStartup + 15f;

            while (Time.realtimeSinceStartup < deadline)
            {
                Vector3 toTarget = target - player.transform.position;
                toTarget.y = 0f;
                if (toTarget.magnitude < 0.05f) break;

                controller.Move(Vector3.ClampMagnitude(toTarget, Mathf.Min(10f * Time.deltaTime, 0.4f)));
                yield return null;
            }

            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            yield return null;
        }

        [UnityTest]
        public IEnumerator Level_HasTheField_WithItsPlantsPrewarmed_AndTheDebugLine()
        {
            yield return GameBoot.BootToReady();
            yield return null;   // el Start() del campo hace el Prewarm

            var field = Object.FindAnyObjectByType<PlotField>();
            Assert.IsNotNull(field, "MainLevel no tiene el campo de parcelas: ¿corriste FarmIdle > Fase 2 > Construir assets y escenas?");
            Assert.AreEqual(9, field.Plots.Count, "El campo es de 3 x 3.");

            // El campo inicial: de 1 a 3 cultivos distintos, cada uno con su planta.
            var initialCrops = new HashSet<CropData>();
            foreach (PlotView plot in field.Plots)
            {
                Assert.IsNotNull(plot.Crop, "Hay una parcela sin cultivo: " + plot.name);
                initialCrops.Add(plot.Crop);
            }
            Assert.That(initialCrops.Count, Is.InRange(1, 3));

            // El Prewarm cuenta el NIVEL ENTERO: una planta por parcela de todos los campos, también los de las zonas cerradas
            // (apagados), así abrir una zona no hace Instantiate. Quedan desactivadas y sin ningún extra.
            PlotView[] allPlots = Object.FindObjectsByType<PlotView>(FindObjectsInactive.Include);
            Assert.Greater(allPlots.Length, 9, "Además del campo inicial hay parcelas de zonas cerradas.");

            var plotsPerPrefab = new Dictionary<GameObject, int>();
            foreach (PlotView plot in allPlots)
            {
                plotsPerPrefab.TryGetValue(plot.Crop.PlantPrefab, out int count);
                plotsPerPrefab[plot.Crop.PlantPrefab] = count + 1;
            }

            foreach (KeyValuePair<GameObject, int> entry in plotsPerPrefab)
            {
                Assert.IsTrue(PoolManager.Instance.TryGetStats(entry.Key, out PoolStats stats), "No hay pool para " + entry.Key.name);
                Assert.AreEqual(entry.Value, stats.Created, "Plantas prewarmeadas de " + entry.Key.name);
                Assert.AreEqual(entry.Value, stats.Inactive);
                Assert.AreEqual(0, stats.Misses);
            }
            Assert.AreEqual(allPlots.Length, CountPlantObjects());

            // Todas vacías, y el panel de debug lo cuenta (sin que la UI conozca las parcelas).
            Assert.AreEqual(9, PlotCensus.Instance.Empty);
            Assert.AreEqual(0, PlotCensus.Instance.Growing);
            Assert.AreEqual(0, PlotCensus.Instance.Ready);
            StringAssert.Contains("Parcelas: 9 vacías, 0 creciendo, 0 listas", FindDebugText().text);
        }

        [UnityTest]
        public IEnumerator WalkingOverThePlots_SowsThem_GrowsThem_AndNeverInstantiates()
        {
            yield return GameBoot.BootToReady();
            yield return null;
            GameManager.Instance.StartPlaying();   // lo mismo que el toque en "Tap to Play"

            var field = Object.FindAnyObjectByType<PlotField>();
            Assert.IsNotNull(field);
            IReadOnlyList<PlotView> plots = field.Plots;

            GameObject player = GameObject.FindWithTag("Player");
            Assert.IsNotNull(player, "No está el jugador.");
            Assert.IsNotNull(player.GetComponent<PlayerActor>(), "El jugador tiene que ser un IActor (PlayerActor).");

            var created = new Dictionary<CropData, int>();
            foreach (PlotView plot in plots)
            {
                PoolManager.Instance.TryGetStats(plot.Crop.PlantPrefab, out PoolStats stats);
                created[plot.Crop] = stats.Created;
            }
            int plantObjects = CountPlantObjects();

            // El hueco entre dos parcelas no siembra nada: se llega al pasillo por un costado y se camina por el medio,
            // a 1,2 unidades del centro de cada una (el trigger del jugador alcanza hasta 1,1).
            Vector3 gap = (plots[0].transform.position + plots[1].transform.position) * 0.5f;
            yield return WalkTo(player, new Vector3(gap.x, 0f, player.transform.position.z));
            yield return WalkTo(player, gap);
            foreach (PlotView plot in plots)
            {
                Assert.AreEqual(PlotState.Empty, plot.State, "Caminar por el hueco entre parcelas no tiene que sembrar: " + plot.name);
            }
            Assert.AreEqual(9, PlotCensus.Instance.Empty);

            // Caminar hasta la primera parcela: la siembra sola.
            yield return WalkTo(player, plots[1].transform.position);
            Assert.AreEqual(PlotState.Growing, plots[1].State, "Pisar una parcela vacía tiene que sembrarla.");
            Assert.AreEqual(8, PlotCensus.Instance.Empty);
            Assert.AreEqual(1, PlotCensus.Instance.Growing);
            StringAssert.Contains("Parcelas: 8 vacías, 1 creciendo, 0 listas", FindDebugText().text);

            // Recorre el resto del campo en zigzag.
            int[] route = { 0, 2, 5, 4, 3, 6, 7, 8 };
            foreach (int index in route)
            {
                yield return WalkTo(player, plots[index].transform.position);
            }

            foreach (PlotView plot in plots)
            {
                Assert.AreNotEqual(PlotState.Empty, plot.State, "Quedó sin sembrar: " + plot.name);
                Assert.IsNotNull(plot.Plant, "Sin planta visible: " + plot.name);
            }
            Assert.AreEqual(0, PlotCensus.Instance.Empty);

            // Nada de Instantiate durante el juego: ni el pool ni el conteo de objetos lo notaron.
            foreach (PlotView plot in plots)
            {
                Assert.IsTrue(PoolManager.Instance.TryGetStats(plot.Crop.PlantPrefab, out PoolStats stats));
                Assert.AreEqual(created[plot.Crop], stats.Created, "Se crearon plantas durante el juego (" + plot.Crop.DisplayName + ").");
                Assert.AreEqual(0, stats.Misses);
            }
            Assert.AreEqual(plantObjects, CountPlantObjects());

            // El trigo (la fila más cercana: plots 0 a 2) es el primero en quedar listo.
            float deadline = Time.realtimeSinceStartup + 20f;
            while (Time.realtimeSinceStartup < deadline && (plots[0].State != PlotState.Ready || plots[1].State != PlotState.Ready || plots[2].State != PlotState.Ready))
            {
                yield return null;
            }

            for (int i = 0; i < 3; i++)
            {
                Assert.AreEqual(PlotState.Ready, plots[i].State, "El trigo no quedó listo a tiempo: " + plots[i].name);
                Assert.IsTrue(plots[i].Plant.IsReady);
                Assert.IsTrue(plots[i].Plant.ReadyModel.activeSelf, "Lista tiene que verse distinta.");
                Assert.IsFalse(plots[i].Plant.GrowingModel.activeSelf);
            }

            Assert.AreEqual(3, PlotCensus.Instance.Ready, "Solo el trigo estaba listo.");
            Assert.AreEqual(6, PlotCensus.Instance.Growing);
            StringAssert.Contains("Parcelas: 0 vacías, 6 creciendo, 3 listas", FindDebugText().text);
        }
    }
}
