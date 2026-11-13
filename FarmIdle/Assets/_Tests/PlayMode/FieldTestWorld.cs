using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FarmIdle.Tests
{
    // Quien hace de actor en las pruebas: un objeto con collider y Rigidbody cinemático (la física manda los eventos
    // de trigger igual que con el CharacterController del jugador). 'CanAct' simula "el juego no está en Playing".
    public sealed class FakeActor : MonoBehaviour, IActor
    {
        public bool CanAct = true;

        public string ActorName => name;
        public bool CanInteract => CanAct;

        public static FakeActor Create(Vector3 position)
        {
            var go = new GameObject("FakeActor");
            go.transform.position = position;

            var sphere = go.AddComponent<SphereCollider>();
            sphere.radius = 0.4f;

            var body = go.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;

            return go.AddComponent<FakeActor>();
        }
    }

    // Un actor con mochila propia: igual que FakeActor (collider + Rigidbody cinemático, para que la física lo detecte)
    // pero además es un IBackpackOwner con su Inventory, sin pasar por el jugador ni por el BackpackManager. Cuenta
    // cuántas veces una parcela le ofreció un producto y desde dónde, para comprobar que la cosecha le habla solo
    // por la interfaz. También entrega productos (IBackpackDispenser) para probar la zona de venta sin el jugador.
    public sealed class FakeFarmer : MonoBehaviour, IActor, IBackpackOwner, IBackpackDispenser
    {
        public bool CanAct = true;
        public Inventory Backpack;
        public int Offers;         // veces que una parcela le ofreció un producto (acepte o no)
        public Vector3 LastFrom;   // de dónde salió el último producto que aceptó
        public int Dispenses;      // productos que entregó (sacados de su mochila de arriba hacia abajo)
        public int DispenseCalls;  // veces que la zona le pidió un producto (entregue o no)
        public Vector3 LastTo;     // hacia dónde le dijeron que viaja el último producto que entregó
        public float LastFlightSeconds;

        public string ActorName => name;
        public bool CanInteract => CanAct;

        public bool TryAccept(CropData crop, Vector3 from)
        {
            Offers++;
            if (!Backpack.TryAdd(crop)) return false;

            LastFrom = from;
            return true;
        }

        public bool TryDispense(Vector3 to, float flightSeconds, out CropData crop)
        {
            DispenseCalls++;
            if (!Backpack.TryPop(out crop)) return false;

            Dispenses++;
            LastTo = to;
            LastFlightSeconds = flightSeconds;
            return true;
        }

        public static FakeFarmer Create(Vector3 position, int capacity)
        {
            var go = new GameObject("FakeFarmer");
            go.transform.position = position;

            var sphere = go.AddComponent<SphereCollider>();
            sphere.radius = 0.4f;

            var body = go.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;

            var farmer = go.AddComponent<FakeFarmer>();
            farmer.Backpack = new Inventory(capacity);
            return farmer;
        }
    }

    // Los campos de los ScriptableObjects y de los componentes son privados ([SerializeField]): las pruebas los
    // cablean por reflexión, como lo haría el Inspector, sin agregar puertas traseras al código de producción.
    internal static class TestReflection
    {
        public static void Set(object target, string fieldName, object value)
        {
            for (Type type = target.GetType(); type != null; type = type.BaseType)
            {
                FieldInfo field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (field == null) continue;

                field.SetValue(target, value);
                return;
            }

            throw new MissingFieldException(target.GetType().Name, fieldName);
        }

        public static CropData CreateCrop(string id, float growSeconds, GameObject plantPrefab, GameObject productPrefab = null)
        {
            var crop = ScriptableObject.CreateInstance<CropData>();
            crop.name = id;
            Set(crop, "id", id);
            Set(crop, "displayName", id);
            Set(crop, "growSeconds", growSeconds);
            Set(crop, "plantPrefab", plantPrefab);
            Set(crop, "productPrefab", productPrefab);
            return crop;
        }

        public static ZoneData CreateZone(string id, long unlockCost)
        {
            var zone = ScriptableObject.CreateInstance<ZoneData>();
            zone.name = id;
            Set(zone, "id", id);
            Set(zone, "displayName", id);
            Set(zone, "unlockCost", unlockCost);
            return zone;
        }

        public static RewardData CreateReward(string id, RewardKind kind, float magnitude, float durationSeconds = 10f, float cooldownSeconds = 0f)
        {
            var reward = ScriptableObject.CreateInstance<RewardData>();
            reward.name = id;
            Set(reward, "id", id);
            Set(reward, "displayName", id);
            Set(reward, "shortLabel", id);
            Set(reward, "kind", kind);
            Set(reward, "magnitude", magnitude);
            Set(reward, "durationSeconds", durationSeconds);
            Set(reward, "cooldownSeconds", cooldownSeconds);
            return reward;
        }

        public static PerkData CreatePerk(string id, PerkKind kind, float valuePerPick, int maxPicks)
        {
            var perk = ScriptableObject.CreateInstance<PerkData>();
            perk.name = id;
            Set(perk, "id", id);
            Set(perk, "displayName", id);
            Set(perk, "shortLabel", id);
            Set(perk, "kind", kind);
            Set(perk, "valuePerPick", valuePerPick);
            Set(perk, "maxPicks", maxPicks);
            return perk;
        }

        // Una mejora con sus niveles: cada uno es (costo, efecto total).
        public static UpgradeData CreateUpgrade(string id, UpgradeKind kind, params (long cost, float value)[] levels)
        {
            var upgrade = ScriptableObject.CreateInstance<UpgradeData>();
            upgrade.name = id;
            Set(upgrade, "id", id);
            Set(upgrade, "displayName", id);
            Set(upgrade, "kind", kind);

            var table = new UpgradeLevel[levels.Length];
            for (int i = 0; i < levels.Length; i++) table[i] = new UpgradeLevel(levels[i].cost, levels[i].value);
            Set(upgrade, "levels", table);
            return upgrade;
        }
    }

    // Un campo de parcelas armado por código, con sus servicios (GameManager ya en Playing, PoolManager, PlotCensus),
    // un cultivo de prueba que crece rápido y una planta "molde" con un AwakeCounter: sus Awakes cuentan los
    // Instantiate reales, independientemente de lo que diga el pool.
    // Después de Create hay que esperar un frame: el Start() del PlotField es el que hace el Prewarm.
    internal sealed class FieldTestWorld
    {
        public GameObject Root { get; private set; }
        public GameManager Game { get; private set; }
        public PoolManager Pools { get; private set; }
        public PlotCensus Census { get; private set; }
        public GameObject PlantTemplate { get; private set; }
        public CropData Crop { get; private set; }
        public PlotView[] Plots { get; private set; }
        public PlotField Field { get; private set; }

        private readonly List<CropData> _extraCrops = new List<CropData>();

        // Otro cultivo con la misma planta molde (para mezclar cultivos entre parcelas). Se destruye con el mundo.
        public CropData CreateExtraCrop(string id, GameObject productPrefab = null)
        {
            CropData crop = TestReflection.CreateCrop(id, Crop.GrowSeconds, PlantTemplate, productPrefab);
            _extraCrops.Add(crop);
            return crop;
        }

        public static FieldTestWorld Create(int plotCount, float growSeconds)
        {
            var world = new FieldTestWorld { Root = new GameObject("FieldTestWorld") };

            world.Game = AddService<GameManager>(world.Root);
            world.Game.Initialize();
            world.Game.MarkReady();
            world.Game.StartPlaying();

            world.Pools = AddService<PoolManager>(world.Root);
            world.Pools.Initialize();

            world.Census = AddService<PlotCensus>(world.Root);
            world.Census.Initialize();

            world.PlantTemplate = CreatePlantTemplate();
            world.Crop = TestReflection.CreateCrop("test-crop", growSeconds, world.PlantTemplate);

            var fieldObject = new GameObject("Field");
            fieldObject.transform.SetParent(world.Root.transform, false);

            world.Plots = new PlotView[plotCount];
            for (int i = 0; i < plotCount; i++)
            {
                world.Plots[i] = CreatePlot(fieldObject.transform, i, world.Crop);
            }

            world.Field = fieldObject.AddComponent<PlotField>();
            TestReflection.Set(world.Field, "plots", world.Plots);
            return world;
        }

        // Un segundo campo apagado (como el de una zona cerrada) con 'plotCount' parcelas del mismo cultivo y la misma planta
        // molde. Devuelve su raíz (desactivada) y llena 'plots' y 'field'. Cuelga del mundo y se destruye con él.
        public GameObject AddZoneField(int plotCount, float x, out PlotView[] plots, out PlotField field)
        {
            var root = new GameObject("ZoneField");
            root.SetActive(false);
            root.transform.SetParent(Root.transform, false);
            root.transform.localPosition = new Vector3(x, 0f, 0f);

            plots = new PlotView[plotCount];
            for (int i = 0; i < plotCount; i++) plots[i] = CreatePlot(root.transform, i, Crop);

            field = root.AddComponent<PlotField>();
            TestReflection.Set(field, "plots", plots);
            return root;
        }

        public void Destroy()
        {
            if (Root != null) Object.DestroyImmediate(Root);
            if (PlantTemplate != null) Object.DestroyImmediate(PlantTemplate);
            if (Crop != null) Object.DestroyImmediate(Crop);

            foreach (CropData extra in _extraCrops)
            {
                if (extra != null) Object.DestroyImmediate(extra);
            }
            _extraCrops.Clear();
        }

        private static T AddService<T>(GameObject root) where T : Component
        {
            var go = new GameObject(typeof(T).Name);
            go.transform.SetParent(root.transform, false);
            return go.AddComponent<T>();
        }

        // La planta se arma desactivada y se prende al final: así su Awake corre con las dos formas ya cableadas.
        private static GameObject CreatePlantTemplate()
        {
            var root = new GameObject("Plant (template)");
            root.SetActive(false);
            root.transform.position = new Vector3(0f, -100f, 0f);   // fuera de la vista

            var view = root.AddComponent<PlantView>();
            var growing = new GameObject("Growing");
            growing.transform.SetParent(root.transform, false);
            var ready = new GameObject("Ready");
            ready.transform.SetParent(root.transform, false);
            ready.SetActive(false);
            TestReflection.Set(view, "growingModel", growing);
            TestReflection.Set(view, "readyModel", ready);

            root.AddComponent<AwakeCounter>();
            root.SetActive(true);

            AwakeCounter.Awakes = 0;   // el propio molde ya corrió su Awake
            return root;
        }

        // Parcelas en fila, separadas 3 unidades, con un trigger de 1,4 x 2 x 1,4 como el del builder.
        private static PlotView CreatePlot(Transform parent, int index, CropData crop)
        {
            var go = new GameObject("Plot " + index);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(index * 3f, 0f, 0f);

            var trigger = go.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0f, 1f, 0f);
            trigger.size = new Vector3(1.4f, 2f, 1.4f);

            var view = go.AddComponent<PlotView>();
            go.AddComponent<InteractionTrigger>();

            var anchor = new GameObject("PlantAnchor").transform;
            anchor.SetParent(go.transform, false);
            anchor.localPosition = new Vector3(0f, 0.12f, 0f);

            TestReflection.Set(view, "crop", crop);
            TestReflection.Set(view, "plantAnchor", anchor);
            return view;
        }
    }
}
