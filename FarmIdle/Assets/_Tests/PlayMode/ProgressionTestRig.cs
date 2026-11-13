using UnityEngine;
using Object = UnityEngine.Object;

namespace FarmIdle.Tests
{
    // La progresión armada por código sobre un FieldTestWorld: un GameConfig con zonas y mejoras de prueba y los servicios REALES
    // en el orden del Bootstrapper (EconomyManager con sus monedas iniciales, BackpackManager y ProgressionManager). Sirve para
    // probar compras, efectos y pads sin cargar el nivel. Con 'capacityBonus' > 0 la mochila ya trae una mejora de capacidad en
    // la tabla (para que su máximo cuente con ella). Las zonas y mejoras son ScriptableObjects de prueba que se destruyen al final.
    internal sealed class ProgressionRig
    {
        public GameConfig Config { get; private set; }
        public EconomyManager Economy { get; private set; }
        public BackpackManager Backpack { get; private set; }
        public ProgressionManager Progression { get; private set; }
        public ZoneData[] Zones { get; private set; }
        public UpgradeData[] Upgrades { get; private set; }

        public static ProgressionRig AddTo(FieldTestWorld world, long startingCoins, int backpackCapacity, long[] zoneCosts, params UpgradeData[] upgrades)
        {
            var rig = new ProgressionRig { Upgrades = upgrades };

            rig.Zones = new ZoneData[zoneCosts.Length];
            for (int i = 0; i < zoneCosts.Length; i++)
            {
                rig.Zones[i] = TestReflection.CreateZone("zone-" + i, zoneCosts[i]);
            }

            rig.Config = ScriptableObject.CreateInstance<GameConfig>();
            TestReflection.Set(rig.Config, "startingCoins", (int)startingCoins);
            TestReflection.Set(rig.Config, "backpackCapacity", backpackCapacity);
            TestReflection.Set(rig.Config, "zones", rig.Zones);
            TestReflection.Set(rig.Config, "upgrades", upgrades);
            TestReflection.Set(rig.Config, "purchaseSeconds", 0.8f);
            TestReflection.Set(rig.Config, "purchaseStartDelaySeconds", 0.1f);

            rig.Economy = AddService<EconomyManager>(world);
            rig.Economy.Initialize(rig.Config);

            rig.Backpack = AddService<BackpackManager>(world);
            rig.Backpack.Initialize(rig.Config);

            rig.Progression = AddService<ProgressionManager>(world);
            rig.Progression.Initialize(rig.Config);
            return rig;
        }

        private static T AddService<T>(FieldTestWorld world) where T : Component
        {
            var go = new GameObject(typeof(T).Name);
            go.transform.SetParent(world.Root.transform, false);
            return go.AddComponent<T>();
        }

        // Los servicios cuelgan del mundo y se destruyen con él; solo los datos de prueba quedan aparte.
        public void Destroy()
        {
            if (Zones != null)
            {
                foreach (ZoneData zone in Zones)
                {
                    if (zone != null) Object.DestroyImmediate(zone);
                }
            }

            if (Upgrades != null)
            {
                foreach (UpgradeData upgrade in Upgrades)
                {
                    if (upgrade != null) Object.DestroyImmediate(upgrade);
                }
            }

            if (Config != null) Object.DestroyImmediate(Config);
        }
    }
}
