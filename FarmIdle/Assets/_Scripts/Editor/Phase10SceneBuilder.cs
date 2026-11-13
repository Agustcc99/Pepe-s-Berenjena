using System;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace FarmIdle.EditorTools
{
    // Las misiones diarias (F-05): las plantillas (datos) y el botón "Misiones" del HUD con su tarjeta. Como los demás builders de
    // fase: BuildAll llama a CreateAssets (antes de las escenas) y a BuildHud (al armar el MainLevel). Los assets que ya existen no se
    // pisan; los nuevos entran al final de la lista del GameConfig.
    internal static class Phase10SceneBuilder
    {
        private const string MissionsFolder = "Assets/ScriptableObjects/Missions";
        private const int RowCount = 3;

        private sealed class MissionSpec
        {
            public string File, Id;
            public MissionKind Kind;
            public int Target, MinLevel, Fertilizer;
            public float Minutes;
        }

        // El premio en monedas son minutos de producción actual. Meta y premio crecen juntos; las difíciles dan fertilizante (y, con el
        // stock lleno, esos mismos minutos en monedas). Con el nivel 1 solo pueden salir tres, de tipos distintos: cosechar, vender y comprar.
        private static readonly MissionSpec[] Missions =
        {
            new MissionSpec { File = "Mission_HarvestSmall", Id = "mission_harvest_small", Kind = MissionKind.Harvest, Target = 25, MinLevel = 1, Minutes = 5f },
            new MissionSpec { File = "Mission_HarvestMedium", Id = "mission_harvest_medium", Kind = MissionKind.Harvest, Target = 60, MinLevel = 2, Minutes = 8f },
            new MissionSpec { File = "Mission_HarvestBig", Id = "mission_harvest_big", Kind = MissionKind.Harvest, Target = 120, MinLevel = 4, Minutes = 10f, Fertilizer = 1 },
            new MissionSpec { File = "Mission_SellSmall", Id = "mission_sell_small", Kind = MissionKind.Sell, Target = 30, MinLevel = 1, Minutes = 5f },
            new MissionSpec { File = "Mission_SellBig", Id = "mission_sell_big", Kind = MissionKind.Sell, Target = 120, MinLevel = 4, Minutes = 12f },
            new MissionSpec { File = "Mission_Fertilizer", Id = "mission_fertilizer", Kind = MissionKind.UseFertilizer, Target = 1, MinLevel = 3, Minutes = 8f },
            new MissionSpec { File = "Mission_BuyOne", Id = "mission_buy_one", Kind = MissionKind.Buy, Target = 1, MinLevel = 1, Minutes = 6f },
            new MissionSpec { File = "Mission_BuyTwo", Id = "mission_buy_two", Kind = MissionKind.Buy, Target = 2, MinLevel = 5, Minutes = 10f, Fertilizer = 1 },
        };

        private static string MissionPath(MissionSpec spec) { return MissionsFolder + "/" + spec.File + ".asset"; }

        // ---------- Assets ----------

        internal static void CreateAssets()
        {
            Phase1SceneBuilder.EnsureFolder(MissionsFolder);

            foreach (MissionSpec spec in Missions) CreateMissionData(spec);
            AssetDatabase.SaveAssets();

            var config = Phase2SceneBuilder.LoadRequired<GameConfig>(Phase2SceneBuilder.ConfigPath);
            var serialized = new SerializedObject(config);

            var assets = new List<Object>();
            foreach (MissionSpec spec in Missions) assets.Add(Phase2SceneBuilder.LoadRequired<MissionData>(MissionPath(spec)));
            Phase7SceneBuilder.AppendMissing(serialized, "missions", assets);

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
        }

        private static void CreateMissionData(MissionSpec spec)
        {
            string path = MissionPath(spec);
            if (AssetDatabase.LoadAssetAtPath<MissionData>(path) != null) return;   // respeta lo que se haya ajustado a mano

            var mission = ScriptableObject.CreateInstance<MissionData>();
            var serialized = new SerializedObject(mission);
            serialized.FindProperty("id").stringValue = spec.Id;
            serialized.FindProperty("kind").enumValueIndex = (int)spec.Kind;
            serialized.FindProperty("target").intValue = spec.Target;
            serialized.FindProperty("minLevel").intValue = spec.MinLevel;
            serialized.FindProperty("rewardMinutes").floatValue = spec.Minutes;
            serialized.FindProperty("rewardFertilizer").intValue = spec.Fertilizer;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            AssetDatabase.CreateAsset(mission, path);
        }

        // ---------- HUD ----------

        // El botón, a la izquierda bajo el de "Nueva temporada", y la tarjeta (cuelga del Canvas para tapar toda la pantalla).
        internal static void BuildHud(RectTransform hud, Transform canvas)
        {
            RectTransform holder = EditorUi.CreateRect("MissionHud", hud);
            EditorUi.Stretch(holder);

            Button open = EditorUi.CreateButton("MissionButton", holder, "@mission.button", new Color(0.30f, 0.50f, 0.78f), out TextMeshProUGUI openLabel);
            openLabel.fontSize = 44f;
            EditorUi.Anchor((RectTransform)open.transform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(20f, -550f), new Vector2(460f, 100f));

            Image backdrop = EditorUi.CreatePanel("MissionBackdrop", canvas, new Color(0.02f, 0.05f, 0.03f, 0.70f), true);
            backdrop.sprite = null;
            backdrop.type = Image.Type.Simple;
            EditorUi.Stretch(backdrop.rectTransform);
            var backdropButton = backdrop.gameObject.AddComponent<Button>();
            backdropButton.targetGraphic = backdrop;
            backdropButton.transition = Selectable.Transition.None;

            Image card = EditorUi.CreateCard(backdrop.transform);   // recibe los toques: no cierra
            EditorUi.Anchor(card.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 1180f));

            TextMeshProUGUI title = EditorUi.CreateText("Title", card.transform, "@mission.title", 72f, new Color(1f, 0.92f, 0.55f), TextAlignmentOptions.Center, FontStyles.Bold);
            EditorUi.Anchor(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -80f), new Vector2(600f, 100f));

            Button close = EditorUi.CreateButton("MissionCloseButton", card.transform, "X", new Color(0.75f, 0.25f, 0.25f), out TextMeshProUGUI closeLabel);
            closeLabel.fontSize = 56f;
            EditorUi.Anchor((RectTransform)close.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -24f), new Vector2(110f, 110f));

            var rows = new MissionPanelView.Row[RowCount];
            for (int i = 0; i < RowCount; i++) rows[i] = BuildRow(card.transform, i);

            var view = holder.gameObject.AddComponent<MissionPanelView>();
            EditorUi.Wire(view, ("openButton", open), ("openLabel", openLabel), ("panel", backdrop.gameObject), ("closeButton", close), ("backdropButton", backdropButton));
            WireRows(view, rows);

            backdrop.gameObject.SetActive(false);   // la tarjeta arranca cerrada
        }

        // Una fila: el texto de la misión, la barra con su "N / meta", el premio y el botón de reclamar.
        private static MissionPanelView.Row BuildRow(Transform card, int index)
        {
            Image root = EditorUi.CreatePanel("Row" + index, card, new Color(0.08f, 0.13f, 0.09f, 0.95f), false);
            EditorUi.Anchor(root.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -190f - index * 300f), new Vector2(820f, 270f));

            TextMeshProUGUI text = EditorUi.CreateText("Title", root.transform, "", 46f, Color.white, TextAlignmentOptions.Left, FontStyles.Bold);
            text.enableAutoSizing = true;
            text.fontSizeMin = 28f;
            text.fontSizeMax = 46f;
            EditorUi.Anchor(text.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(24f, -44f), new Vector2(770f, 60f));

            Image back = EditorUi.CreatePanel("BarBack", root.transform, new Color(0.02f, 0.03f, 0.02f, 0.9f), false);
            EditorUi.Anchor(back.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(24f, -118f), new Vector2(560f, 34f));

            Image fill = EditorUi.CreatePanel("Fill", back.transform, new Color(0.45f, 0.85f, 0.30f, 1f), false);
            RectTransform fillRect = fill.rectTransform;
            fillRect.anchorMin = new Vector2(0f, 0f);
            fillRect.anchorMax = new Vector2(1f, 1f);
            fillRect.pivot = new Vector2(0f, 0.5f);
            fillRect.offsetMin = new Vector2(3f, 3f);
            fillRect.offsetMax = new Vector2(-3f, -3f);

            TextMeshProUGUI progress = EditorUi.CreateText("Progress", root.transform, "", 40f, new Color(0.9f, 0.95f, 0.85f), TextAlignmentOptions.Left, FontStyles.Bold);
            EditorUi.Anchor(progress.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(604f, -118f), new Vector2(200f, 50f));

            TextMeshProUGUI reward = EditorUi.CreateText("Reward", root.transform, "", 38f, new Color(1f, 0.85f, 0.3f), TextAlignmentOptions.Left, FontStyles.Bold);
            reward.enableAutoSizing = true;
            reward.fontSizeMin = 24f;
            reward.fontSizeMax = 38f;
            EditorUi.Anchor(reward.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0.5f), new Vector2(24f, 50f), new Vector2(480f, 56f));

            Button claim = EditorUi.CreateButton("ClaimButton", root.transform, "", new Color(0.93f, 0.68f, 0.12f), out TextMeshProUGUI claimLabel);
            claimLabel.fontSize = 40f;
            claimLabel.color = new Color(0.20f, 0.12f, 0.02f);
            EditorUi.Anchor((RectTransform)claim.transform, new Vector2(1f, 0f), new Vector2(1f, 0.5f), new Vector2(-24f, 56f), new Vector2(260f, 80f));
            claim.gameObject.SetActive(false);

            return new MissionPanelView.Row { root = root.gameObject, title = text, progress = progress, reward = reward, fill = fillRect, claim = claim, claimLabel = claimLabel };
        }

        private static void WireRows(MissionPanelView view, MissionPanelView.Row[] rows)
        {
            var serialized = new SerializedObject(view);
            SerializedProperty list = serialized.FindProperty("rows");
            list.arraySize = rows.Length;

            for (int i = 0; i < rows.Length; i++)
            {
                SerializedProperty row = list.GetArrayElementAtIndex(i);
                row.FindPropertyRelative("root").objectReferenceValue = rows[i].root;
                row.FindPropertyRelative("title").objectReferenceValue = rows[i].title;
                row.FindPropertyRelative("progress").objectReferenceValue = rows[i].progress;
                row.FindPropertyRelative("reward").objectReferenceValue = rows[i].reward;
                row.FindPropertyRelative("fill").objectReferenceValue = rows[i].fill;
                row.FindPropertyRelative("claim").objectReferenceValue = rows[i].claim;
                row.FindPropertyRelative("claimLabel").objectReferenceValue = rows[i].claimLabel;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
