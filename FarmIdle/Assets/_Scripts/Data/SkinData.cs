using UnityEngine;

namespace FarmIdle
{
    // F-17 (tanda 3): un color de jugador (skin) cosmético. Datos de solo lectura; cuáles tiene el jugador y cuál lleva puesto vive
    // en SkinManager. No da ninguna ventaja. El de fábrica (naranja) no es un asset: es "sin skin".
    [CreateAssetMenu(fileName = "Skin_Nueva", menuName = "FarmIdle/Datos/Skin")]
    public sealed class SkinData : GameDataAsset
    {
        [SerializeField] private string displayName = "Skin";

        [SerializeField, Min(1), Tooltip("Monedas que cuesta en el pad de skins.")]
        private int cost = 60000;

        [SerializeField] private Color bodyColor = Color.white;

        [SerializeField] private Color noseColor = Color.white;

        public string DisplayName => LocalizedName(displayName);
        public int Cost => cost;
        public Color BodyColor => bodyColor;
        public Color NoseColor => noseColor;
    }
}
