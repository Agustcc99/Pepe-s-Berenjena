using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace FarmIdle.Tests
{
    // El core loop completo en el MainLevel real (el que genera el builder), arrancando desde Bootstrap como el juego:
    // jugador real con su CharacterController, el campo de 9 parcelas, la mochila con su pila, la zona de venta con su
    // feedback, la billetera con el HUD y el panel de debug. Sembrar, cosechar, llenar, vender y seguir cosechando.
    public class SellIntegrationTests
    {
        // Por parcelas adyacentes (ver HarvestIntegrationTests): caminar de a una vecina no pisa ninguna otra.
        private static readonly int[] SowRoute = { 0, 1, 2, 5, 4, 3, 6, 7, 8 };
        private static readonly int[] HarvestRoute = { 7, 6, 3, 4, 5, 2, 1, 0 };

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

        private static IEnumerator WaitFor(Func<bool> condition, float timeoutSeconds = 4f)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
        }

        [UnityTest]
        public IEnumerator Level_HasTheSellPad_NearTheSpawnOutsideTheField_WiredUp_WithItsCoinsPrewarmed_AndTheDebugLine()
        {
            yield return GameBoot.BootToReady();
            yield return null;   // el OnEnable del SellFeedback hace el Prewarm

            var zone = Object.FindAnyObjectByType<SellZone>();
            Assert.IsNotNull(zone, "No hay zona de venta: ¿corriste FarmIdle > Fase 2 > Construir assets y escenas?");

            var trigger = zone.GetComponent<InteractionTrigger>();
            var collider = zone.GetComponent<BoxCollider>();
            Assert.IsNotNull(trigger, "La zona de venta necesita su InteractionTrigger.");
            Assert.IsNotNull(collider);
            Assert.IsTrue(collider.isTrigger);

            // Cerca del punto de aparición (el origen) y fuera del campo.
            GameObject player = GameObject.FindWithTag("Player");
            Vector3 pad = zone.transform.position;
            Assert.Less(Vector3.Distance(new Vector3(pad.x, 0f, pad.z), new Vector3(player.transform.position.x, 0f, player.transform.position.z)), 6f, "El pad queda cerca del punto de aparición.");
            Assert.Greater(Vector3.Distance(new Vector3(pad.x, 0f, pad.z), new Vector3(player.transform.position.x, 0f, player.transform.position.z)), collider.bounds.extents.x + 0.5f, "Y no se aparece encima.");

            var field = Object.FindAnyObjectByType<PlotField>();
            foreach (PlotView plot in field.Plots)
            {
                Bounds plotBounds = plot.GetComponent<Collider>().bounds;
                Assert.IsFalse(collider.bounds.Intersects(plotBounds), "El pad no se superpone con " + plot.name);
            }

            Assert.Greater(zone.Target.position.y, pad.y, "Los productos vuelan a un punto sobre el pad.");

            // El feedback: monedas del pool y texto, sin nada activo al empezar.
            var feedback = zone.GetComponent<SellFeedback>();
            Assert.IsNotNull(feedback);
            Assert.IsTrue(PoolManager.Instance.TryGetStats(feedback.CoinPrefab, out PoolStats coins), "No hay pool de monedas.");
            Assert.Greater(coins.Created, 0);
            Assert.AreEqual(coins.Created, coins.Inactive);
            Assert.AreEqual(0, coins.Misses);
            Assert.IsFalse(feedback.PopupLabel.enabled);

            StringAssert.Contains("Vendido: 0 unidades, 0 monedas", FieldIntegrationTests.FindDebugText().text);
        }

        [UnityTest]
        public IEnumerator TheCoreLoop_SowHarvestFillSellAndSeeTheBalance_ThenKeepHarvesting_NeverInstantiating()
        {
            yield return GameBoot.BootToReady();
            yield return null;
            GameManager.Instance.StartPlaying();   // lo mismo que el toque en "Tap to Play"

            var field = Object.FindAnyObjectByType<PlotField>();
            IReadOnlyList<PlotView> plots = field.Plots;
            var zone = Object.FindAnyObjectByType<SellZone>();
            var feedback = zone.GetComponent<SellFeedback>();
            GameObject player = GameObject.FindWithTag("Player");
            var view = player.GetComponent<BackpackStackView>();
            IReadOnlyInventory backpack = BackpackManager.Instance.Backpack;
            EconomyManager economy = EconomyManager.Instance;
            economy.RareRoll = () => 1f;   // sin cosecha rara (J-04): paga al cosechar y esta prueba cuenta monedas; con azar real fallaba seguido
            economy.Pricing.SetDailyCrop("", 0f);   // sin cultivo del día (IN-02): la prueba cuenta monedas y el cultivo de hoy depende de la fecha
            int capacity = backpack.Capacity;
            Assert.GreaterOrEqual(capacity, 10, "Esta prueba supone una mochila de al menos 10.");
            long startBalance = economy.Balance;

            // Lo que existe antes de jugar: nada nuevo después (plantas, productos y monedas).
            var createdBefore = new Dictionary<GameObject, int>();
            foreach (PlotView plot in plots)
            {
                foreach (GameObject prefab in new[] { plot.Crop.PlantPrefab, plot.Crop.ProductPrefab })
                {
                    PoolManager.Instance.TryGetStats(prefab, out PoolStats stats);
                    createdBefore[prefab] = stats.Created;
                }
            }
            PoolManager.Instance.TryGetStats(feedback.CoinPrefab, out PoolStats coinStats);
            createdBefore[feedback.CoinPrefab] = coinStats.Created;

            // 1) Siembra caminando.
            foreach (int index in SowRoute) yield return FieldIntegrationTests.WalkTo(player, plots[index].transform.position);
            Assert.AreEqual(0, PlotCensus.Instance.Empty, "Quedó alguna parcela sin sembrar.");

            // 2) Madura todo y se cosecha caminando de vuelta hasta llenar la mochila.
            foreach (PlotView plot in plots) plot.Tick(100f);
            yield return WaitFor(() => backpack.Count >= 1);
            foreach (int index in HarvestRoute)
            {
                int before = backpack.Count;
                yield return FieldIntegrationTests.WalkTo(player, plots[index].transform.position);
                yield return WaitFor(() => backpack.Count == before + 1);
            }
            foreach (PlotView plot in plots) plot.Tick(100f);   // el jugador sigue sobre la última parcela: cosecha la décima
            yield return WaitFor(() => backpack.IsFull);
            Assert.IsTrue(backpack.IsFull, "Cosechando se llena la mochila.");
            Assert.AreEqual(capacity, backpack.Count);
            Assert.AreEqual(startBalance, economy.Balance, "Cosechar no paga: solo vender.");
            StringAssert.Contains("LLENA", FieldIntegrationTests.FindDebugText().text);

            // Lo que hay en la mochila, de abajo hacia arriba, y lo que tiene que pagar.
            var inBackpack = new List<CropData>(backpack.Items);
            long expectedCoins = 0;
            foreach (CropData crop in inBackpack) expectedCoins += economy.Pricing.FinalPrice(crop.SellPrice);
            Assert.Greater(expectedCoins, 0L);

            var soldOrder = new List<CropData>();
            economy.ProductSold += (crop, coins) => soldOrder.Add(crop);
            float walkedAt = Time.time;

            // 3) Se camina hasta el pad: vende de arriba hacia abajo.
            yield return FieldIntegrationTests.WalkTo(player, zone.transform.position);
            yield return WaitFor(() => backpack.Count == 0);
            float emptiedAt = Time.time;
            Assert.AreEqual(0, backpack.Count, "Parado sobre el pad, la mochila se vacía.");

            var expectedOrder = new List<CropData>(inBackpack);
            expectedOrder.Reverse();
            CollectionAssert.AreEqual(expectedOrder, soldOrder, "Se vende de arriba hacia abajo.");
            Assert.AreEqual(startBalance + expectedCoins, economy.Balance, "Las monedas suben por el precio de cada cultivo.");
            Assert.AreEqual(capacity, economy.SoldUnits);
            Assert.AreEqual(expectedCoins, economy.SoldCoins);
            Assert.GreaterOrEqual(emptiedAt - walkedAt, (capacity - 1) * 0.1f, "El ritmo de venta no es instantáneo.");

            // 4) El panel de debug y el HUD lo muestran.
            string debug = FieldIntegrationTests.FindDebugText().text;
            StringAssert.Contains("Mochila: 0/" + capacity, debug);
            StringAssert.Contains("Vendido: " + capacity + " unidades, " + expectedCoins + " monedas", debug);

            TMP_Text hud = Object.FindAnyObjectByType<CoinCounterView>().GetComponentInChildren<TMP_Text>();
            string expectedText = (startBalance + expectedCoins).ToString("N0", System.Globalization.CultureInfo.InvariantCulture);
            yield return WaitFor(() => hud.text == expectedText, 2f);
            Assert.AreEqual(expectedText, hud.text, "El contador del HUD llega al saldo.");

            // 5) La pila de la espalda acompañó: vacía y todo en el pool, igual que las monedas.
            yield return new WaitForSecondsRealtime(1.3f);
            Assert.AreEqual(0, view.ItemCount);
            Assert.AreEqual(0, view.DepartingCount);
            foreach (PlotView plot in plots)
            {
                Assert.IsTrue(PoolManager.Instance.TryGetStats(plot.Crop.ProductPrefab, out PoolStats stats));
                Assert.AreEqual(0, stats.Active, "Los productos vuelven al pool: " + plot.Crop.DisplayName);
            }
            PoolManager.Instance.TryGetStats(feedback.CoinPrefab, out PoolStats coinsAfter);
            Assert.AreEqual(0, coinsAfter.Active, "Las monedas vuelven al pool.");

            // 6) Con la mochila vacía y las parcelas otra vez listas, se sigue cosechando: el loop cierra.
            foreach (PlotView plot in plots) plot.Tick(100f);
            yield return FieldIntegrationTests.WalkTo(player, plots[4].transform.position);
            yield return WaitFor(() => backpack.Count >= 1);
            Assert.GreaterOrEqual(backpack.Count, 1, "Después de vender se vuelve a cosechar.");

            // 7) Ni una planta, un producto o una moneda creados durante el juego.
            foreach (KeyValuePair<GameObject, int> entry in createdBefore)
            {
                Assert.IsTrue(PoolManager.Instance.TryGetStats(entry.Key, out PoolStats stats));
                Assert.AreEqual(entry.Value, stats.Created, "Se crearon instancias de " + entry.Key.name + " durante el juego.");
                Assert.AreEqual(0, stats.Misses);
            }
        }
    }
}
