using System.Text;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FarmIdle.Tests
{
    // Los servicios con estado armados por código, en el mismo orden que el Bootstrapper (economía, mochila, progresión y, al final,
    // el SaveManager con los tres registrados y la partida cargada), sobre un FieldTestWorld (que ya trae GameManager en Playing y
    // el PoolManager). Con un almacenamiento en memoria, así que nada toca la partida real del dispositivo.
    //   Start()   levanta los servicios y carga lo que haya en el almacenamiento (como al abrir el juego).
    //   Stop()    los destruye SIN guardar (como un cierre brusco).
    //   Restart() guarda y vuelve a arrancar: "cerrar y volver a abrir el juego".
    // Datos de prueba: monedas iniciales 50, mochila de 6, cultivos trigo ($4), zanahoria ($6) y tomate ($10), dos zonas (300 y 500),
    // mejoras de capacidad (+4 por 100, +8 por 200), precio (x1,5 por 150), velocidad (x1,2 por 80) y crecimiento (x2 por 60), y dos perks
    // (Fuerza +1 hasta 5 veces y Velocidad +5 % hasta 5 veces). Guardar espera 0,5 s.
    internal sealed class SaveRig
    {
        public GameConfig Config { get; private set; }
        public CropData[] Crops { get; private set; }
        public ZoneData[] Zones { get; private set; }
        public UpgradeData[] Upgrades { get; private set; }
        public PerkData[] Perks { get; private set; }
        public MemorySaveStorage Storage { get; private set; }

        public EconomyManager Economy { get; private set; }
        public BackpackManager Backpack { get; private set; }
        public ProgressionManager Progression { get; private set; }
        public SaveManager Save { get; private set; }

        public CropData Wheat => Crops[0];
        public CropData Carrot => Crops[1];
        public CropData Tomato => Crops[2];
        public UpgradeData Capacity => Upgrades[0];
        public UpgradeData Price => Upgrades[1];
        public UpgradeData Speed => Upgrades[2];
        public UpgradeData Growth => Upgrades[3];
        public PerkData Strength => Perks[0];
        public PerkData Quickness => Perks[1];

        private FieldTestWorld _world;
        private GameObject _services;

        public static SaveRig Create(FieldTestWorld world, MemorySaveStorage storage = null)
        {
            var rig = new SaveRig { _world = world, Storage = storage ?? new MemorySaveStorage() };

            rig.Crops = new[]
            {
                CreateCrop("wheat", 4, world), CreateCrop("carrot", 6, world), CreateCrop("tomato", 10, world)
            };
            rig.Zones = new[] { TestReflection.CreateZone("zone-a", 300), TestReflection.CreateZone("zone-b", 500) };
            rig.Upgrades = new[]
            {
                TestReflection.CreateUpgrade("capacity", UpgradeKind.BackpackCapacity, (100L, 4f), (200L, 8f)),
                TestReflection.CreateUpgrade("price", UpgradeKind.SellPrice, (150L, 1.5f)),
                TestReflection.CreateUpgrade("speed", UpgradeKind.MoveSpeed, (80L, 1.2f)),
                TestReflection.CreateUpgrade("growth", UpgradeKind.GrowthSpeed, (60L, 2f))
            };
            rig.Perks = new[]
            {
                TestReflection.CreatePerk("perk-strength", PerkKind.BackpackCapacity, 1f, 5),
                TestReflection.CreatePerk("perk-quickness", PerkKind.MoveSpeed, 0.05f, 5)
            };

            rig.Config = ScriptableObject.CreateInstance<GameConfig>();
            TestReflection.Set(rig.Config, "startingCoins", 50);
            TestReflection.Set(rig.Config, "backpackCapacity", 6);
            TestReflection.Set(rig.Config, "zones", rig.Zones);
            TestReflection.Set(rig.Config, "upgrades", rig.Upgrades);
            TestReflection.Set(rig.Config, "crops", rig.Crops);
            TestReflection.Set(rig.Config, "perks", rig.Perks);
            TestReflection.Set(rig.Config, "saveDelaySeconds", 0.5f);
            TestReflection.Set(rig.Config, "purchaseSeconds", 0.8f);
            TestReflection.Set(rig.Config, "purchaseStartDelaySeconds", 0.1f);
            return rig;
        }

        private static CropData CreateCrop(string id, int sellPrice, FieldTestWorld world)
        {
            CropData crop = TestReflection.CreateCrop(id, 5f, world.PlantTemplate);
            TestReflection.Set(crop, "sellPrice", sellPrice);
            return crop;
        }

        // Levanta los servicios y carga la partida del almacenamiento, como el Bootstrapper.
        public void Start()
        {
            _services = new GameObject("SaveRigServices");
            _services.transform.SetParent(_world.Root.transform, false);

            Economy = Add<EconomyManager>();
            Economy.Initialize(Config);

            Backpack = Add<BackpackManager>();
            Backpack.Initialize(Config);

            Progression = Add<ProgressionManager>();
            Progression.Initialize(Config);

            Save = Add<SaveManager>();
            Save.Initialize(Config, Storage);
            Save.Register(Economy);
            Save.Register(Progression);
            Save.Register(Backpack);
            Save.Load();
        }

        // Cierre brusco: los servicios desaparecen sin guardar.
        public void Stop()
        {
            if (_services != null) Object.DestroyImmediate(_services);
            _services = null;
        }

        // Cerrar y volver a abrir el juego: guarda (por si había cambios sin escribir) y arranca de nuevo.
        public void Restart()
        {
            Save.SaveNow(force: true);
            Stop();
            Start();
        }

        public void Destroy()
        {
            Stop();
            foreach (CropData crop in Crops) Object.DestroyImmediate(crop);
            foreach (ZoneData zone in Zones) Object.DestroyImmediate(zone);
            foreach (UpgradeData upgrade in Upgrades) Object.DestroyImmediate(upgrade);
            foreach (PerkData perk in Perks) Object.DestroyImmediate(perk);
            Object.DestroyImmediate(Config);
        }

        private T Add<T>() where T : Component
        {
            var go = new GameObject(typeof(T).Name);
            go.transform.SetParent(_services.transform, false);
            return go.AddComponent<T>();
        }

        // Todo el estado que importa en una sola línea, para comparar "antes de cerrar" con "después de volver a abrir".
        public string Describe()
        {
            var text = new StringBuilder();
            text.Append("coins=").Append(Economy.Balance);

            text.Append(" bag=[");
            for (int i = 0; i < Backpack.Backpack.Items.Count; i++)
            {
                if (i > 0) text.Append(',');
                text.Append(Backpack.Backpack.Items[i].Id);
            }
            text.Append("] cap=").Append(Backpack.Backpack.Capacity);

            foreach (ZoneData zone in Zones)
            {
                text.Append(' ').Append(zone.Id).Append(Progression.IsZoneUnlocked(zone) ? "+" : "-").Append(Progression.PaidFor(zone));
            }

            foreach (UpgradeData upgrade in Upgrades)
            {
                text.Append(' ').Append(upgrade.Id).Append(':').Append(Progression.LevelOf(upgrade)).Append('/').Append(Progression.PaidFor(upgrade));
            }

            text.Append(" xp=").Append(Progression.Xp).Append(" lvl=").Append(Progression.Level).Append(" pts=").Append(Progression.PerkPoints);
            foreach (PerkData perk in Perks)
            {
                text.Append(' ').Append(perk.Id).Append(':').Append(Progression.PicksOf(perk));
            }

            ProgressionModifiers m = Progression.Modifiers;
            text.Append(" mods=").Append(m.BackpackBonus).Append('/').Append(m.MoveSpeed.ToString("0.###")).Append('/')
                .Append(m.SellPrice.ToString("0.###")).Append('/').Append(m.GrowthSpeed.ToString("0.###"));
            return text.ToString();
        }
    }
}
