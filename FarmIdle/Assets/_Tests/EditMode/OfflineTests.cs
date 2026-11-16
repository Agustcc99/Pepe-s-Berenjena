using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FarmIdle.Tests
{
    // El progreso offline (B-01): la cuenta (tope, eficiencia, reloj atrasado), el OfflineManager con un reloj de mentira y una fuente
    // de producción falsa (medir, resolver, reclamar, duplicar, guardar) y los datos del GameConfig.
    public class OfflineTests
    {
        private static readonly DateTime Now = new DateTime(2026, 10, 26, 12, 0, 0, DateTimeKind.Utc);

        private GameConfig _config;
        private readonly List<GameObject> _objects = new List<GameObject>();

        private sealed class FakeSource : IProductionSource
        {
            public float Rate;
            public float BaseCoinsPerSecond => Rate;
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

        // La economía y el OfflineManager con el reloj en 'Now' y una fuente que rinde 'rate' monedas base por segundo.
        private OfflineManager CreateOffline(float rate, out EconomyManager economy, out FakeSource source)
        {
            economy = Create<EconomyManager>();
            economy.Initialize(_config);

            OfflineManager offline = Create<OfflineManager>();
            offline.Initialize(_config);
            DateTime clock = Now;
            offline.Clock = () => clock;

            source = new FakeSource { Rate = rate };
            offline.Register(source);
            return offline;
        }

        private static string SaveAwayFor(TimeSpan away, long pendingCoins = 0L)
        {
            return "{\"version\":1,\"lastSeenUtc\":\"" + (Now - away).ToString("o", CultureInfo.InvariantCulture) + "\",\"pendingCoins\":" + pendingCoins + ",\"pendingSeconds\":0}";
        }

        // ---------- La cuenta ----------

        [Test]
        public void TheElapsedTime_IsZero_WithoutAMark_OrWithABackwardsClock()
        {
            Assert.AreEqual(3600d, OfflineMath.ElapsedSeconds(Now.AddHours(-1), Now), 1e-6);
            Assert.AreEqual(0d, OfflineMath.ElapsedSeconds(default(DateTime), Now), "Sin marca (partida nueva o anterior a B-01).");
            Assert.AreEqual(0d, OfflineMath.ElapsedSeconds(Now, Now));
            Assert.AreEqual(0d, OfflineMath.ElapsedSeconds(Now.AddHours(3), Now), "El jugador atrasó la hora: no se acredita nada.");
        }

        [Test]
        public void TheCoins_AreCapped_AndScaledByEveryFactor()
        {
            // 1 h x 2 monedas/s x 0,25 de eficiencia.
            Assert.AreEqual(1800L, OfflineMath.Coins(3600, 7200, 2, 1, 1, 0.25));

            // Con 10 h fuera solo cuentan las 2 h del tope.
            Assert.AreEqual(3600L, OfflineMath.Coins(36000, 7200, 2, 1, 1, 0.25));
            Assert.AreEqual(OfflineMath.Coins(7200, 7200, 2, 1, 1, 0.25), OfflineMath.Coins(36000, 7200, 2, 1, 1, 0.25));

            // Precio (mejoras x prestigio) y velocidad de crecimiento multiplican; se redondea hacia abajo.
            Assert.AreEqual(5400L, OfflineMath.Coins(3600, 7200, 2, 1.5, 2, 0.25));
            Assert.AreEqual(0L, OfflineMath.Coins(3, 7200, 1, 1, 1, 0.25), "0,75 monedas: nada.");
            Assert.AreEqual(1L, OfflineMath.Coins(5, 7200, 1, 1, 1, 0.25), "1,25 monedas: 1.");
        }

        [Test]
        public void TheCoins_AreZero_ForNegativeOrBrokenInputs()
        {
            Assert.AreEqual(0L, OfflineMath.Coins(-100, 7200, 2, 1, 1, 0.25));
            Assert.AreEqual(0L, OfflineMath.Coins(3600, -5, 2, 1, 1, 0.25));
            Assert.AreEqual(0L, OfflineMath.Coins(3600, 7200, 0, 1, 1, 0.25));
            Assert.AreEqual(0L, OfflineMath.Coins(3600, 7200, 2, 1, 1, 0));
            Assert.AreEqual(0L, OfflineMath.Coins(double.NaN, 7200, 2, 1, 1, 0.25));
            Assert.AreEqual(long.MaxValue, OfflineMath.Coins(7200, 7200, double.MaxValue, 1, 1, 1), "Sin desbordar.");
        }

        [Test]
        public void TheCoins_AreLimitedPerHourAway_AndTheLimitFollowsTheCreditedTime()
        {
            // Producción enorme: sin límite serían 720.000 en 2 h.
            Assert.AreEqual(720000L, OfflineMath.Coins(7200, 7200, 400, 1, 1, 0.25));
            Assert.AreEqual(50000L, OfflineMath.Coins(7200, 7200, 400, 1, 1, 0.25, 25000), "2 h a 25.000 por hora.");
            Assert.AreEqual(25000L, OfflineMath.Coins(3600, 7200, 400, 1, 1, 0.25, 25000));
            Assert.AreEqual(12500L, OfflineMath.Coins(1800, 7200, 400, 1, 1, 0.25, 25000), "El límite sigue al tiempo fuera, no es un número fijo.");
            Assert.AreEqual(50000L, OfflineMath.Coins(36000, 7200, 400, 1, 1, 0.25, 25000), "Con 10 h fuera solo cuentan las 2 h del tope de tiempo.");

            // Por debajo del límite no cambia nada; 0 o negativo = sin límite en monedas.
            Assert.AreEqual(1800L, OfflineMath.Coins(3600, 7200, 2, 1, 1, 0.25, 25000));
            Assert.AreEqual(720000L, OfflineMath.Coins(7200, 7200, 400, 1, 1, 0.25, 0));
            Assert.AreEqual(720000L, OfflineMath.Coins(7200, 7200, 400, 1, 1, 0.25, -5));
        }

        [Test]
        public void TheConfig_HasPlayableOfflineValues()
        {
            Assert.AreEqual(0.15f, _config.OfflineEfficiency, 1e-6f, "B-11: 15% de eficiencia (antes 25%).");
            Assert.AreEqual(7200f, _config.OfflineBaseCapSeconds, "DEC-5: 2 h de tope base.");
            Assert.AreEqual(15000f, _config.OfflineMaxCoinsPerHour, "B-11: 15.000 monedas por hora fuera (antes 25.000; provisorio).");
            Assert.Greater(_config.OfflineMinSeconds, 0f);
            Assert.Less(_config.OfflineMinSeconds, _config.OfflineBaseCapSeconds);

            RewardData doubleReward = _config.FirstRewardOfKind(RewardKind.OfflineDouble);
            Assert.IsNotNull(doubleReward, "Falta la recompensa de duplicar (¿corriste FarmIdle > Fase 2 > Construir assets y escenas?).");
            Assert.AreEqual(0f, doubleReward.CooldownSeconds);
            Assert.IsFalse(doubleReward.IsBuff);
        }

        // ---------- El manager ----------

        [Test]
        public void ComingBack_AfterAnHour_LeavesTheCoinsPending_NotCredited()
        {
            OfflineManager offline = CreateOffline(2f, out EconomyManager economy, out _);
            long start = economy.Balance;
            var changes = 0;
            offline.PendingChanged += () => changes++;

            offline.Restore(SaveAwayFor(TimeSpan.FromHours(1)));
            Assert.IsFalse(offline.HasPending, "Todavía no se convirtió en monedas: espera a que se empiece a jugar.");

            offline.Resolve();
            Assert.IsTrue(offline.HasPending);
            Assert.AreEqual(1080L, offline.PendingCoins);
            Assert.AreEqual(3600f, offline.PendingSeconds, 1f);
            Assert.AreEqual(start, economy.Balance, "Nada se acredita solo.");
            Assert.Greater(changes, 0);

            // Resolver dos veces no suma dos veces.
            offline.Resolve();
            Assert.AreEqual(1080L, offline.PendingCoins);
        }

        [Test]
        public void TheOfflineTime_IsCappedAtTwoHours()
        {
            OfflineManager offline = CreateOffline(2f, out _, out _);

            offline.Restore(SaveAwayFor(TimeSpan.FromHours(30)));
            offline.Resolve();

            Assert.AreEqual(OfflineMath.Coins(7200, 7200, 2, 1, 1, _config.OfflineEfficiency), offline.PendingCoins);
            Assert.AreEqual(7200f, offline.PendingSeconds, 1f);
        }

        [Test]
        public void NothingIsPending_ForAShortAbsence_AClockGoneBack_OrAnOldSave()
        {
            OfflineManager offline = CreateOffline(2f, out _, out _);

            offline.Restore(SaveAwayFor(TimeSpan.FromSeconds(60)));
            offline.Resolve();
            Assert.IsFalse(offline.HasPending, "Un minuto fuera: no vale una tarjeta.");

            offline.Restore(SaveAwayFor(TimeSpan.FromHours(-5)));
            offline.Resolve();
            Assert.IsFalse(offline.HasPending, "Hora atrasada: 0 ganancia.");

            offline.Restore(null);
            offline.Resolve();
            Assert.IsFalse(offline.HasPending, "Partida anterior a B-01: no trae la sección.");

            offline.Restore("basura {");
            offline.Resolve();
            Assert.IsFalse(offline.HasPending);

            offline.Restore("{\"version\":1,\"lastSeenUtc\":\"no es una fecha\",\"pendingCoins\":-9}");
            offline.Resolve();
            Assert.IsFalse(offline.HasPending, "Una fecha ilegible no da nada, y las monedas negativas tampoco.");
        }

        [Test]
        public void OnlyTheOpenFields_Count_AndThePricesMultiply()
        {
            OfflineManager offline = CreateOffline(2f, out EconomyManager economy, out FakeSource first);
            var closedZone = new FakeSource { Rate = 3f };

            offline.Register(closedZone);
            offline.Unregister(closedZone);   // la zona se cerró (campo apagado)
            Assert.AreEqual(2f, offline.BaseCoinsPerSecond);

            economy.Pricing.SetUpgradeMultiplier(1.5f);
            economy.Pricing.SetPrestigeMultiplier(2f);
            economy.Pricing.SetBuffMultiplier(3f);   // los buffs de video no cuentan fuera

            offline.Restore(SaveAwayFor(TimeSpan.FromHours(1)));
            offline.Resolve();
            Assert.AreEqual(OfflineMath.Coins(3600, 7200, 2, 1.5 * 2, 1, _config.OfflineEfficiency), offline.PendingCoins);

            // Sin parcelas abiertas no produce nada.
            offline.Unregister(first);
            offline.Restore(SaveAwayFor(TimeSpan.FromHours(1)));
            offline.Resolve();
            Assert.IsFalse(offline.HasPending);
        }

        [Test]
        public void Claiming_Credits_OnceAndDoubled_AndNeverTwice()
        {
            OfflineManager offline = CreateOffline(2f, out EconomyManager economy, out _);
            var claims = new List<string>();
            offline.Claimed += (coins, doubled) => claims.Add(coins + (doubled ? "x2" : ""));

            offline.Restore(SaveAwayFor(TimeSpan.FromHours(1)));
            offline.Resolve();
            long start = economy.Balance;

            Assert.IsTrue(offline.Claim());
            Assert.AreEqual(start + 1080L, economy.Balance);
            Assert.IsFalse(offline.HasPending);
            Assert.IsFalse(offline.Claim(), "Ya se reclamó.");
            Assert.IsFalse(offline.ClaimDoubled(), "Y no se puede duplicar lo que ya se cobró.");

            offline.Restore(SaveAwayFor(TimeSpan.FromHours(1)));
            offline.Resolve();
            Assert.IsTrue(offline.ClaimDoubled());
            Assert.AreEqual(start + 1080L + 2160L, economy.Balance, "Duplicado: el doble de lo pendiente.");

            CollectionAssert.AreEqual(new[] { "1080", "2160x2" }, claims);
        }

        [Test]
        public void ThePendingCoins_AreSaved_SoClosingWithoutClaimingLosesNothing()
        {
            OfflineManager offline = CreateOffline(2f, out _, out _);
            offline.Restore(SaveAwayFor(TimeSpan.FromHours(1)));
            offline.Resolve();
            Assert.AreEqual("offline", offline.SaveId);
            Assert.IsTrue(offline.HasVolatileState, "Tiene que escribirse al pausar aunque nada más haya cambiado.");

            string json = offline.Capture();

            OfflineManager second = Create<OfflineManager>();
            second.Initialize(_config);
            second.Clock = () => Now.AddSeconds(30);   // volvió 30 s después: menos que el mínimo
            second.Restore(json);
            second.Resolve();

            Assert.AreEqual(1080L, second.PendingCoins, "Lo pendiente vuelve igual y no se suma nada por los 30 s.");
        }

        [Test]
        public void TheMark_IsRestamped_OnlyOnceTheTimeAwayWasResolved()
        {
            OfflineManager offline = CreateOffline(2f, out _, out _);
            offline.Restore(SaveAwayFor(TimeSpan.FromHours(1)));

            // Cargó la partida pero todavía no empezó a jugar: si se cierra acá, la marca vieja sigue valiendo.
            DateTime before = ParseMark(offline.Capture());
            Assert.AreEqual(Now.AddHours(-1), before);

            offline.Resolve();
            Assert.AreEqual(Now, ParseMark(offline.Capture()), "Ya se midió: la marca es la de ahora.");
        }

        private static DateTime ParseMark(string json)
        {
            string key = "\"lastSeenUtc\":\"";
            int start = json.IndexOf(key, StringComparison.Ordinal) + key.Length;
            string text = json.Substring(start, json.IndexOf('"', start) - start);
            return DateTime.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        }

        // ---------- La mejora "Almacén" (tanda 2) ----------

        private UpgradeData StorehouseUpgrade()
        {
            UpgradeData storehouse = _config.Upgrades.FirstOrDefault(u => u != null && u.Kind == UpgradeKind.OfflineCap);
            Assert.IsNotNull(storehouse, "Falta la mejora Almacén (¿corriste FarmIdle > Fase 2 > Construir assets y escenas?).");
            return storehouse;
        }

        [Test]
        public void TheStorehouse_HasTwoLevels_ThatReachThreeAndFourHours()
        {
            UpgradeData storehouse = StorehouseUpgrade();

            Assert.AreEqual(2, storehouse.MaxLevel);
            Assert.AreEqual(0f, storehouse.Identity, "Sin comprar no suma nada.");
            Assert.AreEqual(3600f, storehouse.ValueAtLevel(1), "Nivel 1: +1 h (3 h en total).");
            Assert.AreEqual(4 * 3600f, _config.OfflineBaseCapSeconds + storehouse.ValueAtLevel(2), "DEC-5: hasta 4 h con la mejora.");
            StringAssert.Contains("1", storehouse.Describe(1));
            Assert.IsFalse(string.IsNullOrWhiteSpace(storehouse.DisplayName));
        }

        [Test]
        public void TheStorehouse_RaisesTheCap_AndNeverLowersItBelowTheBase()
        {
            UpgradeData storehouse = StorehouseUpgrade();
            OfflineManager offline = CreateOffline(2f, out _, out _);

            ProgressionManager progression = Create<ProgressionManager>();
            progression.Initialize(_config);
            float baseCap = _config.OfflineBaseCapSeconds;
            Assert.AreEqual(baseCap, offline.CapSeconds, "Sin comprar: el tope base (2 h).");

            progression.Restore(new ProgressData { upgradeLevels = new[] { new UpgradeLevelEntry { id = storehouse.Id, level = 1 } } });
            Assert.AreEqual(baseCap + 3600f, offline.CapSeconds);
            Assert.AreEqual(3600, progression.Modifiers.OfflineCapBonusSeconds);

            progression.Restore(new ProgressData { upgradeLevels = new[] { new UpgradeLevelEntry { id = storehouse.Id, level = 2 } } });
            Assert.AreEqual(4 * 3600f, offline.CapSeconds);

            // Con 10 h fuera ahora cuentan las 4 h.
            offline.Restore(SaveAwayFor(TimeSpan.FromHours(10)));
            offline.Resolve();
            Assert.AreEqual(OfflineMath.Coins(4 * 3600, 4 * 3600, 2, 1, 1, _config.OfflineEfficiency), offline.PendingCoins);

            progression.Restore(new ProgressData());
            Assert.AreEqual(baseCap, offline.CapSeconds, "Una nueva temporada la vuelve al tope base, pero nunca por debajo.");
        }

        [Test]
        public void TheMoneyLimit_CapsTheOfflineCoins_AndTheStorehouseRaisesIt()
        {
            UpgradeData storehouse = StorehouseUpgrade();
            OfflineManager offline = CreateOffline(400f, out EconomyManager economy, out _);
            ProgressionManager progression = Create<ProgressionManager>();
            progression.Initialize(_config);

            // 400 monedas/s x 2 h x 0,15 serían 432.000: queda en 2 h x 15.000 por hora.
            offline.Restore(SaveAwayFor(TimeSpan.FromHours(30)));
            offline.Resolve();
            Assert.AreEqual(30000L, offline.PendingCoins);

            // Con el Almacén al máximo (4 h) el límite sube a 60.000, no se queda en 30.000.
            offline.Claim();
            progression.Restore(new ProgressData { upgradeLevels = new[] { new UpgradeLevelEntry { id = storehouse.Id, level = 2 } } });
            offline.Restore(SaveAwayFor(TimeSpan.FromHours(30)));
            offline.Resolve();
            Assert.AreEqual(60000L, offline.PendingCoins);

            // Duplicar (video) multiplica lo ya limitado.
            long before = economy.Balance;
            Assert.IsTrue(offline.ClaimDoubled());
            Assert.AreEqual(120000L, economy.Balance - before);
        }

        [Test]
        public void TheProgressionModifiers_SumTheStorehouse_WithoutTouchingTheRest()
        {
            UpgradeData storehouse = StorehouseUpgrade();
            var progress = new PlayerProgress();
            progress.SetLevel(storehouse.Id, 2);

            ProgressionModifiers modifiers = ProgressionModifiers.Compute(progress, _config.Upgrades);

            Assert.AreEqual(7200, modifiers.OfflineCapBonusSeconds);
            Assert.AreEqual(0, modifiers.BackpackBonus);
            Assert.AreEqual(1f, modifiers.SellPrice);
            Assert.AreEqual(0, ProgressionModifiers.None.OfflineCapBonusSeconds);
        }

        [Test]
        public void ComingBackFromTheBackground_CountsTheTimeBetweenPauseAndResume()
        {
            OfflineManager offline = CreateOffline(2f, out _, out _);
            DateTime clock = Now;
            offline.Clock = () => clock;

            offline.OnApplicationPause(true);
            clock = Now.AddSeconds(30);
            offline.OnApplicationPause(false);
            Assert.IsFalse(offline.HasPending, "Un cambio de app de 30 s no cuenta.");

            offline.OnApplicationPause(true);
            clock = clock.AddMinutes(30);
            offline.OnApplicationPause(false);
            Assert.AreEqual(OfflineMath.Coins(1800, 7200, 2, 1, 1, _config.OfflineEfficiency), offline.PendingCoins);
        }
    }
}
