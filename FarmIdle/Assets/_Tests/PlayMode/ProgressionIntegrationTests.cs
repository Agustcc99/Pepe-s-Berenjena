using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace FarmIdle.Tests
{
    // La progresión en el MainLevel real (el que genera el builder), arrancando desde Bootstrap como el juego: jugador real con
    // su CharacterController, el campo inicial, la zona de venta, las dos zonas cerradas con su portón y la plaza de cuatro mejoras.
    // El recorrido cierra la fase: vender, comprar una zona y tres mejoras parado sobre sus pads, y ver cada efecto.
    public class ProgressionIntegrationTests
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

        private static T[] All<T>() where T : Object
        {
            return Object.FindObjectsByType<T>(FindObjectsInactive.Include);
        }

        private static IEnumerator WaitFor(Func<bool> condition, float timeoutSeconds = 6f)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
        }

        private static UpgradePad PadOf(UpgradeKind kind)
        {
            return All<UpgradePad>().First(pad => pad.Upgrade.Kind == kind);
        }

        private static ZoneController ZoneControllerOf(string zoneId)
        {
            return All<ZoneController>().First(controller => controller.Zone.Id == zoneId);
        }

        private static ZoneUnlockPad GateOf(string zoneId)
        {
            return All<ZoneUnlockPad>().First(pad => pad.Zone.Id == zoneId);
        }

        [UnityTest]
        public IEnumerator Level_HasTheClosedZonesAndTheUpgradePlaza_WiredUp_AndTheProgressLine()
        {
            yield return GameBoot.BootToReady();
            yield return null;

            ProgressionManager progression = ProgressionManager.Instance;
            Assert.IsNotNull(progression, "Falta el ProgressionManager: ¿corriste FarmIdle > Fase 2 > Construir assets y escenas?");
            Assert.AreEqual(8, progression.Zones.Count);   // 5 desde el principio + 3 granjas de temporada (F-24)
            Assert.AreEqual(5, progression.Upgrades.Count);
            Assert.AreEqual(0, progression.UnlockedZoneCount);

            // Las cinco zonas de la temporada 0, cerradas: se ve el terreno vallado y el campo (9 parcelas) espera apagado. Las tres
            // granjas de temporada todavía no aparecen (ni terreno ni portón).
            ZoneController[] zones = All<ZoneController>();
            Assert.AreEqual(8, zones.Length);
            foreach (ZoneController zone in zones)
            {
                bool seasonal = zone.Zone.RequiredSeason > 0;
                Assert.AreEqual(!seasonal, zone.LockedRoot.activeSelf, zone.name + (seasonal ? ": granja de temporada, todavía no aparece." : ": cerrada, se ve el terreno vallado."));
                Assert.IsFalse(zone.UnlockedRoot.activeSelf, zone.name + ": cerrada, el campo no está.");

                var field = zone.UnlockedRoot.GetComponent<PlotField>();
                Assert.IsNotNull(field, zone.name + ": el campo tiene su PlotField.");
                Assert.AreEqual(9, field.Plots.Count);
            }
            Assert.AreEqual(9, PlotCensus.Instance.Total, "Solo cuentan las parcelas del campo inicial.");

            // Cada zona tiene su portón, con su costo a la vista (el de una granja de temporada está apagado hasta que aparece).
            foreach (ZoneController zone in zones)
            {
                ZoneUnlockPad gate = GateOf(zone.Zone.Id);
                Assert.AreEqual(zone.Zone.RequiredSeason == 0, gate.gameObject.activeSelf, zone.name + ": portón.");
                Assert.IsTrue(gate.GetComponent<BoxCollider>().isTrigger);
                Assert.IsNotNull(gate.GetComponent<InteractionTrigger>());
                if (zone.Zone.RequiredSeason > 0) continue;   // apagado: todavía no armó su cartel

                StringAssert.Contains(zone.Zone.UnlockCost.ToString(), gate.Label.text);
                StringAssert.Contains(zone.Zone.DisplayName, gate.Label.text);
            }

            // Las cinco mejoras (la quinta, el Almacén de B-01), una por pad, con su costo.
            UpgradePad[] pads = All<UpgradePad>();
            Assert.AreEqual(5, pads.Length);
            Assert.AreEqual(5, pads.Select(pad => pad.Upgrade.Kind).Distinct().Count(), "Un pad por tipo de mejora.");
            Assert.AreEqual(5, pads.Select(pad => pad.Upgrade.Id).Distinct().Count());
            foreach (UpgradePad pad in pads)
            {
                Assert.IsTrue(pad.GetComponent<BoxCollider>().isTrigger);
                Assert.IsNotNull(pad.GetComponent<InteractionTrigger>());
                StringAssert.Contains(pad.Upgrade.CostOfLevel(1).ToString(), pad.Label.text);
                StringAssert.Contains(pad.Upgrade.DisplayName, pad.Label.text);
            }

            // Ningún trigger se superpone con otro (pads, parcelas del campo inicial, zona de venta) ni con el punto de aparición.
            var triggers = new List<Collider>();
            foreach (PurchasePad pad in All<PurchasePad>()) triggers.Add(pad.GetComponent<Collider>());
            triggers.Add(Object.FindAnyObjectByType<SellZone>().GetComponent<Collider>());
            foreach (PlotView plot in Object.FindAnyObjectByType<PlotField>().Plots) triggers.Add(plot.GetComponent<Collider>());
            for (int i = 0; i < triggers.Count; i++)
            {
                for (int j = i + 1; j < triggers.Count; j++)
                {
                    Assert.IsFalse(triggers[i].bounds.Intersects(triggers[j].bounds), triggers[i].name + " se superpone con " + triggers[j].name);
                }
            }

            GameObject player = GameObject.FindWithTag("Player");
            foreach (Collider trigger in triggers)
            {
                Assert.IsFalse(trigger.bounds.Contains(player.transform.position + Vector3.up), "El jugador no aparece encima de " + trigger.name);
            }

            // El jugador camina a la velocidad base y el panel de debug cuenta el progreso.
            Assert.AreEqual(5f, player.GetComponent<PlayerMovement>().EffectiveMoveSpeed, 1e-4f);
            string debug = FieldIntegrationTests.FindDebugText().text;
            StringAssert.Contains("Progreso: zonas 0/5", debug);
            StringAssert.Contains("Moch 0/3", debug);   // F-04: Mochila (30 lugares) y Velocidad (tope x1,5) se quedan en 3 niveles
            StringAssert.Contains("Vel 0/3", debug);
            StringAssert.Contains("Crec 0/6", debug);
            StringAssert.Contains("Prec 0/6", debug);
        }

        // Los tests de nivel de la escena real usan RealXp: tiene que coincidir con el levelBaseXp del GameConfig del juego (B-02).
        [UnityTest]
        public IEnumerator TheRealLevelCurve_MatchesRealXp()
        {
            yield return GameBoot.BootToReady();
            yield return null;

            ProgressionManager.Instance.GetLevelProgress(out long intoLevel, out long neededForNext);
            Assert.AreEqual(0L, intoLevel);
            Assert.AreEqual(RealXp.ForLevel(2), neededForNext, "RealXp.BaseXp no coincide con el levelBaseXp del GameConfig: ¿corriste Aplicar balance?");
        }

        // F-02: la fila norte. Cada zona se abre, muestra su campo de 9 parcelas y cada fila tiene el cultivo que pide el diseño.
        [UnityTest]
        public IEnumerator TheNorthernZones_Open_WithTheirCrops_OnTheSecondRow()
        {
            yield return GameBoot.BootToReady();
            yield return null;
            GameManager.Instance.StartPlaying();   // los pagos solo valen en juego

            ProgressionManager progression = ProgressionManager.Instance;
            var expected = new (string zone, string[] rows)[]
            {
                ("zone_north", new[] { "corn", "corn", "eggplant" }),
                ("zone_northwest", new[] { "eggplant", "watermelon", "watermelon" }),
                ("zone_northeast", new[] { "watermelon", "sunflower", "sunflower" }),
            };

            int total = PlotCensus.Instance.Total;
            foreach ((string zoneId, string[] rows) in expected)
            {
                ZoneController controller = ZoneControllerOf(zoneId);
                Assert.AreEqual(19f, controller.UnlockedRoot.transform.position.z, 1e-3f, zoneId + ": va en la segunda fila.");
                Assert.LessOrEqual(Vector3.Distance(GateOf(zoneId).transform.position, controller.UnlockedRoot.transform.position), 6.1f, zoneId + ": su portón queda junto al campo.");

                EconomyManager.Instance.AddCoins(controller.Zone.UnlockCost);
                progression.TryPay(controller.Zone, controller.Zone.UnlockCost);
                yield return WaitFor(() => !controller.IsRevealing, 3f);

                Assert.IsTrue(progression.IsZoneUnlocked(controller.Zone), zoneId + ": se abrió.");
                Assert.IsTrue(controller.UnlockedRoot.activeSelf, zoneId + ": aparece el campo.");
                Assert.IsFalse(controller.LockedRoot.activeSelf, zoneId + ": el terreno cerrado se va.");

                IReadOnlyList<PlotView> plots = controller.UnlockedRoot.GetComponent<PlotField>().Plots;
                Assert.AreEqual(9, plots.Count, zoneId);
                for (int i = 0; i < plots.Count; i++)
                {
                    Assert.AreEqual(rows[i / 3], plots[i].Crop.Id, zoneId + ": cultivo de la parcela " + i);
                }

                total += 9;
                Assert.AreEqual(total, PlotCensus.Instance.Total, zoneId + ": sus parcelas entran al censo.");
            }

            Assert.AreEqual(3, progression.UnlockedZoneCount);
        }

        [UnityTest]
        public IEnumerator TheJourney_SellBuyAZoneAndThreeUpgrades_AndSeeEachEffect_NeverInstantiating()
        {
            yield return GameBoot.BootToReady();
            yield return null;
            GameManager.Instance.StartPlaying();   // lo mismo que el toque en "Tap to Play"

            var field = Object.FindAnyObjectByType<PlotField>();
            IReadOnlyList<PlotView> plots = field.Plots;
            var sellZone = Object.FindAnyObjectByType<SellZone>();
            var feedback = sellZone.GetComponent<SellFeedback>();
            GameObject player = GameObject.FindWithTag("Player");
            var view = player.GetComponent<BackpackStackView>();
            var movement = player.GetComponent<PlayerMovement>();
            IReadOnlyInventory backpack = BackpackManager.Instance.Backpack;
            EconomyManager economy = EconomyManager.Instance;
            ProgressionManager progression = ProgressionManager.Instance;

            ZoneController east = ZoneControllerOf("zone_east");
            UpgradePad pricePad = PadOf(UpgradeKind.SellPrice);
            UpgradePad capacityPad = PadOf(UpgradeKind.BackpackCapacity);
            UpgradePad speedPad = PadOf(UpgradeKind.MoveSpeed);
            CropData wheat = plots[0].Crop;
            int baseCapacity = backpack.Capacity;

            // Lo que existe antes de jugar: nada nuevo después (plantas, productos y monedas, también los de las zonas cerradas).
            var createdBefore = new Dictionary<GameObject, int>();
            foreach (PlotView plot in All<PlotView>())
            {
                foreach (GameObject prefab in new[] { plot.Crop.PlantPrefab, plot.Crop.ProductPrefab })
                {
                    PoolManager.Instance.TryGetStats(prefab, out PoolStats stats);
                    createdBefore[prefab] = stats.Created;
                }
            }
            PoolManager.Instance.TryGetStats(feedback.CoinPrefab, out PoolStats coinStats);
            createdBefore[feedback.CoinPrefab] = coinStats.Created;

            // 1) Se vende: una cosecha de trigo, caminando, y al pad. Sin mejoras paga el precio base.
            yield return FieldIntegrationTests.WalkTo(player, plots[1].transform.position);   // siembra
            plots[1].Tick(100f);                                                                 // madura; el jugador sigue encima
            yield return WaitFor(() => backpack.Count >= 1, 4f);
            Assert.GreaterOrEqual(backpack.Count, 1, "Se cosechó el trigo.");
            yield return FieldIntegrationTests.WalkTo(player, sellZone.transform.position);
            yield return WaitFor(() => backpack.Count == 0, 4f);
            Assert.AreEqual(0, backpack.Count);
            Assert.AreEqual(wheat.SellPrice, economy.Balance, "Vender paga el precio base: todavía no hay mejoras.");

            // 2) Con monedas de sobra (por la vía de la economía, como el botón +1000) se compran cuatro cosas, de a una.
            economy.AddCoins(3000);   // alcanza para la zona y las tres mejoras (1.540 con el balance de la Fase 7)
            long balance = economy.Balance;

            // 2a) La zona del Este: se llega a su portón, gotea y se abre.
            ZoneUnlockPad gate = GateOf("zone_east");
            long cost = gate.Zone.UnlockCost;
            yield return FieldIntegrationTests.WalkTo(player, gate.transform.position);
            yield return WaitFor(() => progression.IsZoneUnlocked(gate.Zone));
            Assert.IsTrue(progression.IsZoneUnlocked(gate.Zone), "Parado en el portón la zona se abre.");
            Assert.AreEqual(balance - cost, economy.Balance, "La billetera pagó exactamente el costo de la zona.");
            yield return WaitFor(() => !east.IsRevealing, 3f);
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.IsTrue(east.UnlockedRoot.activeSelf, "Aparece el campo.");
            Assert.IsFalse(east.LockedRoot.activeSelf, "El terreno cerrado se va.");
            Assert.IsFalse(gate.gameObject.activeSelf, "El portón ya no hace falta.");
            Assert.AreEqual(18, PlotCensus.Instance.Total, "Las 9 parcelas nuevas se suman al censo.");
            StringAssert.Contains("zonas 1/5", FieldIntegrationTests.FindDebugText().text);

            // Y el campo nuevo se puede usar: se siembra con una planta del pool.
            PlotView eastPlot = east.UnlockedRoot.GetComponent<PlotField>().Plots[0];
            yield return FieldIntegrationTests.WalkTo(player, eastPlot.transform.position);
            Assert.AreEqual(PlotState.Growing, eastPlot.State, "Pisar una parcela nueva la siembra.");
            Assert.IsNotNull(eastPlot.Plant);

            // 2b) Mejora de precio: el multiplicador llega al PriceCalculator.
            balance = economy.Balance;
            cost = pricePad.Upgrade.CostOfLevel(1);
            yield return FieldIntegrationTests.WalkTo(player, pricePad.transform.position);
            yield return WaitFor(() => progression.LevelOf(pricePad.Upgrade) == 1);
            Assert.AreEqual(1, progression.LevelOf(pricePad.Upgrade));
            Assert.AreEqual(balance - cost, economy.Balance);
            Assert.AreEqual(pricePad.Upgrade.ValueAtLevel(1), economy.Pricing.UpgradeMultiplier, 1e-5f);
            Assert.Greater(economy.Pricing.FinalPrice(wheat.SellPrice), wheat.SellPrice, "El trigo ya paga más que el precio base.");

            // ...y se nota en la venta: otra cosecha, y el pad paga el precio mejorado de cada producto.
            plots[1].Tick(100f);
            yield return FieldIntegrationTests.WalkTo(player, plots[1].transform.position);
            yield return WaitFor(() => backpack.Count >= 1, 4f);
            Assert.GreaterOrEqual(backpack.Count, 1);
            long expectedCoins = 0;
            foreach (CropData crop in backpack.Items) expectedCoins += economy.Pricing.FinalPrice(crop.SellPrice);
            long soldBefore = economy.SoldCoins;
            yield return FieldIntegrationTests.WalkTo(player, sellZone.transform.position);
            yield return WaitFor(() => backpack.Count == 0, 4f);
            Assert.AreEqual(expectedCoins, economy.SoldCoins - soldBefore, "La venta pagó con el multiplicador de la mejora.");

            // 2c) Mejora de capacidad: la mochila crece y la pila acompaña.
            balance = economy.Balance;
            cost = capacityPad.Upgrade.CostOfLevel(1);
            yield return FieldIntegrationTests.WalkTo(player, capacityPad.transform.position);
            yield return WaitFor(() => progression.LevelOf(capacityPad.Upgrade) == 1);
            Assert.AreEqual(balance - cost, economy.Balance);
            Assert.AreEqual(baseCapacity + Mathf.RoundToInt(capacityPad.Upgrade.ValueAtLevel(1)), backpack.Capacity, "La capacidad sube lo que dice la mejora.");
            Assert.AreEqual(backpack.Count, view.ItemCount, "La pila acompaña a la mochila.");

            // 2d) Mejora de velocidad: el jugador camina más rápido (llega por Managers, no por una referencia directa).
            balance = economy.Balance;
            cost = speedPad.Upgrade.CostOfLevel(1);
            yield return FieldIntegrationTests.WalkTo(player, speedPad.transform.position);
            yield return WaitFor(() => progression.LevelOf(speedPad.Upgrade) == 1);
            Assert.AreEqual(balance - cost, economy.Balance);
            Assert.AreEqual(5f * speedPad.Upgrade.ValueAtLevel(1), movement.EffectiveMoveSpeed, 1e-4f);

            // 3) El panel de debug lo cuenta.
            string debug = FieldIntegrationTests.FindDebugText().text;
            StringAssert.Contains("zonas 1/5", debug);
            StringAssert.Contains("Moch 1/3", debug);
            StringAssert.Contains("Vel 1/3", debug);
            StringAssert.Contains("Prec 1/6", debug);
            StringAssert.Contains("Crec 0/6", debug);

            // 4) Ni una planta, un producto o una moneda creados durante el juego (ni al abrir la zona ni al crecer la mochila).
            yield return new WaitForSecondsRealtime(1f);
            foreach (KeyValuePair<GameObject, int> entry in createdBefore)
            {
                Assert.IsTrue(PoolManager.Instance.TryGetStats(entry.Key, out PoolStats stats));
                Assert.AreEqual(entry.Value, stats.Created, "Se crearon instancias de " + entry.Key.name + " durante el juego.");
                Assert.AreEqual(0, stats.Misses);
            }
        }

        [UnityTest]
        public IEnumerator WithTheRealPlayer_PauseAndAdFreezeThePurchases_AndLeavingStopsThePayment()
        {
            yield return GameBoot.BootToReady();
            yield return null;
            GameManager.Instance.StartPlaying();

            GameObject player = GameObject.FindWithTag("Player");
            EconomyManager economy = EconomyManager.Instance;
            ProgressionManager progression = ProgressionManager.Instance;
            UpgradePad pad = PadOf(UpgradeKind.GrowthSpeed);   // la más barata (110)
            UpgradeData upgrade = pad.Upgrade;
            economy.AddCoins(1000);

            // Se pisa el pad y se pausa antes de que termine el pago: la compra se congela.
            yield return FieldIntegrationTests.WalkTo(player, pad.transform.position);
            yield return WaitFor(() => progression.PaidFor(upgrade) > 0L, 3f);
            Assert.Greater(progression.PaidFor(upgrade), 0L, "Parado en el pad, el pago empezó a gotear.");

            GameManager.Instance.Pause();
            long paid = progression.PaidFor(upgrade);
            long balance = economy.Balance;
            yield return new WaitForSecondsRealtime(0.8f);
            Assert.AreEqual(paid, progression.PaidFor(upgrade), "En pausa el pago no avanza.");
            Assert.AreEqual(balance, economy.Balance);
            GameManager.Instance.Resume();

            GameManager.Instance.BeginAd();
            yield return new WaitForSecondsRealtime(0.8f);
            Assert.AreEqual(paid, progression.PaidFor(upgrade), "Con un anuncio en pantalla tampoco.");
            Assert.AreEqual(balance, economy.Balance);
            GameManager.Instance.EndAd();

            // Irse del pad corta el pago (con lo que ya se pagó, si no llegó al costo, guardado).
            yield return FieldIntegrationTests.WalkTo(player, new Vector3(0f, 0f, 0f));
            yield return new WaitForSecondsRealtime(0.3f);
            long afterLeaving = progression.PaidFor(upgrade);
            long balanceAfterLeaving = economy.Balance;
            yield return new WaitForSecondsRealtime(0.8f);
            Assert.AreEqual(afterLeaving, progression.PaidFor(upgrade), "Lejos del pad no se sigue pagando.");
            Assert.AreEqual(balanceAfterLeaving, economy.Balance);
        }
    }
}
