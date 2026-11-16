using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace FarmIdle.EditorTools
{
    // HUD de la Fase 9 (AR-05): el Ã­cono de sonido con su tarjeta de ajustes, la tarjeta de "Bienvenido de vuelta" (progreso offline),
    // el contador de temporada, el destello de pantalla y la flecha del borde de la guÃ­a. Los sprites los crea Phase9SceneBuilder.CreateAssets.
    internal static class HudBuilder
    {
        // El ícono va arriba a la derecha, debajo del contador de monedas (los botones de debug se corrieron más abajo). La tarjeta y su
        // fondo cuelgan del Canvas para tapar toda la pantalla; la flecha del borde vive en el HUD (el área segura).
        internal static void BuildHud(RectTransform hud, Transform canvas)
        {
            Sprite soundOn = Phase2SceneBuilder.LoadRequired<Sprite>(Phase9SceneBuilder.SoundOnPath);
            Sprite soundOff = Phase2SceneBuilder.LoadRequired<Sprite>(Phase9SceneBuilder.SoundOffPath);
            Sprite arrowSprite = Phase2SceneBuilder.LoadRequired<Sprite>(Phase9SceneBuilder.ArrowSpritePath);

            // Ícono de sonido: un círculo oscuro con el parlante.
            Image iconBack = EditorUi.CreatePanel("SoundButton", hud, new Color(0.05f, 0.07f, 0.06f, 0.65f), true);
            EditorUi.Anchor(iconBack.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-30f, -160f), new Vector2(110f, 110f));
            var openButton = iconBack.gameObject.AddComponent<Button>();
            openButton.targetGraphic = iconBack;

            Image icon = EditorUi.CreateImage("Icon", iconBack.transform, soundOn, Color.white, false);
            EditorUi.Anchor(icon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(84f, 84f));

            // La tarjeta de ajustes.
            Image backdrop = EditorUi.CreatePanel("SettingsBackdrop", canvas, new Color(0.02f, 0.05f, 0.03f, 0.70f), true);
            backdrop.sprite = null;
            backdrop.type = Image.Type.Simple;
            EditorUi.Stretch(backdrop.rectTransform);
            var backdropButton = backdrop.gameObject.AddComponent<Button>();
            backdropButton.targetGraphic = backdrop;
            backdropButton.transition = Selectable.Transition.None;

            Image card = EditorUi.CreateCard(backdrop.transform);   // recibe los toques: no cierra
            EditorUi.Anchor(card.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(860f, 1600f));

            TextMeshProUGUI title = EditorUi.CreateText("Title", card.transform, "@settings.title", 72f, new Color(1f, 0.92f, 0.55f), TextAlignmentOptions.Center, FontStyles.Bold);
            EditorUi.Anchor(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -80f), new Vector2(600f, 100f));

            Button close = EditorUi.CreateButton("CloseButton", card.transform, "X", new Color(0.75f, 0.25f, 0.25f), out TextMeshProUGUI closeLabel);
            closeLabel.fontSize = 56f;
            EditorUi.Anchor((RectTransform)close.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -24f), new Vector2(110f, 110f));

            Slider music = AddSliderRow(card.transform, "@settings.music", 0, out _);
            Slider sfx = AddSliderRow(card.transform, "@settings.sfx", 1, out _);

            // Los botones van apilados bajo los deslizadores (y medido desde el borde de arriba de la tarjeta).
            // Mute, vibración e idioma cambian de texto según el estado: los escribe SettingsPanelView (por eso arrancan vacíos y sin clave).
            Button mute = AddCardButton(card.transform, "MuteButton", "", new Color(0.30f, 0.50f, 0.78f), -530f, 110f, out TextMeshProUGUI muteLabel);
            Button vibration = AddCardButton(card.transform, "VibrationButton", "", new Color(0.30f, 0.50f, 0.78f), -650f, 110f, out TextMeshProUGUI vibrationLabel);
            Button language = AddCardButton(card.transform, "LanguageButton", "", new Color(0.30f, 0.50f, 0.78f), -770f, 110f, out TextMeshProUGUI languageLabel);
            Button textSize = AddCardButton(card.transform, "TextSizeButton", "", new Color(0.30f, 0.50f, 0.78f), -890f, 110f, out TextMeshProUGUI textSizeLabel);   // "Texto grande" (U-04)
            Button control = AddCardButton(card.transform, "ControlButton", "", new Color(0.30f, 0.50f, 0.78f), -1010f, 110f, out TextMeshProUGUI controlLabel);   // joystick flotante o fijo (U-11)
            Button credits = AddCardButton(card.transform, "CreditsButton", "@settings.credits", new Color(0.35f, 0.42f, 0.38f), -1130f, 100f, out _);

            // "Privacidad": solo se ve si el jugador está en una región que exige el consentimiento (lo decide el servicio de anuncios).
            Button privacy = AddCardButton(card.transform, "PrivacyButton", "@settings.privacy", new Color(0.35f, 0.42f, 0.38f), -1240f, 96f, out _);
            privacy.gameObject.SetActive(false);

            // La política de privacidad: oculta hasta que haya una dirección publicada (ROADMAP M-04; se carga en el campo privacyPolicyUrl).
            Button policy = AddCardButton(card.transform, "PrivacyPolicyButton", "@settings.privacy_policy", new Color(0.35f, 0.42f, 0.38f), -1344f, 96f, out _);
            policy.gameObject.SetActive(false);

            Button delete = AddCardButton(card.transform, "DeleteSaveButton", "@settings.delete", new Color(0.65f, 0.22f, 0.22f), -1470f, 110f, out _);

            // Créditos: un panel encima de la tarjeta.
            Image creditsPanel = EditorUi.CreatePanel("CreditsPanel", card.transform, new Color(0.08f, 0.13f, 0.09f, 0.99f), true);
            EditorUi.Stretch(creditsPanel.rectTransform);
            TextMeshProUGUI creditsBody = EditorUi.CreateText("Body", creditsPanel.transform, "@settings.credits_body", 46f, Color.white, TextAlignmentOptions.Center);
            creditsBody.textWrappingMode = TextWrappingModes.Normal;
            EditorUi.Anchor(creditsBody.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 80f), new Vector2(740f, 800f));
            Button creditsClose = AddCardButton(creditsPanel.transform, "CreditsCloseButton", "@settings.close", new Color(0.30f, 0.50f, 0.78f), -1330f, 110f, out _);
            creditsPanel.gameObject.SetActive(false);

            // Confirmación de "Borrar partida": nada se borra hasta tocar "Sí, borrar".
            Image confirmPanel = EditorUi.CreatePanel("DeleteConfirmPanel", card.transform, new Color(0.08f, 0.13f, 0.09f, 0.99f), true);
            EditorUi.Stretch(confirmPanel.rectTransform);
            TextMeshProUGUI confirmBody = EditorUi.CreateText("Body", confirmPanel.transform, "@settings.delete_confirm", 56f, Color.white, TextAlignmentOptions.Center);
            confirmBody.textWrappingMode = TextWrappingModes.Normal;
            EditorUi.Anchor(confirmBody.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 200f), new Vector2(740f, 300f));
            Button confirmYes = EditorUi.CreateButton("DeleteConfirmYesButton", confirmPanel.transform, "@settings.delete_yes", new Color(0.75f, 0.25f, 0.25f), out TextMeshProUGUI yesLabel);
            yesLabel.fontSize = 52f;
            EditorUi.Anchor((RectTransform)confirmYes.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -40f), new Vector2(560f, 120f));
            Button confirmNo = EditorUi.CreateButton("DeleteConfirmNoButton", confirmPanel.transform, "@settings.cancel", new Color(0.30f, 0.50f, 0.78f), out TextMeshProUGUI noLabel);
            noLabel.fontSize = 52f;
            EditorUi.Anchor((RectTransform)confirmNo.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -200f), new Vector2(560f, 120f));
            confirmPanel.gameObject.SetActive(false);

            var view = iconBack.gameObject.AddComponent<SettingsPanelView>();
            EditorUi.Wire(view,
                ("openButton", openButton),
                ("openIcon", icon),
                ("soundOnSprite", soundOn),
                ("soundOffSprite", soundOff),
                ("panel", backdrop.gameObject),
                ("musicSlider", music),
                ("sfxSlider", sfx),
                ("muteButton", mute),
                ("muteLabel", muteLabel),
                ("closeButton", close),
                ("backdropButton", backdropButton),
                ("privacyButton", privacy),
                ("vibrationButton", vibration),
                ("vibrationLabel", vibrationLabel),
                ("languageButton", language),
                ("languageLabel", languageLabel),
                ("textSizeButton", textSize),
                ("textSizeLabel", textSizeLabel),
                ("controlButton", control),
                ("controlLabel", controlLabel),
                ("creditsButton", credits),
                ("creditsPanel", creditsPanel.gameObject),
                ("creditsCloseButton", creditsClose),
                ("privacyPolicyButton", policy),
                ("deleteButton", delete),
                ("deleteConfirmPanel", confirmPanel.gameObject),
                ("deleteConfirmYesButton", confirmYes),
                ("deleteConfirmNoButton", confirmNo));

            // La flecha del borde de la pantalla, un poco más chica que el ícono, en dorado con borde oscuro.
            RectTransform arrowHolder = EditorUi.CreateRect("TutorialArrow", hud);
            EditorUi.Stretch(arrowHolder);
            Image arrow = EditorUi.CreateImage("Arrow", arrowHolder, arrowSprite, new Color(1f, 0.82f, 0.22f), false);
            EditorUi.Anchor(arrow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(120f, 120f));
            var outline = arrow.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.05f, 0.07f, 0.06f, 0.9f);
            outline.effectDistance = new Vector2(5f, -5f);
            arrow.gameObject.SetActive(false);

            // U-10: el cartel que dice qué hacer; acompaña a la flecha o se pone encima del lugar cuando está a la vista.
            Image hint = EditorUi.CreatePanel("GuideLabel", arrowHolder, new Color(0.05f, 0.07f, 0.06f, 0.78f), false);
            EditorUi.Anchor(hint.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(780f, 92f));
            TextMeshProUGUI hintText = EditorUi.CreateText("Text", hint.transform, "", 32f, new Color(1f, 0.92f, 0.55f), TextAlignmentOptions.Center, FontStyles.Bold);
            // Una sola línea. El ancho y el tamaño están pensados para que los carteles (hasta ~30 letras) entren SIN achicarse aun con
            // "Texto grande": cuando TextMeshPro achica un texto para que entre, lo deja un par de píxeles pasado y la prueba lo marca.
            hintText.enableAutoSizing = true;
            hintText.fontSizeMin = 18f;
            hintText.fontSizeMax = 32f;
            hintText.textWrappingMode = TextWrappingModes.NoWrap;
            hintText.raycastTarget = false;
            EditorUi.Stretch(hintText.rectTransform);
            hintText.rectTransform.offsetMin = new Vector2(16f, 8f);
            hintText.rectTransform.offsetMax = new Vector2(-16f, -8f);
            hint.gameObject.SetActive(false);

            var edge = arrowHolder.gameObject.AddComponent<TutorialEdgeArrow>();
            EditorUi.Wire(edge, ("area", hud), ("arrow", arrow.rectTransform), ("labelRoot", hint.rectTransform), ("label", hintText));

            // U-11: las cuatro flechas del joystick fijo, dentro del círculo y debajo de la palanca. Se ven solo en ese modo.
            FloatingJoystick joystick = canvas.GetComponentInChildren<FloatingJoystick>(true);
            Transform joystickBackground = joystick != null ? joystick.transform.Find("JoystickBackground") : null;
            if (joystickBackground == null) throw new System.InvalidOperationException("El Canvas no tiene el joystick (TouchArea/JoystickBackground).");

            RectTransform fixedArrows = EditorUi.CreateRect("FixedArrows", joystickBackground);
            EditorUi.Stretch(fixedArrows);
            fixedArrows.SetAsFirstSibling();
            for (int i = 0; i < 4; i++)
            {
                float degrees = i * 90f;   // 0 arriba, 90 izquierda, 180 abajo, 270 derecha (la flecha apunta a +Y sin rotar)
                Vector2 direction = new Vector2(-Mathf.Sin(degrees * Mathf.Deg2Rad), Mathf.Cos(degrees * Mathf.Deg2Rad));
                Image fixedArrow = EditorUi.CreateImage("Arrow" + i, fixedArrows, arrowSprite, new Color(1f, 1f, 1f, 0.75f), false);
                EditorUi.Anchor(fixedArrow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), direction * 86f, new Vector2(44f, 44f));
                fixedArrow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, degrees);
            }

            fixedArrows.gameObject.SetActive(false);
            EditorUi.Wire(joystick, ("fixedArrows", fixedArrows.gameObject));

            backdrop.gameObject.SetActive(false);   // la tarjeta arranca cerrada

            BuildSeasonHud(hud, canvas);
            BuildShopHud(hud, canvas);
            BuildOfflineCard(canvas);

            // El destello de los bordes (fertilizante, nivel, Turbo): a pantalla completa, detrás del HUD y encima del mundo.
            Image flash = EditorUi.CreateImage("ScreenFlash", canvas, Phase2SceneBuilder.LoadRequired<Sprite>(Phase9SceneBuilder.VignettePath), new Color(1f, 1f, 1f, 0f), false);
            EditorUi.Stretch(flash.rectTransform);
            flash.transform.SetSiblingIndex(hud.GetSiblingIndex());
            var flashView = flash.gameObject.AddComponent<ScreenFlashView>();
            EditorUi.Wire(flashView, ("image", flash));
        }

        // F-03: el botón "¡Nueva temporada!" (a la izquierda, bajo la barra de nivel; apagado hasta que no quede nada por comprar) y su
        // tarjeta de confirmación, que cuelga del Canvas para tapar toda la pantalla igual que la de ajustes.
        private static void BuildSeasonHud(RectTransform hud, Transform canvas)
        {
            RectTransform holder = EditorUi.CreateRect("SeasonHud", hud);
            EditorUi.Stretch(holder);

            Button open = EditorUi.CreateButton("SeasonButton", holder, "@season.button", new Color(0.93f, 0.68f, 0.12f), out TextMeshProUGUI openLabel);
            openLabel.fontSize = 44f;
            openLabel.color = new Color(0.20f, 0.12f, 0.02f);
            EditorUi.Anchor((RectTransform)open.transform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(20f, -420f), new Vector2(560f, 110f));
            open.gameObject.SetActive(false);

            Image backdrop = EditorUi.CreatePanel("SeasonBackdrop", canvas, new Color(0.02f, 0.05f, 0.03f, 0.70f), true);
            backdrop.sprite = null;
            backdrop.type = Image.Type.Simple;
            EditorUi.Stretch(backdrop.rectTransform);

            Image card = EditorUi.CreateCard(backdrop.transform);
            EditorUi.Anchor(card.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(860f, 900f));

            TextMeshProUGUI title = EditorUi.CreateText("Title", card.transform, "@season.title", 72f, new Color(1f, 0.92f, 0.55f), TextAlignmentOptions.Center, FontStyles.Bold);
            EditorUi.Anchor(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -90f), new Vector2(760f, 100f));

            TextMeshProUGUI body = EditorUi.CreateText("Body", card.transform, "", 46f, Color.white, TextAlignmentOptions.Center);
            body.textWrappingMode = TextWrappingModes.Normal;
            EditorUi.Anchor(body.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 90f), new Vector2(760f, 480f));

            Button yes = AddCardButton(card.transform, "SeasonYesButton", "@season.yes", new Color(0.93f, 0.68f, 0.12f), -660f, 120f, out TextMeshProUGUI yesLabel);
            yesLabel.color = new Color(0.20f, 0.12f, 0.02f);
            Button no = AddCardButton(card.transform, "SeasonNoButton", "@season.no", new Color(0.30f, 0.50f, 0.78f), -800f, 110f, out _);

            var view = holder.gameObject.AddComponent<SeasonPanelView>();
            EditorUi.Wire(view,
                ("openButton", open),
                ("panel", backdrop.gameObject),
                ("bodyLabel", body),
                ("yesButton", yes),
                ("noButton", no));

            backdrop.gameObject.SetActive(false);   // la tarjeta arranca cerrada
        }

        // F-11: el botón "Semillas" (bajo el de temporada; apagado hasta la primera temporada) y la tarjeta de la tienda, con una fila
        // por mejora del catálogo del GameConfig.
        private static void BuildShopHud(RectTransform hud, Transform canvas)
        {
            var config = Phase2SceneBuilder.LoadRequired<GameConfig>(Phase2SceneBuilder.ConfigPath);
            int rows = config.PrestigeShop.Count;

            RectTransform holder = EditorUi.CreateRect("ShopHud", hud);
            EditorUi.Stretch(holder);

            Button open = EditorUi.CreateButton("ShopButton", holder, "@shop.button", new Color(0.55f, 0.78f, 0.25f), out TextMeshProUGUI openLabel);
            openLabel.fontSize = 44f;
            openLabel.color = new Color(0.10f, 0.20f, 0.03f);
            EditorUi.Anchor((RectTransform)open.transform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(20f, -545f), new Vector2(560f, 110f));
            open.gameObject.SetActive(false);

            Image backdrop = EditorUi.CreatePanel("ShopBackdrop", canvas, new Color(0.02f, 0.05f, 0.03f, 0.70f), true);
            backdrop.sprite = null;
            backdrop.type = Image.Type.Simple;
            EditorUi.Stretch(backdrop.rectTransform);

            float cardHeight = 330f + rows * 200f + 190f;
            Image card = EditorUi.CreateCard(backdrop.transform);
            EditorUi.Anchor(card.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(920f, cardHeight));

            TextMeshProUGUI title = EditorUi.CreateText("Title", card.transform, "@shop.title", 68f, new Color(1f, 0.92f, 0.55f), TextAlignmentOptions.Center, FontStyles.Bold);
            EditorUi.Anchor(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -80f), new Vector2(820f, 100f));

            TextMeshProUGUI seeds = EditorUi.CreateText("Seeds", card.transform, "", 48f, Color.white, TextAlignmentOptions.Center);
            EditorUi.Anchor(seeds.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -180f), new Vector2(820f, 70f));

            var buttons = new System.Collections.Generic.List<UnityEngine.Object>();
            var labels = new System.Collections.Generic.List<UnityEngine.Object>();
            for (int i = 0; i < rows; i++)
            {
                Button row = EditorUi.CreateButton("ShopRow" + i, card.transform, "", new Color(0.30f, 0.50f, 0.78f), out TextMeshProUGUI label);
                label.fontSize = 34f;
                label.textWrappingMode = TextWrappingModes.Normal;
                EditorUi.Anchor((RectTransform)row.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -330f - i * 200f), new Vector2(820f, 185f));
                buttons.Add(row);
                labels.Add(label);
            }

            Button close = AddCardButton(card.transform, "ShopCloseButton", "@shop.close", new Color(0.93f, 0.68f, 0.12f), -(330f + rows * 200f + 20f), 110f, out TextMeshProUGUI closeLabel);
            closeLabel.color = new Color(0.20f, 0.12f, 0.02f);

            var view = holder.gameObject.AddComponent<PrestigeShopView>();
            EditorUi.Wire(view, ("openButton", open), ("panel", backdrop.gameObject), ("seedsLabel", seeds), ("closeButton", close));
            EditorUi.WireArray(view, "rowButtons", buttons);
            EditorUi.WireArray(view, "rowLabels", labels);

            backdrop.gameObject.SetActive(false);   // la tarjeta arranca cerrada
        }

        // B-01: la tarjeta de bienvenida del progreso offline ("Reclamar" y "Duplicar (video)"). Cuelga del Canvas y arranca apagada; la
        // prende OfflineRewardView cuando hay monedas pendientes.
        private static void BuildOfflineCard(Transform canvas)
        {
            var config = Phase2SceneBuilder.LoadRequired<GameConfig>(Phase2SceneBuilder.ConfigPath);

            // La vista vive en un contenedor siempre activo: si viviera en el fondo, al apagarlo dejaría de escuchar.
            RectTransform holder = EditorUi.CreateRect("OfflineCard", canvas);
            EditorUi.Stretch(holder);

            Image backdrop = EditorUi.CreatePanel("OfflineBackdrop", holder, new Color(0.02f, 0.05f, 0.03f, 0.70f), true);
            backdrop.sprite = null;
            backdrop.type = Image.Type.Simple;
            EditorUi.Stretch(backdrop.rectTransform);

            Image card = EditorUi.CreateCard(backdrop.transform);
            EditorUi.Anchor(card.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(860f, 840f));

            TextMeshProUGUI title = EditorUi.CreateText("Title", card.transform, "@offline.title", 72f, new Color(1f, 0.92f, 0.55f), TextAlignmentOptions.Center, FontStyles.Bold);
            EditorUi.Anchor(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -90f), new Vector2(760f, 100f));

            TextMeshProUGUI body = EditorUi.CreateText("Body", card.transform, "", 52f, Color.white, TextAlignmentOptions.Center);
            body.textWrappingMode = TextWrappingModes.Normal;
            EditorUi.Anchor(body.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 90f), new Vector2(760f, 360f));

            Button claim = AddCardButton(card.transform, "OfflineClaimButton", "@offline.claim", new Color(0.35f, 0.70f, 0.30f), -560f, 120f, out _);
            Button doubleIt = AddCardButton(card.transform, "OfflineDoubleButton", "@offline.double", new Color(0.93f, 0.68f, 0.12f), -700f, 120f, out TextMeshProUGUI doubleLabel);
            doubleLabel.color = new Color(0.20f, 0.12f, 0.02f);

            var view = holder.gameObject.AddComponent<OfflineRewardView>();
            EditorUi.Wire(view,
                ("reward", config.FirstRewardOfKind(RewardKind.OfflineDouble)),
                ("panel", backdrop.gameObject),
                ("bodyLabel", body),
                ("claimButton", claim),
                ("doubleButton", doubleIt));

            backdrop.gameObject.SetActive(false);   // la tarjeta arranca cerrada
        }

        // Un botón de la tarjeta, centrado, a `y` unidades bajo el borde de arriba.
        private static Button AddCardButton(Transform parent, string name, string text, Color color, float y, float height, out TextMeshProUGUI label)
        {
            Button button = EditorUi.CreateButton(name, parent, text, color, out label);
            label.fontSize = 46f;
            EditorUi.Anchor((RectTransform)button.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, y), new Vector2(620f, height));
            return button;
        }

        // Una fila "Etiqueta + deslizador" de la tarjeta.
        private static Slider AddSliderRow(Transform card, string label, int row, out TextMeshProUGUI labelText)
        {
            float y = -230f - row * 150f;

            labelText = EditorUi.CreateText("Label" + row, card, label, 50f, Color.white, TextAlignmentOptions.Left, FontStyles.Bold);
            EditorUi.Anchor(labelText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(60f, y), new Vector2(240f, 70f));

            var go = new GameObject("Slider" + row, typeof(RectTransform), typeof(Slider));
            go.transform.SetParent(card, false);
            EditorUi.Anchor((RectTransform)go.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-70f, y), new Vector2(480f, 70f));

            Image back = EditorUi.CreatePanel("Background", go.transform, new Color(0.04f, 0.07f, 0.05f, 1f), true);
            back.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            back.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            back.rectTransform.sizeDelta = new Vector2(0f, 26f);

            RectTransform fillArea = EditorUi.CreateRect("Fill Area", go.transform);
            fillArea.anchorMin = new Vector2(0f, 0.5f);
            fillArea.anchorMax = new Vector2(1f, 0.5f);
            fillArea.sizeDelta = new Vector2(-50f, 26f);
            Image fill = EditorUi.CreatePanel("Fill", fillArea, new Color(0.45f, 0.85f, 0.30f, 1f), false);
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = new Vector2(0f, 1f);
            fill.rectTransform.sizeDelta = new Vector2(10f, 0f);

            RectTransform handleArea = EditorUi.CreateRect("Handle Slide Area", go.transform);
            EditorUi.Stretch(handleArea);
            handleArea.offsetMin = new Vector2(25f, 0f);
            handleArea.offsetMax = new Vector2(-25f, 0f);
            Image handle = EditorUi.CreatePanel("Handle", handleArea, Color.white, true);
            handle.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            handle.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            handle.rectTransform.sizeDelta = new Vector2(50f, 62f);

            var slider = go.GetComponent<Slider>();
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;
            slider.value = 1f;
            return slider;
        }
    }
}
