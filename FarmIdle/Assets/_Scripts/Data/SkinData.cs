using UnityEngine;

namespace FarmIdle
{
    // Sombrero que trae una skin. Straw es el de paja de fábrica (sin skin); Flower lo conserva y le suma una flor.
    public enum SkinHat
    {
        Straw,
        Cap,       // gorra con visera
        Beanie,    // gorro de lana con pompón
        Sprout,    // un brote sobre la cabeza (sin sombrero)
        TopHat,    // galera
        Flower,    // sombrero de paja con una flor
        Crown      // corona con una gema
    }

    // Diseño que trae una skin en el cuerpo, además del color.
    public enum SkinPattern
    {
        None,
        Stripe,    // franja a la cintura
        Scarf,     // bufanda con una punta colgando
        Overalls,  // pechera y tiradores de jardinero
        Dots       // lunares
    }

    // F-17 (tanda 3): un aspecto del jugador (skin) cosmético: color, sombrero y diseño. Datos de solo lectura; cuáles tiene el
    // jugador y cuál lleva puesto vive en SkinManager. No da ninguna ventaja. El de fábrica (naranja, sombrero de paja) no es un
    // asset: es "sin skin".
    [CreateAssetMenu(fileName = "Skin_Nueva", menuName = "FarmIdle/Datos/Skin")]
    public sealed class SkinData : GameDataAsset
    {
        [SerializeField] private string displayName = "Skin";

        [SerializeField, Min(1), Tooltip("Monedas que cuesta en el pad de skins.")]
        private int cost = 60000;

        [SerializeField] private Color bodyColor = Color.white;

        [SerializeField] private Color noseColor = Color.white;

        [SerializeField, Tooltip("Sombrero que lleva esta skin.")]
        private SkinHat hat = SkinHat.Straw;

        [SerializeField, Tooltip("Diseño del cuerpo (franja, bufanda, jardinero, lunares).")]
        private SkinPattern pattern = SkinPattern.None;

        [SerializeField, Tooltip("Color principal del sombrero.")]
        private Color hatColor = Color.white;

        [SerializeField, Tooltip("Color del detalle: pompón, cinta, flor, gema y el diseño del cuerpo.")]
        private Color accentColor = Color.white;

        public string DisplayName => LocalizedName(displayName);
        public int Cost => cost;
        public Color BodyColor => bodyColor;
        public Color NoseColor => noseColor;
        public SkinHat Hat => hat;
        public SkinPattern Pattern => pattern;
        public Color HatColor => hatColor;
        public Color AccentColor => accentColor;
    }
}
