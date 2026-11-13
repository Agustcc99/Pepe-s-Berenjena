using UnityEngine;
using Object = UnityEngine.Object;

namespace FarmIdle.Tests
{
    // El lado del jugador armado por código sobre un FieldTestWorld: el BackpackManager, un granjero con PlayerActor y
    // BackpackStackView (con su ancla en la espalda, como en el builder) y cultivos de prueba, cada uno con un producto
    // "molde" con un AwakeCounter: sus Awakes cuentan los Instantiate reales, independientemente de lo que diga el pool.
    // El granjero lleva collider y Rigidbody cinemático, así que la física lo detecta como al jugador real.
    // Se arma desactivado y se enciende al final: el OnEnable de la pila ya ve todas sus referencias.
    internal sealed class BackpackRig
    {
        public GameConfig Config { get; private set; }
        public BackpackManager Manager { get; private set; }
        public GameObject Farmer { get; private set; }
        public PlayerActor Actor { get; private set; }
        public BackpackStackView View { get; private set; }
        public Transform Anchor { get; private set; }
        public CropData[] Crops { get; private set; }
        public GameObject[] ProductTemplates { get; private set; }
        public UpgradeData CapacityUpgrade { get; private set; }

        // Instancias que creó el Prewarm de la pila al activarse (lo que sumó el AwakeCounter).
        public int PrewarmAwakes { get; private set; }

        public IReadOnlyInventory Backpack => Manager.Backpack;

        // Cuántas instancias de cada producto deja creadas el Prewarm de la pila: la mochila entera más los que pueden
        // estar volando hacia la venta.
        // (la máxima: las mejoras de capacidad la agrandan después y la pila ya tiene todo prewarmeado)
        public int ProductPrewarm => Manager.MaxCapacity + View.DepartingHeadroom;

        // 'capacityUpgradeBonus' > 0 suma una mejora de capacidad de un solo nivel (esos lugares extra) al GameConfig, para que
        // la mochila pueda crecer después (SetCapacityBonus) y el Prewarm ya cuente con eso.
        public static BackpackRig AddTo(FieldTestWorld world, Vector3 farmerPosition, int capacity, int cropCount, int capacityUpgradeBonus = 0)
        {
            var rig = new BackpackRig();

            rig.Config = ScriptableObject.CreateInstance<GameConfig>();
            TestReflection.Set(rig.Config, "backpackCapacity", capacity);

            if (capacityUpgradeBonus > 0)
            {
                rig.CapacityUpgrade = TestReflection.CreateUpgrade("test-capacity", UpgradeKind.BackpackCapacity, (100L, (float)capacityUpgradeBonus));
                TestReflection.Set(rig.Config, "upgrades", new[] { rig.CapacityUpgrade });
            }

            var managerObject = new GameObject("BackpackManager");
            managerObject.transform.SetParent(world.Root.transform, false);
            rig.Manager = managerObject.AddComponent<BackpackManager>();
            rig.Manager.Initialize(rig.Config);

            rig.ProductTemplates = new GameObject[cropCount];
            rig.Crops = new CropData[cropCount];
            for (int i = 0; i < cropCount; i++)
            {
                rig.ProductTemplates[i] = CreateProductTemplate("Product-" + i);
                rig.Crops[i] = TestReflection.CreateCrop("crop-" + i, world.Crop.GrowSeconds, world.PlantTemplate, rig.ProductTemplates[i]);
            }

            rig.Farmer = new GameObject("Farmer");
            rig.Farmer.SetActive(false);
            rig.Farmer.transform.position = farmerPosition;

            var sphere = rig.Farmer.AddComponent<SphereCollider>();
            sphere.radius = 0.4f;
            var body = rig.Farmer.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;

            rig.Actor = rig.Farmer.AddComponent<PlayerActor>();

            rig.Anchor = new GameObject("BackpackAnchor").transform;
            rig.Anchor.SetParent(rig.Farmer.transform, false);
            rig.Anchor.localPosition = new Vector3(0f, 0.5f, -0.9f);

            rig.View = rig.Farmer.AddComponent<BackpackStackView>();
            TestReflection.Set(rig.View, "actor", rig.Actor);
            TestReflection.Set(rig.View, "anchor", rig.Anchor);
            TestReflection.Set(rig.View, "crops", rig.Crops);

            int awakesBefore = AwakeCounter.Awakes;
            rig.Farmer.SetActive(true);
            rig.PrewarmAwakes = AwakeCounter.Awakes - awakesBefore;
            return rig;
        }

        // Un producto "molde": se arma desactivado y se prende al final, así cada clon corre su Awake al crearse.
        public static GameObject CreateProductTemplate(string name)
        {
            var template = new GameObject(name);
            template.SetActive(false);
            template.transform.position = new Vector3(0f, -100f, 0f);   // fuera de la vista
            template.AddComponent<AwakeCounter>();
            template.SetActive(true);
            return template;
        }

        // Primero el granjero: al apagarse, la pila devuelve sus productos al pool. El BackpackManager cuelga del mundo
        // y se destruye con él.
        public void Destroy()
        {
            if (Farmer != null) Object.DestroyImmediate(Farmer);

            if (Crops != null)
            {
                foreach (CropData crop in Crops)
                {
                    if (crop != null) Object.DestroyImmediate(crop);
                }
            }

            if (ProductTemplates != null)
            {
                foreach (GameObject template in ProductTemplates)
                {
                    if (template != null) Object.DestroyImmediate(template);
                }
            }

            if (CapacityUpgrade != null) Object.DestroyImmediate(CapacityUpgrade);
            if (Config != null) Object.DestroyImmediate(Config);
        }
    }
}
