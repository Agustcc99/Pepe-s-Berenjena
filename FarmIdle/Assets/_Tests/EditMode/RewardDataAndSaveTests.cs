using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FarmIdle.Tests
{
    // Los datos de la Fase 8 (recompensas y GameConfig), el crecimiento por fertilizante en PlotLogic y la ida y vuelta de las dos
    // secciones nuevas de la partida (buffs y fertilizante) con el GameConfig real.
    public class RewardDataAndSaveTests
    {
        private GameConfig _config;
        private readonly List<GameObject> _objects = new List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            string[] guids = AssetDatabase.FindAssets("t:GameConfig");
            Assert.AreEqual(1, guids.Length);
            _config = AssetDatabase.LoadAssetAtPath<GameConfig>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _objects)
            {
                if (go != null) Object.DestroyImmediate(go);
            }

            _objects.Clear();
        }

        private T Create<T>() where T : Component
        {
            var go = new GameObject(typeof(T).Name);
            _objects.Add(go);
            return go.AddComponent<T>();
        }

        // ---------- Datos ----------

        [Test]
        public void TheConfig_HasTheTwoRewards_WithPlayableValues()
        {
            RewardData turbo = _config.FirstRewardOfKind(RewardKind.MoveSpeedBuff);
            RewardData fertilizer = _config.FirstRewardOfKind(RewardKind.Fertilizer);

            Assert.IsNotNull(turbo, "Falta la recompensa de velocidad (¿corriste FarmIdle > Fase 2 > Construir assets y escenas?).");
            Assert.IsNotNull(fertilizer, "Falta la recompensa de fertilizante.");

            Assert.AreEqual(3f, turbo.Magnitude, "El buff es x3 nominal.");
            Assert.Greater(turbo.DurationSeconds, 0f);
            Assert.IsTrue(turbo.IsBuff);
            Assert.AreEqual(1f, fertilizer.Magnitude);
            Assert.IsFalse(fertilizer.IsBuff);
            Assert.IsFalse(string.IsNullOrWhiteSpace(turbo.Placement));
            Assert.AreEqual(turbo.Id, turbo.Placement, "Sin placement propio se usa el Id.");
        }

        [Test]
        public void EveryReward_IsLinkedInTheConfig_AndFoundById()
        {
            string[] guids = AssetDatabase.FindAssets("t:RewardData");
            Assert.Greater(guids.Length, 1);

            foreach (string guid in guids)
            {
                var reward = AssetDatabase.LoadAssetAtPath<RewardData>(AssetDatabase.GUIDToAssetPath(guid));
                Assert.AreSame(reward, _config.FindReward(reward.Id), "No está en la lista de recompensas del GameConfig: " + reward.name);
                Assert.IsFalse(string.IsNullOrWhiteSpace(reward.DisplayName));
                Assert.GreaterOrEqual(reward.CooldownSeconds, 0f);
            }

            Assert.IsNull(_config.FindReward("no-existe"));
            Assert.IsNull(_config.FindReward(null));
        }

        [Test]
        public void TheSpeedNumbers_AreCoherent()
        {
            // Base 5 u/s, permanente máximo x1,5 = 7,5 u/s: el tope final tiene que dejar lugar al buff y no ser incontrolable.
            const float baseSpeed = 5f;
            float permanentMax = baseSpeed * _config.MaxMoveSpeedMultiplier;

            Assert.Greater(_config.MaxFinalMoveSpeed, permanentMax, "El tope final tiene que estar por encima de la velocidad permanente máxima.");
            Assert.LessOrEqual(_config.MaxFinalMoveSpeed, 2.5f * baseSpeed, "Más de x2,5 de la base ya es difícil de controlar en un campo de ~7 unidades.");

            float turbo = _config.FirstRewardOfKind(RewardKind.MoveSpeedBuff).Magnitude;
            Assert.AreEqual(_config.MaxFinalMoveSpeed, MoveSpeedRules.Final(baseSpeed, 1f, turbo, _config.MaxFinalMoveSpeed), 1e-4f);
            Assert.AreEqual(_config.MaxFinalMoveSpeed, MoveSpeedRules.Final(baseSpeed, _config.MaxMoveSpeedMultiplier, turbo, _config.MaxFinalMoveSpeed), 1e-4f);
        }

        // B-03: la tabla de ubicaciones de rewarded vive en los datos (una RewardData por ubicación, con su 'placement' y su enfriamiento).
        [Test]
        public void EveryRewardedPlacement_IsInTheData_WithItsOwnPlacement()
        {
            var placements = new System.Collections.Generic.HashSet<string>();
            foreach (RewardData reward in _config.Rewards)
            {
                Assert.IsTrue(placements.Add(reward.Placement), "El placement '" + reward.Placement + "' está repetido.");
                Assert.GreaterOrEqual(reward.CooldownSeconds, 0f);
            }

            Assert.IsNotNull(_config.FirstRewardOfKind(RewardKind.Fertilizer), "Ubicación: fertilizante.");
            Assert.IsNotNull(_config.FirstRewardOfKind(RewardKind.MoveSpeedBuff), "Ubicación: Turbo.");
            Assert.IsNotNull(_config.FirstRewardOfKind(RewardKind.OfflineDouble), "Ubicación: duplicar ganancia offline.");
        }

        [Test]
        public void TheAdAndOfferSettings_AreSane()
        {
            Assert.GreaterOrEqual(_config.AdMinIntervalSeconds, 180f, "B-03: mínimo ~3 min entre anuncios (el SDK de CrazyGames ignora pedidos con menos).");
            Assert.Greater(_config.AdTimeoutSeconds, 10f);
            Assert.GreaterOrEqual(_config.OfferMaxIntervalSeconds, _config.OfferMinIntervalSeconds);
            Assert.Greater(_config.OfferLifetimeSeconds, 5f, "El símbolo tiene que durar lo suficiente para llegar a él.");
            Assert.Greater(_config.FertilizerPrice, 0);
            Assert.GreaterOrEqual(_config.FertilizerMaxStock, 1);
            Assert.GreaterOrEqual(_config.FertilizerBuyMaxStock, 1);
            Assert.LessOrEqual(_config.FertilizerBuyMaxStock, _config.FertilizerMaxStock, "Comprar no puede llenar más que el video.");
            Assert.Greater(_config.FertilizerBuyCooldownSeconds, 0f, "La compra con monedas tiene enfriamiento.");
        }

        // ---------- B-08: el precio del fertilizante sube con las granjas ----------

        [Test]
        public void TheFertilizerPrice_RisesWithEveryOpenZone_AndIsRoundedToFifty()
        {
            Assert.Greater(_config.FertilizerPriceStepPerZone, 1f, "B-08: el factor por zona tiene que encarecer el fertilizante.");
            Assert.AreEqual((long)_config.FertilizerPrice, _config.FertilizerPriceFor(0), "Sin zonas abiertas es el precio base.");

            long previous = _config.FertilizerPriceFor(0);
            for (int zones = 1; zones <= _config.Zones.Count; zones++)
            {
                long price = _config.FertilizerPriceFor(zones);
                Assert.Greater(price, previous, "Cada zona abierta encarece el fertilizante (zonas: " + zones + ").");
                Assert.AreEqual(0L, price % 50L, "Redondeado a 50 (zonas: " + zones + ").");
                previous = price;
            }
        }

        [Test]
        public void TheFertilizerPrice_AtTheShippedFactor_IsTheDesignTable()
        {
            // x1,8 sobre 1.500 (B-08), redondeado a 50.
            long[] expected = { 1500, 2700, 4850, 8750, 15750, 28350 };
            Assert.AreEqual(1500, _config.FertilizerPrice);
            Assert.AreEqual(1.8f, _config.FertilizerPriceStepPerZone, 0.0001f);
            Assert.GreaterOrEqual(_config.Zones.Count + 1, expected.Length, "La tabla cubre de 0 a todas las zonas.");
            for (int zones = 0; zones < expected.Length; zones++) Assert.AreEqual(expected[zones], _config.FertilizerPriceFor(zones), "Zonas abiertas: " + zones);
        }

        [Test]
        public void TheFertilizerPrice_WithFactorOne_StaysTheBasePrice_EvenIfItIsNotARoundNumber()
        {
            var config = ScriptableObject.CreateInstance<GameConfig>();
            try
            {
                var serialized = new SerializedObject(config);
                serialized.FindProperty("fertilizerPrice").intValue = 130;
                serialized.FindProperty("fertilizerPriceStepPerZone").floatValue = 1f;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                Assert.AreEqual(130L, config.FertilizerPriceFor(0));
                Assert.AreEqual(130L, config.FertilizerPriceFor(5), "Factor 1 = precio fijo.");

                serialized.FindProperty("fertilizerPriceStepPerZone").floatValue = 2f;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                Assert.AreEqual(130L, config.FertilizerPriceFor(0), "Sin zonas no se redondea.");
                Assert.AreEqual(250L, config.FertilizerPriceFor(1), "260 se redondea a 250.");
            }
            finally
            {
                Object.DestroyImmediate(config);
            }
        }

        // ---------- PlotLogic ----------

        [Test]
        public void FinishGrowing_MakesAGrowingPlotReady_AtItsFullTime()
        {
            var plot = new PlotLogic();
            var changes = new List<string>();
            plot.StateChanged += (from, to) => changes.Add(from + ">" + to);

            plot.TryPlant(30f);
            plot.Tick(5f);
            Assert.IsTrue(plot.TryFinishGrowing());

            Assert.AreEqual(PlotState.Ready, plot.State);
            Assert.AreEqual(1f, plot.Progress);
            Assert.AreEqual(30f, plot.Elapsed, 1e-5f);
            CollectionAssert.AreEqual(new[] { "Empty>Growing", "Growing>Ready" }, changes);
        }

        [Test]
        public void FinishGrowing_DoesNothingOnAnEmptyOrReadyPlot()
        {
            var plot = new PlotLogic();
            Assert.IsFalse(plot.TryFinishGrowing(), "Vacía: el fertilizante no siembra.");

            plot.TryPlant(10f);
            plot.Tick(11f);
            Assert.AreEqual(PlotState.Ready, plot.State);
            Assert.IsFalse(plot.TryFinishGrowing(), "Ya lista: nada que terminar.");
        }

        // ---------- Partida: buffs ----------

        [Test]
        public void TheBuffsSection_RoundTrips_WithTheRemainingSeconds_AndWithoutAStartEvent()
        {
            RewardData turbo = _config.FirstRewardOfKind(RewardKind.MoveSpeedBuff);

            BuffManager first = Create<BuffManager>();
            first.Initialize(_config);
            Assert.IsTrue(first.Activate(turbo));
            string json = first.Capture();
            Assert.AreEqual("buffs", first.SaveId);

            BuffManager second = Create<BuffManager>();
            second.Initialize(_config);
            int started = 0;
            second.BuffStarted += reward => started++;
            second.Restore(json);

            Assert.IsTrue(second.IsActive(turbo));
            Assert.AreEqual(turbo.DurationSeconds, second.RemainingOf(turbo), 1e-3f);
            Assert.AreEqual(0, started, "Restaurar no es 'recién activado'.");
            Assert.AreEqual(3f, second.MoveSpeedMultiplier);
        }

        [Test]
        public void TheBuffsSection_IsTolerant_WithOldMissingUnknownOrGarbledData()
        {
            BuffManager manager = Create<BuffManager>();
            manager.Initialize(_config);
            manager.Activate(_config.FirstRewardOfKind(RewardKind.MoveSpeedBuff));

            manager.Restore(null);   // un archivo de la Fase 7 no trae la sección
            Assert.IsFalse(manager.HasAnyBuff);

            manager.Restore("{\"version\":1,\"buffs\":[{\"id\":\"ya-no-existe\",\"remaining\":10}]}");
            Assert.IsFalse(manager.HasAnyBuff, "Un buff que ya no está en el GameConfig se descarta.");

            manager.Restore("{ esto no es json");
            Assert.IsFalse(manager.HasAnyBuff);

            RewardData turbo = _config.FirstRewardOfKind(RewardKind.MoveSpeedBuff);
            manager.Restore("{\"version\":1,\"buffs\":[{\"id\":\"" + turbo.Id + "\",\"remaining\":99999}]}");
            Assert.AreEqual(turbo.DurationSeconds, manager.RemainingOf(turbo), 1e-3f, "Un archivo editado no puede dar más de lo que dura.");
        }

        [Test]
        public void TheBuffsSection_IsVolatile_OnlyWhileThereIsABuff()
        {
            BuffManager manager = Create<BuffManager>();
            manager.Initialize(_config);

            Assert.IsFalse(manager.HasVolatileState);
            manager.Activate(_config.FirstRewardOfKind(RewardKind.MoveSpeedBuff));
            Assert.IsTrue(manager.HasVolatileState, "Con un buff, el guardado al pausar tiene que escribir igual.");
        }

        [Test]
        public void ASellPriceBuff_IsApplied_ThroughThePriceCalculator_AndRemovesIt()
        {
            EconomyManager economy = Create<EconomyManager>();
            economy.Initialize(_config);
            BuffManager buffs = Create<BuffManager>();
            buffs.Initialize(_config);

            RewardData reward = ProgressionTestData.Reward("double-coins", RewardKind.SellPriceBuff, 2f, 10f);
            try
            {
                Assert.AreEqual(1f, economy.Pricing.BuffMultiplier);
                Assert.IsTrue(buffs.Activate(reward));
                Assert.AreEqual(2f, economy.Pricing.BuffMultiplier, "El precio se aplica por PriceCalculator.SetBuffMultiplier.");
                Assert.AreEqual(1f, economy.Pricing.UpgradeMultiplier, "Y sin tocar el multiplicador de mejoras.");

                buffs.ClearAll();
                Assert.AreEqual(1f, economy.Pricing.BuffMultiplier);
            }
            finally
            {
                Object.DestroyImmediate(reward);
            }
        }

        // ---------- Partida: fertilizante ----------

        [Test]
        public void TheFertilizerSection_RoundTrips_AndIsTolerant()
        {
            FertilizerManager first = Create<FertilizerManager>();
            first.Initialize(_config);
            first.DebugGive(2);
            string json = first.Capture();
            Assert.AreEqual("fertilizer", first.SaveId);

            FertilizerManager second = Create<FertilizerManager>();
            second.Initialize(_config);
            second.Restore(json);
            Assert.AreEqual(2, second.Stock);

            second.Restore(null);
            Assert.AreEqual(0, second.Stock, "Sin sección (archivo de la Fase 7): sin stock.");

            second.Restore("{\"version\":1,\"stock\":99}");
            Assert.AreEqual(_config.FertilizerMaxStock, second.Stock, "Se acota al máximo.");

            second.Restore("basura {");
            Assert.AreEqual(0, second.Stock);
        }

        private sealed class FakeField : IFertilizable
        {
            public int Growing;
            public int GrowCalls;

            public int GrowingCount => Growing;

            public int GrowAllNow()
            {
                GrowCalls++;
                int grown = Growing;
                Growing = 0;
                return grown;
            }
        }

        [Test]
        public void TryUse_SpendsOneAndGrowsEveryRegisteredField_ButNeverWastesItOnNothing()
        {
            FertilizerManager manager = Create<FertilizerManager>();
            manager.Initialize(_config);
            var open = new FakeField { Growing = 4 };
            var other = new FakeField { Growing = 3 };
            manager.Register(open);
            manager.Register(other);
            int used = -1;
            manager.Used += grown => used = grown;

            Assert.AreEqual(0, manager.TryUse(), "Sin stock no hace nada.");
            Assert.AreEqual(4, open.Growing);

            manager.DebugGive(2);
            Assert.AreEqual(7, manager.TryUse());
            Assert.AreEqual(7, used);
            Assert.AreEqual(1, manager.Stock);
            Assert.AreEqual(0, open.Growing);
            Assert.AreEqual(0, other.Growing);

            Assert.AreEqual(0, manager.TryUse(), "Nada creciendo: no se gasta la unidad.");
            Assert.AreEqual(1, manager.Stock);
        }

        [Test]
        public void AnUnregisteredField_IsLeftAlone()
        {
            FertilizerManager manager = Create<FertilizerManager>();
            manager.Initialize(_config);
            var open = new FakeField { Growing = 2 };
            var closedZone = new FakeField { Growing = 5 };
            manager.Register(open);
            manager.Register(closedZone);
            manager.Unregister(closedZone);   // la zona se cerró (campo apagado)
            manager.DebugGive(1);

            Assert.AreEqual(2, manager.TryUse());
            Assert.AreEqual(0, closedZone.GrowCalls);
            Assert.AreEqual(5, closedZone.Growing);
        }
    }
}
