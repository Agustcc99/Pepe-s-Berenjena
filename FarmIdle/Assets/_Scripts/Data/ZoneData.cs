using UnityEngine;

namespace FarmIdle
{
    // Una zona que arranca bloqueada y se desbloquea con monedas: su campo de parcelas aparece al abrirse. Los datos son de
    // solo lectura; qué zonas están abiertas vive en el progreso (PlayerProgress), que guarda este asset por Id.
    // La escena decide dónde está la zona y qué cultivos tiene (cada ZoneController apunta a su ZoneData).
    [CreateAssetMenu(fileName = "Zone_Nueva", menuName = "FarmIdle/Datos/Zona")]
    public sealed class ZoneData : GameDataAsset
    {
        [SerializeField] private string displayName = "Zona";

        [SerializeField, Min(1), Tooltip("Monedas que cuesta desbloquear la zona.")]
        private long unlockCost = 300;

        [SerializeField, Min(0), Tooltip("F-24: desde qué temporada aparece esta granja para comprarla (0 = desde el principio). Antes no se ve, ni su portón, ni cuenta para cerrar la temporada.")]
        private int requiredSeason;

        public string DisplayName => LocalizedName(displayName);
        public long UnlockCost => unlockCost;
        public int RequiredSeason => requiredSeason;
    }
}
