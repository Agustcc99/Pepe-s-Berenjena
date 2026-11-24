using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FarmIdle.Tests
{
    // F-24: una granja nueva por temporada y el perk de Suerte (más cosecha dorada), con el GameConfig real.
    public class SeasonFarmAndLuckTests
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

        private void CreateServices(out EconomyManager economy, out ProgressionManager progression, out PrestigeManager prestige)
        {
            economy = Create<EconomyManager>();
            economy.Initialize(_config);
            Create<BackpackManager>().Initialize(_config);
            progression = Create<ProgressionManager>();
            progression.Initialize(_config);
            prestige = Create<PrestigeManager>();
            prestige.Initialize(_config);
        }

        private ZoneData Zone(string id)
        {
            return _config.Zones.First(zone => zone.Id == id);
        }

        // Todas las mejoras al máximo y abiertas las zonas que se pidan.
        private ProgressData Bought(params ZoneData[] zones)
        {
            return new ProgressData
            {
                unlockedZones = zones.Select(zone => zone.Id).ToArray(),
                upgradeLevels = _config.Upgrades.Select(upgrade => new UpgradeLevelEntry { id = upgrade.Id, level = upgrade.MaxLevel }).ToArray()
            };
        }

        private ZoneData[] BaseZones()
        {
            return _config.Zones.Where(zone => zone.RequiredSeason == 0).ToArray();
        }

        // ---------- Granjas de temporada ----------

        [Test]
        public void TheRealConfig_HasOneNewFarmPerSeason_EachMoreExpensive()
        {
            ZoneData[] seasonal = _config.Zones.Where(zone => zone.RequiredSeason > 0).OrderBy(zone => zone.RequiredSeason).ToArray();

            CollectionAssert.AreEqual(new[] { 1, 2, 3 }, seasonal.Select(zone => zone.RequiredSeason).ToArray(), "Una granja por temporada, de la 1 a la 3.");
            long mostExpensiveBase = BaseZones().Max(zone => zone.UnlockCost);
            Assert.Greater(seasonal[0].UnlockCost, mostExpensiveBase, "La primera cuesta más que cualquiera de las de siempre.");
            Assert.Greater(seasonal[1].UnlockCost, seasonal[0].UnlockCost);
            Assert.Greater(seasonal[2].UnlockCost, seasonal[1].UnlockCost);
        }

        [Test]
        public void ASeasonalFarm_IsHiddenAndCannotBeBought_UntilItsSeason()
        {
            CreateServices(out EconomyManager economy, out ProgressionManager progression, out PrestigeManager prestige);
            ZoneData dawn = Zone("zone_dawn");

            Assert.IsFalse(progression.IsZoneAvailable(dawn), "En la temporada 0 todavía no existe.");
            Assert.IsTrue(progression.IsZoneAvailable(Zone("zone_east")), "Las de siempre sí.");

            economy.AddCoins(10000000L);
            Assert.AreEqual(0L, progression.TryPay(dawn, dawn.UnlockCost), "No se cobra por una granja que no apareció.");
            Assert.IsFalse(progression.IsZoneUnlocked(dawn));

            // Con todo lo de la temporada 0 comprado ya se puede cerrar: la granja de temporada no cuenta todavía.
            progression.Restore(Bought(BaseZones()));
            Assert.IsTrue(prestige.IsReady, "Sin la granja de temporada, la temporada 0 ya está completa.");

            // El pad de decoraciones se abre con todas las zonas que existen: la granja de temporada que no apareció no lo traba.
            var decorations = Create<DecorationManager>();
            decorations.Initialize(_config);
            Assert.AreEqual(BaseZones().Length, progression.AvailableZoneCount);
            Assert.IsTrue(decorations.IsUnlocked);
        }

        [Test]
        public void StartingTheSeason_BringsTheNextFarm_AndThenItCountsForClosingTheSeason()
        {
            CreateServices(out EconomyManager economy, out ProgressionManager progression, out PrestigeManager prestige);
            ZoneData dawn = Zone("zone_dawn");
            ZoneData oak = Zone("zone_oak");

            progression.Restore(Bought(BaseZones()));
            Assert.IsTrue(prestige.TryStartSeason());
            Assert.AreEqual(1, prestige.Seasons);

            Assert.IsTrue(progression.IsZoneAvailable(dawn), "La temporada 1 trae el Campo del Alba...");
            Assert.IsFalse(progression.IsZoneAvailable(oak), "...y todavía no la Quinta del Roble.");
            Assert.AreEqual(0, progression.UnlockedZoneCount, "El reinicio cierra todo.");

            // Se vuelve a comprar todo lo de siempre: ahora falta la granja nueva para poder cerrar la temporada.
            progression.Restore(Bought(BaseZones()));
            Assert.IsFalse(prestige.IsReady, "Falta el Campo del Alba.");

            economy.AddCoins(dawn.UnlockCost);
            Assert.AreEqual(dawn.UnlockCost, progression.TryPay(dawn, dawn.UnlockCost));
            Assert.IsTrue(progression.IsZoneUnlocked(dawn));
            Assert.IsTrue(prestige.IsReady, "Con la granja nueva comprada, se puede cerrar la temporada 1.");

            Assert.IsTrue(prestige.TryStartSeason());
            Assert.IsTrue(progression.IsZoneAvailable(oak), "La temporada 2 trae la siguiente.");
        }

        // ---------- Perk de Suerte ----------

        [Test]
        public void TheRealConfig_HasTheLuckPerk()
        {
            PerkData luck = _config.Perks.First(perk => perk.Kind == PerkKind.RareChance);
            Assert.AreEqual("perk_luck", luck.Id);
            Assert.Greater(luck.ValuePerPick, 0f);
            Assert.AreEqual(10, luck.MaxPicks);
            Assert.Less(_config.RareHarvestChance + luck.TotalAt(luck.MaxPicks), 0.25f, "Al máximo la dorada sigue siendo rara.");
            StringAssert.Contains("0,5", luck.DescribePick().Replace('.', ','));
        }

        [Test]
        public void EachLuckPick_RaisesTheGoldenHarvestChance()
        {
            CreateServices(out EconomyManager economy, out ProgressionManager progression, out _);
            PerkData luck = _config.Perks.First(perk => perk.Kind == PerkKind.RareChance);
            CropData crop = _config.Crops[0];

            economy.RareRoll = () => 0.04f;   // justo arriba del 3 % de siempre
            Assert.AreEqual(0f, economy.RareChanceBonus);
            Assert.AreEqual(0L, economy.TryRareHarvest(crop), "Con 0,04 y 3 % no sale dorada.");

            progression.Restore(new ProgressData { perkPicks = new[] { new PerkPickEntry { id = luck.Id, picks = 4 } } });
            Assert.AreEqual(luck.ValuePerPick * 4f, progression.Modifiers.RareChanceBonus, 1e-6f);
            Assert.AreEqual(luck.ValuePerPick * 4f, economy.RareChanceBonus, 1e-6f, "El perk llega a la economía.");
            Assert.Greater(economy.TryRareHarvest(crop), 0L, "Con 4 elecciones (5 %) el mismo 0,04 sí sale dorada.");

            progression.Restore(new ProgressData());
            Assert.AreEqual(0f, economy.RareChanceBonus, "Sin elecciones vuelve a lo de siempre.");
        }
    }
}
