using System;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace FarmIdle.EditorTools
{
    // Piezas de la Fase 8: anuncios con recompensa, fertilizante y el símbolo de oferta de velocidad. Los datos de las recompensas, el
    // enganche al GameConfig, las mallas (arte provisorio con la paleta), el prefab del símbolo, los tres pads del fertilizante, los
    // lugares donde aparece el símbolo, el HUD de buffs, la capa de "anuncio en pantalla" y los botones de debug nuevos.
    // Igual que las demás, es una biblioteca: Phase2SceneBuilder.BuildAll la llama. Los datos que ya existen NO se pisan (los números
    // nuevos a assets viejos van por ApplyBalance).
    //   Menú:    FarmIdle > Fase 8 > Aplicar balance de recompensas
    //   Consola: Unity.exe -batchmode -quit -projectPath <ruta> -executeMethod FarmIdle.EditorTools.Phase8SceneBuilder.ApplyBalance
    internal static class Phase8SceneBuilder
    {
        private const string RewardsFolder = "Assets/ScriptableObjects/Rewards";
        private const string MeshesFolder = "Assets/Meshes/Environment";
        private const string PrefabsFolder = "Assets/Prefabs/Rewards";
        private const string SymbolPrefabPath = PrefabsFolder + "/RewardSymbol.prefab";

        private const float RowZ = PlazaBuilder.VideoRowZ;   // Videos (oeste) y Fertilizante (este): los primeros sectores, junto a la entrada (es lo que más se visita)
        private const float SymbolTrigger = 2.2f;

        // ---------- Tablas de diseño ----------

        // Las recompensas. Turbo: x3 nominal sobre la velocidad (el tope final de 11 u/s del GameConfig lo acota: ver MoveSpeedRules),
        // 30 s de juego; el enfriamiento de 90 s evita pedirlo de nuevo enseguida (el símbolo igual aparece cada 120-180 s). Fertilizante
        // por video: +1 al stock, enfriamiento de 120 s: con el tope de 3 de stock da como mucho ~+5-10 % de ritmo (simulación de la fase).
        private sealed class RewardSpec
        {
            public string File, Id, Name, Short;
            public RewardKind Kind;
            public float Magnitude, Duration, Cooldown;
        }

        private static readonly RewardSpec[] Rewards =
        {
            new RewardSpec { File = "Reward_Turbo", Id = "reward_turbo", Name = "Turbo", Short = "Turbo", Kind = RewardKind.MoveSpeedBuff, Magnitude = 3f, Duration = 30f, Cooldown = 90f },
            new RewardSpec { File = "Reward_FertilizerVideo", Id = "reward_fertilizer", Name = "Fertilizante", Short = "Fert", Kind = RewardKind.Fertilizer, Magnitude = 1f, Duration = 0f, Cooldown = 120f },
            // B-01: duplica lo ganado fuera. Sin enfriamiento propio: la tarjeta se ofrece una vez por regreso (B-03 fija las reglas finales).
            new RewardSpec { File = "Reward_OfflineDouble", Id = "reward_offline_double", Name = "Duplicar", Short = "x2", Kind = RewardKind.OfflineDouble, Magnitude = 2f, Duration = 0f, Cooldown = 0f },
        };

        // Los lugares del símbolo de oferta: terreno libre, lejos de los pads, los portones y los campos.
        private static readonly Vector3[] SymbolSlots =
        {
            new Vector3(-7.5f, 0f, 1.5f), new Vector3(7.5f, 0f, 1.5f), new Vector3(-8.5f, 0f, -5f), new Vector3(8.5f, 0f, -5f)
        };

        private static string RewardPath(RewardSpec reward) { return RewardsFolder + "/" + reward.File + ".asset"; }

        internal enum PadIcon { Use, Buy, Video, Turbo, Symbol, SymbolBase, Decor, Helper, Skin, Order, Truck }

        private static string MeshPath(PadIcon icon) { return MeshesFolder + "/SM_Reward_" + icon + ".asset"; }

        // ---------- Balance ----------

        [MenuItem("FarmIdle/Fase 8/Aplicar balance de recompensas")]
        public static void ApplyBalance()
        {
            foreach (RewardSpec spec in Rewards)
            {
                var reward = AssetDatabase.LoadAssetAtPath<RewardData>(RewardPath(spec));
                if (reward == null) continue;

                var serialized = new SerializedObject(reward);
                serialized.FindProperty("magnitude").floatValue = spec.Magnitude;
                serialized.FindProperty("durationSeconds").floatValue = spec.Duration;
                serialized.FindProperty("cooldownSeconds").floatValue = spec.Cooldown;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(reward);
            }

            var config = AssetDatabase.LoadAssetAtPath<GameConfig>(Phase2SceneBuilder.ConfigPath);
            if (config != null)
            {
                var serialized = new SerializedObject(config);
                SetFloat(serialized, "maxFinalMoveSpeed", 11f);
                SetInt(serialized, "fertilizerPrice", 1500);
                SetInt(serialized, "fertilizerMaxStock", 3);
                SetInt(serialized, "fertilizerBuyMaxStock", 1);
                SetFloat(serialized, "fertilizerBuyCooldownSeconds", 180f);
                SetFloat(serialized, "fertilizerPriceStepPerZone", 1.8f);
                SetFloat(serialized, "adMinIntervalSeconds", 180f);
                SetFloat(serialized, "adTimeoutSeconds", 120f);
                SetFloat(serialized, "offerFirstDelaySeconds", 60f);
                SetFloat(serialized, "offerMinIntervalSeconds", 120f);
                SetFloat(serialized, "offerMaxIntervalSeconds", 180f);
                SetFloat(serialized, "offerLifetimeSeconds", 25f);
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(config);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[FarmIdle] Balance de la Fase 8 aplicado a las recompensas y al GameConfig.");
        }

        private static void SetFloat(SerializedObject serialized, string field, float value)
        {
            SerializedProperty property = serialized.FindProperty(field);
            if (property == null) throw new InvalidOperationException("GameConfig no tiene el campo '" + field + "'.");
            property.floatValue = value;
        }

        private static void SetInt(SerializedObject serialized, string field, int value)
        {
            SerializedProperty property = serialized.FindProperty(field);
            if (property == null) throw new InvalidOperationException("GameConfig no tiene el campo '" + field + "'.");
            property.intValue = value;
        }

        // ---------- Assets ----------

        internal static void CreateAssets()
        {
            Phase1SceneBuilder.EnsureFolder(RewardsFolder);
            Phase1SceneBuilder.EnsureFolder(MeshesFolder);
            Phase1SceneBuilder.EnsureFolder(PrefabsFolder);

            foreach (RewardSpec spec in Rewards) CreateRewardData(spec);

            foreach (PadIcon icon in new[] { PadIcon.Use, PadIcon.Buy, PadIcon.Video, PadIcon.Turbo, PadIcon.Symbol, PadIcon.SymbolBase, PadIcon.Decor, PadIcon.Helper, PadIcon.Skin, PadIcon.Order, PadIcon.Truck })
            {
                Phase3SceneBuilder.SaveMesh(BuildMesh(icon).ToMesh("SM_Reward_" + icon), MeshPath(icon));
            }

            LinkConfig();
            AssetDatabase.SaveAssets();

            DecorationBuilder.CreateAssets();   // F-17: datos y mallas de las decoraciones de la granja
            HelperCrewBuilder.CreateAssets();   // F-19: mallas de los ayudantes que se ven

            BuildSymbolPrefab();
            AssetDatabase.SaveAssets();
        }

        private static void CreateRewardData(RewardSpec spec)
        {
            string path = RewardPath(spec);
            if (AssetDatabase.LoadAssetAtPath<RewardData>(path) != null) return;   // respeta lo que se haya ajustado a mano

            var reward = ScriptableObject.CreateInstance<RewardData>();
            var serialized = new SerializedObject(reward);
            serialized.FindProperty("id").stringValue = spec.Id;
            serialized.FindProperty("displayName").stringValue = spec.Name;
            serialized.FindProperty("shortLabel").stringValue = spec.Short;
            serialized.FindProperty("kind").enumValueIndex = (int)spec.Kind;
            serialized.FindProperty("magnitude").floatValue = spec.Magnitude;
            serialized.FindProperty("durationSeconds").floatValue = spec.Duration;
            serialized.FindProperty("cooldownSeconds").floatValue = spec.Cooldown;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            AssetDatabase.CreateAsset(reward, path);
        }

        // El GameConfig recibe la lista de recompensas solo si todavía la tiene vacía.
        private static void LinkConfig()
        {
            var config = Phase2SceneBuilder.LoadRequired<GameConfig>(Phase2SceneBuilder.ConfigPath);
            var serialized = new SerializedObject(config);

            SerializedProperty property = serialized.FindProperty("rewards");
            if (property == null || !property.isArray) throw new InvalidOperationException("GameConfig no tiene la lista 'rewards'.");

            // Las recompensas nuevas (por ejemplo, la de B-01) entran a un config que ya tenía la lista, sin tocar el orden.
            var rewardAssets = new System.Collections.Generic.List<UnityEngine.Object>();
            foreach (RewardSpec spec in Rewards) rewardAssets.Add(Phase2SceneBuilder.LoadRequired<RewardData>(RewardPath(spec)));
            Phase7SceneBuilder.AppendMissing(serialized, "rewards", rewardAssets);

            foreach (string field in new[] { "maxFinalMoveSpeed", "fertilizerPrice", "fertilizerMaxStock", "fertilizerBuyMaxStock", "fertilizerBuyCooldownSeconds", "fertilizerPriceStepPerZone", "adMinIntervalSeconds", "adTimeoutSeconds", "offerFirstDelaySeconds", "offerMinIntervalSeconds", "offerMaxIntervalSeconds", "offerLifetimeSeconds" })
            {
                if (serialized.FindProperty(field) == null) throw new InvalidOperationException("GameConfig no tiene el campo '" + field + "'.");
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(config);
        }

        // ---------- Mallas ----------

        // La misma plataforma baja que los pads de mejoras y de perks, con un dibujo en el medio que dice qué es.
        private static PaletteMeshBuilder BuildMesh(PadIcon icon)
        {
            var mesh = new PaletteMeshBuilder();

            if (icon == PadIcon.Symbol) return BuildSymbolMesh(mesh);
            if (icon == PadIcon.SymbolBase) return BuildSymbolBaseMesh(mesh);

            const float frame = 0.12f;
            const float size = PadBuilder.PadSize;
            const float top = PadBuilder.PadTop;
            int accent = icon == PadIcon.Use ? Palette.LeafLight : (icon == PadIcon.Buy ? Palette.WheatGold : (icon == PadIcon.Decor ? Palette.Flower : Palette.Water));

            mesh.AddBox(new Vector3(0f, frame * 0.5f, 0f), new Vector3(size, frame, size), Quaternion.identity, Palette.Wood);
            mesh.AddBox(new Vector3(0f, top * 0.5f, 0f), new Vector3(size - 0.3f, top, size - 0.3f), Quaternion.identity, Palette.Cream);
            mesh.AddCylinder(new Vector3(0f, top + 0.01f, 0f), 0.98f, 0.02f, 16, Quaternion.identity, accent);
            mesh.AddCylinder(new Vector3(0f, top + 0.02f, 0f), 0.84f, 0.02f, 16, Quaternion.identity, Palette.Cream);

            float y = top + 0.03f;
            switch (icon)
            {
                case PadIcon.Use:
                    AddSack(mesh, new Vector3(0f, y, 0f), 1f);
                    // Un brote verde a cada lado: "hace crecer".
                    for (int side = -1; side <= 1; side += 2)
                    {
                        mesh.AddStanding(new Vector3(side * 0.62f, y, -0.05f), new Vector3(0.07f, 0.34f, 0.07f), Quaternion.identity, Palette.LeafDark);
                        mesh.AddBox(new Vector3(side * 0.62f + side * 0.1f, y + 0.36f, -0.05f), new Vector3(0.26f, 0.04f, 0.14f), Quaternion.Euler(0f, 0f, side * -25f), Palette.LeafLight);
                    }
                    break;

                case PadIcon.Buy:
                    AddSack(mesh, new Vector3(-0.35f, y, 0f), 0.8f);
                    // Una pila de monedas a la derecha.
                    mesh.AddCylinder(new Vector3(0.5f, y + 0.07f, 0f), 0.34f, 0.14f, 12, Quaternion.identity, Palette.WheatStraw);
                    mesh.AddCylinder(new Vector3(0.5f, y + 0.21f, 0f), 0.34f, 0.14f, 12, Quaternion.identity, Palette.WheatGold);
                    mesh.AddCylinder(new Vector3(0.5f, y + 0.35f, 0f), 0.34f, 0.14f, 12, Quaternion.identity, Palette.WheatStraw);
                    break;

                case PadIcon.Video:
                    // Una pantalla: marco oscuro, vidrio y un triángulo de "play" mirando a la cámara (hacia -z).
                    mesh.AddBox(new Vector3(0f, y + 0.55f, 0f), new Vector3(1.3f, 0.86f, 0.16f), Quaternion.identity, Palette.SoilDark);
                    mesh.AddBox(new Vector3(0f, y + 0.55f, -0.05f), new Vector3(1.14f, 0.7f, 0.06f), Quaternion.identity, Palette.Water);
                    mesh.AddCone(new Vector3(0f, y + 0.55f, -0.09f), 0.2f, new Vector3(0f, y + 0.55f, -0.2f), 3, Palette.White);
                    mesh.AddBox(new Vector3(0f, y + 0.07f, 0f), new Vector3(0.5f, 0.14f, 0.3f), Quaternion.identity, Palette.Wood);
                    break;

                case PadIcon.Turbo:
                    // Un rayo dorado de cara a la cámara (hacia -z) sobre una placa oscura: trazo de arriba, barra del medio y trazo de abajo.
                    mesh.AddBox(new Vector3(0f, y + 0.8f, 0.08f), new Vector3(1f, 1.5f, 0.06f), Quaternion.identity, Palette.SoilDark);
                    mesh.AddBox(new Vector3(0.1f, y + 1.1f, -0.02f), new Vector3(0.26f, 0.76f, 0.14f), Quaternion.Euler(0f, 0f, -31.6f), Palette.WheatGold);
                    mesh.AddBox(new Vector3(0.02f, y + 0.78f, -0.02f), new Vector3(0.62f, 0.18f, 0.14f), Quaternion.identity, Palette.WheatGold);
                    mesh.AddBox(new Vector3(0.06f, y + 0.49f, -0.02f), new Vector3(0.26f, 0.64f, 0.14f), Quaternion.Euler(0f, 0f, -25.8f), Palette.WheatGold);
                    mesh.AddBox(new Vector3(0f, y + 0.07f, 0f), new Vector3(0.5f, 0.14f, 0.3f), Quaternion.identity, Palette.Wood);
                    break;

                case PadIcon.Decor:
                case PadIcon.Helper:
                case PadIcon.Skin: case PadIcon.Truck:
                    DecorationBuilder.AddPadIcon(mesh, y, icon);   // F-17
                    break;
                case PadIcon.Order: OrdersBuilder.AddPadIcon(mesh, y); break;   // F-18
            }

            return mesh;
        }

        // Un saco atado con una franja verde.
        private static void AddSack(PaletteMeshBuilder mesh, Vector3 origin, float scale)
        {
            mesh.AddBox(origin + new Vector3(0f, 0.28f, 0f) * scale, new Vector3(0.8f, 0.56f, 0.58f) * scale, Quaternion.identity, Palette.WheatStraw);
            mesh.AddBox(origin + new Vector3(0f, 0.66f, 0f) * scale, new Vector3(0.46f, 0.2f, 0.4f) * scale, Quaternion.identity, Palette.WheatStraw);
            mesh.AddBox(origin + new Vector3(0f, 0.57f, 0f) * scale, new Vector3(0.52f, 0.05f, 0.46f) * scale, Quaternion.identity, Palette.Wood);
            mesh.AddBox(origin + new Vector3(0f, 0.3f, -0.295f * scale), new Vector3(0.62f, 0.2f, 0.02f) * scale, Quaternion.identity, Palette.LeafLight);
        }

        // El símbolo en sí (lo que flota y gira): un disco dorado con dos flechas azules encima, origen en su centro.
        private static PaletteMeshBuilder BuildSymbolMesh(PaletteMeshBuilder mesh)
        {
            mesh.AddCylinder(Vector3.zero, 0.55f, 0.16f, 16, Quaternion.identity, Palette.WheatGold);
            mesh.AddCylinder(new Vector3(0f, 0.09f, 0f), 0.44f, 0.04f, 16, Quaternion.identity, Palette.WheatStraw);
            for (int i = 0; i < 2; i++)
            {
                float z = -0.14f + i * 0.28f;
                mesh.AddBox(new Vector3(0.17f, 0.15f, z), new Vector3(0.44f, 0.07f, 0.11f), Quaternion.Euler(0f, 45f, 0f), Palette.Water);
                mesh.AddBox(new Vector3(-0.17f, 0.15f, z), new Vector3(0.44f, 0.07f, 0.11f), Quaternion.Euler(0f, -45f, 0f), Palette.Water);
            }

            return mesh;
        }

        // La base en el suelo: un aro celeste que marca dónde pararse.
        private static PaletteMeshBuilder BuildSymbolBaseMesh(PaletteMeshBuilder mesh)
        {
            mesh.AddCylinder(new Vector3(0f, 0.015f, 0f), 1.05f, 0.03f, 20, Quaternion.identity, Palette.Water);
            mesh.AddCylinder(new Vector3(0f, 0.03f, 0f), 0.9f, 0.03f, 20, Quaternion.identity, Palette.Cream);
            return mesh;
        }

        // ---------- Prefab del símbolo ----------

        private static void BuildSymbolPrefab()
        {
            var material = Phase2SceneBuilder.LoadRequired<Material>(Phase3SceneBuilder.PaletteMaterialPath);
            var symbolMesh = Phase2SceneBuilder.LoadRequired<Mesh>(MeshPath(PadIcon.Symbol));
            var baseMesh = Phase2SceneBuilder.LoadRequired<Mesh>(MeshPath(PadIcon.SymbolBase));

            var root = new GameObject("RewardSymbol");

            // Trigger de la altura del jugador (2), como los demás pads.
            var trigger = root.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0f, 1f, 0f);
            trigger.size = new Vector3(SymbolTrigger, 2f, SymbolTrigger);

            var symbol = root.AddComponent<RewardSymbol>();
            root.AddComponent<InteractionTrigger>();

            PadBuilder.CreateMeshChild(root.transform, "Base", baseMesh, material, Vector3.zero);
            Transform floater = PadBuilder.CreateMeshChild(root.transform, "Floater", symbolMesh, material, new Vector3(0f, 1.1f, 0f));

            // La barra de espera, delante del símbolo (hacia la cámara).
            PadBuilder.CreateMeshChild(root.transform, "FillBack", Phase2SceneBuilder.LoadRequired<Mesh>(MeshesFolder + "/SM_PadFillBack.asset"), material, new Vector3(0f, 0f, -1.4f));
            Transform fill = PadBuilder.CreateMeshChild(root.transform, "Fill", Phase2SceneBuilder.LoadRequired<Mesh>(MeshesFolder + "/SM_PadFill.asset"), material, new Vector3(-1.1f, 0f, -1.4f));

            PadBuilder.CreateLabel(root.transform);
            Transform labelTransform = root.transform.Find("Label");
            labelTransform.localPosition = new Vector3(0f, 2.9f, 0.2f);

            EditorUi.Wire(symbol,
                ("label", labelTransform.GetComponent<TextMeshPro>()),
                ("fillBar", fill),
                ("padVisual", floater),
                ("floater", floater));

            PrefabUtility.SaveAsPrefabAsset(root, SymbolPrefabPath);
            Object.DestroyImmediate(root);
        }

        // ---------- Escena ----------

        // Los tres pads del fertilizante (video, usar, comprar) en una fila detrás de los pads de perk, y los lugares del símbolo de
        // oferta con su spawner. Los assets se recargan por ruta justo antes de usarlos.
        internal static void BuildWorldPieces()
        {
            var root = new GameObject("RewardsPlaza");

            var config = Phase2SceneBuilder.LoadRequired<GameConfig>(Phase2SceneBuilder.ConfigPath);
            RewardData fertilizerVideo = config.FirstRewardOfKind(RewardKind.Fertilizer);
            if (fertilizerVideo == null) throw new InvalidOperationException("El GameConfig no tiene la recompensa de fertilizante.");

            RewardData turboVideo = config.FirstRewardOfKind(RewardKind.MoveSpeedBuff);
            if (turboVideo == null) throw new InvalidOperationException("El GameConfig no tiene la recompensa de Turbo.");

            // Los dos videos en el sector Videos (oeste de la avenida) y usar y comprar fertilizante en el sector Fertilizante (este).
            var turbo = BuildDwellPad<RewardPad>(root.transform, "TurboPad_Video", new Vector3(PlazaBuilder.WestFarX, 0f, RowZ), PadIcon.Turbo);
            EditorUi.Wire(turbo, ("reward", turboVideo));
            turbo.gameObject.AddComponent<AdGatedObject>();   // sin anuncios (Basic Launch de la web) el pad se apaga

            var video = BuildDwellPad<RewardPad>(root.transform, "FertilizerPad_Video", new Vector3(PlazaBuilder.WestNearX, 0f, RowZ), PadIcon.Video);
            EditorUi.Wire(video, ("reward", fertilizerVideo));
            video.gameObject.AddComponent<AdGatedObject>();

            BuildDwellPad<FertilizerUsePad>(root.transform, "FertilizerPad_Use", new Vector3(PlazaBuilder.EastNearX, 0f, RowZ), PadIcon.Use);
            BuildDwellPad<FertilizerBuyPad>(root.transform, "FertilizerPad_Buy", new Vector3(PlazaBuilder.EastFarX, 0f, RowZ), PadIcon.Buy);

            DecorationBuilder.BuildWorldPieces(root.transform);   // F-17: pads de decoraciones y ayudantes, y las decoraciones
            HelperCrewBuilder.Build(root.transform);              // F-19: los peones, apagados hasta que se compra su nivel

            var slotsRoot = new GameObject("OfferSlots");
            slotsRoot.transform.SetParent(root.transform, false);

            var slots = new List<Object>();
            for (int i = 0; i < SymbolSlots.Length; i++)
            {
                var slot = new GameObject("Slot" + (i + 1));
                slot.transform.SetParent(slotsRoot.transform, false);
                slot.transform.position = SymbolSlots[i];
                slots.Add(slot.transform);
            }

            var spawner = slotsRoot.AddComponent<OfferSpawner>();
            EditorUi.Wire(spawner, ("symbolPrefab", Phase2SceneBuilder.LoadRequired<GameObject>(SymbolPrefabPath)));
            EditorUi.WireArray(spawner, "slots", slots);
        }

        internal static T BuildDwellPad<T>(Transform parent, string name, Vector3 position, PadIcon icon) where T : DwellPad
        {
            var material = Phase2SceneBuilder.LoadRequired<Material>(Phase3SceneBuilder.PaletteMaterialPath);
            var mesh = Phase2SceneBuilder.LoadRequired<Mesh>(MeshPath(icon));

            var pad = new GameObject(name);
            pad.transform.SetParent(parent, false);
            pad.transform.position = position;

            // Trigger de la altura del jugador (2), como los demás pads.
            var trigger = pad.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0f, 1f, 0f);
            float side = Mathf.Min(PadBuilder.TriggerSize, PadBuilder.PadSize - 0.4f);
            trigger.size = new Vector3(side, 2f, side);

            var component = pad.AddComponent<T>();
            pad.AddComponent<InteractionTrigger>();

            Transform visual = PadBuilder.CreateMeshChild(pad.transform, "Visual", mesh, material, Vector3.zero);

            float front = -(PadBuilder.PadSize * 0.5f + 0.35f);
            PadBuilder.CreateMeshChild(pad.transform, "FillBack", Phase2SceneBuilder.LoadRequired<Mesh>(MeshesFolder + "/SM_PadFillBack.asset"), material, new Vector3(0f, 0f, front));
            Transform fill = PadBuilder.CreateMeshChild(pad.transform, "Fill", Phase2SceneBuilder.LoadRequired<Mesh>(MeshesFolder + "/SM_PadFill.asset"), material, new Vector3(-1.1f, 0f, front));

            PadBuilder.CreateLabel(pad.transform);

            EditorUi.Wire(component,
                ("label", pad.transform.Find("Label").GetComponent<TextMeshPro>()),
                ("fillBar", fill),
                ("padVisual", visual));
            return component;
        }

        // ---------- HUD ----------

        // Los buffs activos y el stock de fertilizante, a la izquierda a media altura (arriba están el nivel y el contador de FPS).
        // Tres filas armadas de antemano: dos de buffs y una de fertilizante; las que no se usan quedan apagadas.
        internal static void BuildHud(RectTransform hud)
        {
            RectTransform root = EditorUi.CreateRect("RewardHud", hud);
            EditorUi.Anchor(root, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(20f, 80f), new Vector2(430f, 220f));

            RewardHudView.Row[] buffRows = { CreateHudRow(root, "BuffRow1", 0, new Color(0.25f, 0.65f, 0.95f)), CreateHudRow(root, "BuffRow2", 1, new Color(0.25f, 0.65f, 0.95f)) };
            RewardHudView.Row fertilizerRow = CreateHudRow(root, "FertilizerRow", 2, UiTheme.BarFill);
            fertilizerRow.fill.gameObject.SetActive(false);   // el fertilizante no vence: sin barra

            var view = root.gameObject.AddComponent<RewardHudView>();
            var serialized = new SerializedObject(view);
            SerializedProperty rows = serialized.FindProperty("buffRows");
            rows.arraySize = buffRows.Length;
            for (int i = 0; i < buffRows.Length; i++)
            {
                SerializedProperty row = rows.GetArrayElementAtIndex(i);
                row.FindPropertyRelative("root").objectReferenceValue = buffRows[i].root;
                row.FindPropertyRelative("text").objectReferenceValue = buffRows[i].text;
                row.FindPropertyRelative("fill").objectReferenceValue = buffRows[i].fill;
            }

            SerializedProperty fertilizer = serialized.FindProperty("fertilizerRow");
            fertilizer.FindPropertyRelative("root").objectReferenceValue = fertilizerRow.root;
            fertilizer.FindPropertyRelative("text").objectReferenceValue = fertilizerRow.text;
            fertilizer.FindPropertyRelative("fill").objectReferenceValue = fertilizerRow.fill;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static RewardHudView.Row CreateHudRow(RectTransform parent, string name, int index, Color barColor)
        {
            Image pill = EditorUi.CreatePanel(name, parent, UiTheme.Pill, false);
            EditorUi.Anchor(pill.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -index * 76f), new Vector2(430f, 66f));

            TextMeshProUGUI text = EditorUi.CreateText("Text", pill.transform, "", 34f, Color.white, TextAlignmentOptions.Left, FontStyles.Bold);
            text.enableAutoSizing = true;
            text.fontSizeMin = 20f;
            text.fontSizeMax = 34f;
            EditorUi.Anchor(text.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(18f, 5f), new Vector2(395f, 48f));

            Image back = EditorUi.CreatePanel("BarBack", pill.transform, UiTheme.BarBack, false);
            EditorUi.Anchor(back.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(14f, 6f), new Vector2(402f, 10f));

            Image fill = EditorUi.CreatePanel("Fill", back.transform, barColor, false);
            RectTransform fillRect = fill.rectTransform;
            fillRect.anchorMin = new Vector2(0f, 0f);
            fillRect.anchorMax = new Vector2(1f, 1f);
            fillRect.pivot = new Vector2(0f, 0.5f);
            fillRect.offsetMin = new Vector2(1f, 1f);
            fillRect.offsetMax = new Vector2(-1f, -1f);

            pill.gameObject.SetActive(false);
            return new RewardHudView.Row { root = pill.gameObject, text = text, fill = fillRect };
        }

        // La capa que tapa el juego mientras hay un anuncio en pantalla. Va debajo del menú inicial y encima del HUD.
        internal static void BuildAdOverlay(Transform canvas)
        {
            Image dim = EditorUi.CreatePanel("AdOverlay", canvas, new Color(0.02f, 0.05f, 0.03f, 0.82f), true);
            dim.sprite = null;
            dim.type = Image.Type.Simple;
            EditorUi.Stretch(dim.rectTransform);

            TextMeshProUGUI message = EditorUi.CreateText("Message", dim.transform, "", 72f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            EditorUi.Anchor(message.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), new Vector2(980f, 300f));

            var view = dim.gameObject.AddComponent<AdShowingView>();   // agrega también el CanvasGroup que exige
            EditorUi.Wire(view, ("message", message));
            dim.GetComponent<CanvasGroup>().alpha = 0f;
            dim.GetComponent<CanvasGroup>().blocksRaycasts = false;
            dim.GetComponent<CanvasGroup>().interactable = false;
        }

        // Tres botones de debug más (solo se ven en builds de desarrollo): el resultado del anuncio simulado, un fertilizante gratis y la
        // oferta ya. Siguen la columna de la derecha del panel.
        internal static void AddDebugButtons(RectTransform panel, DebugPanel debugPanel)
        {
            Button adMode = Phase2SceneBuilder.CreateDebugButton(panel, "Button Anuncio", "Anuncio: completo", 5, new Color(0.55f, 0.35f, 0.80f), out TextMeshProUGUI adModeLabel);
            adModeLabel.enableAutoSizing = true;
            adModeLabel.fontSizeMin = 18f;
            adModeLabel.fontSizeMax = 34f;

            Button fertilizer = Phase2SceneBuilder.CreateDebugButton(panel, "Button +Fert", "+Fert", 6, new Color(0.30f, 0.65f, 0.30f), out _);
            Button offer = Phase2SceneBuilder.CreateDebugButton(panel, "Button Oferta", "Oferta", 7, new Color(0.25f, 0.55f, 0.85f), out _);

            EditorUi.Wire(debugPanel,
                ("adModeButton", adMode),
                ("adModeLabel", adModeLabel),
                ("giveFertilizerButton", fertilizer),
                ("offerNowButton", offer));
        }
    }
}
