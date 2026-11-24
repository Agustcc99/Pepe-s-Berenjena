using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace FarmIdle.Tests
{
    // La Fase 7 en el MainLevel real, arrancando desde Bootstrap como el juego (con un almacenamiento en memoria: nunca tocan la partida
    // del dispositivo): abrir el juego con una partida guardada, con un archivo roto, ganar un nivel, gastar los puntos parado en los
    // pads de perk y el ciclo entero "jugar, cerrar, volver a abrir".
    public class LevelAndSaveIntegrationTests
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

        private static PerkPad PerkPadOf(PerkKind kind)
        {
            return All<PerkPad>().First(pad => pad.Perk.Kind == kind);
        }

        private static TMP_Text TextContaining(string fragment)
        {
            return All<TMP_Text>().FirstOrDefault(label => label != null && label.gameObject.activeInHierarchy && label.text != null && label.text.Contains(fragment));
        }

        private static CropData CropById(string id)
        {
            return All<PlotView>().Select(plot => plot.Crop).First(crop => crop.Id == id);
        }

        // Una partida guardada armada a mano con los ids REALES del juego.
        private static MemorySaveStorage SavedGame(long coins, string[] bag, ProgressData progress)
        {
            var data = new SaveData();
            data.Set("economy", "{\"version\":1,\"coins\":" + coins + "}");
            data.Set("backpack", "{\"version\":1,\"crops\":[" + string.Join(",", bag.Select(id => "\"" + id + "\"")) + "]}");
            data.Set("progress", JsonUtility.ToJson(progress));

            var storage = new MemorySaveStorage();
            storage.Put(SaveFile.MainSlot, SaveSerializer.Serialize(data));
            return storage;
        }

        // ---------- Abrir el juego con una partida guardada ----------

        [UnityTest]
        public IEnumerator TheGame_OpensFromASavedGame_WithTheWorldAlreadyAsItWas_AndNothingInstantiated()
        {
            string[] bag = { "wheat", "carrot", "tomato", "wheat", "pumpkin", "carrot", "wheat", "tomato", "carrot", "wheat", "pumpkin", "tomato" };
            var progress = new ProgressData
            {
                unlockedZones = new[] { "zone_east" },
                upgradeLevels = new[] { new UpgradeLevelEntry { id = "upgrade_capacity", level = 1 }, new UpgradeLevelEntry { id = "upgrade_speed", level = 1 } },
                payments = new[] { new PaymentEntry { key = PlayerProgress.ZoneKey("zone_west"), coins = 700 } },
                xp = RealXp.ForLevel(5) + 100,   // nivel 5
                perkPicks = new[] { new PerkPickEntry { id = "perk_strength", picks = 2 }, new PerkPickEntry { id = "perk_speed", picks = 1 } }
            };

            yield return GameBoot.BootToReady(SavedGame(777, bag, progress));
            yield return null;

            Assert.IsNull(SaveManager.StorageForNextBoot, "El almacenamiento de prueba se consumió: el juego no usó la carpeta del dispositivo.");
            Assert.AreEqual(SaveLoadStatus.Loaded, SaveManager.Instance.LoadStatus);

            // El estado de los servicios.
            ProgressionManager progression = ProgressionManager.Instance;
            Assert.AreEqual(777L, EconomyManager.Instance.Balance);
            Assert.AreEqual(12, BackpackManager.Instance.Backpack.Count);
            Assert.AreEqual(10 + 4 + 2, BackpackManager.Instance.Backpack.Capacity, "Base + mejora de monedas + dos de Fuerza.");
            Assert.AreEqual(5, progression.Level);
            Assert.AreEqual(1, progression.PerkPoints, "4 puntos ganados, 3 gastados.");
            Assert.AreEqual(1.10f * 1.015f, progression.Modifiers.MoveSpeed, 1e-4f);

            // El mundo: la zona del Este aparece de una, sin efecto y sin su portón; la del Oeste sigue cerrada con lo pagado.
            ZoneController east = All<ZoneController>().First(zone => zone.Zone.Id == "zone_east");
            Assert.IsTrue(east.UnlockedRoot.activeSelf);
            Assert.IsFalse(east.LockedRoot.activeSelf);
            Assert.IsFalse(east.IsRevealing, "Al cargar una zona abierta no corre el efecto de apertura.");
            Assert.AreEqual(Vector3.one, east.UnlockedRoot.transform.localScale);
            Assert.IsFalse(All<ZoneUnlockPad>().First(pad => pad.Zone.Id == "zone_east").gameObject.activeSelf, "Su portón ya no hace falta.");
            ZoneUnlockPad westGate = All<ZoneUnlockPad>().First(pad => pad.Zone.Id == "zone_west");
            Assert.IsTrue(westGate.gameObject.activeSelf);
            Assert.AreEqual(700L, progression.PaidFor(westGate.Zone));
            StringAssert.Contains((westGate.Zone.UnlockCost - 700).ToString(), westGate.Label.text, "El cartel del portón muestra lo que falta.");

            // La pila de la espalda muestra la mochila, en dos columnas (16 lugares), y salió toda del pool.
            GameObject player = GameObject.FindWithTag("Player");
            var view = player.GetComponent<BackpackStackView>();
            Assert.AreEqual(12, view.ItemCount);
            Assert.AreEqual(2, view.Columns);
            foreach (CropData crop in BackpackManager.Instance.Backpack.Items.Distinct())
            {
                Assert.IsTrue(PoolManager.Instance.TryGetStats(crop.ProductPrefab, out PoolStats stats));
                Assert.AreEqual(0, stats.Misses, "Restaurar no pidió nada fuera del Prewarm.");
                Assert.AreEqual(BackpackManager.Instance.MaxCapacity + view.DepartingHeadroom, stats.Created, "Y no creó instancias de más.");
            }
            Assert.AreEqual(5f * progression.Modifiers.MoveSpeed, player.GetComponent<PlayerMovement>().EffectiveMoveSpeed, 1e-4f);

            // El HUD y el panel de debug.
            Assert.IsNotNull(TextContaining("Nv 5"), "La barra de nivel muestra el nivel guardado.");
            Assert.IsNotNull(TextContaining("1 punto"), "Y avisa del punto sin gastar.");
            string debug = FieldIntegrationTests.FindDebugText().text;
            StringAssert.Contains("Nivel: 5", debug);
            StringAssert.Contains("puntos 1", debug);
            StringAssert.Contains("Guardado: v1 Loaded", debug);
            StringAssert.Contains("zonas 1/5", debug);
        }

        [UnityTest]
        public IEnumerator TheGame_OpensFineWithADamagedSaveFile_AndKeepsWorking()
        {
            var storage = new MemorySaveStorage();
            storage.Put(SaveFile.MainSlot, "\u0000\u0000 garbage { not json");
            LogAssert.Expect(LogType.Warning, new Regex("Partida"));

            yield return GameBoot.BootToReady(storage);
            yield return null;

            Assert.AreEqual(GameState.Ready, GameManager.Instance.State, "Un archivo dañado no rompe el arranque.");
            Assert.AreEqual(SaveLoadStatus.CorruptStartedFresh, SaveManager.Instance.LoadStatus);
            Assert.AreEqual(0L, EconomyManager.Instance.Balance);
            Assert.AreEqual(0, BackpackManager.Instance.Backpack.Count);
            Assert.AreEqual(1, ProgressionManager.Instance.Level);
            Assert.IsTrue(storage.Has(SaveFile.CorruptSlot), "El archivo roto se conserva aparte.");
            StringAssert.Contains("CorruptStartedFresh", FieldIntegrationTests.FindDebugText().text);

            // Se juega y se guarda con normalidad.
            GameManager.Instance.StartPlaying();
            EconomyManager.Instance.AddCoins(25);
            Assert.IsTrue(SaveManager.Instance.SaveNow());
            Assert.IsTrue(SaveSerializer.Parse(ReadMain(storage), SaveMigrator.Default).IsUsable);
        }

        private static string ReadMain(MemorySaveStorage storage)
        {
            storage.TryRead(SaveFile.MainSlot, out string text);
            return text;
        }

        // El módulo Data del SDK de CrazyGames admite 1 MB por usuario (1.048.576 bytes de JSON en total, ROADMAP D-03) y el espejo de la
        // partida va ahí. Una partida nueva con todas las secciones registradas mide poco; si una sección nueva la hiciera crecer mucho,
        // esto avisa antes de acercarse al límite. Tope de 100 KB: diez veces menos que el límite.
        [UnityTest]
        public IEnumerator TheMainSave_IsFarBelowThePortalDataLimit()
        {
            var storage = new MemorySaveStorage();
            yield return GameBoot.BootToReady(storage);

            GameManager.Instance.StartPlaying();
            Assert.IsTrue(SaveManager.Instance.SaveNow(force: true));

            int bytes = System.Text.Encoding.UTF8.GetByteCount(ReadMain(storage));
            Assert.Less(bytes, 100 * 1024, "La partida principal mide " + bytes + " bytes; el módulo Data del portal admite 1.048.576 en total.");
        }

        [UnityTest]
        public IEnumerator ASaveFromAFutureVersion_OpensANewGame_AndKeepsTheFutureFileUntouched()
        {
            var storage = new MemorySaveStorage();
            string future = "{\"version\":" + (SaveData.CurrentVersion + 4) + ",\"sections\":[{\"id\":\"economy\",\"json\":\"{\\\"coins\\\":5}\"}]}";
            storage.Put(SaveFile.MainSlot, future);
            LogAssert.Expect(LogType.Warning, new Regex("Partida"));

            yield return GameBoot.BootToReady(storage);
            yield return null;

            Assert.AreEqual(SaveLoadStatus.FutureVersionStartedFresh, SaveManager.Instance.LoadStatus);
            Assert.AreEqual(0L, EconomyManager.Instance.Balance);
            storage.TryRead(SaveFile.FutureSlot, out string kept);
            Assert.AreEqual(future, kept);
        }

        // ---------- Subir de nivel ----------

        [UnityTest]
        public IEnumerator Selling_FillsTheXpBar_AndLevelingUpAnnouncesItAndOffersThePoint()
        {
            yield return GameBoot.BootToReady();
            yield return null;
            GameManager.Instance.StartPlaying();

            ProgressionManager progression = ProgressionManager.Instance;
            CropData pumpkin = CropById("pumpkin");
            Assert.IsNotNull(TextContaining("Nv 1"));
            Assert.IsNull(TextContaining("elegí un perk"), "Sin puntos no hay aviso.");

            var levels = new List<int>();
            progression.LevelChanged += levels.Add;

            // La XP de una venta es el precio base: se vende lo justo para llegar al nivel 2.
            for (int i = 0; i < RealXp.UnitsToReachLevel(2, pumpkin.SellPrice); i++) EconomyManager.Instance.SellProduct(pumpkin);

            Assert.AreEqual(2, progression.Level);
            CollectionAssert.AreEqual(new[] { 2 }, levels);
            yield return null;
            Assert.IsNotNull(TextContaining("Nv 2"));
            Assert.IsNotNull(TextContaining("¡Nivel 2!"), "Aparece el cartel de nivel nuevo.");
            Assert.IsNotNull(TextContaining("1 punto"), "Y el aviso del punto para gastar en la plaza.");

            // El cartel se va solo.
            yield return new WaitForSecondsRealtime(2.4f);
            Assert.IsNull(TextContaining("¡Nivel 2!"));
            Assert.IsNotNull(TextContaining("1 punto"), "El aviso del punto sigue hasta que se gaste.");
        }

        // ---------- Los pads de perk ----------

        [UnityTest]
        public IEnumerator Level_HasThePerkPads_WiredUp_AndNotOverlappingAnythingElse()
        {
            yield return GameBoot.BootToReady();
            yield return null;

            PerkPad[] pads = All<PerkPad>();
            Assert.AreEqual(5, pads.Length, "Fuerza, Velocidad y Suerte (F-24), Regateo y Mano verde (IN-04).");
            CollectionAssert.AreEquivalent(new[] { PerkKind.BackpackCapacity, PerkKind.MoveSpeed, PerkKind.RareChance, PerkKind.SellPrice, PerkKind.GrowthSpeed }, pads.Select(pad => pad.Perk.Kind));
            foreach (PerkPad pad in pads)
            {
                Assert.IsTrue(pad.GetComponent<BoxCollider>().isTrigger);
                Assert.IsNotNull(pad.GetComponent<InteractionTrigger>());
                StringAssert.Contains(pad.Perk.DisplayName, pad.Label.text);
                StringAssert.Contains("0/" + pad.Perk.MaxPicks, pad.Label.text);
            }

            var triggers = new List<Collider>();
            foreach (PurchasePad pad in All<PurchasePad>()) triggers.Add(pad.GetComponent<Collider>());
            foreach (PerkPad pad in pads) triggers.Add(pad.GetComponent<Collider>());
            triggers.Add(Object.FindAnyObjectByType<SellZone>().GetComponent<Collider>());
            foreach (PlotView plot in Object.FindAnyObjectByType<PlotField>().Plots) triggers.Add(plot.GetComponent<Collider>());
            for (int i = 0; i < triggers.Count; i++)
            {
                for (int j = i + 1; j < triggers.Count; j++)
                {
                    Assert.IsFalse(triggers[i].bounds.Intersects(triggers[j].bounds), triggers[i].name + " se superpone con " + triggers[j].name);
                }
            }
        }

        [UnityTest]
        public IEnumerator StandingOnAPerkPad_SpendsThePoints_OneByOne_WithTheRealEffect()
        {
            var progress = new ProgressData { xp = RealXp.ForLevel(5) };   // nivel 5: cuatro puntos
            yield return GameBoot.BootToReady(SavedGame(0, new string[0], progress));
            yield return null;
            GameManager.Instance.StartPlaying();

            GameObject player = GameObject.FindWithTag("Player");
            ProgressionManager progression = ProgressionManager.Instance;
            PerkPad strength = PerkPadOf(PerkKind.BackpackCapacity);
            PerkPad speed = PerkPadOf(PerkKind.MoveSpeed);
            Assert.AreEqual(4, progression.PerkPoints);

            // Cruzar el pad sin quedarse no gasta nada (hay una espera corta al llegar).
            yield return FieldIntegrationTests.WalkTo(player, strength.transform.position + new Vector3(-3f, 0f, 0f));
            Assert.AreEqual(0, progression.PicksOf(strength.Perk));

            // Parado encima gasta un punto, y mientras se quede, los siguientes de a uno.
            yield return FieldIntegrationTests.WalkTo(player, strength.transform.position);
            yield return WaitFor(() => progression.PicksOf(strength.Perk) >= 1, 3f);
            Assert.GreaterOrEqual(progression.PicksOf(strength.Perk), 1, "Parado en el pad se gastó un punto.");
            Assert.Less(progression.PicksOf(strength.Perk), 4, "Pero no todos de golpe: la elección lleva su ritmo.");

            yield return WaitFor(() => progression.PerkPoints == 0, 6f);
            Assert.AreEqual(4, progression.PicksOf(strength.Perk), "Quedándose parado, gasta los cuatro.");
            Assert.AreEqual(10 + 4, BackpackManager.Instance.Backpack.Capacity, "Fuerza: cuatro lugares más en la mochila.");
            StringAssert.Contains("4/10", strength.Label.text);
            StringAssert.Contains("Puntos: 0", strength.Label.text);

            // Sin puntos, el pad de Velocidad no hace nada.
            yield return FieldIntegrationTests.WalkTo(player, speed.transform.position);
            yield return new WaitForSecondsRealtime(1f);
            Assert.AreEqual(0, progression.PicksOf(speed.Perk));
            Assert.AreEqual(1f, progression.Modifiers.MoveSpeed);
        }

        [UnityTest]
        public IEnumerator APerkPad_WorksOnlyWhilePlaying()
        {
            var progress = new ProgressData { xp = RealXp.ForLevel(3) };   // nivel 3: dos puntos
            yield return GameBoot.BootToReady(SavedGame(0, new string[0], progress));
            yield return null;
            GameManager.Instance.StartPlaying();

            GameObject player = GameObject.FindWithTag("Player");
            ProgressionManager progression = ProgressionManager.Instance;
            PerkPad speed = PerkPadOf(PerkKind.MoveSpeed);

            yield return FieldIntegrationTests.WalkTo(player, speed.transform.position);
            GameManager.Instance.Pause();
            yield return new WaitForSecondsRealtime(1.2f);
            Assert.AreEqual(0, progression.PicksOf(speed.Perk), "En pausa el pad no gasta puntos.");

            GameManager.Instance.Resume();
            yield return WaitFor(() => progression.PerkPoints == 0, 5f);
            Assert.AreEqual(2, progression.PicksOf(speed.Perk));
            Assert.AreEqual(1.03f, progression.Modifiers.MoveSpeed, 1e-4f, "Dos elecciones de +1,5 %.");
        }

        // ---------- Jugar, cerrar y volver a abrir ----------

        [UnityTest]
        public IEnumerator PlayingThenClosingAndReopeningTheGame_BringsBackEverything()
        {
            var storage = new MemorySaveStorage();
            yield return GameBoot.BootToReady(storage);
            yield return null;
            GameManager.Instance.StartPlaying();

            EconomyManager economy = EconomyManager.Instance;
            ProgressionManager progression = ProgressionManager.Instance;
            BackpackManager backpack = BackpackManager.Instance;
            CropData wheat = CropById("wheat");
            CropData tomato = CropById("tomato");
            ZoneData east = progression.Zones.First(zone => zone.Id == "zone_east");
            UpgradeData capacity = progression.Upgrades.First(upgrade => upgrade.Kind == UpgradeKind.BackpackCapacity);

            economy.AddCoins(5000);
            Assert.AreEqual(east.UnlockCost, progression.TryPay(east, east.UnlockCost));
            progression.TryPay(capacity, capacity.CostOfLevel(1));
            long partial = progression.TryPay(progression.Zones.First(zone => zone.Id == "zone_west"), 400);
            Assert.AreEqual(400L, partial);
            CropData pumpkin = CropById("pumpkin");
            for (int i = 0; i < RealXp.UnitsToReachLevel(2, pumpkin.SellPrice); i++) economy.SellProduct(pumpkin);   // justo el nivel 2
            Assert.IsTrue(progression.TryChoosePerk(progression.Perks.First(perk => perk.Kind == PerkKind.BackpackCapacity)));
            backpack.TryAdd(wheat);
            backpack.TryAdd(tomato);
            backpack.TryAdd(wheat);

            long coins = economy.Balance;
            int capacityNow = backpack.Backpack.Capacity;
            Assert.IsTrue(SaveManager.Instance.SaveNow());

            // Se cierra el juego (los servicios y el nivel desaparecen) y se vuelve a abrir con el MISMO almacenamiento.
            yield return GameBoot.CleanUp();
            yield return GameBoot.BootToReady(storage);
            yield return null;

            Assert.AreEqual(SaveLoadStatus.Loaded, SaveManager.Instance.LoadStatus);
            economy = EconomyManager.Instance;
            progression = ProgressionManager.Instance;
            backpack = BackpackManager.Instance;
            Assert.AreEqual(coins, economy.Balance);
            Assert.AreEqual(capacityNow, backpack.Backpack.Capacity);
            CollectionAssert.AreEqual(new[] { "wheat", "tomato", "wheat" }, backpack.Backpack.Items.Select(crop => crop.Id));
            Assert.IsTrue(progression.IsZoneUnlocked(progression.Zones.First(zone => zone.Id == "zone_east")));
            Assert.AreEqual(1, progression.LevelOf(progression.Upgrades.First(upgrade => upgrade.Kind == UpgradeKind.BackpackCapacity)));
            Assert.AreEqual(400L, progression.PaidFor(progression.Zones.First(zone => zone.Id == "zone_west")), "El pago a medias.");
            Assert.AreEqual(2, progression.Level);
            Assert.AreEqual(0, progression.PerkPoints);
            Assert.AreEqual(3, GameObject.FindWithTag("Player").GetComponent<BackpackStackView>().ItemCount, "La pila de la espalda muestra la mochila.");
            Assert.IsNotNull(TextContaining("Nv 2"));
        }
    }
}
