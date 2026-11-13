using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FarmIdle.Tests
{
    // El premio de login diario (F-07): las cuentas de la racha y el DailyRewardManager con un reloj de mentira sobre el GameConfig real
    // (una vez por día, racha que sigue o se corta, reloj atrasado, premio de fertilizante, guardado).
    public class DailyRewardTests
    {
        private static readonly DateTime Start = new DateTime(2026, 10, 26, 9, 0, 0);

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

        private DailyRewardManager CreateServices(out EconomyManager economy, out FertilizerManager fertilizer, Func<DateTime> clock)
        {
            economy = Create<EconomyManager>();
            economy.Initialize(_config);
            fertilizer = Create<FertilizerManager>();
            fertilizer.Initialize(_config);

            DailyRewardManager daily = Create<DailyRewardManager>();
            daily.Initialize(_config);
            daily.Clock = clock;
            return daily;
        }

        private static int Key(DateTime date) { return DailyRewardRules.DayKeyOf(date); }

        // ---------- Las cuentas ----------

        [Test]
        public void TryDate_AcceptsRealDates_AndRejectsGarbage()
        {
            Assert.IsTrue(DailyRewardRules.TryDate(20261026, out DateTime date));
            Assert.AreEqual(new DateTime(2026, 10, 26), date);
            Assert.IsTrue(DailyRewardRules.TryDate(20240229, out _), "2024 es bisiesto.");

            foreach (int bad in new[] { 0, -5, 20261301, 20260230, 20250229, 20261000, 99999999 })
            {
                Assert.IsFalse(DailyRewardRules.TryDate(bad, out _), bad.ToString());
            }
        }

        [Test]
        public void CanClaim_FollowsTheStreakRules()
        {
            int today = Key(Start);

            Assert.IsTrue(DailyRewardRules.CanClaim(0, 0, today, 1, out int next));
            Assert.AreEqual(1, next, "La primera vez empieza en 1.");

            Assert.IsFalse(DailyRewardRules.CanClaim(today, 3, today, 1, out _), "Hoy ya se reclamó.");
            Assert.IsFalse(DailyRewardRules.CanClaim(Key(Start.AddDays(2)), 3, today, 1, out _), "Con la hora atrasada no se puede.");

            Assert.IsTrue(DailyRewardRules.CanClaim(Key(Start.AddDays(-1)), 3, today, 1, out next));
            Assert.AreEqual(4, next, "Ayer: la racha sigue.");

            Assert.IsTrue(DailyRewardRules.CanClaim(Key(Start.AddDays(-2)), 3, today, 1, out next));
            Assert.AreEqual(4, next, "Saltear un día se perdona.");

            Assert.IsTrue(DailyRewardRules.CanClaim(Key(Start.AddDays(-3)), 3, today, 1, out next));
            Assert.AreEqual(1, next, "Saltear dos días corta la racha.");

            Assert.IsTrue(DailyRewardRules.CanClaim(Key(Start.AddDays(-3)), 3, today, 2, out next));
            Assert.AreEqual(4, next, "Con 2 días de gracia, saltear dos todavía sigue.");

            Assert.IsTrue(DailyRewardRules.CanClaim(Key(new DateTime(2026, 10, 31)), 5, Key(new DateTime(2026, 11, 1)), 1, out next));
            Assert.AreEqual(6, next, "Cambio de mes.");

            Assert.IsFalse(DailyRewardRules.CanClaim(0, 0, 0, 1, out _), "Una fecha de hoy ilegible no da premio.");
            Assert.IsTrue(DailyRewardRules.CanClaim(20261399, 9, today, 1, out next), "Una fecha guardada ilegible cuenta como 'nunca reclamó'.");
            Assert.AreEqual(1, next);
        }

        [Test]
        public void DayOfCycle_WrapsEverySevenDays()
        {
            Assert.AreEqual(0, DailyRewardRules.DayOfCycle(0));
            Assert.AreEqual(1, DailyRewardRules.DayOfCycle(1));
            Assert.AreEqual(7, DailyRewardRules.DayOfCycle(7));
            Assert.AreEqual(1, DailyRewardRules.DayOfCycle(8));
            Assert.AreEqual(7, DailyRewardRules.DayOfCycle(14));
        }

        [Test]
        public void TheConfig_HasASevenDayLadder_ThatGrows_WithFertilizerOnTheBigDays()
        {
            float previous = 0f;
            for (int day = 1; day <= 7; day++)
            {
                Assert.Greater(_config.DailyRewardMinutes(day), previous, "El premio crece día a día (día " + day + ").");
                previous = _config.DailyRewardMinutes(day);
            }

            Assert.Greater(_config.DailyRewardFertilizer(7), 0, "El día 7 es el grande.");
            Assert.AreEqual(0, _config.DailyRewardFertilizer(0));
            Assert.AreEqual(0, _config.DailyRewardFertilizer(8));
            Assert.AreEqual(1, _config.DailyGraceDays);
        }

        // ---------- El manager ----------

        [Test]
        public void TheFirstDay_CanBeClaimedOnce_AndPaysTheMinimum()
        {
            DateTime clock = Start;
            DailyRewardManager daily = CreateServices(out EconomyManager economy, out _, () => clock);
            var claims = new List<string>();
            daily.Claimed += (day, coins, fertilizer) => claims.Add(day + ":" + coins + ":" + fertilizer);

            Assert.IsTrue(daily.IsClaimable);
            Assert.AreEqual(1, daily.Day);
            Assert.AreEqual(0, daily.Streak);

            long before = economy.Balance;
            Assert.IsTrue(daily.TryClaim());
            Assert.AreEqual(before + _config.MissionMinCoins, economy.Balance, "Sin producción todavía, paga el mínimo.");
            Assert.AreEqual(1, daily.Streak);
            Assert.IsFalse(daily.IsClaimable);
            Assert.AreEqual(1, daily.Day, "Hoy ya reclamó el día 1.");

            Assert.IsFalse(daily.TryClaim(), "Una sola vez por día.");
            clock = Start.AddHours(12);   // la misma tarde: nada
            Assert.IsFalse(daily.TryClaim());
            Assert.AreEqual(before + _config.MissionMinCoins, economy.Balance);
            CollectionAssert.AreEqual(new[] { "1:" + _config.MissionMinCoins + ":0" }, claims);
        }

        [Test]
        public void ClaimingEveryDay_ClimbsTheLadder_AndWrapsAfterDaySeven()
        {
            DateTime clock = Start;
            DailyRewardManager daily = CreateServices(out _, out _, () => clock);
            var days = new List<int>();
            daily.Claimed += (day, coins, fert) => days.Add(day);

            for (int i = 0; i < 9; i++)
            {
                Assert.IsTrue(daily.TryClaim(), "Día " + i);
                clock = clock.AddDays(1);
            }

            CollectionAssert.AreEqual(new[] { 1, 2, 3, 4, 5, 6, 7, 1, 2 }, days);
            Assert.AreEqual(9, daily.Streak);
        }

        [Test]
        public void SkippingOneDayKeepsTheStreak_ButSkippingTwoLosesIt()
        {
            DateTime clock = Start;
            DailyRewardManager daily = CreateServices(out _, out _, () => clock);
            Assert.IsTrue(daily.TryClaim());
            clock = clock.AddDays(1);
            Assert.IsTrue(daily.TryClaim());   // racha 2

            clock = clock.AddDays(2);          // saltó un día
            Assert.AreEqual(3, daily.Day);
            Assert.IsTrue(daily.TryClaim());
            Assert.AreEqual(3, daily.Streak);

            clock = clock.AddDays(3);          // saltó dos días
            Assert.IsTrue(daily.IsClaimable);
            Assert.AreEqual(1, daily.Day, "La racha se cortó: vuelve al día 1.");
            Assert.IsTrue(daily.TryClaim());
            Assert.AreEqual(1, daily.Streak);
        }

        [Test]
        public void AClockGoneBack_GivesNothing_AndLosesNothing()
        {
            DateTime clock = Start;
            DailyRewardManager daily = CreateServices(out EconomyManager economy, out _, () => clock);
            Assert.IsTrue(daily.TryClaim());
            clock = clock.AddDays(1);
            Assert.IsTrue(daily.TryClaim());
            long balance = economy.Balance;

            clock = Start.AddDays(-5);         // atrasó la hora
            Assert.IsFalse(daily.IsClaimable);
            Assert.IsFalse(daily.TryClaim());
            Assert.AreEqual(balance, economy.Balance);
            Assert.AreEqual(2, daily.Streak, "La racha no se pierde.");

            clock = Start.AddDays(2);          // vuelve la hora real (un día después del último reclamo)
            Assert.IsTrue(daily.IsClaimable);
            Assert.AreEqual(3, daily.Day);
        }

        [Test]
        public void TheFertilizerDay_GivesFertilizer_AndFallsBackToCoinsWhenTheStockIsFull()
        {
            DateTime clock = Start;
            DailyRewardManager daily = CreateServices(out EconomyManager economy, out FertilizerManager fertilizer, () => clock);
            for (int i = 0; i < 3; i++)
            {
                Assert.IsTrue(daily.TryClaim());
                clock = clock.AddDays(1);
            }

            Assert.AreEqual(4, daily.Day);
            Assert.Greater(_config.DailyRewardFertilizer(4), 0);
            int stock = fertilizer.Stock;
            Assert.Greater(daily.RewardFertilizerForDay(4), 0);
            Assert.IsTrue(daily.TryClaim());
            Assert.Greater(fertilizer.Stock, stock, "El día 4 entra fertilizante al stock.");

            // Con el stock lleno, el día 4 (acá de la segunda vuelta: racha de 10 días hasta ayer) cae a monedas.
            daily.Restore("{\"version\":1,\"lastClaim\":" + Key(clock.AddDays(-1)) + ",\"streak\":10}");
            fertilizer.DebugGive(99);
            Assert.IsTrue(fertilizer.IsFull);
            Assert.AreEqual(4, daily.Day);
            long coins = economy.Balance;
            Assert.AreEqual(0, daily.RewardFertilizerForDay(4));
            Assert.IsTrue(daily.TryClaim());
            Assert.Greater(economy.Balance, coins, "Con el stock lleno el premio cae a monedas.");
        }

        [Test]
        public void CaptureThenRestore_KeepsTheStreak_AndALoadedGameDoesNotClaimAnything()
        {
            DateTime clock = Start;
            DailyRewardManager daily = CreateServices(out EconomyManager economy, out _, () => clock);
            Assert.IsTrue(daily.TryClaim());
            clock = clock.AddDays(1);
            Assert.IsTrue(daily.TryClaim());
            string json = daily.Capture();
            long balance = economy.Balance;
            Assert.AreEqual("daily", daily.SaveId);

            daily.Restore(null);
            Assert.AreEqual(0, daily.Streak);
            Assert.IsTrue(daily.IsClaimable, "Sin partida: nunca reclamó.");

            daily.Restore(json);
            Assert.AreEqual(2, daily.Streak);
            Assert.IsFalse(daily.IsClaimable, "Hoy ya había reclamado.");
            Assert.AreEqual(balance, economy.Balance, "Restaurar no paga nada.");

            clock = clock.AddDays(1);
            Assert.AreEqual(3, daily.Day);
        }

        [Test]
        public void ABadSave_NeverThrows_AndStartsLikeANewGame()
        {
            DailyRewardManager daily = CreateServices(out _, out _, () => Start);

            Assert.DoesNotThrow(() => daily.Restore("esto no es json"));
            Assert.AreEqual(0, daily.Streak);
            Assert.IsTrue(daily.IsClaimable);

            Assert.DoesNotThrow(() => daily.Restore("{\"version\":1,\"lastClaim\":20269999,\"streak\":5}"));
            Assert.AreEqual(0, daily.Streak, "Una fecha inválida descarta la racha.");

            Assert.DoesNotThrow(() => daily.Restore("{\"version\":1,\"lastClaim\":20261025,\"streak\":-4}"));
            Assert.AreEqual(0, daily.Streak, "Una racha negativa se corrige.");
            Assert.AreEqual(1, daily.Day);

            Assert.DoesNotThrow(() => daily.Restore(string.Empty));
            Assert.IsTrue(daily.IsClaimable);
        }
    }
}
