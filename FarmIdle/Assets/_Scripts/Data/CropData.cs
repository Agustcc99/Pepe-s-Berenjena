using UnityEngine;

namespace FarmIdle
{
    // Un cultivo: cuánto tarda en crecer, cómo se ve la planta, cómo se ve el producto cosechado y cuánto paga.
    // Datos de solo lectura.
    [CreateAssetMenu(fileName = "Crop_Nuevo", menuName = "FarmIdle/Datos/Cultivo")]
    public sealed class CropData : GameDataAsset
    {
        [SerializeField] private string displayName = "Cultivo";
        [SerializeField] private Sprite icon;

        [SerializeField, Tooltip("Planta que crece en la parcela (con un PlantView). Sale del PoolManager: el campo hace el Prewarm al cargar el nivel.")]
        private GameObject plantPrefab;

        [SerializeField, Tooltip("Producto que se apila en la espalda del granjero al cosechar (modelo chico y sin colliders). Sale del PoolManager: BackpackStackView hace el Prewarm al cargar el nivel.")]
        private GameObject productPrefab;

        [SerializeField, Min(0.5f), Tooltip("Segundos desde que se siembra hasta que se puede cosechar.")]
        private float growSeconds = 8f;

        [SerializeField, Min(0), Tooltip("Monedas que paga una unidad al venderla (precio base, antes de mejoras y buffs).")]
        private int sellPrice = 2;

        public string DisplayName => LocalizedName(displayName);
        public Sprite Icon => icon;
        public GameObject PlantPrefab => plantPrefab;
        public GameObject ProductPrefab => productPrefab;
        public float GrowSeconds => growSeconds;
        public int SellPrice => sellPrice;
    }
}
