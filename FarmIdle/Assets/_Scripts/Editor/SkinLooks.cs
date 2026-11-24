using UnityEditor;
using UnityEngine;

namespace FarmIdle.EditorTools
{
    // IN-04: lo que cada skin trae además del color: sombrero, diseño en el cuerpo y los colores de ambos. Los assets de las skins los
    // crea DecorationBuilder (con el precio y los colores del cuerpo); esto completa los campos nuevos, también en los que ya existían.
    internal static class SkinLooks
    {
        private static readonly (string id, SkinHat hat, SkinPattern pattern, Color hatColor, Color accent)[] Looks =
        {
            ("skin_tomato", SkinHat.Cap, SkinPattern.Stripe, new Color(0.62f, 0.10f, 0.10f), new Color(1f, 0.92f, 0.55f)),
            ("skin_sky", SkinHat.Beanie, SkinPattern.Scarf, new Color(0.96f, 0.96f, 1f), new Color(0.92f, 0.28f, 0.28f)),
            ("skin_leaf", SkinHat.Sprout, SkinPattern.Overalls, new Color(0.22f, 0.58f, 0.18f), new Color(0.55f, 0.36f, 0.16f)),
            ("skin_grape", SkinHat.TopHat, SkinPattern.Dots, new Color(0.18f, 0.10f, 0.28f), new Color(0.98f, 0.82f, 0.25f)),
            ("skin_rose", SkinHat.Flower, SkinPattern.Scarf, new Color(1f, 1f, 1f), new Color(1f, 0.85f, 0.25f)),
            ("skin_gold", SkinHat.Crown, SkinPattern.Stripe, new Color(1f, 0.84f, 0.28f), new Color(0.82f, 0.14f, 0.20f)),
        };

        internal static void Apply(string dataFolder)
        {
            foreach ((string id, SkinHat hat, SkinPattern pattern, Color hatColor, Color accent) look in Looks)
            {
                var data = AssetDatabase.LoadAssetAtPath<SkinData>(dataFolder + "/" + look.id + ".asset");
                if (data == null) continue;

                var serialized = new SerializedObject(data);
                serialized.FindProperty("hat").enumValueIndex = (int)look.hat;
                serialized.FindProperty("pattern").enumValueIndex = (int)look.pattern;
                serialized.FindProperty("hatColor").colorValue = look.hatColor;
                serialized.FindProperty("accentColor").colorValue = look.accent;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(data);
            }
        }
    }
}
