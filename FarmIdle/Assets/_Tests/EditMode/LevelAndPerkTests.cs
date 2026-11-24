using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace FarmIdle.Tests
{
    // Niveles y perks (Fase 7), la parte de C# puro: la curva de XP, la XP y las elecciones dentro del progreso (con su
    // ida y vuelta a ProgressData) y cómo los perks se suman a los efectos de las mejoras de monedas.
    public class LevelAndPerkTests
    {
        // ---------- LevelCurve ----------

        [Test]
        public void LevelCurve_ThresholdsAreBaseTimesStepsSquared()
        {
            var curve = new LevelCurve(125, 20);

            Assert.AreEqual(0L, curve.XpForLevel(1));
            Assert.AreEqual(125L, curve.XpForLevel(2));
            Assert.AreEqual(500L, curve.XpForLevel(3));
            Assert.AreEqual(2000L, curve.XpForLevel(5));
            Assert.AreEqual(10125L, curve.XpForLevel(10));
            Assert.AreEqual(45125L, curve.XpForLevel(20));
            Assert.AreEqual(45125L, curve.XpForLevel(99), "Más allá del tope vale como el tope.");
            Assert.AreEqual(0L, curve.XpForLevel(0));
        }

        [Test]
        public void LevelCurve_LevelForXp_ChangesExactlyAtTheThresholds()
        {
            var curve = new LevelCurve(125, 20);

            Assert.AreEqual(1, curve.LevelForXp(-5));
            Assert.AreEqual(1, curve.LevelForXp(0));
            Assert.AreEqual(1, curve.LevelForXp(124));
            Assert.AreEqual(2, curve.LevelForXp(125));
            Assert.AreEqual(2, curve.LevelForXp(499));
            Assert.AreEqual(3, curve.LevelForXp(500));
            Assert.AreEqual(19, curve.LevelForXp(45124));
            Assert.AreEqual(20, curve.LevelForXp(45125));
            Assert.AreEqual(20, curve.LevelForXp(long.MaxValue), "Nunca pasa del nivel máximo, ni con una XP gigante.");
        }

        [Test]
        public void LevelCurve_AgreesWithItsThresholds_ForEveryLevel()
        {
            var curve = new LevelCurve(125, 20);

            for (int level = 1; level <= 20; level++)
            {
                long threshold = curve.XpForLevel(level);
                Assert.AreEqual(level, curve.LevelForXp(threshold), "Justo en el umbral del nivel " + level + ".");
                if (threshold > 0) Assert.AreEqual(level - 1, curve.LevelForXp(threshold - 1), "Un punto antes del umbral del nivel " + level + ".");
            }
        }

        [Test]
        public void LevelCurve_Progress_ReportsXpIntoTheLevel_AndNothingAtTheTop()
        {
            var curve = new LevelCurve(125, 20);

            curve.Progress(0, out long into, out long needed);
            Assert.AreEqual(0L, into);
            Assert.AreEqual(125L, needed);

            curve.Progress(300, out into, out needed);   // nivel 2 empieza en 125 y el 3 en 500
            Assert.AreEqual(175L, into);
            Assert.AreEqual(375L, needed);

            curve.Progress(99999, out into, out needed);
            Assert.AreEqual(0L, into);
            Assert.AreEqual(0L, needed);
        }

        [Test]
        public void LevelCurve_RejectsNonsenseSettings()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new LevelCurve(0, 20));
            Assert.Throws<ArgumentOutOfRangeException>(() => new LevelCurve(125, 1));
        }

        // ---------- PlayerProgress: XP y perks ----------

        [Test]
        public void AddXp_Accumulates_Announces_AndIgnoresNonPositiveAmounts()
        {
            var progress = new PlayerProgress();
            var changes = new List<ProgressChange>();
            progress.Changed += changes.Add;

            progress.AddXp(10);
            progress.AddXp(5);
            progress.AddXp(0);
            progress.AddXp(-3);

            Assert.AreEqual(15L, progress.Xp);
            Assert.AreEqual(2, changes.Count);
            Assert.AreEqual(ProgressChangeKind.XpChanged, changes[0].Kind);
        }

        [Test]
        public void AddXp_SaturatesInsteadOfWrapping()
        {
            var progress = new PlayerProgress();
            progress.AddXp(long.MaxValue - 1);
            progress.AddXp(100);

            Assert.AreEqual(long.MaxValue, progress.Xp);
        }

        [Test]
        public void AddPerkPick_CountsPerPerk_AndAnnounces()
        {
            var progress = new PlayerProgress();
            var changes = new List<ProgressChange>();
            progress.Changed += changes.Add;

            progress.AddPerkPick("strength");
            progress.AddPerkPick("strength");
            progress.AddPerkPick("speed");

            Assert.AreEqual(2, progress.PerkPicksOf("strength"));
            Assert.AreEqual(1, progress.PerkPicksOf("speed"));
            Assert.AreEqual(0, progress.PerkPicksOf("nothing"));
            Assert.AreEqual(3, progress.TotalPerkPicks);
            Assert.AreEqual(3, changes.Count);
            Assert.AreEqual(ProgressChangeKind.PerkChosen, changes[2].Kind);
            Assert.AreEqual("speed", changes[2].Id);
            Assert.Throws<ArgumentException>(() => progress.AddPerkPick(""));
        }

        [Test]
        public void XpAndPerks_SurviveTheRoundTripToProgressData_IncludingThroughJson()
        {
            var progress = new PlayerProgress();
            progress.UnlockZone("east");
            progress.SetLevel("capacity", 2);
            progress.AddPaid(PlayerProgress.UpgradeKey("speed"), 123);
            progress.AddXp(4321);
            progress.AddPerkPick("strength");
            progress.AddPerkPick("strength");
            progress.AddPerkPick("speed");

            string json = JsonUtility.ToJson(progress.ToData());
            ProgressData read = JsonUtility.FromJson<ProgressData>(json);

            var copy = new PlayerProgress();
            copy.LoadFrom(read);

            Assert.AreEqual(4321L, copy.Xp);
            Assert.AreEqual(2, copy.PerkPicksOf("strength"));
            Assert.AreEqual(1, copy.PerkPicksOf("speed"));
            Assert.AreEqual(3, copy.TotalPerkPicks);
            Assert.IsTrue(copy.IsZoneUnlocked("east"));
            Assert.AreEqual(2, copy.LevelOf("capacity"));
            Assert.AreEqual(123L, copy.PaidOf(PlayerProgress.UpgradeKey("speed")));
            Assert.AreEqual(json, JsonUtility.ToJson(copy.ToData()), "El segundo guardado es idéntico al primero: el formato es estable.");
        }

        [Test]
        public void LoadFrom_IgnoresBrokenXpAndPerkEntries_AndAnEmptyDataResetsEverything()
        {
            var progress = new PlayerProgress();
            progress.AddXp(500);
            progress.AddPerkPick("old");

            var broken = new ProgressData
            {
                xp = -50,
                perkPicks = new[] { null, new PerkPickEntry { id = "", picks = 3 }, new PerkPickEntry { id = "neg", picks = -1 }, new PerkPickEntry { id = "ok", picks = 2 } }
            };
            progress.LoadFrom(broken);

            Assert.AreEqual(0L, progress.Xp, "Una XP negativa se toma como 0.");
            Assert.AreEqual(0, progress.PerkPicksOf("old"), "Lo anterior se reemplaza entero.");
            Assert.AreEqual(2, progress.PerkPicksOf("ok"));
            Assert.AreEqual(2, progress.TotalPerkPicks);

            progress.LoadFrom(null);
            Assert.AreEqual(0L, progress.Xp);
            Assert.AreEqual(0, progress.TotalPerkPicks);
        }

        // ---------- ProgressionModifiers con perks ----------

        private static ProgressionModifiers Compute(IReadOnlyPlayerProgress progress, UpgradeData[] upgrades, PerkData[] perks, float cap = 0f)
        {
            return ProgressionModifiers.Compute(progress, upgrades, perks, cap);
        }

        [Test]
        public void Perks_AddBackpackSlots_AndSpeedAddsAmongPerksThenMultipliesTheUpgrades()
        {
            PerkData strength = ProgressionTestData.Perk("strength", PerkKind.BackpackCapacity, 1f, 10);
            PerkData speed = ProgressionTestData.Perk("speed", PerkKind.MoveSpeed, 0.015f, 10);
            UpgradeData bag = ProgressionTestData.Upgrade("bag", UpgradeKind.BackpackCapacity, (100L, 4f));
            UpgradeData boots = ProgressionTestData.Upgrade("boots", UpgradeKind.MoveSpeed, (100L, 1.2f));

            try
            {
                var progress = new PlayerProgress();
                progress.SetLevel("bag", 1);
                progress.SetLevel("boots", 1);
                for (int i = 0; i < 3; i++) progress.AddPerkPick("strength");
                for (int i = 0; i < 4; i++) progress.AddPerkPick("speed");

                ProgressionModifiers m = Compute(progress, new[] { bag, boots }, new[] { strength, speed });

                Assert.AreEqual(4 + 3, m.BackpackBonus, "Las monedas dan +4 y Fuerza +1 por elección.");
                Assert.AreEqual(1.2f * 1.06f, m.MoveSpeed, 1e-5f, "La mejora de monedas x1,2 por el +6 % de cuatro elecciones de Velocidad.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(strength);
                UnityEngine.Object.DestroyImmediate(speed);
                UnityEngine.Object.DestroyImmediate(bag);
                UnityEngine.Object.DestroyImmediate(boots);
            }
        }

        // IN-04: Regateo y Mano verde dan +1 % por elección, como un multiplicador APARTE del de las mejoras de monedas.
        [Test]
        public void BarterAndGreenThumb_MultiplyPriceAndGrowth_OnTopOfTheCoinUpgrades()
        {
            PerkData barter = ProgressionTestData.Perk("barter", PerkKind.SellPrice, 0.01f, 10);
            PerkData green = ProgressionTestData.Perk("green", PerkKind.GrowthSpeed, 0.01f, 10);
            UpgradeData price = ProgressionTestData.Upgrade("price", UpgradeKind.SellPrice, (100L, 1.5f));
            UpgradeData growth = ProgressionTestData.Upgrade("growth", UpgradeKind.GrowthSpeed, (100L, 2f));

            try
            {
                var progress = new PlayerProgress();
                progress.SetLevel("price", 1);
                progress.SetLevel("growth", 1);
                for (int i = 0; i < 4; i++) progress.AddPerkPick("barter");
                for (int i = 0; i < 10; i++) progress.AddPerkPick("green");

                ProgressionModifiers m = Compute(progress, new[] { price, growth }, new[] { barter, green });

                Assert.AreEqual(1.5f * 1.04f, m.SellPrice, 1e-5f);
                Assert.AreEqual(2f * 1.10f, m.GrowthSpeed, 1e-5f);

                ProgressionModifiers none = Compute(new PlayerProgress(), null, new[] { barter, green });
                Assert.AreEqual(1f, none.SellPrice, "Sin elecciones no cambia nada.");
                Assert.AreEqual(1f, none.GrowthSpeed);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(barter);
                UnityEngine.Object.DestroyImmediate(green);
                UnityEngine.Object.DestroyImmediate(price);
                UnityEngine.Object.DestroyImmediate(growth);
            }
        }

        [Test]
        public void Perks_NeverGoPastTheirMaxPicks_EvenIfTheSaveSaysMore()
        {
            PerkData strength = ProgressionTestData.Perk("strength", PerkKind.BackpackCapacity, 1f, 5);

            try
            {
                var progress = new PlayerProgress();
                for (int i = 0; i < 20; i++) progress.AddPerkPick("strength");

                Assert.AreEqual(5, Compute(progress, null, new[] { strength }).BackpackBonus);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(strength);
            }
        }

        [Test]
        public void TheSpeedCap_LimitsTheTotalPermanentSpeed_ButOnlyWhenSet()
        {
            PerkData speed = ProgressionTestData.Perk("speed", PerkKind.MoveSpeed, 0.1f, 10);
            UpgradeData boots = ProgressionTestData.Upgrade("boots", UpgradeKind.MoveSpeed, (100L, 1.3f));

            try
            {
                var progress = new PlayerProgress();
                progress.SetLevel("boots", 1);
                for (int i = 0; i < 10; i++) progress.AddPerkPick("speed");   // x1,3 por x2,0 = x2,6

                Assert.AreEqual(2.6f, Compute(progress, new[] { boots }, new[] { speed }).MoveSpeed, 1e-4f, "Sin tope, se compone sin límite.");
                Assert.AreEqual(1.5f, Compute(progress, new[] { boots }, new[] { speed }, 1.5f).MoveSpeed, 1e-6f, "Con tope, se acota.");
                Assert.AreEqual(1.3f, Compute(new PlayerProgressWith("boots"), new[] { boots }, new[] { speed }, 1.5f).MoveSpeed, 1e-6f, "Por debajo del tope no se toca.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(speed);
                UnityEngine.Object.DestroyImmediate(boots);
            }
        }

        // Un progreso con un nivel de mejora ya puesto (para armar la prueba de arriba en una línea).
        private sealed class PlayerProgressWith : IReadOnlyPlayerProgress
        {
            private readonly string _upgradeId;

            public PlayerProgressWith(string upgradeId) { _upgradeId = upgradeId; }

            public bool IsZoneUnlocked(string zoneId) { return false; }
            public int LevelOf(string upgradeId) { return upgradeId == _upgradeId ? 1 : 0; }
            public long PaidOf(string purchaseKey) { return 0L; }
            public int UnlockedZoneCount => 0;
            public long Xp => 0L;
            public int PerkPicksOf(string perkId) { return 0; }
            public int TotalPerkPicks => 0;
            public event Action<ProgressChange> Changed { add { } remove { } }
        }

        [Test]
        public void NullPerkEntries_AreSkipped_AndNoPerksMeansNoChange()
        {
            var progress = new PlayerProgress();
            ProgressionModifiers a = Compute(progress, null, new PerkData[] { null });
            ProgressionModifiers b = ProgressionModifiers.Compute(progress, null);

            Assert.AreEqual(0, a.BackpackBonus);
            Assert.AreEqual(1f, a.MoveSpeed);
            Assert.AreEqual(b.MoveSpeed, a.MoveSpeed);
        }

        // ---------- PerkData ----------

        [Test]
        public void PerkData_TotalAt_ClampsToTheValidRange_AndDescribesOnePick()
        {
            PerkData strength = ProgressionTestData.Perk("strength", PerkKind.BackpackCapacity, 1f, 10);
            PerkData speed = ProgressionTestData.Perk("speed", PerkKind.MoveSpeed, 0.015f, 10);

            try
            {
                Assert.AreEqual(0f, strength.TotalAt(-3));
                Assert.AreEqual(4f, strength.TotalAt(4));
                Assert.AreEqual(10f, strength.TotalAt(99));
                Assert.AreEqual(0.15f, speed.TotalAt(10), 1e-6f);

                Assert.AreEqual("+1 lugar", strength.DescribePick());
                Assert.AreEqual("+1,5 %", speed.DescribePick());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(strength);
                UnityEngine.Object.DestroyImmediate(speed);
            }
        }
    }
}
