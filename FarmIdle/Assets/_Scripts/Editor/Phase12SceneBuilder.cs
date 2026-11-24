using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace FarmIdle.EditorTools
{
    // El premio de login diario (F-07): el botón "Premio diario" del HUD y su tarjeta con los 7 días. Como los demás builders de fase,
    // BuildAll lo llama al armar el MainLevel (no hay assets de datos: los premios viven en el GameConfig).
    internal static class Phase12SceneBuilder
    {
        private const int Days = DailyRewardRules.CycleDays;

        // El botón, arriba a la derecha bajo el de "Logros", y la tarjeta (cuelga del Canvas para tapar toda la pantalla).
        internal static void BuildHud(RectTransform hud, Transform canvas)
        {
            RectTransform holder = EditorUi.CreateRect("DailyRewardHud", hud);
            EditorUi.Stretch(holder);

            Button open = EditorUi.CreateButton("DailyRewardButton", holder, "@daily.button", UiTheme.Primary, out TextMeshProUGUI openLabel);
            openLabel.fontSize = 36f;
            openLabel.color = UiTheme.OnPrimary;
            EditorUi.Anchor((RectTransform)open.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-30f, -400f), new Vector2(330f, 90f));

            Image backdrop = EditorUi.CreatePanel("DailyRewardBackdrop", canvas, UiTheme.Backdrop, true);
            backdrop.sprite = null;
            backdrop.type = Image.Type.Simple;
            EditorUi.Stretch(backdrop.rectTransform);
            var backdropButton = backdrop.gameObject.AddComponent<Button>();
            backdropButton.targetGraphic = backdrop;
            backdropButton.transition = Selectable.Transition.None;

            Image card = EditorUi.CreateCard(backdrop.transform);   // recibe los toques: no cierra
            EditorUi.Anchor(card.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 1180f));

            TextMeshProUGUI title = EditorUi.CreateText("Title", card.transform, "@daily.title", 72f, UiTheme.Title, TextAlignmentOptions.Center, FontStyles.Bold);
            EditorUi.Anchor(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -80f), new Vector2(600f, 100f));

            Button close = EditorUi.CreateButton("DailyRewardCloseButton", card.transform, "X", UiTheme.Danger, out TextMeshProUGUI closeLabel);
            closeLabel.fontSize = 56f;
            EditorUi.Anchor((RectTransform)close.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -24f), new Vector2(110f, 110f));

            // Los 7 casilleros: cuatro arriba y tres abajo, centrados.
            var slots = new DailyRewardView.Slot[Days];
            for (int i = 0; i < Days; i++)
            {
                int row = i / 4;
                int inRow = row == 0 ? 4 : Days - 4;
                float x = (i % 4 - (inRow - 1) * 0.5f) * 215f;
                slots[i] = BuildSlot(card.transform, i, new Vector2(x, -200f - row * 270f));
            }

            TextMeshProUGUI reward = EditorUi.CreateText("Reward", card.transform, "", 44f, UiTheme.Reward, TextAlignmentOptions.Center, FontStyles.Bold);
            reward.enableAutoSizing = true;
            reward.fontSizeMin = 26f;
            reward.fontSizeMax = 44f;
            EditorUi.Anchor(reward.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -800f), new Vector2(800f, 80f));

            TextMeshProUGUI streak = EditorUi.CreateText("Streak", card.transform, "", 40f, UiTheme.BodyText, TextAlignmentOptions.Center, FontStyles.Bold);
            EditorUi.Anchor(streak.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -890f), new Vector2(700f, 60f));

            Button claim = EditorUi.CreateButton("DailyClaimButton", card.transform, "", UiTheme.Primary, out TextMeshProUGUI claimLabel);
            claimLabel.fontSize = 48f;
            claimLabel.color = UiTheme.OnPrimary;
            EditorUi.Anchor((RectTransform)claim.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -1040f), new Vector2(520f, 110f));

            var view = holder.gameObject.AddComponent<DailyRewardView>();
            EditorUi.Wire(view, ("openButton", open), ("panel", backdrop.gameObject), ("closeButton", close), ("backdropButton", backdropButton),
                ("claimButton", claim), ("claimLabel", claimLabel), ("rewardText", reward), ("streakText", streak));
            WireSlots(view, slots);

            backdrop.gameObject.SetActive(false);   // la tarjeta arranca cerrada
        }

        // Un casillero: el fondo (cambia de color según esté reclamado, sea el de hoy o falte), "Día N" y el premio chico (monedas o fertilizante).
        private static DailyRewardView.Slot BuildSlot(Transform card, int index, Vector2 position)
        {
            Image back = EditorUi.CreatePanel("Day" + (index + 1), card, UiTheme.Row, false);
            EditorUi.Anchor(back.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), position, new Vector2(200f, 240f));

            TextMeshProUGUI day = EditorUi.CreateText("Day", back.transform, "", 44f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            day.enableAutoSizing = true;
            day.fontSizeMin = 26f;
            day.fontSizeMax = 44f;
            EditorUi.Anchor(day.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -60f), new Vector2(180f, 70f));

            TextMeshProUGUI sub = EditorUi.CreateText("Sub", back.transform, "", 34f, UiTheme.Reward, TextAlignmentOptions.Center, FontStyles.Bold);
            sub.enableAutoSizing = true;
            sub.fontSizeMin = 20f;
            sub.fontSizeMax = 34f;
            EditorUi.Anchor(sub.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -160f), new Vector2(180f, 70f));

            return new DailyRewardView.Slot { back = back, day = day, sub = sub };
        }

        private static void WireSlots(DailyRewardView view, DailyRewardView.Slot[] slots)
        {
            var serialized = new SerializedObject(view);
            SerializedProperty list = serialized.FindProperty("slots");
            list.arraySize = slots.Length;

            for (int i = 0; i < slots.Length; i++)
            {
                SerializedProperty slot = list.GetArrayElementAtIndex(i);
                slot.FindPropertyRelative("back").objectReferenceValue = slots[i].back;
                slot.FindPropertyRelative("day").objectReferenceValue = slots[i].day;
                slot.FindPropertyRelative("sub").objectReferenceValue = slots[i].sub;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
