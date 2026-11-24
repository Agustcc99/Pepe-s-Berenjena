using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace FarmIdle.Tests
{
    // La Fase 8 en el MainLevel real, arrancando desde Bootstrap como el juego (con almacenamiento en memoria y el anuncio simulado):
    // ver un video da el Turbo o el fertilizante, el buff vence solo y se guarda, el fertilizante hace crecer todo sin Instantiate y
    // respeta las zonas cerradas, los pads y el símbolo de oferta se manejan parado encima, y un archivo de la Fase 7 sigue cargando.
    public class RewardsIntegrationTests
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

        private static MockAdService Mock => (MockAdService)AdsManager.Instance.Service;
        private static RewardData Turbo => OfferManager.Instance.Reward;
        private static GameObject Player => GameObject.FindWithTag("Player");

        // Los pads fijos de video (D-14: fertilizante y Turbo), no el símbolo del pool (otro tipo).
        private static RewardPad VideoPadOf(RewardKind kind)
        {
            return All<RewardPad>().First(pad => pad.GetType() == typeof(RewardPad) && pad.Reward.Kind == kind);
        }

        // El video de fertilizante: la recompensa que trae su pad fijo.
        private static RewardData FertilizerReward => VideoPadOf(RewardKind.Fertilizer).Reward;

        // La fila del buff en el HUD ("Turbo  29 s"); el cartel del pad fijo de Turbo (D-14) también dice "Turbo", por eso los dos espacios.
        private static TMP_Text TurboHudRow()
        {
            return TextContaining("Turbo  ");
        }

        private static T PadOf<T>() where T : DwellPad
        {
            return All<T>().First(pad => pad.GetType() == typeof(T));
        }

        private static IEnumerator BootAndPlay(MemorySaveStorage storage = null)
        {
            yield return storage != null ? GameBoot.BootToReady(storage) : GameBoot.BootToReady();
            yield return null;
            GameManager.Instance.StartPlaying();
            Mock.DurationSeconds = 0.3f;
        }

        // Camina a un pad de la fila del fertilizante (z = -16,9) rodeando la plaza de mejoras y los pads de perk: pasar por encima de
        // un pad de compra de camino "gotea" monedas (con razón) y ensuciaría las cuentas de la prueba. El corredor de x = 9 y el de
        // z = -20,5 no tocan ningún trigger.
        private static IEnumerator WalkToFertilizerRow(Vector3 pad)
        {
            yield return FieldIntegrationTests.WalkTo(Player, new Vector3(9f, 0f, 0f));
            yield return FieldIntegrationTests.WalkTo(Player, new Vector3(9f, 0f, -20.5f));
            yield return FieldIntegrationTests.WalkTo(Player, new Vector3(pad.x, 0f, -20.5f));
            yield return FieldIntegrationTests.WalkTo(Player, pad);
        }

        private static TMP_Text TextContaining(string fragment)
        {
            return All<TMP_Text>().FirstOrDefault(label => label != null && label.gameObject.activeInHierarchy && label.text != null && label.text.Contains(fragment));
        }

        // ---------- Los servicios y la escena ----------

        [UnityTest]
        public IEnumerator TheGame_BootsWithTheNewServices_InOrder_AndTheRewardsAreLinked()
        {
            yield return GameBoot.BootToReady();
            yield return null;

            Assert.IsNotNull(BuffManager.Instance);
            Assert.IsNotNull(FertilizerManager.Instance);
            Assert.IsNotNull(AdsManager.Instance);
            Assert.IsNotNull(OfferManager.Instance);
            Assert.AreEqual("Mock", AdsManager.Instance.Service.Name, "En el Editor el anuncio es el simulado.");
            Assert.IsNotNull(Turbo);
            Assert.AreEqual(RewardKind.MoveSpeedBuff, Turbo.Kind);
            Assert.AreEqual(RewardKind.Fertilizer, FertilizerReward.Kind);

            Transform services = GameObject.Find("[Services]").transform;
            string[] order = Enumerable.Range(0, services.childCount).Select(i => services.GetChild(i).name).ToArray();
            CollectionAssert.AreEqual(new[]
            {
                "GameManager", "PoolManager", "EconomyManager", "PlotCensus", "BackpackManager", "ProgressionManager",
                "BuffManager", "FertilizerManager", "PrestigeManager", "OfflineManager", "MissionManager", "AchievementManager", "DailyRewardManager", "DecorationManager", "HelperManager", "SkinManager", "OrdersManager", "DailyCropManager", "TruckManager", "PlotStateManager", "SettingsManager", "TutorialManager", "SaveManager", "AdsManager", "OfferManager",
                "AudioManager", "HapticsManager", "AnalyticsManager", "PortalManager"
            }, order);

            // Las secciones nuevas se registraron después de las existentes (y el SaveManager las tiene todas): economía, progreso,
            // mochila, buffs y fertilizante (Fase 8) más ajustes y guía (Fase 9), prestigio, offline, misiones, logros y premio diario.
            Assert.AreEqual(18, SaveManager.Instance.SaveableCount);   // F-18: más la sección "orders"; B-11: más "plots"; F-26: más "truck"
        }

        [UnityTest]
        public IEnumerator TheLevel_HasTheFertilizerPads_TheSlots_AndTheHud_WithoutOverlappingAnything()
        {
            yield return GameBoot.BootToReady();
            yield return null;

            Assert.IsNotNull(PadOf<FertilizerUsePad>());
            Assert.IsNotNull(PadOf<FertilizerBuyPad>());
            RewardPad video = VideoPadOf(RewardKind.Fertilizer);
            RewardPad turbo = VideoPadOf(RewardKind.MoveSpeedBuff);
            Assert.AreSame(FertilizerReward, video.Reward);
            Assert.AreSame(Turbo, turbo.Reward, "El pad fijo de Turbo da el mismo Turbo que el símbolo.");
            foreach (DwellPad pad in new DwellPad[] { PadOf<FertilizerUsePad>(), PadOf<FertilizerBuyPad>(), video, turbo })
            {
                Assert.IsTrue(pad.GetComponent<BoxCollider>().isTrigger);
                Assert.IsNotNull(pad.GetComponent<InteractionTrigger>());
                Assert.IsNotNull(pad.Label);
            }

            StringAssert.Contains("Usar fertilizante", PadOf<FertilizerUsePad>().Label.text);
            StringAssert.Contains(FertilizerManager.Instance.Price.ToString(), PadOf<FertilizerBuyPad>().Label.text);
            StringAssert.Contains("Video: +1 fertilizante", video.Label.text);
            StringAssert.Contains("Video: Turbo", turbo.Label.text);

            // Ningún trigger nuevo (pads y lugares del símbolo) se superpone con los demás.
            var triggers = new List<Collider>();
            foreach (PurchasePad pad in All<PurchasePad>()) triggers.Add(pad.GetComponent<Collider>());
            foreach (PerkPad pad in All<PerkPad>()) triggers.Add(pad.GetComponent<Collider>());
            foreach (DwellPad pad in All<DwellPad>().Where(p => p.gameObject.scene.IsValid() && p.GetType() != typeof(RewardSymbol))) triggers.Add(pad.GetComponent<Collider>());
            triggers.Add(Object.FindAnyObjectByType<SellZone>().GetComponent<Collider>());
            foreach (PlotField field in All<PlotField>())
            {
                foreach (PlotView plot in field.Plots) triggers.Add(plot.GetComponent<Collider>());
            }

            OfferSpawner spawner = Object.FindAnyObjectByType<OfferSpawner>();
            Assert.AreEqual(4, spawner.SlotCount);
            for (int slot = 0; slot < spawner.SlotCount; slot++)
            {
                // El símbolo mide 2,2 x 2 x 2,2 de trigger: el lugar tiene que estar libre para ese tamaño.
                var bounds = new Bounds(spawner.Slot(slot).position + new Vector3(0f, 1f, 0f), new Vector3(2.2f, 2f, 2.2f));
                foreach (Collider other in triggers)
                {
                    Assert.IsFalse(bounds.Intersects(other.bounds), "El lugar " + slot + " del símbolo se superpone con " + other.name);
                }
            }

            for (int i = 0; i < triggers.Count; i++)
            {
                for (int j = i + 1; j < triggers.Count; j++)
                {
                    Assert.IsFalse(triggers[i].bounds.Intersects(triggers[j].bounds), triggers[i].name + " se superpone con " + triggers[j].name);
                }
            }
        }

        // ---------- Ver un video ----------

        [UnityTest]
        public IEnumerator WatchingAVideo_GivesTheTurbo_WhichShowsInTheHud_AndIsSavedAtOnce()
        {
            var storage = new MemorySaveStorage();
            yield return BootAndPlay(storage);

            Assert.IsNull(TurboHudRow(), "Sin buff no hay fila en el HUD.");
            float walk = Player.GetComponent<PlayerMovement>().EffectiveMoveSpeed;
            Assert.AreEqual(5f, walk, 1e-4f, "Velocidad base de partida nueva.");

            Assert.AreEqual(AdRequestStatus.Started, AdsManager.Instance.TryRequest(Turbo));
            Assert.AreEqual(GameState.AdShowing, GameManager.Instance.State);
            Assert.AreEqual(0f, Time.timeScale);
            yield return null;
            var overlay = Object.FindAnyObjectByType<AdShowingView>();
            Assert.AreEqual(1f, overlay.GetComponent<CanvasGroup>().alpha, "La capa de anuncio tapa el juego.");

            yield return WaitFor(() => GameManager.Instance.State == GameState.Playing);
            yield return null;

            Assert.IsTrue(BuffManager.Instance.IsActive(Turbo));
            Assert.AreEqual(0f, overlay.GetComponent<CanvasGroup>().alpha, "Y se va al terminar.");
            Assert.AreEqual(11f, Player.GetComponent<PlayerMovement>().EffectiveMoveSpeed, 1e-4f, "5 u/s x3 = 15, acotado a 11.");
            Assert.IsNotNull(TurboHudRow(), "El HUD muestra el buff con sus segundos.");
            StringAssert.Contains("Buffs: Turbo", FieldIntegrationTests.FindDebugText().text);

            // Se guardó en el acto (no esperó los 3 s del guardado diferido).
            storage.TryRead(SaveFile.MainSlot, out string text);
            SaveData data = SaveSerializer.Parse(text, SaveMigrator.Default).Data;
            Assert.IsNotNull(data.Find("buffs"), "La sección de buffs está en el archivo.");
            StringAssert.Contains(Turbo.Id, data.Find("buffs").json);
        }

        // D-14: en la web el símbolo flotante está apagado, así que el Turbo por video se pide desde su pad fijo de la plaza.
        [UnityTest]
        public IEnumerator StandingOnTheTurboPad_AsksForTheVideo_AndGivesTheTurbo_AndSharesTheCooldownWithTheSymbol()
        {
            yield return BootAndPlay();

            RewardPad pad = VideoPadOf(RewardKind.MoveSpeedBuff);
            Assert.IsFalse(BuffManager.Instance.IsActive(Turbo));
            Assert.AreEqual(0f, AdsManager.Instance.RewardCooldownRemaining(Turbo));

            yield return WalkToFertilizerRow(pad.transform.position);
            yield return WaitFor(() => GameManager.Instance.State == GameState.AdShowing, 4f);
            Assert.AreEqual(GameState.AdShowing, GameManager.Instance.State, "Quedarse parado pide el video.");

            yield return WaitFor(() => GameManager.Instance.State == GameState.Playing);
            yield return null;

            Assert.IsTrue(BuffManager.Instance.IsActive(Turbo));
            Assert.Greater(AdsManager.Instance.RewardCooldownRemaining(Turbo), 0f, "El enfriamiento del Turbo es el mismo que el del símbolo.");
        }

        [UnityTest]
        public IEnumerator WatchingAVideo_SkippedOrFailed_GivesNothing_AndTheLevelKeepsRunning()
        {
            yield return BootAndPlay();

            Mock.DurationSeconds = 0.1f;

            // Un error al cargar/mostrar: no da nada, vuelve a Playing y NO activa el enfriamiento entre anuncios (nunca se vio uno).
            Mock.NextResult = AdResult.Failed;
            Assert.AreEqual(AdRequestStatus.Started, AdsManager.Instance.TryRequest(Turbo));
            yield return WaitFor(() => GameManager.Instance.State == GameState.Playing);
            Assert.AreEqual(GameState.Playing, GameManager.Instance.State, "Error");
            Assert.IsFalse(BuffManager.Instance.HasAnyBuff, "Un error no da recompensa.");
            Assert.AreEqual(5f, Player.GetComponent<PlayerMovement>().EffectiveMoveSpeed, 1e-4f);
            Assert.AreEqual(0f, AdsManager.Instance.AdCooldownRemaining());

            // Sin anuncio disponible: ni siquiera empieza.
            Mock.Available = false;
            Assert.AreEqual(AdRequestStatus.NoAd, AdsManager.Instance.TryRequest(Turbo));
            Assert.AreEqual(GameState.Playing, GameManager.Instance.State);
            Mock.Available = true;

            // Cerrado/saltado antes de terminar: no da nada, vuelve a Playing y SÍ activa el enfriamiento entre anuncios.
            Mock.NextResult = AdResult.Closed;
            Assert.AreEqual(AdRequestStatus.Started, AdsManager.Instance.TryRequest(Turbo));
            yield return WaitFor(() => GameManager.Instance.State == GameState.Playing);
            Assert.AreEqual(GameState.Playing, GameManager.Instance.State, "Saltado");
            Assert.IsFalse(BuffManager.Instance.HasAnyBuff, "Saltar el anuncio no da recompensa.");
            Assert.AreEqual(5f, Player.GetComponent<PlayerMovement>().EffectiveMoveSpeed, 1e-4f);
            Assert.Greater(AdsManager.Instance.AdCooldownRemaining(), 10f, "Hay un mínimo entre anuncios (20 s de reloj real).");
            Assert.AreEqual(AdRequestStatus.Cooldown, AdsManager.Instance.TryRequest(Turbo));
        }

        [UnityTest]
        public IEnumerator TheTurbo_NumbersForANewAndAMaxedPlayer()
        {
            // Jugador casi al máximo: mejora de velocidad nivel 3 (x1,30) y 10 perks de velocidad (+15 %) = x1,495 permanente.
            var progress = new ProgressData
            {
                upgradeLevels = new[] { new UpgradeLevelEntry { id = "upgrade_speed", level = 3 } },
                xp = 13000,   // nivel 12: 11 puntos
                perkPicks = new[] { new PerkPickEntry { id = "perk_speed", picks = 10 } }
            };
            var data = new SaveData();
            data.Set("economy", "{\"version\":1,\"coins\":0}");
            data.Set("progress", JsonUtility.ToJson(progress));
            var storage = new MemorySaveStorage();
            storage.Put(SaveFile.MainSlot, SaveSerializer.Serialize(data));

            yield return BootAndPlay(storage);

            PlayerMovement movement = Player.GetComponent<PlayerMovement>();
            Assert.AreEqual(5f * 1.3f * 1.15f, movement.EffectiveMoveSpeed, 1e-3f, "Permanente casi máximo: ~7,5 u/s.");

            AdsManager.Instance.TryRequest(Turbo);
            yield return WaitFor(() => GameManager.Instance.State == GameState.Playing);

            Assert.AreEqual(11f, movement.EffectiveMoveSpeed, 1e-4f, "Con el buff: 11 u/s en vez de ~22.");
        }

        // ---------- Cerrar y volver a abrir ----------

        [UnityTest]
        public IEnumerator ClosingAndReopening_KeepsTheTurboAndTheFertilizerStock()
        {
            var storage = new MemorySaveStorage();
            yield return BootAndPlay(storage);

            AdsManager.Instance.TryRequest(Turbo);
            yield return WaitFor(() => GameManager.Instance.State == GameState.Playing);
            FertilizerManager.Instance.DebugGive(2);
            yield return new WaitForSecondsRealtime(0.5f);

            float remaining = BuffManager.Instance.RemainingOf(Turbo);
            Assert.Less(remaining, Turbo.DurationSeconds);
            Assert.Greater(remaining, 0f);
            Assert.IsTrue(SaveManager.Instance.SaveNow(force: true));

            // Se cierra el juego y se vuelve a abrir con el MISMO almacenamiento.
            yield return GameBoot.CleanUp();
            yield return GameBoot.BootToReady(storage);
            yield return null;

            Assert.AreEqual(SaveLoadStatus.Loaded, SaveManager.Instance.LoadStatus);
            Assert.IsTrue(BuffManager.Instance.IsActive(Turbo), "El buff sigue activo.");
            Assert.AreEqual(remaining, BuffManager.Instance.RemainingOf(Turbo), 0.01f, "Con los segundos que le quedaban: en el menú (Ready) el tiempo no corre.");
            Assert.AreEqual(2, FertilizerManager.Instance.Stock);
            Assert.IsNotNull(TurboHudRow());

            GameManager.Instance.StartPlaying();
            Assert.AreEqual(11f, Player.GetComponent<PlayerMovement>().EffectiveMoveSpeed, 1e-4f);
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.Less(BuffManager.Instance.RemainingOf(Turbo), remaining, "Ya jugando, sigue envejeciendo.");
        }

        [UnityTest]
        public IEnumerator ASavedTurbo_ExpiresByItself_AfterReopening()
        {
            var storage = new MemorySaveStorage();
            var data = new SaveData();
            data.Set("buffs", "{\"version\":1,\"buffs\":[{\"id\":\"reward_turbo\",\"remaining\":0.8}]}");
            storage.Put(SaveFile.MainSlot, SaveSerializer.Serialize(data));

            yield return BootAndPlay(storage);

            Assert.IsTrue(BuffManager.Instance.IsActive(Turbo), "Cargó con 0,8 s.");
            yield return WaitFor(() => !BuffManager.Instance.HasAnyBuff, 6f);

            Assert.IsFalse(BuffManager.Instance.HasAnyBuff);
            Assert.AreEqual(5f, Player.GetComponent<PlayerMovement>().EffectiveMoveSpeed, 1e-4f);
        }

        [UnityTest]
        public IEnumerator AFileFromPhaseSeven_WithoutTheNewSections_StillLoads_AndKeepsUnknownSections()
        {
            var data = new SaveData();
            data.Set("economy", "{\"version\":1,\"coins\":321}");
            data.Set("backpack", "{\"version\":1,\"crops\":[\"wheat\"]}");
            data.Set("progress", JsonUtility.ToJson(new ProgressData { xp = RealXp.ForLevel(2) + 5 }));
            data.Set("audio", "{\"version\":1,\"volume\":0.5}");   // una sección de una fase futura: se conserva tal cual
            var storage = new MemorySaveStorage();
            storage.Put(SaveFile.MainSlot, SaveSerializer.Serialize(data));

            yield return BootAndPlay(storage);

            Assert.AreEqual(SaveLoadStatus.Loaded, SaveManager.Instance.LoadStatus);
            Assert.AreEqual(321L, EconomyManager.Instance.Balance);
            Assert.AreEqual(1, BackpackManager.Instance.Backpack.Count);
            Assert.AreEqual(2, ProgressionManager.Instance.Level);
            Assert.IsFalse(BuffManager.Instance.HasAnyBuff, "Sin sección de buffs: ninguno.");
            Assert.AreEqual(0, FertilizerManager.Instance.Stock, "Sin sección de fertilizante: 0.");

            FertilizerManager.Instance.DebugGive(1);
            Assert.IsTrue(SaveManager.Instance.SaveNow());

            storage.TryRead(SaveFile.MainSlot, out string text);
            SaveData written = SaveSerializer.Parse(text, SaveMigrator.Default).Data;
            Assert.IsNotNull(written.Find("economy"));
            Assert.IsNotNull(written.Find("progress"));
            Assert.IsNotNull(written.Find("backpack"));
            Assert.IsNotNull(written.Find("buffs"));
            Assert.IsNotNull(written.Find("fertilizer"));
            StringAssert.Contains("\"stock\":1", written.Find("fertilizer").json);
            Assert.AreEqual("{\"version\":1,\"volume\":0.5}", written.Find("audio").json, "La sección desconocida se reescribe igual.");
            Assert.AreEqual(SaveData.CurrentVersion, written.version, "El formato del sobre no cambió.");
        }

        // ---------- Fertilizante ----------

        [UnityTest]
        public IEnumerator Fertilizer_GrowsEveryOpenField_AtOnce_WithoutInstantiate_AndSkipsTheClosedZones()
        {
            yield return BootAndPlay();

            var main = All<PlotField>().First(field => field.gameObject.activeInHierarchy);
            ZoneController east = All<ZoneController>().First(zone => zone.Zone.Id == "zone_east");
            Assert.IsFalse(east.UnlockedRoot.activeSelf, "La zona del Este está cerrada.");

            // Siembra el campo inicial a mano (por la misma vía que al pisarlo).
            foreach (PlotView plot in main.Plots) Assert.IsTrue(plot.TrySow());
            Assert.AreEqual(9, PlotCensus.Instance.Growing);

            var before = new Dictionary<GameObject, PoolStats>();
            foreach (PlotView plot in All<PlotView>())
            {
                Assert.IsTrue(PoolManager.Instance.TryGetStats(plot.Crop.PlantPrefab, out PoolStats stats));
                before[plot.Crop.PlantPrefab] = stats;
            }

            FertilizerManager fertilizer = FertilizerManager.Instance;
            Assert.AreEqual(0, fertilizer.TryUse(), "Sin stock no hace nada.");
            fertilizer.DebugGive(1);

            Assert.AreEqual(9, fertilizer.GrowingCount, "Solo cuentan los campos abiertos.");
            Assert.AreEqual(9, fertilizer.TryUse(), "Terminó de crecer todo el campo de golpe.");

            Assert.AreEqual(0, fertilizer.Stock);
            Assert.AreEqual(9, PlotCensus.Instance.Ready);
            Assert.AreEqual(0, PlotCensus.Instance.Growing);
            foreach (PlotView plot in main.Plots) Assert.AreEqual(PlotState.Ready, plot.State);

            // La zona cerrada no se tocó.
            foreach (PlotView plot in east.UnlockedRoot.GetComponentsInChildren<PlotView>(true))
            {
                Assert.AreEqual(PlotState.Empty, plot.State, "Una zona cerrada no se fertiliza: " + plot.name);
                Assert.IsNull(plot.Plant);
            }

            // Se abre la zona: sus parcelas aparecen (sin Instantiate), se siembran y el siguiente fertilizante las alcanza.
            EconomyManager.Instance.AddCoins(east.Zone.UnlockCost);
            Assert.AreEqual(east.Zone.UnlockCost, ProgressionManager.Instance.TryPay(east.Zone, east.Zone.UnlockCost));
            Assert.IsTrue(east.UnlockedRoot.activeSelf);
            yield return null;

            foreach (PlotView plot in east.UnlockedRoot.GetComponentsInChildren<PlotView>(true)) Assert.IsTrue(plot.TrySow());
            fertilizer.DebugGive(1);
            Assert.AreEqual(9, fertilizer.GrowingCount, "Ahora el campo del Este está abierto y creciendo.");
            Assert.AreEqual(9, fertilizer.TryUse());
            Assert.AreEqual(18, PlotCensus.Instance.Ready);

            foreach (KeyValuePair<GameObject, PoolStats> entry in before)
            {
                Assert.IsTrue(PoolManager.Instance.TryGetStats(entry.Key, out PoolStats now));
                Assert.AreEqual(entry.Value.Created, now.Created, "El fertilizante no hizo Instantiate: " + entry.Key.name);
                Assert.AreEqual(0, now.Misses);
            }
        }

        [UnityTest]
        public IEnumerator TheUsePad_UsesAFertilizer_ByStandingOnIt_OncePerVisit_AndNeverOnNothing()
        {
            yield return BootAndPlay();
            FertilizerUsePad pad = PadOf<FertilizerUsePad>();
            PlotField main = All<PlotField>().First(field => field.gameObject.activeInHierarchy);
            FertilizerManager fertilizer = FertilizerManager.Instance;

            fertilizer.DebugGive(2);

            // Nada creciendo: pararse no gasta nada.
            yield return FieldIntegrationTests.WalkTo(Player, pad.transform.position);
            yield return new WaitForSecondsRealtime(1f);
            Assert.AreEqual(2, fertilizer.Stock, "No se desperdicia con el campo vacío.");
            StringAssert.Contains("Nada creciendo", pad.Label.text);

            // Con plantas creciendo, al irse y volver a pararse actúa (una vez por visita).
            yield return FieldIntegrationTests.WalkTo(Player, pad.transform.position + new Vector3(0f, 0f, 4f));
            foreach (PlotView plot in main.Plots) plot.TrySow();
            yield return FieldIntegrationTests.WalkTo(Player, pad.transform.position);
            yield return WaitFor(() => fertilizer.Stock < 2, 4f);

            Assert.AreEqual(1, fertilizer.Stock, "Gastó UNA unidad.");
            Assert.AreEqual(9, PlotCensus.Instance.Ready, "Todo el campo quedó listo.");

            // Sigue parado, ahora sin nada creciendo: no gasta la segunda.
            foreach (PlotView plot in main.Plots) plot.TryHarvest();
            foreach (PlotView plot in main.Plots) plot.TrySow();
            yield return new WaitForSecondsRealtime(1.2f);
            Assert.AreEqual(1, fertilizer.Stock, "Una sola acción por visita.");
        }

        [UnityTest]
        public IEnumerator TheBuyPad_PriceRises_WhenAZoneOpens_AndTheLabelFollows()
        {
            yield return BootAndPlay();
            FertilizerManager fertilizer = FertilizerManager.Instance;
            ProgressionManager progression = ProgressionManager.Instance;
            FertilizerBuyPad buy = PadOf<FertilizerBuyPad>();

            long before = fertilizer.Price;
            StringAssert.Contains(before.ToString(), buy.Label.text);

            ZoneData locked = null;
            foreach (ZoneData zone in progression.Zones)
            {
                if (zone != null && !progression.IsZoneUnlocked(zone)) { locked = zone; break; }
            }

            Assert.IsNotNull(locked, "Una partida nueva tiene zonas por abrir.");
            int zonesBefore = progression.UnlockedZoneCount;
            EconomyManager.Instance.AddCoins(locked.UnlockCost);
            progression.TryPay(locked, locked.UnlockCost);
            Assert.AreEqual(zonesBefore + 1, progression.UnlockedZoneCount);

            // El cartel mira su estado unas 5 veces por segundo (DwellPad): se espera a que se repinte.
            yield return WaitFor(() => buy.Label.text.Contains(fertilizer.Price.ToString()), 2f);

            yield return null;
            yield return null;
            Assert.Greater(fertilizer.Price, before, "B-08: abrir una zona encarece el fertilizante.");
            StringAssert.Contains(fertilizer.Price.ToString(), buy.Label.text, "El cartel del pad muestra el precio nuevo.");
        }

        [UnityTest]
        public IEnumerator TheBuyPad_SpendsCoins_OnceNotPerFrame_AndTheVideoPadGivesOneFromAnAd()
        {
            yield return BootAndPlay();
            FertilizerManager fertilizer = FertilizerManager.Instance;
            long price = fertilizer.Price;
            EconomyManager.Instance.AddCoins(price * 3);

            FertilizerBuyPad buy = PadOf<FertilizerBuyPad>();
            yield return WalkToFertilizerRow(buy.transform.position);
            yield return WaitFor(() => fertilizer.Stock >= 1, 4f);
            yield return new WaitForSecondsRealtime(1f);

            Assert.AreEqual(1, fertilizer.Stock, "Una compra por visita aunque se quede parado.");
            Assert.AreEqual(price * 2, EconomyManager.Instance.Balance, "Cobró una sola vez.");

            // El video: parado en su pad, pide el anuncio simulado y al completarlo suma uno.
            RewardPad video = VideoPadOf(RewardKind.Fertilizer);
            yield return FieldIntegrationTests.WalkTo(Player, new Vector3(buy.transform.position.x, 0f, -20.5f));
            yield return FieldIntegrationTests.WalkTo(Player, new Vector3(video.transform.position.x, 0f, -20.5f));
            yield return FieldIntegrationTests.WalkTo(Player, video.transform.position);
            yield return WaitFor(() => GameManager.Instance.State == GameState.AdShowing, 4f);
            Assert.AreEqual(GameState.AdShowing, GameManager.Instance.State, "Quedarse parado pide el video.");

            yield return WaitFor(() => GameManager.Instance.State == GameState.Playing);
            Assert.AreEqual(2, fertilizer.Stock);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Crossing_ThePads_WithoutStopping_DoesNotAskForAVideoOrSpendAnything()
        {
            yield return BootAndPlay();
            EconomyManager.Instance.AddCoins(1000);

            // Cruzar de lado a lado la fila de pads del fertilizante sin detenerse.
            Vector3 left = VideoPadOf(RewardKind.MoveSpeedBuff).transform.position + new Vector3(-1.8f, 0f, 0f);
            Vector3 right = PadOf<FertilizerBuyPad>().transform.position + new Vector3(1.8f, 0f, 0f);
            yield return FieldIntegrationTests.WalkTo(Player, left);
            yield return FieldIntegrationTests.WalkTo(Player, right);

            Assert.AreEqual(GameState.Playing, GameManager.Instance.State, "No se pidió ningún video por rozar el pad.");
            Assert.AreEqual(0, FertilizerManager.Instance.Stock);
            Assert.AreEqual(1000L, EconomyManager.Instance.Balance);
        }

        // ---------- El símbolo de oferta ----------

        [UnityTest]
        public IEnumerator TheOfferSymbol_AppearsFromThePool_AndTakingItGivesTheTurbo()
        {
            yield return BootAndPlay();
            OfferSpawner spawner = Object.FindAnyObjectByType<OfferSpawner>();
            OfferManager offers = OfferManager.Instance;

            Assert.IsTrue(PoolManager.Instance.TryGetStats(spawner.SymbolPrefab, out PoolStats prewarmed));
            Assert.AreEqual(1, prewarmed.Created, "El símbolo se precarga al cargar el nivel.");
            Assert.IsNull(spawner.ActiveSymbol);
            Assert.IsFalse(offers.HasOffer);

            offers.ForceOfferSoon();
            yield return WaitFor(() => offers.HasOffer, 3f);

            Assert.IsTrue(offers.HasOffer);
            RewardSymbol symbol = spawner.ActiveSymbol;
            Assert.IsNotNull(symbol, "El símbolo salió del pool.");
            Assert.AreSame(Turbo, symbol.Reward);
            Assert.AreEqual(spawner.Slot(offers.CurrentSlot).position.x, symbol.transform.position.x, 1e-4f, "Está en el lugar sorteado.");
            Assert.IsTrue(PoolManager.Instance.TryGetStats(spawner.SymbolPrefab, out PoolStats shown));
            Assert.AreEqual(1, shown.Created, "Sin Instantiate.");
            Assert.AreEqual(0, shown.Misses);
            Assert.AreEqual(1, shown.Active);
            yield return null;
            StringAssert.Contains("Turbo", symbol.Label.text);

            // Se lo toma parado al lado: pide el video, y al completarlo el turbo queda activo y el símbolo se va.
            yield return FieldIntegrationTests.WalkTo(Player, symbol.transform.position);
            yield return WaitFor(() => GameManager.Instance.State == GameState.AdShowing, 4f);
            Assert.AreEqual(GameState.AdShowing, GameManager.Instance.State);
            yield return WaitFor(() => GameManager.Instance.State == GameState.Playing);

            Assert.IsTrue(BuffManager.Instance.IsActive(Turbo));
            Assert.IsFalse(offers.HasOffer, "Tomado: el símbolo se fue.");
            Assert.IsNull(spawner.ActiveSymbol);
            Assert.IsTrue(PoolManager.Instance.TryGetStats(spawner.SymbolPrefab, out PoolStats after));
            Assert.AreEqual(0, after.Active, "Volvió al pool.");
            Assert.AreEqual(1, after.Created);
        }

        [UnityTest]
        public IEnumerator TheOfferSymbol_NeverAppears_WhileTheBuffIsActive_OrAnAdIsShowing()
        {
            yield return BootAndPlay();
            OfferManager offers = OfferManager.Instance;

            BuffManager.Instance.Activate(Turbo);
            offers.ForceOfferSoon();
            yield return new WaitForSecondsRealtime(0.8f);
            Assert.IsFalse(offers.HasOffer, "Con el buff activo no se ofrece otro.");

            BuffManager.Instance.ClearAll();
            Mock.HoldOpen = true;
            Assert.AreEqual(AdRequestStatus.Started, AdsManager.Instance.TryRequest(FertilizerReward));
            yield return new WaitForSecondsRealtime(0.6f);
            Assert.IsFalse(offers.HasOffer, "Con un anuncio en pantalla tampoco.");

            Mock.CompleteNow(AdResult.Failed);   // un error no activa el enfriamiento entre anuncios (nunca se vio uno)
            yield return WaitFor(() => offers.HasOffer, 4f);
            Assert.IsTrue(offers.HasOffer, "Pasado todo, la oferta que estaba pendiente aparece.");
        }

        [UnityTest]
        public IEnumerator TheOfferSymbol_LeavesByItself_WhenItsTimeRunsOut()
        {
            yield return BootAndPlay();
            OfferSpawner spawner = Object.FindAnyObjectByType<OfferSpawner>();
            OfferManager offers = OfferManager.Instance;

            offers.ForceOfferSoon();
            yield return WaitFor(() => offers.HasOffer, 3f);
            Assert.IsNotNull(spawner.ActiveSymbol);
            Assert.Greater(offers.OfferRemaining, 20f, "Vive unos 25 s de juego.");

            bool ended = false;
            bool wasTaken = true;
            offers.OfferEnded += taken =>
            {
                ended = true;
                wasTaken = taken;
            };
            offers.CancelOffer();

            Assert.IsTrue(ended);
            Assert.IsFalse(wasTaken);
            Assert.IsNull(spawner.ActiveSymbol, "El símbolo volvió al pool.");
            Assert.Greater(offers.NextOfferIn, 100f, "La próxima oferta se sortea entre 120 y 180 s de juego.");
        }

        // ---------- Debug ----------

        [UnityTest]
        public IEnumerator TheDebugPanel_ShowsTheAdsAndBuffs_AndCyclesTheMockResult()
        {
            yield return BootAndPlay();

            string text = FieldIntegrationTests.FindDebugText().text;
            StringAssert.Contains("Anuncios: Mock", text);
            StringAssert.Contains("Buffs: ninguno", text);
            StringAssert.Contains("Fert 0/", text);
            StringAssert.Contains("oferta en", text);

            var panel = Object.FindAnyObjectByType<DebugPanel>();
            UnityEngine.UI.Button adMode = panel.GetComponentsInChildren<UnityEngine.UI.Button>(true).First(b => b.name == "Button Anuncio");
            UnityEngine.UI.Button giveFert = panel.GetComponentsInChildren<UnityEngine.UI.Button>(true).First(b => b.name == "Button +Fert");

            Assert.AreEqual(AdResult.Rewarded, Mock.NextResult);
            adMode.onClick.Invoke();
            Assert.AreEqual(AdResult.Closed, Mock.NextResult, "Saltado.");
            adMode.onClick.Invoke();
            Assert.AreEqual(AdResult.Failed, Mock.NextResult, "Error.");
            adMode.onClick.Invoke();
            Assert.IsFalse(Mock.Available, "Sin anuncio.");
            adMode.onClick.Invoke();
            Assert.IsTrue(Mock.Available);
            Assert.AreEqual(AdResult.Rewarded, Mock.NextResult, "Vuelve a completo.");

            giveFert.onClick.Invoke();
            Assert.AreEqual(1, FertilizerManager.Instance.Stock);
            yield return null;
            StringAssert.Contains("Fert 1/", FieldIntegrationTests.FindDebugText().text);
            Assert.IsNotNull(TextContaining("Fertilizante x1"), "El HUD muestra el stock.");
        }
    }
}
