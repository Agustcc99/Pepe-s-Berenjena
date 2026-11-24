using UnityEngine;

namespace FarmIdle
{
    // F-17 (tanda 1): una decoración cosmética de la granja. Datos de solo lectura; qué decoraciones tiene el jugador vive en
    // DecorationManager (se guarda cuántas, porque se compran en el orden de la lista del GameConfig). No da ninguna ventaja.
    [CreateAssetMenu(fileName = "Decoration_Nueva", menuName = "FarmIdle/Datos/Decoración")]
    public sealed class DecorationData : GameDataAsset
    {
        [SerializeField] private string displayName = "Decoración";

        [SerializeField, Min(1), Tooltip("Monedas que cuesta en el pad de decoraciones.")]
        private int cost = 25000;

        [SerializeField, Range(0f, 0.2f), Tooltip("Encanto: cuánto sube el precio de venta mientras la tengas (0,02 = +2 %). Se suma con las demás.")]
        private float charm = 0.02f;

        public float Charm => charm;
        public string DisplayName => LocalizedName(displayName);
        public int Cost => cost;
    }
}
