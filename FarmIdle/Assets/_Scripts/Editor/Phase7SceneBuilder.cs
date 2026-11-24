using System;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace FarmIdle.EditorTools
{
    // Piezas de la Fase 7: niveles y perks (los datos de Fuerza y Velocidad, sus pads de la plaza, la barra de XP del HUD) y el
    // enganche de los cultivos y los perks al GameConfig (la partida guardada busca los cultivos de la mochila por Id).
    // Igual que las demás, es una biblioteca: Phase2SceneBuilder.BuildAll la llama. Los datos que ya existen NO se pisan
    // (ver Phase2SceneBuilder.CropSpecs y ProgressionData: los números del balance nuevo van por ApplyBalance).
    //   Menú:    FarmIdle > Fase 7 > Aplicar balance de cultivos y mejoras
    //   Consola: Unity.exe -batchmode -quit -projectPath <ruta> -executeMethod FarmIdle.EditorTools.Phase7SceneBuilder.ApplyBalance
    internal static class Phase7SceneBuilder
    {
        private const string PerksFolder = "Assets/ScriptableObjects/Perks";
        private const string MeshesFolder = "Assets/Meshes/Environment";

        internal const int LevelBaseXp = 325;


        // ---------- Tablas de diseño ----------

        // Los perks. Fuerza: +1 lugar por elección, 10 elecciones (la mochila llega a 10 + 10 de perks + 10 de monedas = 30).
        // Velocidad: +1,5 % por elección, 10 elecciones (+15 %): con la mejora de monedas al máximo (x1,30) da x1,495, justo bajo el
        // tope permanente de x1,5 del GameConfig. Mínimo a propósito: que no le reste valor al x3 de velocidad por video de la Fase 8.
        private sealed class PerkSpec
        {
            public string File, Id, Name, Short;
            public PerkKind Kind;
            public float ValuePerPick;
            public int MaxPicks;
            public int Accent;      // color del aro del pad (celda de la paleta)
            public float X;         // posición en la fila de perks
            public float Z = PlazaBuilder.PointsRowZ;   // primera fila; IN-04 abre una segunda (PointsRow2Z)
        }

        private static readonly PerkSpec[] Perks =
        {
            new PerkSpec { File = "Perk_Strength", Id = "perk_strength", Name = "Fuerza", Short = "Fue", Kind = PerkKind.BackpackCapacity, ValuePerPick = 1f, MaxPicks = 10, Accent = Palette.TomatoRed, X = PlazaBuilder.EastNearX },
            new PerkSpec { File = "Perk_Speed", Id = "perk_speed", Name = "Velocidad", Short = "Vel", Kind = PerkKind.MoveSpeed, ValuePerPick = 0.015f, MaxPicks = 10, Accent = Palette.Flower, X = PlazaBuilder.EastFarX },
            // F-24: Suerte, la tercera columna del sector Puntos. +0,5 puntos de probabilidad de cosecha dorada por elección: al máximo
            // (10) la dorada pasa de 3 % a 8 % (J-04). PROVISORIO: se ajusta con datos reales (B-05).
            new PerkSpec { File = "Perk_Luck", Id = "perk_luck", Name = "Suerte", Short = "Sue", Kind = PerkKind.RareChance, ValuePerPick = 0.005f, MaxPicks = 10, Accent = Palette.WheatGold, X = PlazaBuilder.HelperX },
            // IN-04: segunda fila del sector Puntos. +1 % de precio / de crecimiento por elección (+10 % al máximo), un multiplicador
            // APARTE del de las mejoras y de la tienda de prestigio (que mantienen su tope). PROVISORIOS: se ajustan con datos reales (B-05).
            new PerkSpec { File = "Perk_Barter", Id = "perk_barter", Name = "Regateo", Short = "Reg", Kind = PerkKind.SellPrice, ValuePerPick = 0.01f, MaxPicks = 10, Accent = Palette.CarrotOrange, X = PlazaBuilder.EastNearX, Z = PlazaBuilder.PointsRow2Z },
            new PerkSpec { File = "Perk_GreenThumb", Id = "perk_greenthumb", Name = "Mano verde", Short = "Man", Kind = PerkKind.GrowthSpeed, ValuePerPick = 0.01f, MaxPicks = 10, Accent = Palette.LeafLight, X = PlazaBuilder.EastFarX, Z = PlazaBuilder.PointsRow2Z },
        };

        private static string PerkPath(PerkSpec perk) { return PerksFolder + "/" + perk.File + ".asset"; }
        private static string PadMeshPath(PerkSpec perk) { return MeshesFolder + "/SM_PerkPad_" + perk.Kind + ".asset"; }

        // ---------- Balance ----------

        [MenuItem("FarmIdle/Fase 7/Aplicar balance de cultivos y mejoras")]
        public static void ApplyBalance()
        {
            Phase2SceneBuilder.ApplyCropBalance();
            ProgressionData.ApplyBalance();

            // B-02: con este valor el bot llega al nivel 20 cerca de la hora 3 de jugador real (Docs/ECONOMIA_V1.md). El valor por
            // defecto del código sigue en 125: los bancos de prueba con un GameConfig armado a mano dependen de él.
            var config = Phase2SceneBuilder.LoadRequired<GameConfig>(Phase2SceneBuilder.ConfigPath);
            var configSerialized = new SerializedObject(config);
            configSerialized.FindProperty("levelBaseXp").intValue = LevelBaseXp;
            configSerialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(config);

            foreach (PerkSpec spec in Perks)
            {
                var perk = AssetDatabase.LoadAssetAtPath<PerkData>(PerkPath(spec));
                if (perk == null) continue;

                var serialized = new SerializedObject(perk);
                serialized.FindProperty("valuePerPick").floatValue = spec.ValuePerPick;
                serialized.FindProperty("maxPicks").intValue = spec.MaxPicks;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(perk);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[FarmIdle] Balance de la Fase 7 aplicado a los cultivos, zonas, mejoras y perks.");
        }

        // ---------- Assets ----------

        internal static void CreateAssets()
        {
            Phase1SceneBuilder.EnsureFolder(PerksFolder);
            Phase1SceneBuilder.EnsureFolder(MeshesFolder);

            foreach (PerkSpec perk in Perks)
            {
                CreatePerkData(perk);
                Phase3SceneBuilder.SaveMesh(BuildPerkPadMesh(perk).ToMesh("SM_PerkPad_" + perk.Kind), PadMeshPath(perk));
            }

            LinkConfig();
            AssetDatabase.SaveAssets();
        }

        private static void CreatePerkData(PerkSpec spec)
        {
            string path = PerkPath(spec);
            if (AssetDatabase.LoadAssetAtPath<PerkData>(path) != null) return;   // respeta lo que se haya ajustado a mano

            var perk = ScriptableObject.CreateInstance<PerkData>();
            var serialized = new SerializedObject(perk);
            serialized.FindProperty("id").stringValue = spec.Id;
            serialized.FindProperty("displayName").stringValue = spec.Name;
            serialized.FindProperty("shortLabel").stringValue = spec.Short;
            serialized.FindProperty("kind").enumValueIndex = (int)spec.Kind;
            serialized.FindProperty("valuePerPick").floatValue = spec.ValuePerPick;
            serialized.FindProperty("maxPicks").intValue = spec.MaxPicks;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            AssetDatabase.CreateAsset(perk, path);
        }

        // El GameConfig recibe la lista de perks y la de cultivos solo si todavía las tiene vacías.
        private static void LinkConfig()
        {
            var config = Phase2SceneBuilder.LoadRequired<GameConfig>(Phase2SceneBuilder.ConfigPath);
            var serialized = new SerializedObject(config);

            var perks = new List<Object>();
            foreach (PerkSpec perk in Perks) perks.Add(Phase2SceneBuilder.LoadRequired<PerkData>(PerkPath(perk)));
            FillIfEmpty(serialized, "perks", perks);
            AppendMissing(serialized, "perks", perks);   // perks nuevos (F-24) entran a un config que ya tenía la lista

            var crops = new List<Object>();
            foreach (Phase2SceneBuilder.CropSpec crop in Phase2SceneBuilder.CropSpecs)
            {
                crops.Add(Phase2SceneBuilder.LoadRequired<CropData>(Phase2SceneBuilder.CropsFolder + "/" + crop.File + ".asset"));
            }
            AppendMissing(serialized, "crops", crops);   // cultivos nuevos (F-01) entran a un config que ya tenía la lista

            foreach (string field in new[] { "levelBaseXp", "maxLevel", "maxMoveSpeedMultiplier", "saveDelaySeconds" })
            {
                if (serialized.FindProperty(field) == null) throw new InvalidOperationException("GameConfig no tiene el campo '" + field + "'.");
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(config);
        }

        // Suma al final los valores que todavía no están en la lista, sin tocar el orden ni lo que ya había.
        internal static void AppendMissing(SerializedObject serialized, string field, IReadOnlyList<Object> values)
        {
            SerializedProperty property = serialized.FindProperty(field);
            if (property == null || !property.isArray) throw new InvalidOperationException("GameConfig no tiene la lista '" + field + "'.");

            foreach (Object value in values)
            {
                bool present = false;
                for (int i = 0; i < property.arraySize; i++)
                {
                    if (property.GetArrayElementAtIndex(i).objectReferenceValue == value)
                    {
                        present = true;
                        break;
                    }
                }

                if (present) continue;

                property.arraySize++;
                property.GetArrayElementAtIndex(property.arraySize - 1).objectReferenceValue = value;
            }
        }

        private static void FillIfEmpty(SerializedObject serialized, string field, IReadOnlyList<Object> values)
        {
            SerializedProperty property = serialized.FindProperty(field);
            if (property == null || !property.isArray) throw new InvalidOperationException("GameConfig no tiene la lista '" + field + "'.");
            if (property.arraySize > 0) return;

            property.arraySize = values.Count;
            for (int i = 0; i < values.Count; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }
        }

        // ---------- Mallas ----------

        // La misma plataforma baja que las mejoras (marco de madera, tablero crema, aro del color del perk) con un dibujo en el medio.
        private static PaletteMeshBuilder BuildPerkPadMesh(PerkSpec perk)
        {
            var mesh = new PaletteMeshBuilder();
            const float frame = 0.12f;
            const float size = PadBuilder.PadSize;
            const float top = PadBuilder.PadTop;

            mesh.AddBox(new Vector3(0f, frame * 0.5f, 0f), new Vector3(size, frame, size), Quaternion.identity, Palette.Wood);
            mesh.AddBox(new Vector3(0f, top * 0.5f, 0f), new Vector3(size - 0.3f, top, size - 0.3f), Quaternion.identity, Palette.Cream);
            mesh.AddCylinder(new Vector3(0f, top + 0.01f, 0f), 0.98f, 0.02f, 16, Quaternion.identity, perk.Accent);
            mesh.AddCylinder(new Vector3(0f, top + 0.02f, 0f), 0.84f, 0.02f, 16, Quaternion.identity, Palette.Cream);

            float y = top + 0.03f;
            switch (perk.Kind)
            {
                case PerkKind.BackpackCapacity:
                    // Una pesa: barra y dos discos de cada lado, apoyada en el pad.
                    {
                        Quaternion alongX = Quaternion.Euler(0f, 0f, 90f);
                        float center = y + 0.32f;
                        mesh.AddCylinder(new Vector3(0f, center, 0f), 0.07f, 1.3f, 8, alongX, Palette.Stone);
                        for (int side = -1; side <= 1; side += 2)
                        {
                            mesh.AddCylinder(new Vector3(side * 0.42f, center, 0f), 0.32f, 0.14f, 12, alongX, Palette.Wood);
                            mesh.AddCylinder(new Vector3(side * 0.56f, center, 0f), 0.24f, 0.14f, 12, alongX, Palette.TomatoRed);
                        }
                    }
                    break;

                case PerkKind.MoveSpeed:
                    // Tres flechas hacia adelante, cada vez más chicas (se ven "corriendo").
                    for (int i = 0; i < 3; i++)
                    {
                        float z = -0.45f + i * 0.45f;
                        float arm = 0.62f - i * 0.1f;
                        mesh.AddBox(new Vector3(arm * 0.35f, y + 0.05f, z), new Vector3(arm, 0.1f, 0.15f), Quaternion.Euler(0f, 45f, 0f), Palette.Flower);
                        mesh.AddBox(new Vector3(-arm * 0.35f, y + 0.05f, z), new Vector3(arm, 0.1f, 0.15f), Quaternion.Euler(0f, -45f, 0f), Palette.Flower);
                    }
                    break;

                case PerkKind.RareChance:
                    // Un trébol de cuatro hojas con el centro dorado y un tallito.
                    for (int i = 0; i < 4; i++)
                    {
                        float angle = i * 90f * Mathf.Deg2Rad;
                        var leaf = new Vector3(Mathf.Cos(angle) * 0.34f, y + 0.07f, Mathf.Sin(angle) * 0.34f);
                        mesh.AddCylinder(leaf, 0.3f, 0.14f, 12, Quaternion.identity, i % 2 == 0 ? Palette.LeafLight : Palette.LeafDark);
                    }

                    mesh.AddSphere(new Vector3(0f, y + 0.16f, 0f), 0.17f, Palette.WheatGold);
                    mesh.AddBox(new Vector3(0.18f, y + 0.04f, -0.6f), new Vector3(0.08f, 0.08f, 0.5f), Quaternion.Euler(0f, 20f, 0f), Palette.LeafDark);
                    break;

                case PerkKind.SellPrice:
                    // Regateo: una pila de tres monedas de oro con una más parada de canto detrás.
                    for (int i = 0; i < 3; i++) mesh.AddCylinder(new Vector3(-0.2f, y + 0.07f + i * 0.14f, 0.15f), 0.5f, 0.12f, 14, Quaternion.identity, i == 2 ? Palette.WheatGold : Palette.CarrotOrange);
                    mesh.AddCylinder(new Vector3(0.45f, y + 0.5f, -0.2f), 0.5f, 0.12f, 14, Quaternion.Euler(90f, 0f, 0f), Palette.WheatGold);
                    break;

                case PerkKind.GrowthSpeed:
                    // Mano verde: un brote con dos hojas saliendo de una maceta.
                    mesh.AddCylinder(new Vector3(0f, y + 0.15f, 0f), 0.7f, 0.3f, 12, Quaternion.identity, Palette.Wood);
                    mesh.AddBox(new Vector3(0f, y + 0.5f, 0f), new Vector3(0.09f, 0.55f, 0.09f), Quaternion.identity, Palette.LeafDark);
                    mesh.AddBox(new Vector3(0.28f, y + 0.82f, 0f), new Vector3(0.6f, 0.07f, 0.34f), Quaternion.Euler(0f, 0f, 28f), Palette.LeafLight);
                    mesh.AddBox(new Vector3(-0.28f, y + 0.74f, 0f), new Vector3(0.6f, 0.07f, 0.34f), Quaternion.Euler(0f, 0f, -28f), Palette.LeafLight);
                    break;
            }

            return mesh;
        }

        // ---------- Escena ----------

        // Los dos pads de perk, una fila detrás de la plaza de mejoras. Los assets se recargan por ruta justo antes de usarlos.
        internal static void BuildPerkPads()
        {
            var root = new GameObject("PerkPlaza");
            var material = Phase2SceneBuilder.LoadRequired<Material>(Phase3SceneBuilder.PaletteMaterialPath);

            foreach (PerkSpec spec in Perks)
            {
                var perkData = Phase2SceneBuilder.LoadRequired<PerkData>(PerkPath(spec));
                var mesh = Phase2SceneBuilder.LoadRequired<Mesh>(PadMeshPath(spec));

                var pad = new GameObject(spec.File.Replace("Perk_", "PerkPad_"));
                pad.transform.SetParent(root.transform, false);
                pad.transform.position = new Vector3(spec.X, 0f, spec.Z);

                // Trigger de la altura del jugador (2), como los demás pads.
                var trigger = pad.AddComponent<BoxCollider>();
                trigger.isTrigger = true;
                trigger.center = new Vector3(0f, 1f, 0f);
                float side = Mathf.Min(PadBuilder.TriggerSize, PadBuilder.PadSize - 0.4f);
                trigger.size = new Vector3(side, 2f, side);

                var component = pad.AddComponent<PerkPad>();
                pad.AddComponent<InteractionTrigger>();

                Transform visual = PadBuilder.CreateMeshChild(pad.transform, "Visual", mesh, material, Vector3.zero);
                PadBuilder.CreateLabel(pad.transform);

                EditorUi.Wire(component,
                    ("perk", perkData),
                    ("label", pad.transform.Find("Label").GetComponent<TextMeshPro>()),
                    ("padVisual", visual));
            }
        }

        // La barra de nivel, arriba a la izquierda (el contador de monedas está a la derecha; el de FPS, que es de desarrollo, se
        // movió más abajo para no taparla). Dos textos cuelgan fuera de la píldora: los puntos libres (debajo) y el cartel de
        // "¡Nivel N!" (más abajo todavía).
        internal static void BuildLevelBar(RectTransform hud)
        {
            Image pill = EditorUi.CreatePanel("LevelBar", hud, UiTheme.Pill, false);
            RectTransform root = pill.rectTransform;
            EditorUi.Anchor(root, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), new Vector2(330f, -85f), new Vector2(600f, 110f));

            TextMeshProUGUI level = EditorUi.CreateText("Level", root, "", 52f, new Color(1f, 0.92f, 0.45f), TextAlignmentOptions.Left, FontStyles.Bold);
            level.enableAutoSizing = true;
            level.fontSizeMin = 28f;
            level.fontSizeMax = 52f;
            EditorUi.Anchor(level.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(22f, 0f), new Vector2(180f, 90f));

            // Barra: fondo oscuro y relleno que escala en X desde su borde izquierdo.
            Image back = EditorUi.CreatePanel("BarBack", root, UiTheme.BarBack, false);
            EditorUi.Anchor(back.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(210f, 16f), new Vector2(370f, 34f));

            Image fill = EditorUi.CreatePanel("Fill", back.transform, UiTheme.BarFill, false);
            RectTransform fillRect = fill.rectTransform;
            fillRect.anchorMin = new Vector2(0f, 0f);
            fillRect.anchorMax = new Vector2(1f, 1f);
            fillRect.pivot = new Vector2(0f, 0.5f);
            fillRect.offsetMin = new Vector2(3f, 3f);
            fillRect.offsetMax = new Vector2(-3f, -3f);

            TextMeshProUGUI xp = EditorUi.CreateText("Xp", root, "", 26f, UiTheme.BodyText, TextAlignmentOptions.Left);
            EditorUi.Anchor(xp.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(214f, -26f), new Vector2(370f, 34f));

            TextMeshProUGUI points = EditorUi.CreateText("Points", root, "", 34f, new Color(1f, 0.92f, 0.45f), TextAlignmentOptions.Left, FontStyles.Bold);
            EditorUi.Anchor(points.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(10f, -8f), new Vector2(800f, 50f));
            points.gameObject.SetActive(false);

            TextMeshProUGUI levelUp = EditorUi.CreateText("LevelUp", root, "", 84f, new Color(1f, 0.85f, 0.25f), TextAlignmentOptions.Left, FontStyles.Bold);
            EditorUi.Anchor(levelUp.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(10f, -70f), new Vector2(800f, 110f));
            levelUp.gameObject.SetActive(false);

            var view = root.gameObject.AddComponent<LevelBarView>();
            EditorUi.Wire(view,
                ("levelLabel", level),
                ("fill", fillRect),
                ("xpLabel", xp),
                ("pointsLabel", points),
                ("levelUpLabel", levelUp),
                ("pulseTarget", root));
        }
    }
}
