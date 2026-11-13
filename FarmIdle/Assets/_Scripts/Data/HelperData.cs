using UnityEngine;

namespace FarmIdle
{
    // F-17 (tanda 2): un nivel de ayudantes automáticos. Datos de solo lectura; el nivel que tiene el jugador vive en HelperManager.
    // Los ayudantes cosechan y venden solos: suman monedas por segundo, como una fracción de lo que producen las parcelas abiertas.
    // Cada asset es un NIVEL (se compran en el orden de la lista del GameConfig) y 'share' es el efecto TOTAL de ese nivel.
    [CreateAssetMenu(fileName = "Helper_Nuevo", menuName = "FarmIdle/Datos/Ayudante")]
    public sealed class HelperData : GameDataAsset
    {
        [SerializeField] private string displayName = "Ayudante";

        [SerializeField, Min(1), Tooltip("Monedas que cuesta este nivel en el pad de ayudantes.")]
        private int cost = 100000;

        [SerializeField, Range(0.01f, 1f), Tooltip("Efecto TOTAL del nivel: fracción de la producción de las parcelas (OfflineManager.ProductionPerSecond) que se suma sola en monedas. 0,1 = +10 %.")]
        private float share = 0.1f;

        public string DisplayName => LocalizedName(displayName);
        public int Cost => cost;
        public float Share => share;
    }
}
