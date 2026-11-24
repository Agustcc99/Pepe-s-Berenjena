using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace FarmIdle.EditorTools
{
    // Piezas de la Fase 3: arte provisorio (paleta, mallas low poly y prefabs de plantas), el enganche de cada
    // CropData con su planta y el campo de parcelas de MainLevel.
    // Igual que Phase1SceneBuilder, es una biblioteca: Phase2SceneBuilder.BuildAll la llama para regenerar todo.
    // Los prefabs y las mallas se rehacen en cada corrida; los CropData existentes no se pisan (solo se les
    // completa el prefab de planta si todavía no tienen uno).
    internal static class Phase3SceneBuilder
    {
        private const string MeshesFolder = "Assets/Meshes/Plots";
        private const string PlantPrefabsFolder = "Assets/Prefabs/Plants";
        private const string PalettePath = Phase1SceneBuilder.MaterialsFolder + "/T_Palette.png";
        internal const string PaletteMaterialPath = Phase1SceneBuilder.MaterialsFolder + "/M_Palette.mat";
        private const string SoilMeshPath = MeshesFolder + "/SM_PlotSoil.asset";

        // Campo de 3 x 3 parcelas. Cada fila es de un cultivo, en el orden de 'Plants' (la más cercana al jugador es
        // la que crece más rápido). Los datos de cada cultivo están en Crop_<nombre>.asset.
        internal const int FieldRows = 3;
        internal const int FieldColumns = 3;

        // La parcela mide 2 x 2; el trigger es más chico (1,4) porque el collider del jugador (radio 0,4) se suma:
        // el jugador "pisa" la parcela cuando su centro está sobre la tierra, y al caminar por el hueco entre dos
        // parcelas no activa ninguna.
        internal const float PlotPitch = 2.4f;
        private const float TriggerSize = 1.4f;
        private const float SoilTop = 0.12f;   // altura de la tierra: de ahí nace la planta

        // A unos pasos del punto de aparición del jugador (el origen), en la parte alta de la pantalla.
        private static readonly Vector3 FieldCenter = new Vector3(0f, 0f, 7f);

        // Cada planta tiene dos mallas: la verde que crece y la madura. Lleva el nombre de su archivo CropData.
        // Las tres primeras son las filas del campo inicial, en ese orden; las que siguen son de las zonas (Fase 6).
        private static readonly (string name, Func<bool, PaletteMeshBuilder> build)[] Plants =
        {
            ("Wheat", BuildWheat),
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
            Phase1SceneBuilder.EnsureFolder(Phase1SceneBuilder.MaterialsFolder);
            Phase1SceneBuilder.EnsureFolder(MeshesFolder);
            Phase1SceneBuilder.EnsureFolder(PlantPrefabsFolder);

            CreatePaletteTexture();
            Phase1SceneBuilder.CreateMaterial("M_Palette", Color.white, Phase2SceneBuilder.LoadRequired<Texture2D>(PalettePath), Vector2.one);

            SaveMesh(BuildSoil().ToMesh("SM_PlotSoil"), SoilMeshPath);

            foreach ((string name, Func<bool, PaletteMeshBuilder> build) in Plants)
            {
                // Con arte de Kenney (KenneyArt.Prepare ya corrió) la planta sale de sus modelos; sin él, el provisorio.
                SaveMesh((KenneyArt.Plant(name, false) ?? build(false)).ToMesh("SM_" + name + "_Growing"), MeshPath(name, "Growing"));
                SaveMesh((KenneyArt.Plant(name, true) ?? build(true)).ToMesh("SM_" + name + "_Ready"), MeshPath(name, "Ready"));
                CreatePlantPrefab(name);
            }

            LinkCropsToPlants();
            AssetDatabase.SaveAssets();
        }

        private static string MeshPath(string plant, string form) { return MeshesFolder + "/SM_" + plant + "_" + form + ".asset"; }
        private static string PlantPrefabPath(string plant) { return PlantPrefabsFolder + "/Plant_" + plant + ".prefab"; }
        internal static string CropPath(string plant) { return Phase2SceneBuilder.CropsFolder + "/Crop_" + plant + ".asset"; }

        private static void CreatePaletteTexture()
        {
            File.WriteAllBytes(PalettePath, Palette.RenderPng());
            AssetDatabase.ImportAsset(PalettePath);

            // Filtro Point y sin mipmaps ni compresión: cada celda se ve como un color puro.
            var importer = (TextureImporter)AssetImporter.GetAtPath(PalettePath);
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        // Guarda la malla como asset sin cambiar su GUID si ya existía (así los prefabs que la usan no pierden la referencia).
        internal static void SaveMesh(Mesh mesh, string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(mesh, path);
                return;
            }

            EditorUtility.CopySerialized(mesh, existing);
            EditorUtility.SetDirty(existing);
            Object.DestroyImmediate(mesh);
        }

        private static void CreatePlantPrefab(string plant)
        {
            var growingMesh = Phase2SceneBuilder.LoadRequired<Mesh>(MeshPath(plant, "Growing"));
            var readyMesh = Phase2SceneBuilder.LoadRequired<Mesh>(MeshPath(plant, "Ready"));
            var material = Phase2SceneBuilder.LoadRequired<Material>(PaletteMaterialPath);

            var root = new GameObject("Plant_" + plant);
            var view = root.AddComponent<PlantView>();
            GameObject growing = CreateModel("Growing", root.transform, growingMesh, material);
            GameObject ready = CreateModel("Ready", root.transform, readyMesh, material);
            ready.SetActive(false);   // arranca la forma verde
            EditorUi.Wire(view, ("growingModel", growing), ("readyModel", ready));

            PrefabUtility.SaveAsPrefabAsset(root, PlantPrefabPath(plant));
            Object.DestroyImmediate(root);
        }

        private static GameObject CreateModel(string name, Transform parent, Mesh mesh, Material material)
        {
            var model = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            model.transform.SetParent(parent, false);
            model.GetComponent<MeshFilter>().sharedMesh = mesh;
            model.GetComponent<MeshRenderer>().sharedMaterial = material;
            return model;
        }

        // Los CropData ya existen (los creó la Fase 2): solo se les engancha la planta si todavía no tienen una,
        // para respetar lo que se haya asignado a mano (por ejemplo, un modelo definitivo).
        private static void LinkCropsToPlants()
        {
            foreach (var (name, _) in Plants)
            {
                var crop = Phase2SceneBuilder.LoadRequired<CropData>(CropPath(name));
                var serialized = new SerializedObject(crop);
                SerializedProperty property = serialized.FindProperty("plantPrefab");
                if (property.objectReferenceValue != null) continue;

                property.objectReferenceValue = Phase2SceneBuilder.LoadRequired<GameObject>(PlantPrefabPath(name));
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(crop);
            }
        }

        // ---------- Mallas ----------

        // Marco de madera con tierra clara adentro. Con el borde más alto que la tierra se lee como "parcela".
        private static PaletteMeshBuilder BuildSoil()
        {
            var mesh = new PaletteMeshBuilder();
            mesh.AddBox(new Vector3(0f, SoilTop * 0.5f, 0f), new Vector3(1.76f, SoilTop, 1.76f), Quaternion.identity, Palette.SoilLight);

            const float frameHeight = 0.16f;
            mesh.AddBox(new Vector3(0f, frameHeight * 0.5f, 0.94f), new Vector3(2f, frameHeight, 0.12f), Quaternion.identity, Palette.Wood);
            mesh.AddBox(new Vector3(0f, frameHeight * 0.5f, -0.94f), new Vector3(2f, frameHeight, 0.12f), Quaternion.identity, Palette.Wood);
            mesh.AddBox(new Vector3(0.94f, frameHeight * 0.5f, 0f), new Vector3(0.12f, frameHeight, 1.76f), Quaternion.identity, Palette.Wood);
            mesh.AddBox(new Vector3(-0.94f, frameHeight * 0.5f, 0f), new Vector3(0.12f, frameHeight, 1.76f), Quaternion.identity, Palette.Wood);
            return mesh;
        }

        // Las plantas se modelan con el origen en la base (a nivel de la tierra) y a su tamaño completo:
        // PlantView las agranda de a poco al crecer.

        // Manojo de tallos: verdes mientras crece; al madurar, tallos de paja con espiga dorada.
        private static PaletteMeshBuilder BuildWheat(bool ready)
        {
            var mesh = new PaletteMeshBuilder();
            Vector2[] spots =
            {
                new Vector2(0f, 0.02f), new Vector2(0.30f, 0.10f), new Vector2(-0.30f, 0.08f),
                new Vector2(0.14f, -0.30f), new Vector2(-0.16f, -0.28f), new Vector2(0.02f, 0.34f)
            };

            for (int i = 0; i < spots.Length; i++)
            {
                // Un poco de variedad (alto e inclinación) que se repite igual en cada corrida del builder.
                Quaternion tilt = Quaternion.Euler(((i * 7) % 5 - 2) * 3f, i * 40f, ((i * 3) % 5 - 2) * 3f);
                float height = (ready ? 0.9f : 0.5f) + (i % 3) * 0.07f;
                var basePoint = new Vector3(spots[i].x, 0f, spots[i].y);

                int stalkColor = ready ? Palette.WheatStraw : (i % 2 == 0 ? Palette.LeafLight : Palette.LeafDark);
                mesh.AddStanding(basePoint, new Vector3(0.07f, height, 0.07f), tilt, stalkColor);

                if (!ready) continue;

                Vector3 top = basePoint + tilt * new Vector3(0f, height, 0f);
                mesh.AddStanding(top, new Vector3(0.13f, 0.3f, 0.13f), tilt, Palette.WheatGold);

                Vector3 earTop = top + tilt * new Vector3(0f, 0.3f, 0f);
                mesh.AddCone(earTop, 0.07f, earTop + tilt * new Vector3(0f, 0.12f, 0f), 4, Palette.WheatGold);
            }

            return mesh;
        }

        // Tres matas de hojas; al madurar se ve la parte de arriba de cada zanahoria, anaranjada.
        private static PaletteMeshBuilder BuildCarrot(bool ready)
        {
            var mesh = new PaletteMeshBuilder();
            Vector3[] carrots = { new Vector3(0f, 0f, 1f), new Vector3(0.38f, 0.06f, 0.75f), new Vector3(-0.36f, -0.08f, 0.85f) };   // x, z, escala

            foreach (Vector3 carrot in carrots)
            {
                float scale = carrot.z;
                var origin = new Vector3(carrot.x, 0f, carrot.y);
                float leafBase = ready ? 0.14f * scale : 0f;
                float leafHeight = (ready ? 0.62f : 0.4f) * scale;

                for (int leaf = 0; leaf < 5; leaf++)
                {
                    Quaternion rotation = Quaternion.Euler(0f, leaf * 72f + 15f, 0f) * Quaternion.Euler(26f, 0f, 0f);
                    int color = leaf % 2 == 0 ? Palette.LeafLight : Palette.LeafDark;
                    mesh.AddStanding(origin + new Vector3(0f, leafBase, 0f), new Vector3(0.1f * scale, leafHeight, 0.03f * scale), rotation, color);
                }

                if (ready)
                {
                    mesh.AddCone(origin + new Vector3(0f, 0.17f * scale, 0f), 0.17f * scale, origin + new Vector3(0f, -0.13f * scale, 0f), 6, Palette.CarrotOrange);
                }
            }

            return mesh;
        }

        // Mata de tres bloques verdes; al madurar suma tomates rojos.
        private static PaletteMeshBuilder BuildTomato(bool ready)
        {
            var mesh = new PaletteMeshBuilder();
            mesh.AddBox(new Vector3(0f, 0.2f, 0f), new Vector3(0.62f, 0.4f, 0.62f), Quaternion.identity, Palette.LeafDark);
            mesh.AddBox(new Vector3(0f, 0.54f, 0f), new Vector3(0.44f, 0.28f, 0.44f), Quaternion.Euler(0f, 30f, 0f), Palette.LeafLight);
            mesh.AddBox(new Vector3(0f, 0.78f, 0f), new Vector3(0.26f, 0.2f, 0.26f), Quaternion.Euler(0f, 60f, 0f), Palette.LeafDark);

            if (ready)
            {
                mesh.AddSphere(new Vector3(0.33f, 0.26f, 0.06f), 0.11f, Palette.TomatoRed);
                mesh.AddSphere(new Vector3(-0.32f, 0.22f, -0.10f), 0.11f, Palette.TomatoRed);
                mesh.AddSphere(new Vector3(0.06f, 0.30f, 0.34f), 0.11f, Palette.TomatoRed);
                mesh.AddSphere(new Vector3(-0.10f, 0.26f, -0.34f), 0.11f, Palette.TomatoRed);
                mesh.AddSphere(new Vector3(0.18f, 0.62f, 0.14f), 0.10f, Palette.TomatoRed);
                mesh.AddSphere(new Vector3(-0.16f, 0.64f, -0.12f), 0.10f, Palette.TomatoRed);
            }

            return mesh;
        }

        // Calabaza (cultivo de la zona dorada, Fase 6; provisoria hasta que haya un modelo de Kenney): mientras crece, una mata de
        // hojas anchas con el brote; al madurar, tres calabazas anaranjadas con gajos oscuros y tallito.
        private static PaletteMeshBuilder BuildPumpkin(bool ready)
        {
            var mesh = new PaletteMeshBuilder();

            // Hojas anchas y chatas que salen en abanico de la base.
            for (int leaf = 0; leaf < 6; leaf++)
            {
                Quaternion rotation = Quaternion.Euler(0f, leaf * 60f + 10f, 0f) * Quaternion.Euler(0f, 0f, -20f);
                int color = leaf % 2 == 0 ? Palette.LeafLight : Palette.LeafDark;
                mesh.AddStanding(new Vector3(0f, 0.02f, 0f) + Quaternion.Euler(0f, leaf * 60f + 10f, 0f) * new Vector3(0.12f, 0f, 0f),
                    new Vector3(0.42f, ready ? 0.05f : 0.06f, 0.3f), rotation, color);
            }

            if (!ready)
            {
                mesh.AddSphere(new Vector3(0f, 0.2f, 0f), 0.17f, Palette.LeafLight);
                return mesh;
            }

            // (x, z, radio) de cada calabaza.
            Vector3[] pumpkins = { new Vector3(0f, 0f, 0.34f), new Vector3(0.52f, 0.34f, 0.24f), new Vector3(-0.5f, -0.3f, 0.26f) };
            foreach (Vector3 pumpkin in pumpkins)
            {
                float radius = pumpkin.z;
                var center = new Vector3(pumpkin.x, radius * 0.95f, pumpkin.y);
                mesh.AddSphere(center, radius, Palette.CarrotOrange);
                mesh.AddSphere(center + new Vector3(radius * 0.55f, -radius * 0.05f, 0f), radius * 0.78f, Palette.CarrotDark);
                mesh.AddSphere(center + new Vector3(-radius * 0.55f, -radius * 0.05f, 0f), radius * 0.78f, Palette.CarrotDark);
                mesh.AddSphere(center + new Vector3(0f, 0f, radius * 0.1f), radius * 0.9f, Palette.CarrotOrange);
                mesh.AddBox(center + new Vector3(0f, radius * 0.95f, 0f), new Vector3(0.08f, 0.16f, 0.08f), Quaternion.identity, Palette.LeafDark);
            }

            return mesh;
        }

        // Maíz (F-01, tier 5; provisorio): cuatro cañas con hojas largas que se abren; al madurar, más altas y con mazorcas
        // doradas envueltas en hojas claras.
        private static PaletteMeshBuilder BuildCorn(bool ready)
        {
            var mesh = new PaletteMeshBuilder();
            Vector3[] stalks = { new Vector3(0.28f, 0.2f, 1f), new Vector3(-0.3f, 0.1f, 0.9f), new Vector3(0.1f, -0.32f, 1.05f), new Vector3(-0.14f, 0.34f, 0.85f) };   // x, z, escala

            foreach (Vector3 stalk in stalks)
            {
                float height = (ready ? 1.05f : 0.55f) * stalk.z;
                var origin = new Vector3(stalk.x, 0f, stalk.y);
                mesh.AddStanding(origin, new Vector3(0.08f, height, 0.08f), Quaternion.identity, Palette.LeafDark);

                for (int leaf = 0; leaf < 3; leaf++)
                {
                    Quaternion rotation = Quaternion.Euler(0f, leaf * 120f + stalk.x * 90f, 0f) * Quaternion.Euler(52f, 0f, 0f);
                    mesh.AddStanding(origin + new Vector3(0f, height * (0.35f + leaf * 0.2f), 0f), new Vector3(0.1f, 0.38f * stalk.z, 0.025f), rotation, leaf % 2 == 0 ? Palette.LeafLight : Palette.LeafDark);
                }

                if (ready)
                {
                    var cob = origin + new Vector3(0.1f, height * 0.55f, 0f);
                    mesh.AddCylinder(cob, 0.07f, 0.3f, 8, Quaternion.identity, Palette.WheatGold);
                    mesh.AddBox(cob + new Vector3(0.02f, 0f, 0f), new Vector3(0.05f, 0.24f, 0.1f), Quaternion.identity, Palette.LeafLight);
                }
            }

            return mesh;
        }

        // Berenjena (F-01, tier 6; provisoria): mata baja de hojas; al madurar, cuelgan cuatro berenjenas violetas grandes (gordas
        // arriba, afinadas hacia abajo) con capuchón verde, por fuera de las hojas para que se lean desde la cámara (playtest Q-01).
        private static PaletteMeshBuilder BuildEggplant(bool ready)
        {
            var mesh = new PaletteMeshBuilder();
            mesh.AddBox(new Vector3(0f, 0.14f, 0f), new Vector3(0.46f, 0.28f, 0.46f), Quaternion.Euler(0f, 15f, 0f), Palette.LeafDark);
            mesh.AddBox(new Vector3(0f, 0.38f, 0f), new Vector3(0.3f, 0.22f, 0.3f), Quaternion.Euler(0f, 45f, 0f), Palette.LeafLight);

            if (!ready) return mesh;

            Vector2[] fruits = { new Vector2(0.52f, 0.1f), new Vector2(-0.52f, 0.0f), new Vector2(0.2f, -0.52f), new Vector2(-0.2f, 0.5f) };
            foreach (Vector2 fruit in fruits)
            {
                var top = new Vector3(fruit.x, 0.82f, fruit.y);
                mesh.AddBox(top + new Vector3(0f, 0.2f, 0f), new Vector3(0.05f, 0.14f, 0.05f), Quaternion.identity, Palette.LeafDark);                       // rabito
                mesh.AddBox(top + new Vector3(0f, 0.08f, 0f), new Vector3(0.3f, 0.07f, 0.3f), Quaternion.Euler(0f, 30f, 0f), Palette.LeafDark);             // capuchón
                mesh.AddSphere(top, 0.19f, Palette.Purple);
                mesh.AddCone(top, 0.19f, top + new Vector3(0f, -0.5f, 0f), 8, Palette.Purple);
            }

            return mesh;
        }

        // Sandía (F-01, tier 7; provisoria): hojas anchas con el brote, como la calabaza; al madurar, dos sandías de gajos
        // verdes con un corte rojo arriba.
        private static PaletteMeshBuilder BuildWatermelon(bool ready)
        {
            var mesh = new PaletteMeshBuilder();

            for (int leaf = 0; leaf < 6; leaf++)
            {
                Quaternion rotation = Quaternion.Euler(0f, leaf * 60f + 25f, 0f) * Quaternion.Euler(0f, 0f, -18f);
                mesh.AddStanding(new Vector3(0f, 0.02f, 0f) + Quaternion.Euler(0f, leaf * 60f + 25f, 0f) * new Vector3(0.12f, 0f, 0f),
                    new Vector3(0.4f, ready ? 0.05f : 0.06f, 0.28f), rotation, leaf % 2 == 0 ? Palette.LeafDark : Palette.LeafLight);
            }

            if (!ready)
            {
                mesh.AddSphere(new Vector3(0f, 0.2f, 0f), 0.16f, Palette.LeafDark);
                return mesh;
            }

            Vector3[] melons = { new Vector3(-0.12f, -0.1f, 0.44f), new Vector3(0.5f, 0.38f, 0.3f) };   // x, z, radio
            foreach (Vector3 melon in melons)
            {
                float radius = melon.z;
                var center = new Vector3(melon.x, radius * 0.95f, melon.y);
                mesh.AddSphere(center, radius, Palette.LeafDark);
                mesh.AddSphere(center + new Vector3(radius * 0.5f, 0f, 0f), radius * 0.8f, Palette.LeafLight);
                mesh.AddSphere(center + new Vector3(-radius * 0.5f, 0f, 0f), radius * 0.8f, Palette.LeafLight);
                mesh.AddBox(center + new Vector3(0f, radius * 0.98f, 0f), new Vector3(0.05f, 0.1f, 0.05f), Quaternion.identity, Palette.Wood);   // rabito (sin corte rojo: se veía como un cuadrado suelto)
            }

            return mesh;
        }

        // Girasol (F-01, tier 8; provisorio): tallo con dos hojas; al madurar, flor grande de pétalos dorados y centro marrón,
        // inclinada hacia la cámara.
        private static PaletteMeshBuilder BuildSunflower(bool ready)
        {
            var mesh = new PaletteMeshBuilder();
            float height = ready ? 1.1f : 0.5f;
            mesh.AddStanding(Vector3.zero, new Vector3(0.1f, height, 0.1f), Quaternion.identity, Palette.LeafDark);
            mesh.AddStanding(new Vector3(0f, height * 0.3f, 0f), new Vector3(0.32f, 0.05f, 0.18f), Quaternion.Euler(0f, 0f, -25f), Palette.LeafLight);
            mesh.AddStanding(new Vector3(0f, height * 0.5f, 0f), new Vector3(0.32f, 0.05f, 0.18f), Quaternion.Euler(0f, 180f, -25f), Palette.LeafLight);

            if (!ready)
            {
                mesh.AddSphere(new Vector3(0f, height + 0.04f, 0f), 0.1f, Palette.LeafLight);
                return mesh;
            }

            Quaternion tilt = Quaternion.Euler(-35f, 0f, 0f);
            var head = new Vector3(0f, height + 0.05f, 0f);
            mesh.AddCylinder(head, 0.36f, 0.06f, 14, tilt, Palette.WheatGold);
            mesh.AddCylinder(head + tilt * new Vector3(0f, 0.03f, 0f), 0.19f, 0.07f, 12, tilt, Palette.Wood);
            return mesh;
        }

        // ---------- Escena ----------

        // Agrega el campo a la escena abierta. Los assets se recargan por ruta justo antes de usarlos.
        internal static void BuildField()
        {
            if (Plants.Length < FieldRows)
            {
                throw new InvalidOperationException("Hace falta un cultivo por cada fila del campo.");
            }

            var soilMesh = Phase2SceneBuilder.LoadRequired<Mesh>(SoilMeshPath);
            var material = Phase2SceneBuilder.LoadRequired<Material>(PaletteMaterialPath);

            var field = new GameObject("Field");
            field.transform.position = FieldCenter;

            var views = new List<Object>();
            for (int row = 0; row < FieldRows; row++)
            {
                var crop = Phase2SceneBuilder.LoadRequired<CropData>(CropPath(Plants[row].name));
                for (int column = 0; column < FieldColumns; column++)
                {
                    views.Add(CreatePlot(field.transform, row, column, crop, soilMesh, material));
                }
            }

            var plotField = field.AddComponent<PlotField>();
            EditorUi.WireArray(plotField, "plots", views);
            DailyCropBuilder.AddMarkers(field.transform, views);   // IN-02
        }

        internal static PlotView CreatePlot(Transform field, int row, int column, CropData crop, Mesh soilMesh, Material material)
        {
            var plot = new GameObject("Plot r" + row + "c" + column);
            plot.transform.SetParent(field, false);
            plot.transform.localPosition = new Vector3((column - (FieldColumns - 1) * 0.5f) * PlotPitch, 0f, (row - (FieldRows - 1) * 0.5f) * PlotPitch);

            // Trigger de la altura del jugador (2) para que lo toque aunque el collider quede unos centímetros más alto o bajo.
            var trigger = plot.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0f, 1f, 0f);
            trigger.size = new Vector3(TriggerSize, 2f, TriggerSize);

            var view = plot.AddComponent<PlotView>();
            plot.AddComponent<InteractionTrigger>();

            GameObject soil = CreateModel("Soil", plot.transform, soilMesh, material);
            soil.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;   // plana y a ras del suelo: no proyecta nada útil

            var anchor = new GameObject("PlantAnchor").transform;
            anchor.SetParent(plot.transform, false);
            anchor.localPosition = new Vector3(0f, SoilTop, 0f);

            EditorUi.Wire(view, ("crop", crop), ("plantAnchor", anchor));
            return view;
        }
    }
}
