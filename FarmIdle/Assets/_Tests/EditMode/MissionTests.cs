using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FarmIdle.Tests
{
    // Las misiones diarias (F-05): el sorteo por fecha, el premio, los datos de las plantillas y el MissionManager con un reloj de mentira
    // (avance, reclamar, cambio de día, reloj atrasado, guardado) sobre el GameConfig real.
    public class MissionTests
    {
        private static readonly DateTime Today = new DateTime(2026, 10, 26, 9, 0, 0);

        private GameConfig _config;
        private readonly List<GameObject> _objects = new List<GameObject>();

        private sealed class FakeField : IFertilizable
        {
            public int Growing = 1;
            public int GrowingCount => Growing;

            public int GrowAllNow()
            {
                int grown = Growing;
                Growing = 0;
                return grown;
            }
        }

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
                if (go != null) UnityEngine.Object.DestroyImmediate(go);
            }

            _objects.Clear();
        }

        private T Create<T>() where T : Component
        {
            var go = new GameObject(typeof(T).Name);
            _objects.Add(go);
            return go.AddComponent<T>();
        }

        // Los servicios que las misiones escuchan, en el orden del Bootstrapper, y el MissionManager con el reloj en 'Today'.
        private MissionManager CreateServices(out EconomyManager economy, out BackpackManager backpack, out ProgressionManager progression, out FertilizerManager fertilizer)
        {
            economy = Create<EconomyManager>();
            economy.Initialize(_config);
            backpack = Create<BackpackManager>();
            backpack.Initialize(_config);
            progression = Create<ProgressionManager>();
            progression.Initialize(_config);
            fertilizer = Create<FertilizerManager>();
            fertilizer.Initialize(_config);

            MissionManager missions = Create<MissionManager>();
            missions.Initialize(_config);
            DateTime clock = Today;
            missions.Clock = () => clock;
            return missions;
        }

        private int IndexOf(MissionManager missions, string id)
        {
            for (int i = 0; i < missions.Count; i++)
            {
                if (missions.DataAt(i).Id == id) return i;
            }

            return -1;
        }

        private static string SaveWith(int day, params string[] ids)
        {
            string entries = string.Join(",", ids.Select(id => "{\"id\":\"" + id + "\",\"progress\":0,\"claimed\":false}"));
            return "{\"version\":1,\"day\":" + day + ",\"missions\":[" + entries + "]}";
        }

        // ---------- Las cuentas ----------

        [Test]
        public void TheDayKey_OrdersDatesAsNumbers()
        {
            Assert.AreEqual(20261026, MissionRules.DayKey(new DateTime(2026, 10, 26, 23, 59, 0)));
            Assert.Greater(MissionRules.DayKey(new DateTime(2027, 1, 1)), MissionRules.DayKey(new DateTime(2026, 12, 31)));
            Assert.Greater(MissionRules.DayKey(new DateTime(2026, 11, 1)), MissionRules.DayKey(new DateTime(2026, 10, 31)));
        }

        [Test]
        public void ThePicks_DependOnlyOnTheDate_NotOnTheOrderOfThePool()
        {
            int day = MissionRules.DayKey(Today);
            List<MissionData> first = MissionRules.Pick(_config.Missions, 10, day, 3);
            List<MissionData> again = MissionRules.Pick(_config.Missions, 10, day, 3);
            List<MissionData> reversed = MissionRules.Pick(_config.Missions.Reverse().ToList(), 10, day, 3);

            CollectionAssert.AreEqual(first, again, "El mismo día salen las mismas.");
            CollectionAssert.AreEqual(first, reversed, "Y no importa en qué orden estén escritas en el GameConfig.");
            Assert.AreEqual(3, first.Count);
            Assert.AreEqual(3, first.Distinct().Count());
        }

        [Test]
        public void ThePicks_ChangeFromOneDayToTheNext_AndPreferDifferentKinds()
        {
            var seen = new HashSet<string>();
            for (int d = 0; d < 20; d++)
            {
                List<MissionData> picked = MissionRules.Pick(_config.Missions, 10, MissionRules.DayKey(Today.AddDays(d)), 3);
                seen.Add(string.Join(",", picked.Select(m => m.Id)));
                Assert.AreEqual(3, picked.Select(m => m.Kind).Distinct().Count(), "Una de cada tipo: no tres 'cosechá' el mismo día.");
            }

            Assert.Greater(seen.Count, 5, "En 20 días no puede tocar siempre lo mismo.");
        }

        [Test]
        public void ANewPlayer_OnlyGetsTheMissionsTheLevelAllows()
        {
            List<MissionData> picked = MissionRules.Pick(_config.Missions, 1, MissionRules.DayKey(Today), 3);

            Assert.AreEqual(3, picked.Count, "Con el nivel 1 alcanza para 3 misiones.");
            Assert.IsTrue(picked.All(m => m.MinLevel <= 1));
            CollectionAssert.AreEquivalent(new[] { MissionKind.Harvest, MissionKind.Sell, MissionKind.Buy }, picked.Select(m => m.Kind));
        }

        [Test]
        public void ThePick_IsTolerant_WithEmptyOrShortPools()
        {
            Assert.IsEmpty(MissionRules.Pick(null, 5, 20261026, 3));
            Assert.IsEmpty(MissionRules.Pick(new List<MissionData>(), 5, 20261026, 3));
            Assert.AreEqual(1, MissionRules.Pick(_config.Missions.Take(1).ToList(), 99, 20261026, 3).Count, "Con una sola plantilla, una sola misión.");
        }

        [Test]
        public void TheCoinsReward_ScalesWithProduction_WithAFloor()
        {
            Assert.AreEqual(600L, MissionRules.Coins(2d, 5d, 25L), "5 min x 2 monedas/s.");
            Assert.AreEqual(25L, MissionRules.Coins(0d, 5d, 25L), "Sin producción, el mínimo.");
            Assert.AreEqual(25L, MissionRules.Coins(0.01d, 5d, 25L), "Poco rendimiento: el mínimo.");
            Assert.AreEqual(25L, MissionRules.Coins(double.NaN, 5d, 25L));
            Assert.AreEqual(25L, MissionRules.Coins(-3d, 5d, 25L));
            Assert.AreEqual(long.MaxValue, MissionRules.Coins(double.MaxValue, 5d, 25L), "Sin desbordar.");
        }

        // ---------- Los datos ----------

        [Test]
        public void TheMissionTemplates_ArePlayable_AndLinkedInTheConfig()
        {
            Assert.GreaterOrEqual(_config.Missions.Count, 6, "Faltan plantillas (¿corriste FarmIdle > Fase 2 > Construir assets y escenas?).");
            Assert.AreEqual(3, _config.MissionsPerDay);

            string[] guids = AssetDatabase.FindAssets("t:MissionData");
            Assert.AreEqual(guids.Length, _config.Missions.Count, "El GameConfig tiene que listar todas las plantillas del proyecto.");
            Assert.AreEqual(_config.Missions.Count, _config.Missions.Select(m => m.Id).Distinct().Count());

            foreach (MissionData mission in _config.Missions)
            {
                Assert.GreaterOrEqual(mission.Target, 1, mission.Id);
                Assert.GreaterOrEqual(mission.MinLevel, 1, mission.Id);
                Assert.IsTrue(mission.RewardMinutes > 0f || mission.RewardFertilizer > 0, "Una misión sin premio no tiene sentido: " + mission.Id);
                Assert.AreSame(mission, _config.FindMission(mission.Id));
                StringAssert.Contains(mission.Target.ToString(), mission.Describe(), "El texto lleva la meta: " + mission.Id);
            }

            Assert.IsNull(_config.FindMission("no-existe"));
            Assert.IsNull(_config.FindMission(null));
        }

        // ---------- El manager ----------

        [Test]
        public void TheDay_GeneratesThreeMissions_OnceADay()
        {
            MissionManager missions = CreateServices(out _, out _, out _, out _);
            Assert.AreEqual(0, missions.Count, "Antes de jugar no hay nada.");

            missions.EnsureToday();
            Assert.AreEqual(3, missions.Count);
            Assert.AreEqual(MissionRules.DayKey(Today), missions.Day);
            string first = string.Join(",", Enumerable.Range(0, 3).Select(i => missions.DataAt(i).Id));

            missions.EnsureToday();
            Assert.AreEqual(first, string.Join(",", Enumerable.Range(0, 3).Select(i => missions.DataAt(i).Id)), "El mismo día no se vuelve a sortear.");
        }

        [Test]
        public void ANewDay_RegeneratesTheMissions_ButAClockGoneBackDoesNot()
        {
            MissionManager missions = CreateServices(out EconomyManager economy, out BackpackManager backpack, out _, out _);
            DateTime clock = Today;
            missions.Clock = () => clock;
            missions.EnsureToday();

            int harvest = IndexOf(missions, "mission_harvest_small");
            Assert.GreaterOrEqual(harvest, 0, "En el nivel 1 siempre sale esta.");
            Assert.IsTrue(backpack.TryAdd(_config.Crops[0]));
            Assert.AreEqual(1, missions.ProgressAt(harvest));
            int day = missions.Day;

            clock = Today.AddDays(-3);   // atrasó la hora: se conservan las de hoy, con su avance
            missions.EnsureToday();
            Assert.AreEqual(day, missions.Day);
            Assert.AreEqual(1, missions.ProgressAt(IndexOf(missions, "mission_harvest_small")));

            clock = Today.AddHours(10);   // la misma tarde: nada
            missions.EnsureToday();
            Assert.AreEqual(day, missions.Day);

            clock = Today.AddDays(1);
            missions.EnsureToday();
            Assert.AreEqual(day + 1, missions.Day, "Día nuevo: misiones nuevas.");
            Assert.AreEqual(3, missions.Count);
            for (int i = 0; i < missions.Count; i++) Assert.AreEqual(0, missions.ProgressAt(i), "Empiezan de cero.");
        }

        [Test]
        public void TheProgress_CountsHarvests_Sales_AndPurchases_AndCapsAtTheTarget()
        {
            MissionManager missions = CreateServices(out EconomyManager economy, out BackpackManager backpack, out ProgressionManager progression, out _);
            missions.EnsureToday();
            var completed = new List<string>();
            missions.Completed += mission => completed.Add(mission.Id);

            int harvest = IndexOf(missions, "mission_harvest_small");
            int sell = IndexOf(missions, "mission_sell_small");
            int buy = IndexOf(missions, "mission_buy_one");
            Assert.That(new[] { harvest, sell, buy }, Has.All.GreaterThanOrEqualTo(0));

            // Cosechar: entra a la mochila (la mochila se vacía para que haya lugar).
            CropData crop = _config.Crops[0];
            for (int i = 0; i < 30; i++)
            {
                Assert.IsTrue(backpack.TryAdd(crop));
                backpack.TryPop(out _);
            }

            Assert.AreEqual(25, missions.ProgressAt(harvest), "Se acota a la meta.");
            Assert.IsTrue(missions.IsCompleteAt(harvest));

            // Vender.
            for (int i = 0; i < 29; i++) economy.SellProduct(crop);
            Assert.AreEqual(29, missions.ProgressAt(sell));
            Assert.IsFalse(missions.IsCompleteAt(sell));
            economy.SellProduct(crop);
            Assert.IsTrue(missions.IsCompleteAt(sell));

            // Comprar una mejora.
            UpgradeData growth = _config.Upgrades.First(u => u.Kind == UpgradeKind.GrowthSpeed);
            economy.AddCoins(growth.CostOfLevel(1));
            Assert.Greater(progression.TryPay(growth, growth.CostOfLevel(1)), 0L);
            Assert.IsTrue(missions.IsCompleteAt(buy));

            CollectionAssert.AreEquivalent(new[] { "mission_harvest_small", "mission_sell_small", "mission_buy_one" }, completed, "Cada una avisa una sola vez.");
            Assert.AreEqual(3, missions.ClaimableCount);
        }

        [Test]
        public void LoadingAGame_DoesNotCountAsPlaying()
        {
            MissionManager missions = CreateServices(out _, out BackpackManager backpack, out ProgressionManager progression, out _);
            missions.Restore(SaveWith(MissionRules.DayKey(Today), "mission_harvest_small", "mission_buy_one"));

            // Restaurar la mochila y el progreso son reemplazos completos: no son cosechas ni compras.
            backpack.Restore("{\"version\":1,\"crops\":[\"" + _config.Crops[0].Id + "\",\"" + _config.Crops[0].Id + "\"]}");
            progression.Restore(new ProgressData { unlockedZones = new[] { _config.Zones[0].Id } });

            Assert.AreEqual(0, missions.ProgressAt(IndexOf(missions, "mission_harvest_small")));
            Assert.AreEqual(0, missions.ProgressAt(IndexOf(missions, "mission_buy_one")));
        }

        [Test]
        public void Claiming_PaysOnce_AndOnlyWhenComplete()
        {
            MissionManager missions = CreateServices(out EconomyManager economy, out BackpackManager backpack, out _, out _);
            missions.EnsureToday();
            int harvest = IndexOf(missions, "mission_harvest_small");
            var claims = new List<string>();
            missions.Claimed += (mission, coins, fertilizer) => claims.Add(mission.Id + ":" + coins + ":" + fertilizer);
            long start = economy.Balance;

            Assert.IsFalse(missions.TryClaim(harvest), "Todavía no está completa.");
            Assert.IsFalse(missions.TryClaim(-1));
            Assert.IsFalse(missions.TryClaim(99));

            CropData crop = _config.Crops[0];
            for (int i = 0; i < 25; i++)
            {
                backpack.TryAdd(crop);
                backpack.TryPop(out _);
            }

            long expected = missions.RewardCoinsAt(harvest);
            Assert.AreEqual(_config.MissionMinCoins, expected, "Sin parcelas abiertas registradas, vale el mínimo.");
            Assert.IsTrue(missions.TryClaim(harvest));
            Assert.AreEqual(start + expected, economy.Balance);
            Assert.IsTrue(missions.IsClaimedAt(harvest));
            Assert.IsFalse(missions.TryClaim(harvest), "Ya se reclamó.");
            Assert.AreEqual(start + expected, economy.Balance);
            CollectionAssert.AreEqual(new[] { "mission_harvest_small:" + expected + ":0" }, claims);
            Assert.AreEqual(0, missions.ClaimableCount);
        }

        [Test]
        public void TheCoinsReward_FollowsWhatTheFieldsProduceToday()
        {
            MissionManager missions = CreateServices(out EconomyManager economy, out _, out _, out _);
            OfflineManager offline = Create<OfflineManager>();
            offline.Initialize(_config);
            offline.Register(new OfflineTestSource { Rate = 2f });
            missions.EnsureToday();

            int index = IndexOf(missions, "mission_harvest_small");
            MissionData data = missions.DataAt(index);
            Assert.AreEqual(MissionRules.Coins(2d, data.RewardMinutes * _config.MissionRewardScale, _config.MissionMinCoins), missions.RewardCoinsAt(index));
            Assert.Greater(missions.RewardCoinsAt(index), _config.MissionMinCoins);

            economy.Pricing.SetUpgradeMultiplier(2f);   // con mejoras la misma misión paga más
            Assert.AreEqual(MissionRules.Coins(4d, data.RewardMinutes * _config.MissionRewardScale, _config.MissionMinCoins), missions.RewardCoinsAt(index));
        }

        private sealed class OfflineTestSource : IProductionSource
        {
            public float Rate;
            public float BaseCoinsPerSecond => Rate;
        }

        [Test]
        public void AFertilizerMission_PaysFertilizer_ButCoinsWhenTheStockIsFull()
        {
            MissionManager missions = CreateServices(out EconomyManager economy, out _, out _, out FertilizerManager fertilizer);
            missions.Restore(SaveWith(MissionRules.DayKey(Today), "mission_harvest_big"));
            Assert.AreEqual(1, missions.Count);

            // Completa a mano (120 cosechas) con el progreso guardado.
            missions.Restore("{\"version\":1,\"day\":" + MissionRules.DayKey(Today) + ",\"missions\":[{\"id\":\"mission_harvest_big\",\"progress\":120,\"claimed\":false}]}");
            Assert.IsTrue(missions.IsCompleteAt(0));
            Assert.AreEqual(1, missions.RewardFertilizerAt(0));
            Assert.AreEqual(0L, missions.RewardCoinsAt(0));

            Assert.IsTrue(missions.TryClaim(0));
            Assert.AreEqual(1, fertilizer.Stock);

            // Con el stock lleno, el premio cae a las monedas.
            fertilizer.DebugGive(99);
            missions.Restore("{\"version\":1,\"day\":" + MissionRules.DayKey(Today) + ",\"missions\":[{\"id\":\"mission_harvest_big\",\"progress\":120,\"claimed\":false}]}");
            long start = economy.Balance;
            Assert.AreEqual(0, missions.RewardFertilizerAt(0));
            Assert.Greater(missions.RewardCoinsAt(0), 0L);
            Assert.IsTrue(missions.TryClaim(0));
            Assert.Greater(economy.Balance, start);
            Assert.AreEqual(fertilizer.MaxStock, fertilizer.Stock);
        }

        [Test]
        public void UsingTheFertilizer_CountsForItsMission()
        {
            MissionManager missions = CreateServices(out _, out _, out _, out FertilizerManager fertilizer);
            missions.Restore(SaveWith(MissionRules.DayKey(Today), "mission_fertilizer"));
            fertilizer.Register(new FakeField());
            fertilizer.DebugGive(1);

            Assert.IsFalse(missions.IsCompleteAt(0));
            Assert.Greater(fertilizer.TryUse(), 0);
            Assert.IsTrue(missions.IsCompleteAt(0));
        }

        // ---------- La partida ----------

        [Test]
        public void TheMissionsSection_RoundTrips_AndIsTolerant()
        {
            MissionManager missions = CreateServices(out _, out BackpackManager backpack, out _, out _);
            Assert.AreEqual("missions", missions.SaveId);
            missions.EnsureToday();
            int harvest = IndexOf(missions, "mission_harvest_small");
            for (int i = 0; i < 25; i++)
            {
                backpack.TryAdd(_config.Crops[0]);
                backpack.TryPop(out _);
            }

            missions.TryClaim(harvest);
            string json = missions.Capture();

            MissionManager second = Create<MissionManager>();
            second.Initialize(_config);
            second.Restore(json);
            Assert.AreEqual(3, second.Count);
            Assert.AreEqual(missions.Day, second.Day);
            int again = IndexOf(second, "mission_harvest_small");
            Assert.AreEqual(25, second.ProgressAt(again));
            Assert.IsTrue(second.IsClaimedAt(again), "Lo reclamado sigue reclamado: no se cobra dos veces cerrando el juego.");

            second.Restore(null);
            Assert.AreEqual(0, second.Count, "Partida anterior a F-05: sin misiones hasta jugar.");
            second.Restore("basura {");
            Assert.AreEqual(0, second.Count);

            second.Restore("{\"version\":1,\"day\":20261026,\"missions\":[{\"id\":\"ya-no-existe\",\"progress\":3},{\"id\":\"mission_sell_small\",\"progress\":9999,\"claimed\":false},null]}");
            Assert.AreEqual(1, second.Count, "Las plantillas que ya no existen se descartan.");
            Assert.AreEqual(30, second.ProgressAt(0), "Un avance de más se acota a la meta.");
        }
    }
}
