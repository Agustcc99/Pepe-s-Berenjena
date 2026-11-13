using System;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FarmIdle.EditorTools
{
    // Piezas de la Fase 4: los productos que se apilan en la espalda del granjero (arte provisorio con la paleta de la
    // Fase 3), el enganche de cada CropData con su producto, la capacidad de la mochila en el GameConfig y la pila
    // visible en el jugador de MainLevel.
    // Igual que Phase3SceneBuilder, es una biblioteca: Phase2SceneBuilder.BuildAll la llama para regenerar todo. Las
    // mallas y los prefabs se rehacen en cada corrida; los CropData y el GameConfig existentes no se pisan (a un
    // CropData solo se le completa el producto si todavía no tiene uno).
    internal static class Phase4SceneBuilder
    {
        private const string MeshesFolder = "Assets/Meshes/Products";
        private const string ProductPrefabsFolder = "Assets/Prefabs/Products";

        // La espalda del granjero: la cápsula mide 1 de ancho (radio 0,5) y 2 de alto, con el frente hacia +Z. El ancla
        // queda justo detrás, a la altura de la cintura, y la pila sube desde ahí.
        private static readonly Vector3 AnchorLocalPosition = new Vector3(0f, 0.5f, -0.9f);

        // Los productos se agrandan un poco al generarlos: desde la cámara del juego (lejos y en picada) la pila tiene que
        // leerse. El alto por ítem de BackpackStackView (0,26) va con este tamaño.
        private const float ProductScale = 1.3f;

        // Un producto por cultivo, con el nombre de su archivo CropData (Crop_<nombre>.asset). Todos están modelados
        // acostados a lo largo de X y centrados en el origen, para que la pila los apile por el centro.
        private static readonly (string name, Func<PaletteMeshBuilder> build)[] Products =
        {
            ("Wheat", BuildWheatBundle),
            ("Carrot", BuildCarrot),
            ("Tomato", BuildTomato),
            ("Pumpkin", BuildPumpkin),
            ("Corn", BuildCorn),
            ("Eggplant", BuildEggplant),
            ("Watermelon", BuildWatermelon),
            ("Sunflower", BuildSunflower),
        };

        // ---------- Assets ----------

        internal static void CreateAssets()
        {
            Phase1SceneBuilder.EnsureFolder(MeshesFolder);
            Phase1SceneBuilder.EnsureFolder(ProductPrefabsFolder);

            foreach ((string name, Func<PaletteMeshBuilder> build) in Products)
            {
                // Con arte de Kenney el producto ya sale del tamaño y el centro que pide la pila; el provisorio se escala acá.
                PaletteMeshBuilder kenney = KenneyArt.Product(name);
                Mesh mesh = kenney != null ? kenney.ToMesh("SM_Product_" + name) : Scaled(build().ToMesh("SM_Product_" + name), ProductScale);
                Phase3SceneBuilder.SaveMesh(mesh, MeshPath(name));
                CreateProductPrefab(name);
            }

            LinkCropsToProducts();
            SaveConfigDefaults();
            AssetDatabase.SaveAssets();
        }

        private static string MeshPath(string product) { return MeshesFolder + "/SM_Product_" + product + ".asset"; }
        private static string ProductPrefabPath(string product) { return ProductPrefabsFolder + "/Product_" + product + ".prefab"; }

        // El producto es un solo objeto con su malla y el material de la paleta. Sin colliders a propósito: va colgado
        // del jugador y un collider bloquearía a su CharacterController.
        private static void CreateProductPrefab(string product)
        {
            var mesh = Phase2SceneBuilder.LoadRequired<Mesh>(MeshPath(product));
            var material = Phase2SceneBuilder.LoadRequired<Material>(Phase3SceneBuilder.PaletteMaterialPath);

            var root = new GameObject("Product_" + product, typeof(MeshFilter), typeof(MeshRenderer));
            root.GetComponent<MeshFilter>().sharedMesh = mesh;
            root.GetComponent<MeshRenderer>().sharedMaterial = material;

            PrefabUtility.SaveAsPrefabAsset(root, ProductPrefabPath(product));
            Object.DestroyImmediate(root);
        }

        // Los CropData ya existen: solo se les engancha el producto si todavía no tienen uno, para respetar lo que se
        // haya asignado a mano (por ejemplo, un modelo definitivo).
        private static void LinkCropsToProducts()
        {
            foreach (var (name, _) in Products)
            {
                var crop = Phase2SceneBuilder.LoadRequired<CropData>(Phase3SceneBuilder.CropPath(name));
                var serialized = new SerializedObject(crop);
                SerializedProperty property = serialized.FindProperty("productPrefab");
                if (property == null) throw new InvalidOperationException("CropData no tiene el campo 'productPrefab'.");
                if (property.objectReferenceValue != null) continue;

                property.objectReferenceValue = Phase2SceneBuilder.LoadRequired<GameObject>(ProductPrefabPath(name));
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(crop);
            }
        }

        // Un GameConfig anterior a la Fase 4 no tiene guardada la capacidad de la mochila (Unity usa el valor por defecto
        // de la clase al cargarlo). Se vuelve a guardar para que quede escrita en el asset y se pueda ajustar a mano,
        // sin tocar nada de lo que ya tenía.
        private static void SaveConfigDefaults()
        {
            var config = Phase2SceneBuilder.LoadRequired<GameConfig>(Phase2SceneBuilder.ConfigPath);
            var serialized = new SerializedObject(config);
            if (serialized.FindProperty("backpackCapacity") == null) throw new InvalidOperationException("GameConfig no tiene el campo 'backpackCapacity'.");

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(config);
        }

        // ---------- Mallas ----------

        // Escala pareja alrededor del origen (las normales planas no cambian).
        private static Mesh Scaled(Mesh mesh, float scale)
        {
            Vector3[] vertices = mesh.vertices;
            for (int i = 0; i < vertices.Length; i++) vertices[i] *= scale;

            mesh.vertices = vertices;
            mesh.RecalculateBounds();
            return mesh;
        }

        // Atado de trigo: cinco tallos de paja acostados, con las espigas doradas en una punta y un lazo de madera.
        private static PaletteMeshBuilder BuildWheatBundle()
        {
            var mesh = new PaletteMeshBuilder();
            Vector2[] stalks = { new Vector2(0f, 0f), new Vector2(0.05f, 0.045f), new Vector2(0.05f, -0.045f), new Vector2(-0.05f, 0.045f), new Vector2(-0.05f, -0.045f) };   // (y, z)

            foreach (Vector2 stalk in stalks)
            {
                mesh.AddBox(new Vector3(-0.06f, stalk.x, stalk.y), new Vector3(0.46f, 0.05f, 0.05f), Quaternion.identity, Palette.WheatStraw);
                mesh.AddBox(new Vector3(0.24f, stalk.x, stalk.y), new Vector3(0.14f, 0.06f, 0.06f), Quaternion.identity, Palette.WheatGold);
            }

            mesh.AddCone(new Vector3(0.31f, 0f, 0f), 0.035f, new Vector3(0.37f, 0f, 0f), 4, Palette.WheatGold);
            mesh.AddBox(new Vector3(-0.02f, 0f, 0f), new Vector3(0.06f, 0.17f, 0.16f), Quaternion.identity, Palette.Wood);
            return mesh;
        }

        // Zanahoria acostada: el cuerpo anaranjado termina en punta hacia -X y las hojas salen en abanico del otro lado.
        private static PaletteMeshBuilder BuildCarrot()
        {
            var mesh = new PaletteMeshBuilder();
            var shoulder = new Vector3(0.12f, 0f, 0f);
            mesh.AddCone(shoulder, 0.085f, new Vector3(-0.26f, 0f, 0f), 6, Palette.CarrotOrange);

            float[] fan = { -28f, 0f, 28f };
            for (int i = 0; i < fan.Length; i++)
            {
                Quaternion rotation = Quaternion.Euler(0f, fan[i], 0f);
                mesh.AddBox(shoulder + rotation * new Vector3(0.11f, 0f, 0f), new Vector3(0.22f, 0.025f, 0.06f), rotation,
                    i % 2 == 0 ? Palette.LeafLight : Palette.LeafDark);
            }

            return mesh;
        }

        // Tomate: esfera low poly con los sépalos y el tallito verdes arriba.
        private static PaletteMeshBuilder BuildTomato()
        {
            var mesh = new PaletteMeshBuilder();
            mesh.AddSphere(Vector3.zero, 0.12f, Palette.TomatoRed);
            mesh.AddBox(new Vector3(0f, 0.11f, 0f), new Vector3(0.12f, 0.03f, 0.12f), Quaternion.Euler(0f, 20f, 0f), Palette.LeafDark);
            mesh.AddBox(new Vector3(0f, 0.14f, 0f), new Vector3(0.035f, 0.05f, 0.035f), Quaternion.identity, Palette.LeafLight);
            return mesh;
        }

        // Calabaza (provisoria, Fase 6): cuerpo anaranjado con dos gajos oscuros a los lados y un tallito verde.
        private static PaletteMeshBuilder BuildPumpkin()
        {
            var mesh = new PaletteMeshBuilder();
            mesh.AddSphere(Vector3.zero, 0.13f, Palette.CarrotOrange);
            mesh.AddSphere(new Vector3(0f, 0f, 0.075f), 0.1f, Palette.CarrotDark);
            mesh.AddSphere(new Vector3(0f, 0f, -0.075f), 0.1f, Palette.CarrotDark);
            mesh.AddSphere(Vector3.zero, 0.125f, Palette.CarrotOrange);
            mesh.AddBox(new Vector3(0f, 0.13f, 0f), new Vector3(0.04f, 0.06f, 0.04f), Quaternion.identity, Palette.LeafDark);
            return mesh;
        }

        // Maíz (F-01, provisorio): mazorca dorada acostada con hojas claras en la base.
        private static PaletteMeshBuilder BuildCorn()
        {
            var mesh = new PaletteMeshBuilder();
            Quaternion along = Quaternion.Euler(0f, 0f, 90f);
            mesh.AddCylinder(new Vector3(0.03f, 0f, 0f), 0.065f, 0.28f, 8, along, Palette.WheatGold);
            mesh.AddBox(new Vector3(-0.08f, 0f, 0.05f), new Vector3(0.16f, 0.05f, 0.05f), Quaternion.Euler(0f, 15f, 0f), Palette.LeafLight);
            mesh.AddBox(new Vector3(-0.08f, 0f, -0.05f), new Vector3(0.16f, 0.05f, 0.05f), Quaternion.Euler(0f, -15f, 0f), Palette.LeafDark);
            return mesh;
        }

        // Berenjena (F-01, provisoria): cuerpo violeta alargado con capuchón verde.
        private static PaletteMeshBuilder BuildEggplant()
        {
            var mesh = new PaletteMeshBuilder();
            mesh.AddSphere(new Vector3(-0.04f, 0f, 0f), 0.09f, Palette.Purple);
            mesh.AddCone(new Vector3(-0.04f, 0f, 0f), 0.09f, new Vector3(0.16f, 0f, 0f), 7, Palette.Purple);
            mesh.AddBox(new Vector3(-0.12f, 0f, 0f), new Vector3(0.04f, 0.12f, 0.12f), Quaternion.identity, Palette.LeafDark);
            mesh.AddBox(new Vector3(-0.15f, 0f, 0f), new Vector3(0.05f, 0.03f, 0.03f), Quaternion.identity, Palette.LeafLight);
            return mesh;
        }

        // Sandía (F-01, provisoria): bola verde de gajos claros con un corte rojo arriba.
        private static PaletteMeshBuilder BuildWatermelon()
        {
            var mesh = new PaletteMeshBuilder();
            mesh.AddSphere(Vector3.zero, 0.14f, Palette.LeafDark);
            mesh.AddSphere(new Vector3(0.07f, 0f, 0f), 0.11f, Palette.LeafLight);
            mesh.AddSphere(new Vector3(-0.07f, 0f, 0f), 0.11f, Palette.LeafLight);
            mesh.AddBox(new Vector3(0f, 0.125f, 0f), new Vector3(0.11f, 0.03f, 0.11f), Quaternion.Euler(0f, 20f, 0f), Palette.TomatoRed);
            return mesh;
        }

        // Girasol (F-01, provisorio): disco dorado con centro marrón, de canto a lo largo de X.
        private static PaletteMeshBuilder BuildSunflower()
        {
            var mesh = new PaletteMeshBuilder();
            Quaternion along = Quaternion.Euler(0f, 0f, 90f);
            mesh.AddCylinder(Vector3.zero, 0.15f, 0.05f, 12, along, Palette.WheatGold);
            mesh.AddCylinder(new Vector3(0.02f, 0f, 0f), 0.08f, 0.05f, 10, along, Palette.Wood);
            mesh.AddBox(new Vector3(-0.1f, 0f, 0f), new Vector3(0.1f, 0.03f, 0.03f), Quaternion.identity, Palette.LeafDark);
            return mesh;
        }

        // ---------- Escena ----------

        // Le suma al jugador de la escena abierta el ancla de la espalda y la pila visible. Los assets se recargan por
        // ruta justo antes de usarlos.
        internal static void BuildBackpack()
        {
            GameObject player = GameObject.FindWithTag("Player");
            if (player == null)
            {
                throw new InvalidOperationException("No hay jugador (tag Player) en la escena abierta: armá el mundo antes de la mochila.");
            }

            var anchor = new GameObject("BackpackAnchor").transform;
            anchor.SetParent(player.transform, false);
            anchor.localPosition = AnchorLocalPosition;

            var crops = new List<Object>();
            foreach (var (name, _) in Products)
            {
                crops.Add(Phase2SceneBuilder.LoadRequired<CropData>(Phase3SceneBuilder.CropPath(name)));
            }

            var view = player.AddComponent<BackpackStackView>();
            EditorUi.Wire(view, ("actor", player.GetComponent<PlayerActor>()), ("anchor", anchor));
            EditorUi.WireArray(view, "crops", crops);

            BuildFullIndicator(player);
        }

        // El "MAX" sobre la cabeza del granjero (Fase 6): aparece cuando la mochila está llena. Texto en el mundo, apagado al
        // empezar; BackpackFullIndicator lo prende y lo hace mirar a la cámara.
        private static void BuildFullIndicator(GameObject player)
        {
            TMP_FontAsset font = TMP_Settings.defaultFontAsset;
            if (font == null)
            {
                throw new InvalidOperationException("No hay fuente por defecto de TextMeshPro. Ejecutá FarmIdle > Fase 2 > Importar recursos de TextMeshPro.");
            }

            var go = new GameObject("FullLabel", typeof(TextMeshPro));
            go.transform.SetParent(player.transform, false);
            go.transform.localPosition = new Vector3(0f, 2.9f, 0f);

            var label = go.GetComponent<TextMeshPro>();
            label.font = font;
            label.text = Loc.Get("pad.max", GameLanguage.Spanish);
            EditorUi.AddLocalizedKey(go, "pad.max");
            label.fontSize = 6f;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            label.color = new Color(1f, 0.55f, 0.1f);
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Overflow;
            label.rectTransform.sizeDelta = new Vector2(4f, 1.5f);
            label.enabled = false;   // arranca apagado: aparece con la mochila llena

            var indicator = player.AddComponent<BackpackFullIndicator>();
            EditorUi.Wire(indicator, ("label", label));
        }
    }
}
