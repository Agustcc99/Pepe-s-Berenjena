using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace FarmIdle.EditorTools
{
    // Funciones para armar UI (uGUI + TextMeshPro) desde los builders de escenas.
    internal static class EditorUi
    {
        internal static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        internal static Image CreateImage(string name, Transform parent, Sprite sprite, Color color, bool raycastTarget)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);

            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = raycastTarget;
            return image;
        }

        // Rectángulo redondeado (sprite 9-slice propio, ver UiTheme.RoundedPath) que se estira sin deformar las esquinas.
        // Los velos de pantalla completa usan el sprite recto de Unity: con esquinas redondas se verían huecos en los bordes.
        internal static Image CreatePanel(string name, Transform parent, Color color, bool raycastTarget)
        {
            bool fullScreen = name.EndsWith("Backdrop", System.StringComparison.Ordinal) || name == "AdOverlay" || name == "Menu";
            Sprite sprite = fullScreen
                ? AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd")
                : UiTheme.RoundedSprite();
            Image image = CreateImage(name, parent, sprite, color, raycastTarget);
            image.type = Image.Type.Sliced;
            return image;
        }

        // La tarjeta de un panel modal (ajustes, misiones, logros...): mismo color en todos y "salta" al abrirse (PopInOnEnable).
        internal static Image CreateCard(Transform backdrop)
        {
            Image card = CreatePanel("Card", backdrop, UiTheme.Card, true);
            card.gameObject.AddComponent<CanvasGroup>();   // lo usa PopInOnEnable para el fundido
            card.gameObject.AddComponent<PopInOnEnable>();

            // Borde dorado fino y sombra suave: la tarjeta se despega del fondo (efectos de vértices, sin draw calls extra).
            var border = card.gameObject.AddComponent<Outline>();
            border.effectColor = UiTheme.CardBorder;
            border.effectDistance = new Vector2(4f, -4f);
            var drop = card.gameObject.AddComponent<Shadow>();
            drop.effectColor = UiTheme.Shadow;
            drop.effectDistance = new Vector2(0f, -14f);
            return card;
        }

        // Le pone a un objeto con un TMP_Text (UI o 3D) la clave de la tabla de textos: el texto sigue al idioma activo.
        internal static void AddLocalizedKey(GameObject go, string key)
        {
            var localized = go.AddComponent<LocalizedText>();
            var serialized = new SerializedObject(localized);
            serialized.FindProperty("key").stringValue = key;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        // Los textos no deben capturar toques (Raycast Target desactivado): si no, taparían el joystick.
        internal static TextMeshProUGUI CreateText(string name, Transform parent, string text, float fontSize, Color color,
            TextAlignmentOptions alignment, FontStyles style = FontStyles.Normal)
        {
            TMP_FontAsset font = TMP_Settings.defaultFontAsset;
            if (font == null)
            {
                throw new System.InvalidOperationException("No hay fuente por defecto de TextMeshPro. Ejecutá FarmIdle > Fase 2 > Importar recursos de TextMeshPro.");
            }

            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);

            // "@clave": un texto fijo que sale de la tabla de textos (ROADMAP U-03). Se arma en español y LocalizedText lo cambia al idioma activo.
            string key = text != null && text.StartsWith("@") ? text.Substring(1) : null;
            if (key != null) AddLocalizedKey(go, key);

            var label = go.GetComponent<TextMeshProUGUI>();
            label.font = font;
            label.text = key != null ? Loc.Get(key, GameLanguage.Spanish) : text;
            label.fontSize = fontSize;
            label.color = color;
            label.alignment = alignment;
            label.fontStyle = style;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Overflow;
            label.raycastTarget = false;
            return label;
        }

        internal static Button CreateButton(string name, Transform parent, string label, Color color, out TextMeshProUGUI labelText)
        {
            Image background = CreatePanel(name, parent, color, true);

            var button = background.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            background.gameObject.AddComponent<ButtonPressFeedback>();   // se hunde y rebota al tocarlo

            // Relieve: sombra inferior marcada que hace que el botón parezca una pieza que se puede apretar.
            var lip = background.gameObject.AddComponent<Shadow>();
            lip.effectColor = new Color(0f, 0f, 0f, 0.38f);
            lip.effectDistance = new Vector2(0f, -8f);

            labelText = CreateText("Label", background.transform, label, 40f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            Stretch(labelText.rectTransform);
            return button;
        }

        // Botón del HUD (Misiones, Logros): píldora oscura translúcida con letras doradas, igual que la barra de nivel y la tarjeta de
        // pedidos, en vez de un rectángulo azul que desentonaba con el resto.
        internal static Button CreateHudPill(string name, Transform parent, string label, out TextMeshProUGUI labelText)
        {
            Button button = CreateButton(name, parent, label, UiTheme.Pill, out labelText);
            labelText.color = UiTheme.Gold;
            labelText.enableAutoSizing = true;
            labelText.fontSizeMin = 24f;
            labelText.fontSizeMax = 40f;
            return button;
        }

        internal static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        // Ancla el rect a un punto (sin estirarlo) y le da posición y tamaño fijos, en unidades de Canvas.
        internal static void Anchor(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        // Asigna referencias a campos [SerializeField] privados (lo mismo que arrastrar en el Inspector).
        // Falla si el campo no existe o si la referencia es nula: un cable suelto en la escena es un error del builder.
        internal static void Wire(Object target, params (string field, Object value)[] references)
        {
            var serialized = new SerializedObject(target);

            foreach ((string field, Object value) in references)
            {
                SerializedProperty property = serialized.FindProperty(field);
                if (property == null)
                {
                    throw new System.InvalidOperationException("No existe el campo '" + field + "' en " + target.GetType().Name + ".");
                }

                if (value == null)
                {
                    throw new System.InvalidOperationException("La referencia para '" + field + "' en " + target.GetType().Name + " es nula.");
                }

                property.objectReferenceValue = value;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        // Lo mismo para un campo que es una lista (array) de referencias. Falla si algún elemento es nulo.
        internal static void WireArray(Object target, string field, IReadOnlyList<Object> values)
        {
            var serialized = new SerializedObject(target);

            SerializedProperty property = serialized.FindProperty(field);
            if (property == null || !property.isArray)
            {
                throw new System.InvalidOperationException("No existe el campo de lista '" + field + "' en " + target.GetType().Name + ".");
            }

            property.arraySize = values.Count;
            for (int i = 0; i < values.Count; i++)
            {
                if (values[i] == null)
                {
                    throw new System.InvalidOperationException("El elemento " + i + " de '" + field + "' en " + target.GetType().Name + " es nulo.");
                }

                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
