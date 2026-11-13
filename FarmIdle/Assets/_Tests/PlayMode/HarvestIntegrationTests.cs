using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace FarmIdle.Tests
{
    // La cosecha y la pila en el MainLevel real (el que genera el builder), arrancando desde Bootstrap como el juego:
    // jugador real con su CharacterController, las 9 parcelas de 3 cultivos, el BackpackManager y la pila visible en
    // su espalda.
    public class HarvestIntegrationTests
    {
        // Por parcelas adyacentes: fila 0 (trigo) de izquierda a derecha, fila 1 (zanahoria) al revés y fila 2 (tomate)
        // otra vez de izquierda a derecha. Caminar de a una parcela vecina no pasa por el medio de ninguna otra.
        private static readonly int[] SowRoute = { 0, 1, 2, 5, 4, 3, 6, 7, 8 };

        private int _logs;
        private Application.LogCallback _logCounter;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _logCounter = (message, stackTrace, type) => _logs++;
            yield return GameBoot.CleanUp();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Application.logMessageReceived -= _logCounter;
            yield return GameBoot.CleanUp();
        }

        private static IEnumerator WaitFor(Func<bool> condition, float timeoutSeconds = 3f)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
        }

        // Los productos que existen (activos o guardados en el pool): sube solo si alguien hace Instantiate.
        private static int CountProductObjects()
        {
            int count = 0;
            foreach (MeshFilter filter in Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include))
            {
                if (filter.gameObject.name.StartsWith("Product_")) count++;
            }

            return count;
        }

        private static int CountPlantObjects()
        {
            return Object.FindObjectsByType<PlantView>(FindObjectsInactive.Include).Length;
        }

        private static Vector3 SlotWorldPosition(BackpackStackView view, int index)
        {
            return view.Anchor.TransformPoint(new Vector3(0f, (index + 0.5f) * view.ItemHeight, 0f));
        }

        [UnityTest]
        public IEnumerator Level_HasTheBackpackWiredUp_WithItsProductsPrewarmed_AndTheDebugLine()
        {
            yield return GameBoot.BootToReady();
            yield return null;   // el OnEnable de la pila hace el Prewarm

            Assert.IsNotNull(BackpackManager.Instance, "Falta el BackpackManager.");
            IReadOnlyInventory backpack = BackpackManager.Instance.Backpack;
            Assert.GreaterOrEqual(backpack.Capacity, 1);
            Assert.AreEqual(0, backpack.Count);

            GameObject player = GameObject.FindWithTag("Player");
            var view = player.GetComponent<BackpackStackView>();
            Assert.IsNotNull(view, "El jugador no tiene la pila de la mochila: ¿corriste FarmIdle > Fase 2 > Construir assets y escenas?");
            Assert.IsNotNull(player.GetComponent<PlayerActor>());
            Assert.AreSame(player.transform, view.Anchor.parent, "El ancla cuelga del jugador.");
            Assert.Less(view.Anchor.localPosition.z, 0f, "El ancla está en la espalda (el frente del jugador es +Z).");
            Assert.AreEqual(0, view.ItemCount);

            // Un pool de productos por cultivo del nivel (también los de las zonas cerradas), con la mochila entera de cada uno ya
            // creada EN SU MÁXIMA CAPACIDAD (las mejoras de capacidad la agrandan después, sin Instantiate) y sin extras.
            int prewarm = BackpackManager.Instance.MaxCapacity + view.DepartingHeadroom;
            Assert.GreaterOrEqual(BackpackManager.Instance.MaxCapacity, backpack.Capacity);
            var seen = new HashSet<GameObject>();
            foreach (PlotView plot in Object.FindObjectsByType<PlotView>(FindObjectsInactive.Include))
            {
                GameObject prefab = plot.Crop.ProductPrefab;
                Assert.IsNotNull(prefab, "El cultivo no tiene producto: " + plot.Crop.DisplayName);
                if (!seen.Add(prefab)) continue;

                Assert.IsTrue(PoolManager.Instance.TryGetStats(prefab, out PoolStats stats), "No hay pool para el producto de " + plot.Crop.DisplayName);
                Assert.AreEqual(prewarm, stats.Created, "Una mochila entera (al máximo) de " + plot.Crop.DisplayName + ", más los que pueden estar volando a la venta");
                Assert.AreEqual(prewarm, stats.Inactive);
                Assert.AreEqual(0, stats.Misses);
            }
            // F-01: hay cultivos sin parcela todavía (las zonas nuevas llegan con F-02) que ya tienen su pool: solo se piden pools enteros.
            Assert.GreaterOrEqual(CountProductObjects(), seen.Count * prewarm);
            Assert.AreEqual(0, CountProductObjects() % prewarm, "Solo pools enteros de productos.");

            StringAssert.Contains("Mochila: 0/" + backpack.Capacity, FieldIntegrationTests.FindDebugText().text);
        }

        [UnityTest]
        public IEnumerator WalkingTheField_HarvestsAndPilesTheProducts_LIFO_UntilTheBackpackIsFull_NeverInstantiating()
        {
            yield return GameBoot.BootToReady();
            yield return null;
            GameManager.Instance.StartPlaying();   // lo mismo que el toque en "Tap to Play"

            var field = Object.FindAnyObjectByType<PlotField>();
            IReadOnlyList<PlotView> plots = field.Plots;
            Assert.AreEqual(9, plots.Count);
            GameObject player = GameObject.FindWithTag("Player");
            var view = player.GetComponent<BackpackStackView>();
            IReadOnlyInventory backpack = BackpackManager.Instance.Backpack;
            Assert.GreaterOrEqual(backpack.Capacity, 10, "Esta prueba supone una mochila de al menos 10.");
            int capacity = backpack.Capacity;

            // Lo que existe antes de jugar: ni plantas ni productos nuevos después.
            int plantObjects = CountPlantObjects();
            int productObjects = CountProductObjects();
            var createdBefore = new Dictionary<GameObject, int>();
            foreach (PlotView plot in plots)
            {
                foreach (GameObject prefab in new[] { plot.Crop.PlantPrefab, plot.Crop.ProductPrefab })
                {
                    PoolManager.Instance.TryGetStats(prefab, out PoolStats stats);
                    createdBefore[prefab] = stats.Created;
                }
            }

            // 1) Caminando se siembran las 9 parcelas.
            foreach (int index in SowRoute)
            {
                yield return FieldIntegrationTests.WalkTo(player, plots[index].transform.position);
            }
            Assert.AreEqual(0, PlotCensus.Instance.Empty, "Quedó alguna parcela sin sembrar.");
            Assert.AreEqual(0, backpack.Count, "Sembrar no cosecha.");

            // 2) Todo madura de golpe (el campo hace lo mismo con el tiempo del juego). El jugador está parado sobre
            //    la última parcela: la cosecha solo, y esa es la primera.
            foreach (PlotView plot in plots) plot.Tick(100f);
            yield return WaitFor(() => backpack.Count >= 1);
            Assert.AreEqual(1, backpack.Count, "Pisar una parcela lista la cosecha sola.");
            Assert.AreSame(plots[8].Crop, backpack.Items[0]);

            // 3) Vuelve por el mismo camino: cada parcela lista que pisa entra a la mochila, en el orden en que se pisa.
            var harvested = new List<CropData> { plots[8].Crop };
            foreach (int index in new[] { 7, 6, 3, 4, 5, 2, 1, 0 })
            {
                yield return FieldIntegrationTests.WalkTo(player, plots[index].transform.position);
                yield return WaitFor(() => backpack.Count == harvested.Count + 1);

                harvested.Add(plots[index].Crop);
                Assert.AreEqual(harvested.Count, backpack.Count, "No se cosechó la parcela " + index);
                Assert.AreSame(plots[index].Crop, backpack.Items[backpack.Count - 1], "Arriba queda lo último cosechado (parcela " + index + ").");
            }
            yield return null;
            yield return null;

            // Cada parcela cosechada se resembró sola, porque el jugador estaba encima.
            Assert.AreEqual(0, PlotCensus.Instance.Empty, "Las parcelas cosechadas se resiembran solas.");
            CollectionAssert.AreEqual(harvested, backpack.Items);
            StringAssert.Contains("Mochila: 9/" + capacity, FieldIntegrationTests.FindDebugText().text);
            StringAssert.Contains("Tomate 3", FieldIntegrationTests.FindDebugText().text);
            StringAssert.Contains("Zanahoria 3", FieldIntegrationTests.FindDebugText().text);
            StringAssert.Contains("Trigo 3", FieldIntegrationTests.FindDebugText().text);

            // 4) La pila de la espalda: un producto por unidad, del cultivo que corresponde y en el mismo orden.
            yield return new WaitForSecondsRealtime(0.7f);   // terminan los vuelos
            Assert.AreEqual(9, view.ItemCount);
            for (int i = 0; i < 9; i++)
            {
                GameObject item = view.ItemObject(i);
                Assert.IsNotNull(item, "Falta el producto " + i);
                Assert.AreEqual(backpack.Items[i].ProductPrefab.name, item.name, "El producto del lugar " + i);
                Assert.IsFalse(view.IsFlying(i));
                Assert.AreEqual(0f, Vector3.Distance(item.transform.position, SlotWorldPosition(view, i)), 1e-3f);
            }

            // 5) Ni una planta ni un producto creado durante el juego.
            foreach (KeyValuePair<GameObject, int> entry in createdBefore)
            {
                Assert.IsTrue(PoolManager.Instance.TryGetStats(entry.Key, out PoolStats stats));
                Assert.AreEqual(entry.Value, stats.Created, "Se crearon instancias de " + entry.Key.name + " durante el juego.");
                Assert.AreEqual(0, stats.Misses);
            }
            Assert.AreEqual(plantObjects, CountPlantObjects());
            Assert.AreEqual(productObjects, CountProductObjects());

            // 6) Con la mochila llena no cosecha: la última unidad entra, y la parcela siguiente se queda lista.
            foreach (PlotView plot in plots) plot.Tick(100f);
            yield return WaitFor(() => backpack.IsFull);   // el jugador está sobre la parcela 0
            Assert.IsTrue(backpack.IsFull);
            Assert.AreEqual(capacity, backpack.Count);
            StringAssert.Contains("LLENA", FieldIntegrationTests.FindDebugText().text);

            _logs = 0;
            Application.logMessageReceived += _logCounter;
            yield return FieldIntegrationTests.WalkTo(player, plots[1].transform.position);
            yield return new WaitForSeconds(0.3f);   // parado sobre una parcela lista con la mochila llena
            Application.logMessageReceived -= _logCounter;

            Assert.AreEqual(PlotState.Ready, plots[1].State, "Con la mochila llena la parcela sigue lista.");
            Assert.AreEqual(capacity, backpack.Count);
            Assert.AreEqual(0, _logs, "Rechazar por mochila llena no tiene que escribir en la consola.");

            // 7) Apenas hay lugar (por ejemplo la venta de la Fase 5), cosecha sola sin que el jugador se mueva.
            Assert.IsTrue(BackpackManager.Instance.TryPop(out CropData top));
            Assert.AreSame(plots[0].Crop, top, "Sale primero lo último que entró.");
            yield return WaitFor(() => backpack.IsFull);
            Assert.IsTrue(backpack.IsFull);
            Assert.AreNotEqual(PlotState.Ready, plots[1].State);

            // 8) Se vacía de arriba hacia abajo y la pila acompaña: todo vuelve al pool.
            yield return new WaitForSecondsRealtime(0.7f);
            var expectedOrder = new List<CropData>(backpack.Items);
            expectedOrder.Reverse();
            foreach (CropData expected in expectedOrder)
            {
                Assert.IsTrue(BackpackManager.Instance.TryPop(out CropData popped));
                Assert.AreSame(expected, popped);
            }

            Assert.AreEqual(0, backpack.Count);
            Assert.AreEqual(0, view.ItemCount);
            foreach (PlotView plot in plots)
            {
                Assert.IsTrue(PoolManager.Instance.TryGetStats(plot.Crop.ProductPrefab, out PoolStats stats));
                Assert.AreEqual(0, stats.Active, "Los productos tienen que volver al pool: " + plot.Crop.DisplayName);
            }
            StringAssert.Contains("Mochila: 0/" + capacity, FieldIntegrationTests.FindDebugText().text);
        }
    }
}
