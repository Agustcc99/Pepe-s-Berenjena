using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace FarmIdle.EditorTools
{
    // Los logros (F-06): las plantillas (datos) y el botón "Logros" del HUD con su tarjeta de lista con scroll. Como los demás builders de
    // fase: BuildAll llama a CreateAssets (antes de las escenas) y a BuildHud (al armar el MainLevel). Los assets que ya existen no se
    // pisan; los nuevos entran al final de la lista del GameConfig.
    internal static class Phase11SceneBuilder
    {
        private const string AchievementsFolder = "Assets/ScriptableObjects/Achievements";
        private const float RowHeight = 230f;
        private const float RowPitch = 250f;

        private sealed class AchievementSpec
        {
            public string File, Id;
            public AchievementKind Kind;
            public int Target, Fertilizer;
            public float Minutes;
        }

        // En el orden de la tarjeta: por tipo y, dentro de cada uno, de menor a mayor. El premio son minutos de producción actual (con la
        // escala de las misiones); los más difíciles dan fertilizante (y, con el stock lleno, esos mismos minutos en monedas).
        // Metas y premios PROVISORIOS: se ajustan con datos reales (B-05).
        private static readonly AchievementSpec[] Achievements =
        {
            new AchievementSpec { File = "Achievement_Harvest1", Id = "ach_harvest_1", Kind = AchievementKind.Harvest, Target = 50, Minutes = 4f },
            new AchievementSpec { File = "Achievement_Harvest2", Id = "ach_harvest_2", Kind = AchievementKind.Harvest, Target = 500, Minutes = 8f },
            new AchievementSpec { File = "Achievement_Harvest3", Id = "ach_harvest_3", Kind = AchievementKind.Harvest, Target = 5000, Minutes = 20f, Fertilizer = 1 },            new AchievementSpec { File = "Achievement_Sell1", Id = "ach_sell_1", Kind = AchievementKind.Sell, Target = 100, Minutes = 5f },
            new AchievementSpec { File = "Achievement_Sell2", Id = "ach_sell_2", Kind = AchievementKind.Sell, Target = 2000, Minutes = 15f, Fertilizer = 1 },
            new AchievementSpec { File = "Achievement_Coins1", Id = "ach_coins_1", Kind = AchievementKind.CoinsEarned, Target = 10000, Minutes = 6f },
            new AchievementSpec { File = "Achievement_Coins2", Id = "ach_coins_2", Kind = AchievementKind.CoinsEarned, Target = 500000, Minutes = 20f, Fertilizer = 1 },
            new AchievementSpec { File = "Achievement_Level1", Id = "ach_level_1", Kind = AchievementKind.Level, Target = 5, Minutes = 6f },
            new AchievementSpec { File = "Achievement_Level2", Id = "ach_level_2", Kind = AchievementKind.Level, Target = 10, Minutes = 10f },
            new AchievementSpec { File = "Achievement_Level3", Id = "ach_level_3", Kind = AchievementKind.Level, Target = 20, Minutes = 20f, Fertilizer = 2 },
            new AchievementSpec { File = "Achievement_Fertilizer", Id = "ach_fertilizer", Kind = AchievementKind.FertilizerUsed, Target = 5, Minutes = 6f },
            new AchievementSpec { File = "Achievement_Zones", Id = "ach_zones", Kind = AchievementKind.ZonesOpened, Target = 3, Minutes = 10f },
            new AchievementSpec { File = "Achievement_Upgrades", Id = "ach_upgrades", Kind = AchievementKind.UpgradesBought, Target = 10, Minutes = 10f },
            new AchievementSpec { File = "Achievement_Videos", Id = "ach_videos", Kind = AchievementKind.VideosWatched, Target = 5, Minutes = 8f },
            new AchievementSpec { File = "Achievement_Turbo", Id = "ach_turbo", Kind = AchievementKind.TurboUsed, Target = 1, Minutes = 5f },
            new AchievementSpec { File = "Achievement_Missions", Id = "ach_missions", Kind = AchievementKind.MissionsClaimed, Target = 10, Minutes = 10f },
            new AchievementSpec { File = "Achievement_Season1", Id = "ach_season_1", Kind = AchievementKind.Seasons, Target = 1, Minutes = 25f, Fertilizer = 2 },
            new AchievementSpec { File = "Achievement_Season2", Id = "ach_season_2", Kind = AchievementKind.Seasons, Target = 3, Minutes = 40f, Fertilizer = 3 },
        };

        private static string AchievementPath(AchievementSpec spec) { return AchievementsFolder + "/" + spec.File + ".asset"; }

        // ---------- Assets ----------

        internal static void CreateAssets()
        {
            Phase1SceneBuilder.EnsureFolder(AchievementsFolder);

            foreach (AchievementSpec spec in Achievements) CreateAchievementData(spec);
            AssetDatabase.SaveAssets();

            var config = Phase2SceneBuilder.LoadRequired<GameConfig>(Phase2SceneBuilder.ConfigPath);
            var serialized = new SerializedObject(config);

            var assets = new List<Object>();
            foreach (AchievementSpec spec in Achievements) assets.Add(Phase2SceneBuilder.LoadRequired<AchievementData>(AchievementPath(spec)));
            Phase7SceneBuilder.AppendMissing(serialized, "achievements", assets);

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
        }

        private static void CreateAchievementData(AchievementSpec spec)
        {
            string path = AchievementPath(spec);
            if (AssetDatabase.LoadAssetAtPath<AchievementData>(path) != null) return;   // respeta lo que se haya ajustado a mano

            var achievement = ScriptableObject.CreateInstance<AchievementData>();
            var serialized = new SerializedObject(achievement);
            serialized.FindProperty("id").stringValue = spec.Id;
            serialized.FindProperty("kind").enumValueIndex = (int)spec.Kind;
            serialized.FindProperty("target").intValue = spec.Target;
            serialized.FindProperty("rewardMinutes").floatValue = spec.Minutes;
            serialized.FindProperty("rewardFertilizer").intValue = spec.Fertilizer;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            AssetDatabase.CreateAsset(achievement, path);
        }

        // ---------- HUD ----------

        // El botón, arriba a la derecha bajo el ícono de sonido, y la tarjeta (cuelga del Canvas para tapar toda la pantalla).
        internal static void BuildHud(RectTransform hud, Transform canvas)
        {
            RectTransform holder = EditorUi.CreateRect("AchievementHud", hud);
            EditorUi.Stretch(holder);

            Button open = EditorUi.CreateHudPill("AchievementButton", holder, "@achievement.button", out TextMeshProUGUI openLabel);
            EditorUi.Anchor((RectTransform)open.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-30f, -290f), new Vector2(250f, 84f));

            Image backdrop = EditorUi.CreatePanel("AchievementBackdrop", canvas, UiTheme.Backdrop, true);
            backdrop.sprite = null;
            backdrop.type = Image.Type.Simple;
            EditorUi.Stretch(backdrop.rectTransform);
            var backdropButton = backdrop.gameObject.AddComponent<Button>();
            backdropButton.targetGraphic = backdrop;
            backdropButton.transition = Selectable.Transition.None;

            Image card = EditorUi.CreateCard(backdrop.transform);   // recibe los toques: no cierra
            EditorUi.Anchor(card.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 1560f));

            TextMeshProUGUI title = EditorUi.CreateText("Title", card.transform, "@achievement.title", 72f, UiTheme.Title, TextAlignmentOptions.Center, FontStyles.Bold);
            EditorUi.Anchor(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -80f), new Vector2(600f, 100f));

            Button close = EditorUi.CreateButton("AchievementCloseButton", card.transform, "X", UiTheme.Danger, out TextMeshProUGUI closeLabel);
            closeLabel.fontSize = 56f;
            EditorUi.Anchor((RectTransform)close.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -24f), new Vector2(110f, 110f));

            // La lista: un visor con máscara (también recibe el arrastre) y, adentro, el contenido alto con una fila por logro.
            Image viewportImage = EditorUi.CreateImage("Viewport", card.transform, null, new Color(0f, 0f, 0f, 0f), true);
            viewportImage.gameObject.AddComponent<RectMask2D>();
            RectTransform viewport = viewportImage.rectTransform;
            EditorUi.Anchor(viewport, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -170f), new Vector2(840f, 1350f));

            RectTransform content = EditorUi.CreateRect("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = new Vector2(0f, Achievements.Length * RowPitch + 10f);

            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 60f;

            var rows = new AchievementPanelView.Row[Achievements.Length];
            for (int i = 0; i < rows.Length; i++) rows[i] = BuildRow(content, i);

            var view = holder.gameObject.AddComponent<AchievementPanelView>();
            EditorUi.Wire(view, ("openButton", open), ("openLabel", openLabel), ("panel", backdrop.gameObject), ("closeButton", close),
                ("backdropButton", backdropButton), ("scroll", scroll));
            WireRows(view, rows);

            backdrop.gameObject.SetActive(false);   // la tarjeta arranca cerrada
        }

        // Una fila: el texto del logro, la barra con su "N / meta", el premio y el botón de reclamar.
        private static AchievementPanelView.Row BuildRow(Transform content, int index)
        {
            Image root = EditorUi.CreatePanel("Row" + index, content, UiTheme.Row, false);
            EditorUi.Anchor(root.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -10f - index * RowPitch), new Vector2(820f, RowHeight));

            TextMeshProUGUI text = EditorUi.CreateText("Title", root.transform, "", 44f, Color.white, TextAlignmentOptions.Left, FontStyles.Bold);
            text.enableAutoSizing = true;
            text.fontSizeMin = 26f;
            text.fontSizeMax = 44f;
            EditorUi.Anchor(text.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(24f, -40f), new Vector2(770f, 60f));

            Image back = EditorUi.CreatePanel("BarBack", root.transform, UiTheme.BarBack, false);
            EditorUi.Anchor(back.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(24f, -100f), new Vector2(520f, 32f));

            Image fill = EditorUi.CreatePanel("Fill", back.transform, UiTheme.BarFill, false);
            RectTransform fillRect = fill.rectTransform;
            fillRect.anchorMin = new Vector2(0f, 0f);
            fillRect.anchorMax = new Vector2(1f, 1f);
            fillRect.pivot = new Vector2(0f, 0.5f);
            fillRect.offsetMin = new Vector2(3f, 3f);
            fillRect.offsetMax = new Vector2(-3f, -3f);

            TextMeshProUGUI progress = EditorUi.CreateText("Progress", root.transform, "", 34f, UiTheme.BodyText, TextAlignmentOptions.Left, FontStyles.Bold);
            progress.enableAutoSizing = true;
            progress.fontSizeMin = 22f;
            progress.fontSizeMax = 34f;
            EditorUi.Anchor(progress.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(560f, -100f), new Vector2(240f, 50f));

            TextMeshProUGUI reward = EditorUi.CreateText("Reward", root.transform, "", 36f, UiTheme.Reward, TextAlignmentOptions.Left, FontStyles.Bold);
            reward.enableAutoSizing = true;
            reward.fontSizeMin = 22f;
            reward.fontSizeMax = 36f;
            EditorUi.Anchor(reward.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0.5f), new Vector2(24f, 44f), new Vector2(500f, 52f));

            Button claim = EditorUi.CreateButton("ClaimButton", root.transform, "", UiTheme.Primary, out TextMeshProUGUI claimLabel);
            claimLabel.fontSize = 38f;
            claimLabel.color = UiTheme.OnPrimary;
            EditorUi.Anchor((RectTransform)claim.transform, new Vector2(1f, 0f), new Vector2(1f, 0.5f), new Vector2(-24f, 52f), new Vector2(250f, 76f));
            claim.gameObject.SetActive(false);

            return new AchievementPanelView.Row { root = root.gameObject, title = text, progress = progress, reward = reward, fill = fillRect, claim = claim, claimLabel = claimLabel };
        }

        private static void WireRows(AchievementPanelView view, AchievementPanelView.Row[] rows)
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
