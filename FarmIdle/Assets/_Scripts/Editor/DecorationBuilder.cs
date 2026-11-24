using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FarmIdle.EditorTools
{
    // F-17 (tanda 1): las 8 decoraciones cosméticas de la granja. Genera los datos (DecorationData), las mallas (todas con el material
    // de paleta único) y las coloca APAGADAS en el terreno libre a los lados de la plaza; DecorationSet las prende según lo comprado.
    // El pad de compra lo arma Phase8SceneBuilder (comparte el molde de los pads de espera).
    internal static class DecorationBuilder
    {
        private const string DataFolder = "Assets/ScriptableObjects/Decorations";
        private const string MeshesFolder = "Assets/Meshes/Environment";

        // F-20: las decoraciones se veían chicas al lado de la plaza. Se agrandan en la escena (la malla no cambia).
        private const float WorldScale = 1.6f;

        private sealed class DecorationSpec
        {
            public string Id, Name;
            public int Cost;
            public Vector3 Position;
            public float Yaw;
        }

        // En el orden de compra, de la más barata a la más cara: 500.000 monedas en total (~1,4 h de juego con el ingreso del final,
        // ECONOMIA_V1.md). Lugares: el césped libre a los lados de la plaza (x ±11,9 es la valla), a la vista desde adentro.
        private static readonly DecorationSpec[] Specs =
        {
            new DecorationSpec { Id = "deco_lamp", Name = "Farol", Cost = 25000, Position = new Vector3(-14f, 0f, -12f) },
            new DecorationSpec { Id = "deco_scarecrow", Name = "Espantapájaros", Cost = 35000, Position = new Vector3(14f, 0f, -12f), Yaw = 15f },
            new DecorationSpec { Id = "deco_bench", Name = "Banco", Cost = 45000, Position = new Vector3(-14f, 0f, -17f), Yaw = 90f },
            new DecorationSpec { Id = "deco_flowers", Name = "Parterre de flores", Cost = 55000, Position = new Vector3(14f, 0f, -17f) },
            new DecorationSpec { Id = "deco_pond", Name = "Estanque", Cost = 70000, Position = new Vector3(-15f, 0f, -23f) },
            new DecorationSpec { Id = "deco_windmill", Name = "Molino", Cost = 80000, Position = new Vector3(15f, 0f, -23f) },
            new DecorationSpec { Id = "deco_flag", Name = "Bandera", Cost = 90000, Position = new Vector3(-19f, 0f, -14f) },
            new DecorationSpec { Id = "deco_haystack", Name = "Fardo de heno", Cost = 100000, Position = new Vector3(19f, 0f, -14f), Yaw = 30f },
        };

        // Ayudantes (tanda 2): tres niveles, 850.000 en total (~2,4 h con el ingreso del final). Share = fracción de la producción de
        // las parcelas que suman solos. PROVISORIOS: se ajustan con datos reales (B-05).
        private static readonly (string id, string name, int cost, float share)[] Helpers =
        {
            ("helper_1", "Peón", 100000, 0.10f),
            ("helper_2", "Capataz", 250000, 0.20f),
            ("helper_3", "Cosechadora", 500000, 0.35f),
        };

        // Skins del jugador (tanda 3): seis colores, 700.000 en total (~2 h). El naranja de fábrica no es un asset.
        private static readonly (string id, string name, int cost, Color body, Color nose)[] Skins =
        {
            ("skin_tomato", "Tomate", 60000, new Color(0.85f, 0.20f, 0.18f), new Color(1f, 0.9f, 0.5f)),
            ("skin_sky", "Cielo", 80000, new Color(0.25f, 0.60f, 0.95f), Color.white),
            ("skin_leaf", "Hoja", 100000, new Color(0.35f, 0.75f, 0.25f), new Color(1f, 0.95f, 0.6f)),
            ("skin_grape", "Uva", 120000, new Color(0.55f, 0.30f, 0.80f), new Color(1f, 0.85f, 0.95f)),
            ("skin_rose", "Rosa", 150000, new Color(0.95f, 0.50f, 0.70f), Color.white),
            ("skin_gold", "Oro", 190000, new Color(0.95f, 0.78f, 0.20f), new Color(0.6f, 0.3f, 0.1f)),
        };

        // Piezas sueltas que se mueven (F-21): aspas, tela, bichitos. Cuelgan de la decoración y se apagan con ella.
        private static readonly string[] PartIds =
        {
            "part_windmill_blades", "part_flag_cloth", "part_butterfly", "part_firefly", "part_duck", "part_bird", "part_chicken",
        };

        private static string PartMeshPath(string id) { return MeshesFolder + "/SM_" + id + ".asset"; }

        private static string SkinPath(string id) { return DataFolder + "/" + id + ".asset"; }

        private static string HelperPath(string id) { return DataFolder + "/" + id + ".asset"; }

        private static string DataPath(DecorationSpec spec) { return DataFolder + "/" + spec.Id + ".asset"; }
        private static string MeshPath(DecorationSpec spec) { return MeshesFolder + "/SM_" + spec.Id + ".asset"; }

        // ---------- Assets ----------

        internal static void CreateAssets()
        {
            Phase1SceneBuilder.EnsureFolder(DataFolder);
            Phase1SceneBuilder.EnsureFolder(MeshesFolder);

            foreach (DecorationSpec spec in Specs)
            {
                if (AssetDatabase.LoadAssetAtPath<DecorationData>(DataPath(spec)) == null)   // respeta lo que se haya ajustado a mano
                {
                    var data = ScriptableObject.CreateInstance<DecorationData>();
                    var serialized = new SerializedObject(data);
                    serialized.FindProperty("id").stringValue = spec.Id;
                    serialized.FindProperty("displayName").stringValue = spec.Name;
                    serialized.FindProperty("cost").intValue = spec.Cost;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    AssetDatabase.CreateAsset(data, DataPath(spec));
                }

                Phase3SceneBuilder.SaveMesh(BuildMesh(spec.Id).ToMesh("SM_" + spec.Id), MeshPath(spec));
            }

            foreach (string partId in PartIds) Phase3SceneBuilder.SaveMesh(BuildMesh(partId).ToMesh("SM_" + partId), PartMeshPath(partId));

            foreach ((string id, string name, int cost, float share) helper in Helpers)
            {
                if (AssetDatabase.LoadAssetAtPath<HelperData>(HelperPath(helper.id)) != null) continue;

                var data = ScriptableObject.CreateInstance<HelperData>();
                var serialized = new SerializedObject(data);
                serialized.FindProperty("id").stringValue = helper.id;
                serialized.FindProperty("displayName").stringValue = helper.name;
                serialized.FindProperty("cost").intValue = helper.cost;
                serialized.FindProperty("share").floatValue = helper.share;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.CreateAsset(data, HelperPath(helper.id));
            }

            foreach ((string id, string name, int cost, Color body, Color nose) skin in Skins)
            {
                if (AssetDatabase.LoadAssetAtPath<SkinData>(SkinPath(skin.id)) != null) continue;

                var data = ScriptableObject.CreateInstance<SkinData>();
                var serialized = new SerializedObject(data);
                serialized.FindProperty("id").stringValue = skin.id;
                serialized.FindProperty("displayName").stringValue = skin.name;
                serialized.FindProperty("cost").intValue = skin.cost;
                serialized.FindProperty("bodyColor").colorValue = skin.body;
                serialized.FindProperty("noseColor").colorValue = skin.nose;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.CreateAsset(data, SkinPath(skin.id));
            }

            AssetDatabase.SaveAssets();
            LinkConfig();
            AssetDatabase.SaveAssets();
        }

        // Las decoraciones nuevas entran al final de la lista del GameConfig (el orden es el de compra).
        private static void LinkConfig()
        {
            var config = Phase2SceneBuilder.LoadRequired<GameConfig>(Phase2SceneBuilder.ConfigPath);
            var serialized = new SerializedObject(config);

            if (serialized.FindProperty("decorations") == null) throw new InvalidOperationException("GameConfig no tiene la lista 'decorations'.");

            var assets = new List<Object>();
            foreach (DecorationSpec spec in Specs) assets.Add(Phase2SceneBuilder.LoadRequired<DecorationData>(DataPath(spec)));
            Phase7SceneBuilder.AppendMissing(serialized, "decorations", assets);

            if (serialized.FindProperty("helpers") == null) throw new InvalidOperationException("GameConfig no tiene la lista 'helpers'.");

            var helperAssets = new List<Object>();
            foreach ((string id, string name, int cost, float share) helper in Helpers) helperAssets.Add(Phase2SceneBuilder.LoadRequired<HelperData>(HelperPath(helper.id)));
            Phase7SceneBuilder.AppendMissing(serialized, "helpers", helperAssets);

            if (serialized.FindProperty("skins") == null) throw new InvalidOperationException("GameConfig no tiene la lista 'skins'.");

            var skinAssets = new List<Object>();
            foreach ((string id, string name, int cost, Color body, Color nose) skin in Skins) skinAssets.Add(Phase2SceneBuilder.LoadRequired<SkinData>(SkinPath(skin.id)));
            Phase7SceneBuilder.AppendMissing(serialized, "skins", skinAssets);

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(config);
        }

        // ---------- Escena ----------

        internal static void BuildWorldPieces(Transform parent)
        {
            // Los dos pads del sector Almacén (junto al de la mejora de almacén): decoraciones y ayudantes.
            Phase8SceneBuilder.BuildDwellPad<DecorationBuyPad>(parent, "DecorationPad_Buy", new Vector3(PlazaBuilder.DecorX, 0f, PlazaBuilder.StoreRowZ), Phase8SceneBuilder.PadIcon.Decor);
            Phase8SceneBuilder.BuildDwellPad<SkinBuyPad>(parent, "SkinPad_Buy", new Vector3(PlazaBuilder.SkinX, 0f, PlazaBuilder.AttributeRow2Z), Phase8SceneBuilder.PadIcon.Skin);
            Phase8SceneBuilder.BuildDwellPad<HelperBuyPad>(parent, "HelperPad_Buy", new Vector3(PlazaBuilder.HelperX, 0f, PlazaBuilder.StoreRowZ), Phase8SceneBuilder.PadIcon.Helper);

            var material = Phase2SceneBuilder.LoadRequired<Material>(Phase3SceneBuilder.PaletteMaterialPath);

            var root = new GameObject("Decorations");
            root.transform.SetParent(parent, false);
            var set = root.AddComponent<DecorationSet>();

            var items = new List<Object>();
            foreach (DecorationSpec spec in Specs)
            {
                var mesh = Phase2SceneBuilder.LoadRequired<Mesh>(MeshPath(spec));
                Transform item = PadBuilder.CreateMeshChild(root.transform, spec.Id, mesh, material, spec.Position);
                item.rotation = Quaternion.Euler(0f, spec.Yaw, 0f);
                item.localScale = Vector3.one * WorldScale;
                AddLife(item, spec.Id, material);
                item.gameObject.SetActive(false);   // DecorationSet la prende al comprarla
                items.Add(item.gameObject);
            }

            EditorUi.WireArray(set, "items", items);
        }

        // ---------- Vida (F-21) ----------

        // Lo que se mueve en cada decoración. Posiciones en el espacio de la malla (el ×1,6 de la escena las acompaña).
        private static void AddLife(Transform item, string id, Material material)
        {
            switch (id)
            {
                case "deco_lamp":   // luciérnagas que dan vueltas al farol
                    Orbiter(item, "Firefly_A", "part_firefly", material, new Vector3(0f, 2.9f, 0f), 0.55f, 0.35f, 0.25f, 0f, 1.2f);
                    Orbiter(item, "Firefly_B", "part_firefly", material, new Vector3(0f, 2.7f, 0f), 0.7f, 0.25f, 0.3f, 2.1f, 0.9f);
                    Orbiter(item, "Firefly_C", "part_firefly", material, new Vector3(0f, 3.1f, 0f), 0.45f, 0.45f, 0.2f, 4.2f, 1.5f);
                    break;

                case "deco_scarecrow":
                    Motion(item.gameObject, DecorationMotionKind.Sway, Vector3.forward, 3f, 0.25f, 0f, 0f);
                    break;

                case "deco_bench":   // un pajarito en el respaldo
                    Hopper(item, "Bird", "part_bird", material, new Vector3(0.55f, 1.25f, 0.27f), 0.1f, 0.5f);
                    break;

                case "deco_flowers":   // flores al viento y mariposas
                    Motion(item.gameObject, DecorationMotionKind.Sway, Vector3.right, 1.5f, 0.35f, 0f, 0f);
                    Orbiter(item, "Butterfly_A", "part_butterfly", material, new Vector3(0f, 1.1f, 0f), 1.0f, 0.18f, 0.2f, 0f, 3.5f, 0.8f);
                    Orbiter(item, "Butterfly_B", "part_butterfly", material, new Vector3(0f, 1.4f, 0f), 0.7f, 0.13f, 0.25f, 3f, 4.2f, 0.8f);
                    break;

                case "deco_pond":   // dos patos nadando despacio
                    Orbiter(item, "Duck_A", "part_duck", material, new Vector3(0f, 0.15f, 0f), 0.9f, 0.1f, 0.015f, 0f, 0f, 0f);
                    Orbiter(item, "Duck_B", "part_duck", material, new Vector3(0f, 0.15f, 0f), 0.5f, 0.07f, 0.015f, 3.4f, 0f, 0f);
                    break;

                case "deco_windmill":
                    Spinner(item, "Blades", "part_windmill_blades", material, new Vector3(0f, 3f, -1f), 35f);
                    break;

                case "deco_flag":
                    Transform cloth = Part(item, "Cloth", "part_flag_cloth", material, new Vector3(0f, 3.2f, 0f));
                    Motion(cloth.gameObject, DecorationMotionKind.Sway, Vector3.up, 14f, 0.4f, 0f, 0f);
                    break;

                case "deco_haystack":   // una gallina picoteando
                    Hopper(item, "Chicken", "part_chicken", material, new Vector3(0.5f, 0f, -1.3f), 0.08f, 0.9f);
                    break;
            }
        }

        private static Transform Part(Transform parent, string name, string partId, Material material, Vector3 localPosition)
        {
            return PadBuilder.CreateMeshChild(parent, name, Phase2SceneBuilder.LoadRequired<Mesh>(PartMeshPath(partId)), material, localPosition);
        }

        private static void Spinner(Transform item, string name, string partId, Material material, Vector3 position, float degreesPerSecond)
        {
            Transform part = Part(item, name, partId, material, position);
            Motion(part.gameObject, DecorationMotionKind.Spin, Vector3.forward, degreesPerSecond, 0f, 0f, 0f);
        }

        private static void Hopper(Transform item, string name, string partId, Material material, Vector3 position, float height, float speed)
        {
            Transform part = Part(item, name, partId, material, position);
            Motion(part.gameObject, DecorationMotionKind.Hop, Vector3.up, height, speed, 0f, position.x * 3f);
        }

        // Un bicho que da vueltas: un pivote que orbita (mira hacia donde va) con la malla adentro. Con 'pulseSpeed' > 0 la malla
        // además aletea (mariposa: achica el ancho) o brilla (luciérnaga: achica todo) según 'pulseAmount'.
        private static void Orbiter(Transform item, string name, string partId, Material material, Vector3 center, float radius, float turnsPerSecond,
            float bob, float phase, float pulseSpeed, float pulseAmount = 0.5f)
        {
            var pivot = new GameObject(name);
            pivot.transform.SetParent(item, false);
            pivot.transform.localPosition = center;
            Motion(pivot, DecorationMotionKind.Orbit, Vector3.up, radius, turnsPerSecond, bob, phase);

            Transform part = Part(pivot.transform, "Mesh", partId, material, Vector3.zero);
            if (pulseSpeed > 0f)
            {
                Vector3 axes = partId == "part_butterfly" ? Vector3.right : Vector3.one;
                Motion(part.gameObject, DecorationMotionKind.Pulse, axes, pulseAmount, pulseSpeed, 0f, phase);
            }
        }

        private static void Motion(GameObject target, DecorationMotionKind kind, Vector3 axis, float amount, float speed, float bob, float phase)
        {
            var motion = target.AddComponent<DecorationMotion>();
            var serialized = new SerializedObject(motion);
            serialized.FindProperty("kind").enumValueIndex = (int)kind;
            serialized.FindProperty("axis").vector3Value = axis;
            serialized.FindProperty("amount").floatValue = amount;
            serialized.FindProperty("speed").floatValue = speed;
            serialized.FindProperty("bob").floatValue = bob;
            serialized.FindProperty("phase").floatValue = phase;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        // El dibujo del pad de compra (sobre la plataforma común de Phase8SceneBuilder): un mástil con una banderola roja y, al pie,
        // una maceta con una flor. 'y' es la altura de la superficie de la plataforma.
        internal static void AddPadIcon(PaletteMeshBuilder mesh, float y, Phase8SceneBuilder.PadIcon icon)
        {
            if (icon == Phase8SceneBuilder.PadIcon.Skin)
            {
                // Un muñeco naranja como el jugador (cápsula y nariz) con una estrella a un lado: "cambiar de color".
                mesh.AddCylinder(new Vector3(0f, y + 0.65f, 0f), 0.4f, 0.7f, 12, Quaternion.identity, Palette.CarrotOrange);
                mesh.AddSphere(new Vector3(0f, y + 1.0f, 0f), 0.4f, Palette.CarrotOrange);
                mesh.AddSphere(new Vector3(0f, y + 0.3f, 0f), 0.4f, Palette.CarrotOrange);
                mesh.AddBox(new Vector3(0f, y + 1.1f, -0.42f), new Vector3(0.26f, 0.26f, 0.26f), Quaternion.identity, Palette.White);
                mesh.AddBox(new Vector3(0.78f, y + 0.9f, 0f), new Vector3(0.34f, 0.34f, 0.08f), Quaternion.Euler(0f, 0f, 45f), Palette.Flower);
                mesh.AddBox(new Vector3(0.78f, y + 0.9f, 0f), new Vector3(0.34f, 0.34f, 0.08f), Quaternion.identity, Palette.Purple);
                return;
            }

            if (icon == Phase8SceneBuilder.PadIcon.Helper)
            {
                // Un peón de frente: overol azul, cabeza, sombrero de paja y una horquilla al lado.
                mesh.AddBox(new Vector3(0f, y + 0.5f, 0f), new Vector3(0.6f, 0.8f, 0.34f), Quaternion.identity, Palette.Water);
                mesh.AddSphere(new Vector3(0f, y + 1.1f, 0f), 0.24f, Palette.CarrotOrange);
                mesh.AddCylinder(new Vector3(0f, y + 1.28f, 0f), 0.44f, 0.05f, 12, Quaternion.identity, Palette.WheatStraw);
                mesh.AddCone(new Vector3(0f, y + 1.3f, 0f), 0.26f, new Vector3(0f, y + 1.55f, 0f), 8, Palette.WheatStraw);
                mesh.AddStanding(new Vector3(0.62f, y, 0f), new Vector3(0.06f, 1.4f, 0.06f), Quaternion.identity, Palette.Wood);
                mesh.AddBox(new Vector3(0.62f, y + 1.38f, 0f), new Vector3(0.34f, 0.06f, 0.06f), Quaternion.identity, Palette.Stone);
                return;
            }

            mesh.AddStanding(new Vector3(-0.35f, y, 0f), new Vector3(0.1f, 1.45f, 0.1f), Quaternion.identity, Palette.Wood);
            mesh.AddBox(new Vector3(0.03f, y + 1.2f, 0f), new Vector3(0.7f, 0.4f, 0.04f), Quaternion.identity, Palette.TomatoRed);
            mesh.AddCylinder(new Vector3(0.5f, y + 0.17f, 0f), 0.3f, 0.34f, 10, Quaternion.identity, Palette.CarrotDark);
            mesh.AddStanding(new Vector3(0.5f, y + 0.34f, 0f), new Vector3(0.05f, 0.4f, 0.05f), Quaternion.identity, Palette.LeafDark);
            mesh.AddSphere(new Vector3(0.5f, y + 0.82f, 0f), 0.16f, Palette.Flower);
        }

        // ---------- Mallas (origen al pie, de cara a la cámara hacia -z) ----------

        private static PaletteMeshBuilder BuildMesh(string id)
        {
            var mesh = new PaletteMeshBuilder();
            Quaternion none = Quaternion.identity;

            switch (id)
            {
                case "deco_lamp":
                    mesh.AddCylinder(new Vector3(0f, 0.1f, 0f), 0.34f, 0.2f, 10, none, Palette.Stone);
                    mesh.AddStanding(new Vector3(0f, 0.2f, 0f), new Vector3(0.14f, 2.5f, 0.14f), none, Palette.Wood);
                    mesh.AddBox(new Vector3(0f, 2.9f, 0f), new Vector3(0.5f, 0.5f, 0.5f), none, Palette.WheatGold);
                    mesh.AddCone(new Vector3(0f, 3.15f, 0f), 0.42f, new Vector3(0f, 3.6f, 0f), 4, Palette.TomatoDark);
                    break;

                case "deco_scarecrow":
                    mesh.AddStanding(Vector3.zero, new Vector3(0.14f, 2.3f, 0.14f), none, Palette.Wood);
                    mesh.AddBox(new Vector3(0f, 1.6f, 0f), new Vector3(1.7f, 0.12f, 0.12f), none, Palette.Wood);
                    mesh.AddBox(new Vector3(0f, 1.15f, 0f), new Vector3(0.54f, 0.85f, 0.32f), none, Palette.TomatoRed);
                    mesh.AddSphere(new Vector3(0f, 2.0f, 0f), 0.28f, Palette.WheatStraw);
                    mesh.AddCylinder(new Vector3(0f, 2.25f, 0f), 0.46f, 0.05f, 10, none, Palette.Wood);
                    mesh.AddCone(new Vector3(0f, 2.28f, 0f), 0.3f, new Vector3(0f, 2.7f, 0f), 8, Palette.Wood);
                    break;

                case "deco_bench":
                    mesh.AddBox(new Vector3(0f, 0.55f, 0f), new Vector3(2.1f, 0.12f, 0.62f), none, Palette.Wood);
                    mesh.AddBox(new Vector3(0f, 1.0f, 0.27f), new Vector3(2.1f, 0.5f, 0.1f), none, Palette.Wood);
                    for (int side = -1; side <= 1; side += 2)
                    {
                        mesh.AddStanding(new Vector3(side * 0.9f, 0f, -0.2f), new Vector3(0.12f, 0.55f, 0.12f), none, Palette.Stone);
                        mesh.AddStanding(new Vector3(side * 0.9f, 0f, 0.22f), new Vector3(0.12f, 1.2f, 0.12f), none, Palette.Stone);
                    }

                    break;

                case "deco_flowers":
                    mesh.AddBox(new Vector3(0f, 0.12f, 0f), new Vector3(2.6f, 0.24f, 1.4f), none, Palette.SoilDark);
                    int[] petals = { Palette.Flower, Palette.TomatoRed, Palette.White, Palette.Purple };
                    for (int i = 0; i < 8; i++)
                    {
                        float x = -1.05f + (i % 4) * 0.7f;
                        float z = i < 4 ? -0.25f : 0.25f;
                        mesh.AddStanding(new Vector3(x, 0.24f, z), new Vector3(0.05f, 0.4f, 0.05f), none, Palette.LeafLight);
                        mesh.AddSphere(new Vector3(x, 0.72f, z), 0.17f, petals[(i + i / 4) % petals.Length]);
                    }

                    break;

                case "deco_pond":
                    mesh.AddCylinder(new Vector3(0f, 0.06f, 0f), 1.7f, 0.12f, 18, none, Palette.Stone);
                    mesh.AddCylinder(new Vector3(0f, 0.1f, 0f), 1.45f, 0.12f, 18, none, Palette.Water);
                    mesh.AddCylinder(new Vector3(0.5f, 0.17f, 0.2f), 0.3f, 0.03f, 10, none, Palette.LeafLight);
                    mesh.AddCylinder(new Vector3(-0.45f, 0.17f, -0.3f), 0.22f, 0.03f, 10, none, Palette.LeafDark);
                    mesh.AddStanding(new Vector3(-1.3f, 0.1f, 0.7f), new Vector3(0.06f, 0.9f, 0.06f), none, Palette.LeafDark);
                    mesh.AddStanding(new Vector3(-1.15f, 0.1f, 0.85f), new Vector3(0.06f, 1.1f, 0.06f), none, Palette.LeafLight);
                    break;

                case "deco_windmill":
                    mesh.AddCylinder(new Vector3(0f, 1.5f, 0f), 0.95f, 3f, 8, none, Palette.Cream);
                    mesh.AddCone(new Vector3(0f, 3f, 0f), 1.1f, new Vector3(0f, 4.1f, 0f), 8, Palette.TomatoRed);
                    mesh.AddBox(new Vector3(0f, 0.55f, -0.93f), new Vector3(0.6f, 1.1f, 0.06f), none, Palette.Wood);
                    mesh.AddBox(new Vector3(-0.55f, 1.7f, -0.8f), new Vector3(0.34f, 0.4f, 0.06f), none, Palette.Water);   // ventanitas
                    mesh.AddBox(new Vector3(0.55f, 1.7f, -0.8f), new Vector3(0.34f, 0.4f, 0.06f), none, Palette.Water);
                    mesh.AddCylinder(new Vector3(0f, 0.08f, 0f), 1.15f, 0.16f, 8, none, Palette.Stone);   // base de piedra
                    mesh.AddSphere(new Vector3(0f, 3.0f, -1.0f), 0.2f, Palette.Wood);   // el eje; las aspas son una pieza aparte que gira
                    break;

                case "part_windmill_blades":   // origen en el eje
                    for (int blade = 0; blade < 4; blade++)
                    {
                        Quaternion spin = Quaternion.Euler(0f, 0f, blade * 90f + 20f);
                        mesh.AddBox(spin * new Vector3(0f, 0.95f, -0.04f), new Vector3(0.3f, 1.8f, 0.06f), spin, Palette.White);
                        mesh.AddBox(spin * new Vector3(0f, 1.45f, -0.04f), new Vector3(0.34f, 0.7f, 0.07f), spin, Palette.TomatoRed);   // la punta, de color
                    }

                    break;

                case "part_flag_cloth":   // origen en el mástil, la tela sale hacia +x
                    mesh.AddBox(new Vector3(0.6f, 0f, 0f), new Vector3(1.1f, 0.7f, 0.05f), none, Palette.TomatoRed);
                    mesh.AddBox(new Vector3(0.6f, 0f, -0.03f), new Vector3(1.1f, 0.2f, 0.03f), none, Palette.White);
                    break;

                case "part_butterfly":   // alas a los lados (x), mira hacia +z
                    mesh.AddBox(new Vector3(0f, 0f, 0f), new Vector3(0.05f, 0.05f, 0.22f), none, Palette.Wood);
                    mesh.AddBox(new Vector3(-0.15f, 0f, 0f), new Vector3(0.28f, 0.02f, 0.22f), Quaternion.Euler(0f, 0f, 8f), Palette.Flower);
                    mesh.AddBox(new Vector3(0.15f, 0f, 0f), new Vector3(0.28f, 0.02f, 0.22f), Quaternion.Euler(0f, 0f, -8f), Palette.Flower);
                    break;

                case "part_firefly":
                    mesh.AddSphere(Vector3.zero, 0.07f, Palette.WheatGold);
                    break;

                case "part_duck":   // mira hacia +z
                    mesh.AddSphere(new Vector3(0f, 0.12f, 0f), 0.2f, Palette.WheatStraw);
                    mesh.AddSphere(new Vector3(0f, 0.34f, 0.14f), 0.12f, Palette.WheatStraw);
                    mesh.AddCone(new Vector3(0f, 0.33f, 0.24f), 0.05f, new Vector3(0f, 0.32f, 0.38f), 6, Palette.CarrotOrange);
                    mesh.AddBox(new Vector3(0f, 0.2f, -0.17f), new Vector3(0.12f, 0.1f, 0.16f), none, Palette.WheatGold);   // la colita
                    break;

                case "part_bird":   // mira hacia -z (a la cámara)
                    mesh.AddSphere(new Vector3(0f, 0.12f, 0f), 0.13f, Palette.Water);
                    mesh.AddSphere(new Vector3(0f, 0.24f, -0.07f), 0.08f, Palette.Water);
                    mesh.AddCone(new Vector3(0f, 0.24f, -0.13f), 0.03f, new Vector3(0f, 0.23f, -0.25f), 5, Palette.CarrotOrange);
                    mesh.AddBox(new Vector3(0f, 0.14f, 0.15f), new Vector3(0.07f, 0.04f, 0.2f), none, Palette.Purple);
                    break;

                case "part_chicken":   // mira hacia -z
                    mesh.AddSphere(new Vector3(0f, 0.25f, 0f), 0.24f, Palette.White);
                    mesh.AddSphere(new Vector3(0f, 0.52f, -0.12f), 0.13f, Palette.White);
                    mesh.AddBox(new Vector3(0f, 0.66f, -0.12f), new Vector3(0.04f, 0.1f, 0.12f), none, Palette.TomatoRed);   // la cresta
                    mesh.AddCone(new Vector3(0f, 0.5f, -0.23f), 0.04f, new Vector3(0f, 0.48f, -0.37f), 5, Palette.CarrotOrange);
                    mesh.AddBox(new Vector3(0f, 0.3f, 0.22f), new Vector3(0.08f, 0.22f, 0.1f), Quaternion.Euler(-25f, 0f, 0f), Palette.Cream);   // la cola
                    break;

                case "deco_flag":
                    mesh.AddCylinder(new Vector3(0f, 0.1f, 0f), 0.45f, 0.2f, 10, none, Palette.Stone);
                    mesh.AddStanding(new Vector3(0f, 0.2f, 0f), new Vector3(0.1f, 3.5f, 0.1f), none, Palette.Stone);
                    mesh.AddSphere(new Vector3(0f, 3.75f, 0f), 0.12f, Palette.WheatGold);   // la tela es una pieza aparte que se mece
                    break;

                case "deco_haystack":
                    mesh.AddCylinder(new Vector3(0f, 0.5f, 0f), 0.95f, 1f, 12, none, Palette.WheatStraw);
                    mesh.AddSphere(new Vector3(0f, 1.0f, 0f), 0.95f, Palette.WheatStraw);
                    mesh.AddCylinder(new Vector3(1.4f, 0.3f, 0.3f), 0.55f, 0.6f, 10, none, Palette.WheatGold);
                    mesh.AddSphere(new Vector3(1.4f, 0.6f, 0.3f), 0.55f, Palette.WheatGold);
                    break;

                default:
                    throw new InvalidOperationException("Decoración sin malla: " + id);
            }

            return mesh;
        }
    }
}
