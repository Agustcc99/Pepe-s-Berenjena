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

        public string DisplayName => LocalizedName(displayName);
        public int Cost => cost;
    }
}
