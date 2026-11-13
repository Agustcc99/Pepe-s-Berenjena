using System;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace FarmIdle.EditorTools
{
    // Piezas de la Fase 5: la zona de venta. El pad del suelo, la moneda que aparece al vender (arte provisorio con la
    // paleta, que ya trae los colores de Kenney) y los valores de venta del GameConfig.
    // Igual que Phase3SceneBuilder y Phase4SceneBuilder, es una biblioteca: Phase2SceneBuilder.BuildAll la llama para
    // regenerar todo. Las mallas y los prefabs se rehacen en cada corrida; el GameConfig existente no se pisa.
    internal static class Phase5SceneBuilder
    {
        private const string MeshesFolder = "Assets/Meshes/Environment";
        private const string VfxMeshesFolder = "Assets/Meshes/VFX";
        private const string VfxPrefabsFolder = "Assets/Prefabs/VFX";
        private const string PadMeshPath = MeshesFolder + "/SM_SellPad.asset";
        private const string StandMeshPath = MeshesFolder + "/SM_SellStand.asset";
        private const string TruckMeshPath = MeshesFolder + "/SM_SellTruck.asset";
        private const string CrateMeshPath = MeshesFolder + "/SM_SellCrate.asset";
        private const string CoinMeshPath = VfxMeshesFolder + "/SM_Coin.asset";
        private const string CoinPrefabPath = VfxPrefabsFolder + "/Coin_Pop.prefab";
        private const string HarvestPopupPrefabPath = VfxPrefabsFolder + "/Harvest_Popup.prefab";
        private const string ChipLeafPrefabPath = VfxPrefabsFolder + "/Chip_Leaf.prefab";
        private const string ChipGoldPrefabPath = VfxPrefabsFolder + "/Chip_Gold.prefab";

        // Cerca del punto de aparición (el origen, mirando a +Z) y fuera del campo (que empieza en z = 3,6): detrás del
        // jugador, a 3,5 hacia atrás. Al volver del campo se lo cruza de frente y cae en la parte baja de la pantalla.
        private static readonly Vector3 PadPosition = new Vector3(0f, 0f, -3.5f);

        // El puesto de verduras (a la izquierda del pad, mirándolo) y el camión de reparto (a la derecha), a la misma altura del pad.
        // En un celular alargado (9:20) a la altura del pad se ven unas ±3,4 unidades de ancho: por eso van chicos (escala 0,75) y pegados al pad.
        private static readonly Vector3 StandPosition = new Vector3(-2.5f, 0f, -3.5f);
        private static readonly Vector3 TruckPosition = new Vector3(2.45f, 0f, -3.5f);
        private const float DecorScale = 0.75f;

        private const float PadSize = 3f;          // el pad visible mide 3 x 3
        private const float PadTrigger = 2.6f;     // el trigger es un poco más chico: se "pisa" con el centro del jugador (radio 0,4)
        private const float PadTop = 0.14f;        // altura de la superficie
        private const float TargetOffset = 0.95f;  // cuánto adelante del centro (hacia la cámara) llegan los productos
        private const float PopupHeight = 3.1f;    // el "+N" flota por encima de la cabeza del jugador (mide 2)

        // ---------- Assets ----------

        internal static void CreateAssets()
        {
            Phase1SceneBuilder.EnsureFolder(MeshesFolder);
            Phase1SceneBuilder.EnsureFolder(VfxMeshesFolder);
            Phase1SceneBuilder.EnsureFolder(VfxPrefabsFolder);

            Phase3SceneBuilder.SaveMesh(BuildPad().ToMesh("SM_SellPad"), PadMeshPath);
            Phase3SceneBuilder.SaveMesh(BuildStand().ToMesh("SM_SellStand"), StandMeshPath);
            Phase3SceneBuilder.SaveMesh(BuildTruck().ToMesh("SM_SellTruck"), TruckMeshPath);
            Phase3SceneBuilder.SaveMesh(BuildCrate().ToMesh("SM_SellCrate"), CrateMeshPath);
            Phase3SceneBuilder.SaveMesh(BuildCoin().ToMesh("SM_Coin"), CoinMeshPath);
            CreateCoinPrefab();
            CreateHarvestPopupPrefab();
            CreateChipPrefab("Chip_Leaf", Palette.LeafLight, VfxMeshesFolder + "/SM_ChipLeaf.asset", ChipLeafPrefabPath);
            CreateChipPrefab("Chip_Gold", Palette.WheatGold, VfxMeshesFolder + "/SM_ChipGold.asset", ChipGoldPrefabPath);

            SaveConfigDefaults();
            AssetDatabase.SaveAssets();
        }

        // Una moneda suelta: la malla y el material de la paleta, sin colliders (es solo un efecto).
        private static void CreateCoinPrefab()
        {
            var mesh = Phase2SceneBuilder.LoadRequired<Mesh>(CoinMeshPath);
            var material = Phase2SceneBuilder.LoadRequired<Material>(Phase3SceneBuilder.PaletteMaterialPath);

            var root = new GameObject("Coin_Pop", typeof(MeshFilter), typeof(MeshRenderer));
            root.GetComponent<MeshFilter>().sharedMesh = mesh;

            var renderer = root.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;   // vive medio segundo en el aire: la sombra no aporta

            PrefabUtility.SaveAsPrefabAsset(root, CoinPrefabPath);
            Object.DestroyImmediate(root);
        }

        // El "+1" que sube al cosechar (J-01): texto en el mundo, sin colliders. HarvestFeedback lo pide al pool.
        private static void CreateHarvestPopupPrefab()
        {
            TMP_FontAsset font = TMP_Settings.defaultFontAsset;
            if (font == null)
            {
                throw new InvalidOperationException("No hay fuente por defecto de TextMeshPro. Ejecutá FarmIdle > Fase 2 > Importar recursos de TextMeshPro.");
            }

            var root = new GameObject("Harvest_Popup", typeof(TextMeshPro));
            var label = root.GetComponent<TextMeshPro>();
            label.font = font;
            label.text = "+1";
            label.fontSize = 6f;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            label.color = new Color(0.85f, 1f, 0.6f);
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Overflow;
            label.rectTransform.sizeDelta = new Vector2(4f, 1.5f);

            PrefabUtility.SaveAsPrefabAsset(root, HarvestPopupPrefabPath);
            Object.DestroyImmediate(root);
        }

        // Un trocito del estallido (J-02): cubo chico del material de la paleta, sin colliders ni sombra.
        private static void CreateChipPrefab(string name, int color, string meshPath, string prefabPath)
        {
            var builder = new PaletteMeshBuilder();
            builder.AddBox(Vector3.zero, new Vector3(0.16f, 0.16f, 0.16f), Quaternion.identity, color);
            Phase3SceneBuilder.SaveMesh(builder.ToMesh(name), meshPath);

            var root = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            root.GetComponent<MeshFilter>().sharedMesh = Phase2SceneBuilder.LoadRequired<Mesh>(meshPath);
            var renderer = root.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = Phase2SceneBuilder.LoadRequired<Material>(Phase3SceneBuilder.PaletteMaterialPath);
            renderer.shadowCastingMode = ShadowCastingMode.Off;

            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);
        }

        // El objeto de la escena que escucha cosechas y compras para soltar el estallido (J-02).
        internal static void BuildBurstFeedback()
        {
            var go = new GameObject("BurstFeedback");
            var feedback = go.AddComponent<BurstFeedback>();
            EditorUi.Wire(feedback,
                ("harvestChip", Phase2SceneBuilder.LoadRequired<GameObject>(ChipLeafPrefabPath)),
                ("purchaseChip", Phase2SceneBuilder.LoadRequired<GameObject>(ChipGoldPrefabPath)));
        }

        // El objeto de la escena que escucha las cosechas de todas las parcelas.
        internal static void BuildHarvestFeedback()
        {
            var go = new GameObject("HarvestFeedback");
            var feedback = go.AddComponent<HarvestFeedback>();
            EditorUi.Wire(feedback, ("popupPrefab", Phase2SceneBuilder.LoadRequired<GameObject>(HarvestPopupPrefabPath)));
        }

        // Un GameConfig anterior a la Fase 5 no tiene guardados los valores de venta (Unity usa el valor por defecto de la
        // clase al cargarlo). Se vuelve a guardar para que queden escritos en el asset y se puedan ajustar a mano, sin
        // tocar nada de lo que ya tenía.
        private static void SaveConfigDefaults()
        {
            var config = Phase2SceneBuilder.LoadRequired<GameConfig>(Phase2SceneBuilder.ConfigPath);
            var serialized = new SerializedObject(config);

            foreach (string field in new[] { "sellIntervalSeconds", "sellFlightSeconds" })
            {
                if (serialized.FindProperty(field) == null) throw new InvalidOperationException("GameConfig no tiene el campo '" + field + "'.");
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(config);
        }

        // ---------- Mallas ----------

        // Plataforma baja con borde de madera, piso claro y una moneda dorada grande en el medio: se lee como "acá se
        // vende" desde la cámara del juego.
        private static PaletteMeshBuilder BuildPad()
        {
            var mesh = new PaletteMeshBuilder();
            const float frame = 0.12f;

            mesh.AddBox(new Vector3(0f, frame * 0.5f, 0f), new Vector3(PadSize, frame, PadSize), Quaternion.identity, Palette.Wood);
            mesh.AddBox(new Vector3(0f, PadTop * 0.5f, 0f), new Vector3(PadSize - 0.3f, PadTop, PadSize - 0.3f), Quaternion.identity, Palette.Cream);

            // La moneda del piso: aro oscuro y cara clara, apenas sobre la superficie.
            mesh.AddCylinder(new Vector3(0f, PadTop + 0.01f, 0f), 0.95f, 0.02f, 16, Quaternion.identity, Palette.WheatStraw);
            mesh.AddCylinder(new Vector3(0f, PadTop + 0.02f, 0f), 0.78f, 0.02f, 16, Quaternion.identity, Palette.WheatGold);
            return mesh;
        }

        // El puesto de verduras: mostrador de madera con tres cajones de producto, techo de toldo a rayas rojas y crema que baja hacia
        // el frente, fondo de tablas y un cartel con la moneda arriba. Mide 2 de ancho (X) por 1,2 de fondo; el frente mira a +Z
        // (la escena lo gira para que mire a la cámara). Origen en el suelo, al centro.
        private static PaletteMeshBuilder BuildStand()
        {
            var mesh = new PaletteMeshBuilder();

            foreach (float x in new[] { -0.95f, 0.95f })
            {
                mesh.AddStanding(new Vector3(x, 0f, -0.5f), new Vector3(0.1f, 2.1f, 0.1f), Quaternion.identity, Palette.Wood);
                mesh.AddStanding(new Vector3(x, 0f, 0.55f), new Vector3(0.1f, 1.8f, 0.1f), Quaternion.identity, Palette.Wood);
            }

            mesh.AddBox(new Vector3(0f, 1.3f, -0.55f), new Vector3(2f, 1.4f, 0.05f), Quaternion.identity, Palette.SoilLight);   // fondo
            mesh.AddBox(new Vector3(0f, 0.45f, 0.2f), new Vector3(1.9f, 0.9f, 0.5f), Quaternion.identity, Palette.Wood);       // mostrador
            mesh.AddBox(new Vector3(0f, 0.935f, 0.2f), new Vector3(2f, 0.07f, 0.6f), Quaternion.identity, Palette.Cream);

            // Cajones con producto en el piso, delante del mostrador (desde la cámara, que mira en picada, el toldo taparía los del mostrador):
            // tomates, zanahorias y repollos.
            const float crateTop = 0.3f;
            const float crateZ = 1.15f;
            float[] crateX = { -0.62f, 0f, 0.62f };
            foreach (float x in crateX)
            {
                mesh.AddBox(new Vector3(x, 0.15f, crateZ), new Vector3(0.55f, 0.3f, 0.46f), Quaternion.identity, Palette.SoilLight);
            }

            foreach (Vector2 t in new[] { new Vector2(-0.13f, -0.09f), new Vector2(0.13f, -0.09f), new Vector2(-0.13f, 0.1f), new Vector2(0.13f, 0.1f) })
            {
                mesh.AddSphere(new Vector3(crateX[0] + t.x, crateTop + 0.04f, crateZ + t.y), 0.1f, Palette.TomatoRed);
            }

            foreach (Vector2 c in new[] { new Vector2(-0.15f, -0.08f), new Vector2(0.02f, 0.1f), new Vector2(0.16f, -0.05f), new Vector2(-0.05f, 0.0f) })
            {
                var basePoint = new Vector3(crateX[1] + c.x, crateTop - 0.02f, crateZ + c.y);
                mesh.AddCone(basePoint, 0.055f, basePoint + new Vector3(c.x * 0.5f, 0.3f, c.y * 0.5f), 6, Palette.CarrotOrange);
                mesh.AddBox(basePoint + new Vector3(c.x * 0.5f, 0.3f, c.y * 0.5f), new Vector3(0.1f, 0.06f, 0.1f), Quaternion.identity, Palette.LeafLight);   // hojitas
            }

            mesh.AddSphere(new Vector3(crateX[2] - 0.1f, crateTop + 0.08f, crateZ), 0.15f, Palette.LeafLight);
            mesh.AddSphere(new Vector3(crateX[2] + 0.12f, crateTop + 0.07f, crateZ - 0.08f), 0.14f, Palette.LeafDark);
            mesh.AddSphere(new Vector3(crateX[2] + 0.05f, crateTop + 0.06f, crateZ + 0.1f), 0.12f, Palette.LeafLight);

            // Sobre el mostrador, una canasta de frutas a la vista (chica, para que el toldo no la tape del todo).
            mesh.AddBox(new Vector3(0.55f, 1.0f, 0.4f), new Vector3(0.5f, 0.12f, 0.3f), Quaternion.identity, Palette.SoilLight);
            mesh.AddSphere(new Vector3(0.47f, 1.12f, 0.4f), 0.1f, Palette.CarrotOrange);
            mesh.AddSphere(new Vector3(0.65f, 1.12f, 0.4f), 0.1f, Palette.CarrotOrange);

            // Toldo: seis franjas inclinadas (el frente más bajo) y el volado que cuelga adelante.
            float[] stripeX = { -0.925f, -0.555f, -0.185f, 0.185f, 0.555f, 0.925f };
            for (int i = 0; i < stripeX.Length; i++)
            {
                int color = i % 2 == 0 ? Palette.TomatoRed : Palette.Cream;
                mesh.AddBox(new Vector3(stripeX[i], 1.93f, 0.12f), new Vector3(0.37f, 0.06f, 1.35f), Quaternion.Euler(16f, 0f, 0f), color);
                mesh.AddBox(new Vector3(stripeX[i], 1.64f, 0.78f), new Vector3(0.37f, 0.2f, 0.04f), Quaternion.identity, color);
            }

            // Cartel con la moneda, sobre el toldo y mirando al frente.
            mesh.AddStanding(new Vector3(0f, 1.95f, -0.5f), new Vector3(0.08f, 0.3f, 0.08f), Quaternion.identity, Palette.Wood);
            Quaternion faceFront = Quaternion.Euler(90f, 0f, 0f);
            mesh.AddCylinder(new Vector3(0f, 2.4f, -0.5f), 0.34f, 0.07f, 16, faceFront, Palette.WheatStraw);
            mesh.AddCylinder(new Vector3(0f, 2.4f, -0.47f), 0.27f, 0.07f, 16, faceFront, Palette.WheatGold);
            return mesh;
        }

        // El camión de reparto: cabina azul con ventanas y techo claro, capó, caja de madera abierta (para ver los cajones desde la cámara)
        // y ruedas. Mide 1,3 de ancho (X) por 3,9 de largo; la cabina mira a +Z (lejos de la cámara). Origen en el suelo, al centro.
        private static PaletteMeshBuilder BuildTruck()
        {
            var mesh = new PaletteMeshBuilder();
            Quaternion axisX = Quaternion.Euler(0f, 0f, 90f);

            mesh.AddBox(new Vector3(0f, 0.38f, 0f), new Vector3(1.1f, 0.16f, 3.9f), Quaternion.identity, Palette.SoilDark);   // chasis

            foreach (float x in new[] { -0.6f, 0.6f })
            {
                foreach (float z in new[] { 1.1f, -1.05f })
                {
                    mesh.AddCylinder(new Vector3(x, 0.3f, z), 0.3f, 0.22f, 12, axisX, Palette.SoilDark);
                    mesh.AddCylinder(new Vector3(x + Mathf.Sign(x) * 0.02f, 0.3f, z), 0.14f, 0.22f, 8, axisX, Palette.Cream);
                }
            }

            // Caja: piso, costados, pared contra la cabina y una compuerta baja atrás.
            mesh.AddBox(new Vector3(0f, 0.52f, -0.8f), new Vector3(1.3f, 0.1f, 2.2f), Quaternion.identity, Palette.Wood);
            mesh.AddBox(new Vector3(-0.61f, 0.77f, -0.8f), new Vector3(0.08f, 0.4f, 2.2f), Quaternion.identity, Palette.Wood);
            mesh.AddBox(new Vector3(0.61f, 0.77f, -0.8f), new Vector3(0.08f, 0.4f, 2.2f), Quaternion.identity, Palette.Wood);
            mesh.AddBox(new Vector3(0f, 0.77f, 0.26f), new Vector3(1.3f, 0.4f, 0.08f), Quaternion.identity, Palette.Wood);
            mesh.AddBox(new Vector3(0f, 0.64f, -1.9f), new Vector3(1.3f, 0.16f, 0.08f), Quaternion.identity, Palette.Wood);
            mesh.AddBox(new Vector3(0f, 0.42f, -1.98f), new Vector3(1.2f, 0.12f, 0.1f), Quaternion.identity, Palette.Stone);   // paragolpes

            // Cabina y capó.
            mesh.AddBox(new Vector3(0f, 1f, 0.95f), new Vector3(1.25f, 0.95f, 0.95f), Quaternion.identity, Palette.Water);
            mesh.AddBox(new Vector3(0f, 1.5f, 0.95f), new Vector3(1.32f, 0.07f, 1.05f), Quaternion.identity, Palette.Cream);
            mesh.AddBox(new Vector3(-0.63f, 1.2f, 0.95f), new Vector3(0.04f, 0.34f, 0.6f), Quaternion.identity, Palette.Cream);
            mesh.AddBox(new Vector3(0.63f, 1.2f, 0.95f), new Vector3(0.04f, 0.34f, 0.6f), Quaternion.identity, Palette.Cream);
            mesh.AddBox(new Vector3(0f, 1.2f, 0.47f), new Vector3(0.8f, 0.3f, 0.04f), Quaternion.identity, Palette.Cream);
            mesh.AddBox(new Vector3(0f, 0.8f, 1.7f), new Vector3(1.2f, 0.5f, 0.55f), Quaternion.identity, Palette.Water);
            return mesh;
        }

        // Un cajón de la caja del camión: madera con tomates y un repollo arriba. Origen en el fondo, al centro.
        private static PaletteMeshBuilder BuildCrate()
        {
            var mesh = new PaletteMeshBuilder();
            mesh.AddBox(new Vector3(0f, 0.13f, 0f), new Vector3(0.5f, 0.26f, 0.5f), Quaternion.identity, Palette.SoilLight);
            mesh.AddSphere(new Vector3(-0.11f, 0.32f, -0.1f), 0.11f, Palette.TomatoRed);
            mesh.AddSphere(new Vector3(0.12f, 0.32f, -0.08f), 0.11f, Palette.TomatoRed);
            mesh.AddSphere(new Vector3(-0.08f, 0.32f, 0.13f), 0.11f, Palette.TomatoRed);
            mesh.AddSphere(new Vector3(0.12f, 0.34f, 0.13f), 0.12f, Palette.LeafLight);
            return mesh;
        }

        // La moneda que salta: un disco parado (eje a lo largo de Z) con borde oscuro y cara clara, que al girar sobre Y
        // se ve de canto y de frente.
        private static PaletteMeshBuilder BuildCoin()
        {
            var mesh = new PaletteMeshBuilder();
            Quaternion stand = Quaternion.Euler(90f, 0f, 0f);

            mesh.AddCylinder(Vector3.zero, 0.32f, 0.08f, 12, stand, Palette.WheatStraw);
            mesh.AddCylinder(Vector3.zero, 0.25f, 0.11f, 12, stand, Palette.WheatGold);
            return mesh;
        }

        // ---------- Escena ----------

        // Agrega la zona de venta a la escena abierta. Los assets se recargan por ruta justo antes de usarlos.
        internal static void BuildSellPad()
        {
            var padMesh = Phase2SceneBuilder.LoadRequired<Mesh>(PadMeshPath);
            var material = Phase2SceneBuilder.LoadRequired<Material>(Phase3SceneBuilder.PaletteMaterialPath);

            var pad = new GameObject("SellPad");
            pad.transform.position = PadPosition;

            // Trigger de la altura del jugador (2), como el de las parcelas.
            var trigger = pad.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0f, 1f, 0f);
            trigger.size = new Vector3(PadTrigger, 2f, PadTrigger);

            var zone = pad.AddComponent<SellZone>();
            pad.AddComponent<InteractionTrigger>();

            var visual = new GameObject("Visual", typeof(MeshFilter), typeof(MeshRenderer));
            visual.transform.SetParent(pad.transform, false);
            visual.GetComponent<MeshFilter>().sharedMesh = padMesh;
            var renderer = visual.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;   // plana y a ras del suelo

            // Adonde vuelan los productos y de donde saltan las monedas: en el borde del pad que da a la cámara (la del
            // juego mira hacia +Z desde -Z), un poco adelante del centro. Si fuera el centro, el jugador (que se para ahí)
            // taparía la moneda y el producto que llega.
            var target = new GameObject("Target").transform;
            target.SetParent(pad.transform, false);
            target.localPosition = new Vector3(0f, PadTop + 0.25f, -TargetOffset);

            TextMeshPro popup = CreatePopup(pad.transform);

            var feedback = pad.AddComponent<SellFeedback>();
            EditorUi.Wire(zone, ("config", Phase2SceneBuilder.LoadRequired<GameConfig>(Phase2SceneBuilder.ConfigPath)), ("target", target));
            EditorUi.Wire(feedback,
                ("zone", zone),
                ("coinPrefab", Phase2SceneBuilder.LoadRequired<GameObject>(CoinPrefabPath)),
                ("popupLabel", popup),
                ("padVisual", visual.transform));

            BuildStandAndTruck(material, zone);
        }

        // El puesto de verduras (mira a la cámara) a un lado del pad y el camión de reparto al otro. Son adorno, sin colliders.
        private static void BuildStandAndTruck(Material material, SellZone zone)
        {
            var stand = new GameObject("SellStand", typeof(MeshFilter), typeof(MeshRenderer));
            stand.transform.position = StandPosition;
            stand.transform.rotation = Quaternion.Euler(0f, 180f, 0f);   // el frente (+Z del modelo) mira a la cámara (-Z)
            stand.transform.localScale = Vector3.one * DecorScale;
            stand.GetComponent<MeshFilter>().sharedMesh = Phase2SceneBuilder.LoadRequired<Mesh>(StandMeshPath);
            stand.GetComponent<MeshRenderer>().sharedMaterial = material;

            var truck = new GameObject("SellTruck");
            truck.transform.position = TruckPosition;
            truck.transform.localScale = Vector3.one * DecorScale;

            var body = new GameObject("Body", typeof(MeshFilter), typeof(MeshRenderer));
            body.transform.SetParent(truck.transform, false);
            body.GetComponent<MeshFilter>().sharedMesh = Phase2SceneBuilder.LoadRequired<Mesh>(TruckMeshPath);
            body.GetComponent<MeshRenderer>().sharedMaterial = material;

            var crateMesh = Phase2SceneBuilder.LoadRequired<Mesh>(CrateMeshPath);
            Vector2[] slots = { new Vector2(-0.27f, -1.4f), new Vector2(0.27f, -1.4f), new Vector2(-0.27f, -0.85f), new Vector2(0.27f, -0.85f) };
            var crates = new Transform[slots.Length];
            for (int i = 0; i < slots.Length; i++)
            {
                var crate = new GameObject("Crate" + (i + 1), typeof(MeshFilter), typeof(MeshRenderer));
                crate.transform.SetParent(body.transform, false);
                crate.transform.localPosition = new Vector3(slots[i].x, 0.57f, slots[i].y);
                crate.GetComponent<MeshFilter>().sharedMesh = crateMesh;
                var crateRenderer = crate.GetComponent<MeshRenderer>();
                crateRenderer.sharedMaterial = material;
                crateRenderer.shadowCastingMode = ShadowCastingMode.Off;
                crate.transform.localScale = Vector3.zero;   // SellTruck los muestra de a uno
                crates[i] = crate.transform;
            }

            var behaviour = truck.AddComponent<SellTruck>();
            EditorUi.Wire(behaviour, ("zone", zone), ("body", body.transform));
            var serialized = new SerializedObject(behaviour);
            SerializedProperty list = serialized.FindProperty("crates");
            list.arraySize = crates.Length;
            for (int i = 0; i < crates.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = crates[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        // El "+N" flota sobre la zona, en el mundo (no en el Canvas), y SellFeedback lo hace mirar a la cámara.
        private static TextMeshPro CreatePopup(Transform parent)
        {
            TMP_FontAsset font = TMP_Settings.defaultFontAsset;
            if (font == null)
            {
                throw new InvalidOperationException("No hay fuente por defecto de TextMeshPro. Ejecutá FarmIdle > Fase 2 > Importar recursos de TextMeshPro.");
            }

            var go = new GameObject("Popup", typeof(TextMeshPro));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, PopupHeight, 0f);

            var label = go.GetComponent<TextMeshPro>();
            label.font = font;
            label.text = "+0";
            label.fontSize = 9f;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            label.color = new Color(1f, 0.92f, 0.3f);
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Overflow;
            label.enabled = false;   // arranca oculto: aparece con la primera venta
            label.rectTransform.sizeDelta = new Vector2(6f, 2f);
            return label;
        }
    }
}
