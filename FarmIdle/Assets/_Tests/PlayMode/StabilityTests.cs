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
    // Estabilidad con el juego REAL (arrancando desde Bootstrap, almacenamiento en memoria), con el tiempo acelerado: cada cuadro cuenta 0,1 s de
    // juego (Time.captureDeltaTime), así que 10 minutos de juego pasan en unos segundos.
    //   - Resistencia: un jugador que cosecha, vende, compra, abre zonas y usa fertilizante durante 10 minutos de juego. Sin errores ni excepciones,
    //     sin crear objetos (nada de Instantiate), y la partida guardada "ida y vuelta" queda idéntica.
    //   - "Mono": entradas al azar (moverse, pausar, pedir videos con resultados al azar, perder el foco...) durante varios minutos y varias
    //     semillas, y el juego NUNCA queda trabado en AdShowing ni en Paused.
    public class StabilityTests
    {
        private const float FrameSeconds = 0.1f;          // lo que cuenta cada cuadro acelerado en la prueba de resistencia

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return GameBoot.CleanUp();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.captureDeltaTime = 0f;
            Application.SetStackTraceLogType(LogType.Warning, StackTraceLogType.ScriptOnly);
            Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.ScriptOnly);
            yield return GameBoot.CleanUp();
        }

        private static T[] All<T>() where T : Object
        {
            return Object.FindObjectsByType<T>(FindObjectsInactive.Include);
        }

        private static GameObject Player => GameObject.FindWithTag("Player");

        private static PlotView[] InitialPlots()
        {
            return All<PlotField>().First(field => field.gameObject.activeInHierarchy && Mathf.Abs(field.transform.position.x) < 0.1f && Mathf.Abs(field.transform.position.z - 7f) < 0.1f).Plots.ToArray();
        }

        private static int CountObjects()
        {
            return Object.FindObjectsByType<Transform>(FindObjectsInactive.Include).Length;
        }

        // Cuenta los errores y excepciones que se loguean durante la prueba (un error de gameplay no puede pasar de largo).
        private sealed class ErrorCounter : IDisposable
        {
            public readonly List<string> Errors = new List<string>();

            public ErrorCounter()
            {
                Application.logMessageReceived += OnLog;
            }

            private void OnLog(string condition, string stackTrace, LogType type)
            {
                if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) Errors.Add(type + ": " + condition);
            }

            public void Dispose()
            {
                Application.logMessageReceived -= OnLog;
            }
        }

        // Un jugador que se mueve hacia puntos del mundo con el CharacterController real.
        private sealed class Walker
        {
            private readonly CharacterController _controller;
            private readonly Transform _transform;

            public Walker(GameObject player)
            {
                _controller = player.GetComponent<CharacterController>();
                _transform = player.transform;
            }

            public Vector3 Position => _transform.position;

            // Avanza un paso hacia 'goal' (a 6 u/s); devuelve true al llegar.
            public bool Step(Vector3 goal, float dt)
            {
                Vector3 toGoal = goal - _transform.position;
                toGoal.y = 0f;
                if (toGoal.magnitude < 0.15f) return true;

                _controller.Move(Vector3.ClampMagnitude(toGoal, 6f * dt));
                return false;
            }

            public void Move(Vector3 direction, float speed, float dt)
            {
                direction.y = 0f;
                _controller.Move(direction.normalized * (speed * dt));
            }
        }

        // ---------- Resistencia ----------

        [UnityTest]
        public IEnumerator TenMinutesOfAcceleratedPlay_NoErrors_NoInstantiate_AndTheSaveRoundTripsIdentically()
        {
            yield return GameBoot.BootToReady();
            yield return null;
            GameManager.Instance.StartPlaying();
            yield return new WaitForSecondsRealtime(1f);   // calentamiento: Prewarm y primeras cargas

            var errors = new ErrorCounter();
            GameObject player = Player;
            var walker = new Walker(player);
            PlotView[] plots = InitialPlots();
            SellZone sellZone = Object.FindAnyObjectByType<SellZone>();
            UpgradePad[] upgradePads = All<UpgradePad>();
            ZoneUnlockPad[] gates = All<ZoneUnlockPad>();
            FertilizerUsePad usePad = All<FertilizerUsePad>().First();
            PerkPad[] perkPads = All<PerkPad>();
            EconomyManager economy = EconomyManager.Instance;
            int objectsBefore = CountObjects();

            // Puntos de paso del recorrido (cada tanto, una parada en un pad de compra).
            var stops = new List<Vector3>();
            foreach (PlotView plot in plots) stops.Add(plot.transform.position);
            int plotIndex = 0;
            int phase = 0;                 // 0 cosechando, 1 vendiendo, 2 comprando, 3 fertilizante
            Vector3 buyGoal = Vector3.zero;
            float waitUntil = 0f;
            float nextCoins = 20f;
            float nextFertilizer = 90f;
            int purchases = 0;
            int levelBefore = ProgressionManager.Instance.Level;

            Time.captureDeltaTime = FrameSeconds;
            float gameSeconds = 0f;
            float guard = Time.realtimeSinceStartup + 120f;

            while (gameSeconds < 600f && Time.realtimeSinceStartup < guard)
            {
                float dt = Time.deltaTime;
                gameSeconds += dt;
                IReadOnlyInventory bag = BackpackManager.Instance.Backpack;

                // Monedas "de a poco": las compras ocurren por los pads de siempre (el estrés es de los sistemas, no del balance).
                if (gameSeconds >= nextCoins)
                {
                    economy.AddCoins(250);
                    nextCoins += 20f;
                }

                if (gameSeconds >= nextFertilizer)
                {
                    FertilizerManager.Instance.DebugGive(1);
                    nextFertilizer += 90f;
                }

                if (GameManager.Instance.State == GameState.Playing && dt > 0f)
                {
                    switch (phase)
                    {
                        case 0:
                            if (walker.Step(stops[plotIndex], dt)) plotIndex = (plotIndex + 1) % stops.Count;
                            if (bag.Count >= bag.Capacity - 1 || plotIndex == 0 && bag.Count > 3) phase = 1;
                            break;

                        case 1:
                            if (walker.Step(sellZone.transform.position, dt) && bag.Count == 0)
                            {
                                // Hacia la compra más barata que alcanza: una mejora o una zona.
                                Vector3? next = CheapestAffordablePad(upgradePads, gates);
                                phase = next.HasValue ? 2 : (FertilizerManager.Instance.Stock > 0 ? 3 : 0);
                                buyGoal = next ?? usePad.transform.position;
                                waitUntil = 0f;
                            }
                            break;

                        case 2:
                            if (walker.Step(buyGoal, dt))
                            {
                                if (waitUntil <= 0f) waitUntil = gameSeconds + 3f;   // parado en el pad: gotea
                                if (gameSeconds >= waitUntil)
                                {
                                    purchases++;
                                    phase = FertilizerManager.Instance.Stock > 0 ? 3 : 0;
                                    buyGoal = usePad.transform.position;
                                    waitUntil = 0f;
                                }
                            }
                            break;

                        default:
                            if (walker.Step(buyGoal, dt))
                            {
                                if (waitUntil <= 0f) waitUntil = gameSeconds + 1.5f;
                                if (gameSeconds >= waitUntil)
                                {
                                    phase = 0;
                                    waitUntil = 0f;
                                }
                            }
                            break;
                    }
                }

                yield return null;
            }

            Time.captureDeltaTime = 0f;
            yield return null;

            long sold = economy.SoldUnits;
            int level = ProgressionManager.Instance.Level;
            int zones = ProgressionManager.Instance.UnlockedZoneCount;
            Debug.Log("[FarmIdle] Resistencia: " + gameSeconds.ToString("0") + " s de juego, vendió " + sold + " productos, nivel " + level + ", zonas " + zones + ", "
                + "mejoras " + string.Join(" ", ProgressionManager.Instance.Upgrades.Select(u => u.ShortLabel + ProgressionManager.Instance.LevelOf(u))));

            Assert.GreaterOrEqual(gameSeconds, 600f, "No se llegó a simular 10 minutos de juego en el tiempo real que se le dio.");
            CollectionAssert.IsEmpty(errors.Errors, "Hubo errores o excepciones durante la simulación.");
            errors.Dispose();
            Assert.AreEqual(GameState.Playing, GameManager.Instance.State);
            Assert.Greater(sold, 50, "El jugador de la prueba tiene que haber cosechado y vendido de verdad.");
            Assert.Greater(level, levelBefore, "Y haber subido de nivel.");
            Assert.Greater(zones + ProgressionManager.Instance.Upgrades.Sum(u => ProgressionManager.Instance.LevelOf(u)), 0, "Y haber comprado cosas.");

            // Nada de Instantiate: el mismo número de objetos y ningún pool se quedó corto.
            Assert.AreEqual(objectsBefore, CountObjects(), "Jugar 10 minutos no puede crear objetos nuevos.");
            foreach (PlotView plot in All<PlotView>())
            {
                Assert.IsTrue(PoolManager.Instance.TryGetStats(plot.Crop.PlantPrefab, out PoolStats plantStats));
                Assert.AreEqual(0, plantStats.Misses, "El pool de plantas se quedó corto: " + plot.Crop.PlantPrefab.name);
                Assert.IsTrue(PoolManager.Instance.TryGetStats(plot.Crop.ProductPrefab, out PoolStats productStats));
                Assert.AreEqual(0, productStats.Misses, "El pool de productos se quedó corto: " + plot.Crop.ProductPrefab.name);
            }

            // La partida guardada ida y vuelta: guardar, cerrar, abrir, volver a guardar, y las dos son iguales sección por sección.
            Assert.IsTrue(SaveManager.Instance.SaveNow(force: true));
            MemorySaveStorage storage = GameBoot.Storage;
            storage.TryRead(SaveFile.MainSlot, out string before);
            Dictionary<string, string> sectionsBefore = Sections(before);

            yield return GameBoot.CleanUp();
            yield return GameBoot.BootToReady(storage);
            yield return null;

            Assert.IsTrue(SaveManager.Instance.SaveNow(force: true));
            storage.TryRead(SaveFile.MainSlot, out string after);
            Dictionary<string, string> sectionsAfter = Sections(after);

            CollectionAssert.AreEquivalent(sectionsBefore.Keys, sectionsAfter.Keys);
            foreach (KeyValuePair<string, string> section in sectionsBefore)
            {
                Assert.AreEqual(section.Value, sectionsAfter[section.Key], "La sección '" + section.Key + "' cambió después de guardar, cerrar y abrir.");
            }
        }

        private static Vector3? CheapestAffordablePad(UpgradePad[] upgradePads, ZoneUnlockPad[] gates)
        {
            ProgressionManager progression = ProgressionManager.Instance;
            long balance = EconomyManager.Instance.Balance;
            Vector3? best = null;
            long bestCost = long.MaxValue;

            foreach (UpgradePad pad in upgradePads)
            {
                if (!pad.gameObject.activeInHierarchy || progression.IsMaxed(pad.Upgrade)) continue;

                long cost = progression.CostOfNextLevel(pad.Upgrade) - progression.PaidFor(pad.Upgrade);
                if (cost <= balance && cost < bestCost)
                {
                    bestCost = cost;
                    best = pad.transform.position;
                }
            }

            foreach (ZoneUnlockPad gate in gates)
            {
                if (!gate.gameObject.activeInHierarchy || progression.IsZoneUnlocked(gate.Zone)) continue;

                long cost = progression.CostOf(gate.Zone) - progression.PaidFor(gate.Zone);
                if (cost <= balance && cost < bestCost)
                {
                    bestCost = cost;
                    best = gate.transform.position;
                }
            }

            return best;
        }

        // Las secciones de una partida como "id -> json" (sin la fecha del sobre, que cambia en cada guardado).
        private static Dictionary<string, string> Sections(string text)
        {
            SaveParseResult parsed = SaveSerializer.Parse(text, SaveMigrator.Default);
            Assert.IsTrue(parsed.IsUsable, "La partida guardada no se puede leer.");
            return parsed.Data.sections.ToDictionary(section => section.id, section => section.json);
        }

        // ---------- "Mono" ----------

        [UnityTest]
        public IEnumerator TheMonkey_RandomInputsPausesAndAds_NeverLeavesTheGameStuck([Values(1, 2, 3)] int seed)
        {
            yield return GameBoot.BootToReady();
            yield return null;
            GameManager game = GameManager.Instance;
            game.StartPlaying();
            yield return new WaitForSecondsRealtime(0.5f);

            // Los avisos de transiciones rechazadas son parte del juego del mono: sin su traza de pila cada uno cuesta mucho menos.
            Application.SetStackTraceLogType(LogType.Warning, StackTraceLogType.None);
            Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);

            var errors = new ErrorCounter();
            var random = new System.Random(seed);
            var walker = new Walker(Player);
            AdsManager ads = AdsManager.Instance;
            var mock = (MockAdService)ads.Service;
            mock.DurationSeconds = 1.5f;

            // Reglas de anuncios más cortas para que en pocos minutos pasen muchas rondas Y el corte por tiempo de un anuncio colgado (20 s
            // en vez de 120; el resto de la lógica es la de siempre).
            typeof(AdsManager).GetField("_session", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(ads, new AdSession(2f, 20f));
            RewardData[] rewards = { OfferManager.Instance.Reward, All<RewardPad>().First(pad => pad.GetType() == typeof(RewardPad) && pad.Reward.Kind == RewardKind.Fertilizer).Reward };
            var cooldowns = (CooldownTracker)typeof(AdsManager).GetField("_rewardCooldown", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(ads);
            GameObject audio = AudioManager.Instance.gameObject;
            GameObject save = SaveManager.Instance.gameObject;

            float actClock = 0f;   // las acciones al azar se evalúan cada 0,1 s de tiempo real: unas 1,4 por segundo
            Vector3 direction = Vector3.forward;
            int moveFrames = 0;
            float pausedSince = -1f;
            float adSince = -1f;
            float unscaled = 0f;
            float resumeAt = 0f;
            int pauses = 0, adsRequested = 0, adsGranted = 0, focusEvents = 0;
            ads.AdFinished += (reward, result, granted) => { if (granted) adsGranted++; };

            float end = 100f;   // 100 s de tiempo real por semilla (unos 5 cortes por tiempo de un anuncio colgado, de 20 s cada uno)
            float guard = Time.realtimeSinceStartup + 130f;
            while (unscaled < end && Time.realtimeSinceStartup < guard)
            {
                unscaled += Time.unscaledDeltaTime;
                float dt = Time.deltaTime;
                actClock += Time.unscaledDeltaTime;
                bool actNow = actClock >= 0.1f;
                if (actNow) actClock = 0f;

                // El jugador al azar: cambia de rumbo cada tanto, a veces se queda quieto y a veces teletransporta cerca del campo.
                if (moveFrames-- <= 0)
                {
                    moveFrames = random.Next(3, 40);
                    direction = new Vector3((float)random.NextDouble() * 2f - 1f, 0f, (float)random.NextDouble() * 2f - 1f);
                    if (random.Next(10) == 0) direction = Vector3.zero;
                }

                if (game.State == GameState.Playing && dt > 0f)
                {
                    if (direction.sqrMagnitude > 0.0001f) walker.Move(direction, 2f + (float)random.NextDouble() * 5f, dt);
                    if (Mathf.Abs(walker.Position.x) > 18f || walker.Position.z > 24f || walker.Position.z < -24f) direction = -direction;
                }

                // Acciones al azar sobre el juego.
                int roll = actNow ? random.Next(333) : 999;   // 999: ninguna acción en este cuadro
                if (roll < 8)
                {
                    if (game.Pause()) { pauses++; resumeAt = unscaled + (float)random.NextDouble() * 2f; }
                }
                else if (roll < 12)
                {
                    game.Resume();
                }
                else if (roll < 22)
                {
                    if (random.Next(2) == 0)
                    {
                        // Sin esto casi todos los pedidos se rechazan por enfriamiento, buff activo o stock lleno: se "limpia" la mesa a veces.
                        foreach (RewardData reward in rewards) cooldowns.Start(reward.Id, 0f);
                        BuffManager.Instance.ClearAll();
                        FertilizerManager.Instance.Restore(null);
                    }

                    mock.Available = random.Next(5) != 0;
                    mock.NextResult = random.Next(3) == 0 ? AdResult.Failed : (random.Next(2) == 0 ? AdResult.Closed : AdResult.Rewarded);
                    mock.HoldOpen = random.Next(8) == 0;   // un anuncio "colgado": el corte por tiempo lo tiene que destrabar
                    if (ads.TryRequest(rewards[random.Next(rewards.Length)]) == AdRequestStatus.Started) adsRequested++;
                }
                else if (roll < 28)
                {
                    audio.SendMessage("OnApplicationFocus", false);
                    save.SendMessage("OnApplicationFocus", false);
                    audio.SendMessage("OnApplicationPause", true);
                    focusEvents++;
                }
                else if (roll < 34)
                {
                    audio.SendMessage("OnApplicationFocus", true);
                    save.SendMessage("OnApplicationFocus", true);
                    audio.SendMessage("OnApplicationPause", false);
                    focusEvents++;
                }
                else if (roll < 38)
                {
                    mock.CompleteNow(random.Next(2) == 0 ? AdResult.Rewarded : AdResult.Closed);   // un resultado fuera de tiempo
                }
                else if (roll < 42)
                {
                    EconomyManager.Instance.AddCoins(500);
                }
                else if (roll < 46)
                {
                    game.EndAd();   // un EndAd suelto (sin anuncio): no puede sacar al juego de Paused ni romper nada
                    game.StartPlaying();
                }

                // El "usuario" reanuda lo que pausó.
                if (game.State == GameState.Paused && unscaled >= resumeAt && random.Next(20) == 0) game.Resume();

                // Invariantes: nada queda trabado.
                if (game.State == GameState.Paused) { if (pausedSince < 0f) pausedSince = unscaled; }
                else pausedSince = -1f;

                bool adState = game.State == GameState.AdShowing;
                if (adState) { if (adSince < 0f) adSince = unscaled; }
                else adSince = -1f;

                if (pausedSince >= 0f) Assert.Less(unscaled - pausedSince, 60f, "El juego quedó en Paused más de 60 s (semilla " + seed + ").");
                if (adSince >= 0f) Assert.Less(unscaled - adSince, 25f, "El juego quedó en AdShowing más que el corte por tiempo de 20 s (semilla " + seed + ").");
                if (adState && !ads.IsShowing && game.State == GameState.AdShowing && adSince >= 0f && unscaled - adSince > 1f)
                {
                    Assert.Fail("AdShowing sin ningún anuncio en curso (semilla " + seed + ").");
                }

                yield return null;
            }

            // Se suelta todo y el juego tiene que volver solo a Playing.
            mock.HoldOpen = false;
            audio.SendMessage("OnApplicationFocus", true);
            audio.SendMessage("OnApplicationPause", false);
            if (game.State == GameState.Paused) game.Resume();
            float settle = unscaled + 30f;
            while (game.State != GameState.Playing && unscaled < settle && Time.realtimeSinceStartup < guard + 60f)
            {
                unscaled += Time.unscaledDeltaTime;
                if (game.State == GameState.Paused) game.Resume();
                yield return null;
            }

            Time.captureDeltaTime = 0f;
            yield return null;

            Debug.Log("[FarmIdle] Mono semilla " + seed + ": " + unscaled.ToString("0") + " s, " + pauses + " pausas, " + adsRequested + " videos pedidos (" + adsGranted
                + " con recompensa), " + focusEvents + " cambios de foco, vendió " + EconomyManager.Instance.SoldUnits + ", nivel " + ProgressionManager.Instance.Level);

            CollectionAssert.IsEmpty(errors.Errors, "Hubo errores o excepciones con entradas al azar (semilla " + seed + ").");
            errors.Dispose();
            Assert.AreEqual(GameState.Playing, game.State, "Al soltar todo, el juego vuelve a Playing (semilla " + seed + ").");
            Assert.IsFalse(ads.IsShowing, "No queda un anuncio colgado.");
            Assert.IsFalse(AudioManager.Instance.ListenerPaused, "Y el audio no queda silenciado.");
            Assert.Greater(adsRequested, 3, "El mono tiene que haber pedido videos de verdad.");
            Assert.GreaterOrEqual(unscaled, 90f, "El mono no llegó a jugar lo suficiente en el tiempo real que se le dio (semilla " + seed + ").");
        }
    }
}
