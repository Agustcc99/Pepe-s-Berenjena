using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FarmIdle.Tests
{
    // Vigila los datos de la progresión: la lógica de UpgradeData sobre instancias de prueba y los assets reales de zonas y
    // mejoras (ids únicos los revisa GameDataTests para todos los datos). Si algo falla, falta correr
    // FarmIdle > Fase 2 > Construir assets y escenas.
    public class ProgressionDataTests
    {
        private static List<T> LoadAll<T>() where T : Object
        {
            var assets = new List<T>();
            foreach (string guid in AssetDatabase.FindAssets("t:" + typeof(T).Name))
            {
                assets.Add(AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid)));
            }

            return assets;
        }

        private static GameConfig LoadConfig()
        {
            string[] guids = AssetDatabase.FindAssets("t:GameConfig");
            Assert.AreEqual(1, guids.Length, "Tiene que haber un único GameConfig.");
            return AssetDatabase.LoadAssetAtPath<GameConfig>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        // ---------- La lógica de UpgradeData ----------

        [Test]
        public void UpgradeData_LevelsAreOneBased_AndLevelZeroIsNeutral()
        {
            UpgradeData speed = ProgressionTestData.Upgrade("speed", UpgradeKind.MoveSpeed, (100L, 1.15f), (250L, 1.3f));
            UpgradeData capacity = ProgressionTestData.Upgrade("capacity", UpgradeKind.BackpackCapacity, (100L, 4f), (250L, 8f));
            try
            {
                Assert.AreEqual(2, speed.MaxLevel);
                Assert.AreEqual(100L, speed.CostOfLevel(1));
                Assert.AreEqual(250L, speed.CostOfLevel(2));
                Assert.AreEqual(0L, speed.CostOfLevel(0), "El nivel 0 no se compra.");
                Assert.AreEqual(0L, speed.CostOfLevel(3), "Más allá del máximo no hay costo.");

                Assert.AreEqual(1f, speed.Identity);
                Assert.AreEqual(0f, capacity.Identity, "La capacidad suma: su neutro es 0 lugares extra.");
                Assert.AreEqual(1f, speed.ValueAtLevel(0));
                Assert.AreEqual(0f, capacity.ValueAtLevel(0));
                Assert.AreEqual(1f, speed.ValueAtLevel(-3), "Un nivel negativo vale como el 0.");
                Assert.AreEqual(1.15f, speed.ValueAtLevel(1));
                Assert.AreEqual(1.3f, speed.ValueAtLevel(2));
                Assert.AreEqual(1.3f, speed.ValueAtLevel(9), "Por encima del máximo vale como el máximo.");
            }
            finally
            {
                Object.DestroyImmediate(speed);
                Object.DestroyImmediate(capacity);
            }
        }

        [Test]
        public void UpgradeData_Describe_ShowsTheEffectOfALevel()
        {
            UpgradeData speed = ProgressionTestData.Upgrade("speed", UpgradeKind.MoveSpeed, (100L, 1.15f), (250L, 1.5f));
            UpgradeData capacity = ProgressionTestData.Upgrade("capacity", UpgradeKind.BackpackCapacity, (100L, 4f));
            try
            {
                Assert.AreEqual("+15 %", speed.Describe(1));
                Assert.AreEqual("+50 %", speed.Describe(2));
                Assert.AreEqual("+4 lugares", capacity.Describe(1));
            }
            finally
            {
                Object.DestroyImmediate(speed);
                Object.DestroyImmediate(capacity);
            }
        }

        [Test]
        public void UpgradeData_WithoutLevels_IsHarmless()
        {
            UpgradeData empty = ProgressionTestData.Upgrade("empty", UpgradeKind.SellPrice);
            try
            {
                Assert.AreEqual(0, empty.MaxLevel);
                Assert.AreEqual(1f, empty.ValueAtLevel(1));
                Assert.AreEqual(0L, empty.CostOfLevel(1));
            }
            finally
            {
                Object.DestroyImmediate(empty);
            }
        }

        // ---------- Los assets del proyecto ----------

        [Test]
        public void TheProject_HasZonesAndTheThreeRequiredUpgrades()
        {
            List<ZoneData> zones = LoadAll<ZoneData>();
            List<UpgradeData> upgrades = LoadAll<UpgradeData>();

            Assert.GreaterOrEqual(zones.Count, 1, "Falta al menos una zona bloqueable.");
            foreach (UpgradeKind required in new[] { UpgradeKind.BackpackCapacity, UpgradeKind.MoveSpeed, UpgradeKind.SellPrice })
            {
                Assert.IsTrue(upgrades.Any(u => u.Kind == required), "Falta la mejora de tipo " + required + ".");
            }

            Assert.AreEqual(upgrades.Count, upgrades.Select(u => u.Kind).Distinct().Count(), "Una mejora por tipo: dos del mismo tipo se acumularían sin que el diseño lo haya pedido.");
        }

        [Test]
        public void ZoneData_HasPlayableValues()
        {
            foreach (ZoneData zone in LoadAll<ZoneData>())
            {
                string path = AssetDatabase.GetAssetPath(zone);
                Assert.IsFalse(string.IsNullOrWhiteSpace(zone.DisplayName), "Sin nombre: " + path);
                Assert.Greater(zone.UnlockCost, 0L, "Una zona gratis no tiene sentido: " + path);
                Assert.LessOrEqual(zone.UnlockCost, 500000L, "Costo desorbitado: " + path);   // las zonas de F-02 llegan a decenas de miles
            }
        }

        [Test]
        public void UpgradeData_HasPlayableLevels()
        {
            foreach (UpgradeData upgrade in LoadAll<UpgradeData>())
            {
                string path = AssetDatabase.GetAssetPath(upgrade);
                Assert.IsFalse(string.IsNullOrWhiteSpace(upgrade.DisplayName), "Sin nombre: " + path);
                Assert.IsFalse(string.IsNullOrWhiteSpace(upgrade.ShortLabel), "Sin nombre corto para el panel de debug: " + path);
                Assert.GreaterOrEqual(upgrade.MaxLevel, 1, "Sin niveles: " + path);

                long previousCost = 0;
                float previousValue = upgrade.Identity;
                for (int level = 1; level <= upgrade.MaxLevel; level++)
                {
                    long cost = upgrade.CostOfLevel(level);
                    float value = upgrade.ValueAtLevel(level);

                    Assert.Greater(cost, previousCost, "El costo tiene que subir con cada nivel (nivel " + level + "): " + path);
                    // El primer nivel de Crecimiento es a propósito una compra casi regalada (60) para que la primera mejora llegue enseguida:
                    // el salto se mide desde los niveles que ya valen algo.
                    if (previousCost >= 100) Assert.LessOrEqual(cost, previousCost * 10f, "Salto de costo de más de x10 entre niveles (nivel " + level + "): " + path);
                    Assert.LessOrEqual(cost, 500000L, "Costo desorbitado (nivel " + level + "): " + path);

                    Assert.Greater(value, previousValue, "El efecto tiene que crecer con cada nivel (nivel " + level + "): " + path);
                    if (upgrade.Kind == UpgradeKind.BackpackCapacity)
                    {
                        Assert.AreEqual(Mathf.Round(value), value, "La capacidad suma lugares enteros: " + path);
                    }
                    else if (upgrade.Kind == UpgradeKind.OfflineCap)
                    {
                        Assert.AreEqual(Mathf.Round(value), value, "El Almacén suma segundos enteros: " + path);
                        Assert.LessOrEqual(value, 4 * 3600f, "Más de +4 h de tope vuelve inútil jugar activo (DEC-5): " + path);
                    }
                    else
                    {
                        Assert.LessOrEqual(value, 3f, "Un multiplicador de más de x3 rompería la economía (nivel " + level + "): " + path);
                    }

                    previousCost = cost;
                    previousValue = value;
                }
            }
        }

        // La primera compra tiene que llegar en el primer minuto de juego: la cuenta de la fase da unas 150-180 monedas por minuto
        // al empezar, así que lo más barato no puede pasar de ~250. Y el total del catálogo original (las zonas este y oeste con todas
        // las mejoras) se junta en un rato razonable; las zonas de F-02 son contenido de horas y su curva la fija B-02.
        [Test]
        public void TheCheapestPurchase_ComesWithinTheFirstMinutes_AndTheWholeCatalogHasASaneTotal()
        {
            var costs = new List<long>();
            foreach (ZoneData zone in LoadAll<ZoneData>())
            {
                if (zone.Id == "zone_east" || zone.Id == "zone_west") costs.Add(zone.UnlockCost);
            }

            foreach (UpgradeData upgrade in LoadAll<UpgradeData>())
            {
                if (upgrade.Kind == UpgradeKind.OfflineCap) continue;   // B-01: compra de fin de juego, no es del catálogo original

                for (int level = 1; level <= Mathf.Min(3, upgrade.MaxLevel); level++) costs.Add(upgrade.CostOfLevel(level));   // F-04 suma niveles 4 a 6
            }

            Assert.That(costs.Min(), Is.InRange(50L, 250L), "La primera compra tiene que llegar en el primer minuto.");
            Assert.That(costs.Sum(), Is.InRange(2000L, 60000L), "El total del catálogo original tiene que ser razonable (B-02 lo reparte en ~1 h 15 min de jugador).");
        }

        // F-04: más niveles de mejora no pueden pasar el tope permanente de velocidad (x1,5): cada nivel de más que no se note sería
        // plata tirada. Con los perks al máximo, la mejora de monedas más alta tiene que entrar justo bajo el tope.
        [Test]
        public void TheSpeedUpgrade_PlusMaxedSpeedPerks_StaysUnderThePermanentCap()
        {
            GameConfig config = LoadConfig();
            UpgradeData speed = LoadAll<UpgradeData>().Single(u => u.Kind == UpgradeKind.MoveSpeed);
            PerkData perk = LoadAll<PerkData>().Single(p => p.Kind == PerkKind.MoveSpeed);

            float total = speed.ValueAtLevel(speed.MaxLevel) * (1f + perk.ValuePerPick * perk.MaxPicks);
            Assert.LessOrEqual(total, config.MaxMoveSpeedMultiplier + 1e-4f, "La velocidad permanente máxima pasa el tope: algún nivel no se notaría.");
            Assert.Greater(total, config.MaxMoveSpeedMultiplier - 0.05f, "Y tampoco queda lejos del tope: sobra espacio para otro nivel.");
        }

        [Test]
        public void GameConfig_ListsEveryZoneAndUpgrade_WithoutNullsOrDuplicates()
        {
            GameConfig config = LoadConfig();
            List<ZoneData> zones = LoadAll<ZoneData>();
            List<UpgradeData> upgrades = LoadAll<UpgradeData>();

            Assert.IsFalse(config.Zones.Any(z => z == null), "Hay una zona vacía en el GameConfig.");
            Assert.IsFalse(config.Upgrades.Any(u => u == null), "Hay una mejora vacía en el GameConfig.");
            Assert.AreEqual(config.Zones.Count, config.Zones.Distinct().Count(), "Zona repetida en el GameConfig.");
            Assert.AreEqual(config.Upgrades.Count, config.Upgrades.Distinct().Count(), "Mejora repetida en el GameConfig.");

            CollectionAssert.AreEquivalent(zones, config.Zones, "El GameConfig tiene que listar todas las zonas del proyecto.");
            CollectionAssert.AreEquivalent(upgrades, config.Upgrades, "El GameConfig tiene que listar todas las mejoras del proyecto.");
        }

        [Test]
        public void GameConfig_PurchaseTimingAndMaxCapacity_AreSane()
        {
            GameConfig config = LoadConfig();

            Assert.That(config.PurchaseSeconds, Is.InRange(0.5f, 4f), "Pagar tiene que tardar un rato corto pero visible.");
            Assert.GreaterOrEqual(config.PurchaseStartDelaySeconds, 0f);
            Assert.Less(config.PurchaseStartDelaySeconds, 1f, "Una espera de un segundo o más hace pesada cada compra.");

            int expected = config.BackpackCapacity;
            foreach (UpgradeData upgrade in config.Upgrades.Where(u => u.Kind == UpgradeKind.BackpackCapacity))
            {
                expected += Mathf.RoundToInt(upgrade.ValueAtLevel(upgrade.MaxLevel));
            }

            foreach (PerkData perk in config.Perks.Where(p => p.Kind == PerkKind.BackpackCapacity))
            {
                expected += Mathf.RoundToInt(perk.TotalAt(perk.MaxPicks));
            }

            Assert.AreEqual(expected, config.MaxBackpackCapacity, "La capacidad máxima es la base más las mejoras y los perks de capacidad al tope.");
            Assert.GreaterOrEqual(config.MaxBackpackCapacity, config.BackpackCapacity);
            Assert.LessOrEqual(config.MaxBackpackCapacity, 30, "Con más de ~30 productos la pila de la espalda se aprieta demasiado (decisión de la Fase 7).");
        }
    }
}
