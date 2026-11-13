using System.Globalization;
using UnityEngine;

namespace FarmIdle
{
    // Qué da una recompensa por ver un video.
    public enum RewardKind
    {
        Fertilizer,      // 'magnitude' unidades de fertilizante (al stock)
        MoveSpeedBuff,   // buff temporal: 'magnitude' multiplica la velocidad durante 'durationSeconds' (con el tope final del GameConfig)
        SellPriceBuff,   // buff temporal: 'magnitude' multiplica el precio de venta durante 'durationSeconds'
        OfflineDouble    // B-01: duplica lo ganado fuera (la tarjeta de bienvenida); no usa magnitud ni duración
    }

    // Una recompensa de anuncio (rewarded). Datos de solo lectura: lo que está activo (un buff con sus segundos restantes, el stock
    // de fertilizante) vive en los servicios y se guarda por Id. El Id es el contrato con la partida guardada y también el
    // "placement" por defecto que se le pasa al servicio de anuncios.
    [CreateAssetMenu(fileName = "Reward_Nueva", menuName = "FarmIdle/Datos/Recompensa")]
    public sealed class RewardData : GameDataAsset
    {
        [SerializeField] private string displayName = "Recompensa";

        [SerializeField, Tooltip("Nombre corto para el HUD y el panel de debug.")]
        private string shortLabel = "Rec";

        [SerializeField] private RewardKind kind;

        [SerializeField, Min(0f), Tooltip("Fertilizante: unidades que da. Buffs: multiplicador (3 = x3).")]
        private float magnitude = 1f;

        [SerializeField, Min(0f), Tooltip("Buffs: segundos de TIEMPO DE JUEGO que dura (no corre en pausa ni con un anuncio en pantalla). Fertilizante: no se usa.")]
        private float durationSeconds = 30f;

        [SerializeField, Min(0f), Tooltip("Segundos de tiempo de juego que hay que esperar para volver a pedir ESTA recompensa después de ganarla.")]
        private float cooldownSeconds = 120f;

        [SerializeField, Tooltip("Identificador del bloque de anuncio (placement) que se le pide al servicio. Vacío = el Id de la recompensa.")]
        private string placement = "";

        public string DisplayName => LocalizedName(displayName);
        public string ShortLabel => shortLabel;
        public RewardKind Kind => kind;
        public float Magnitude => magnitude;
        public float DurationSeconds => durationSeconds;
        public float CooldownSeconds => cooldownSeconds;
        public string Placement => string.IsNullOrWhiteSpace(placement) ? Id : placement;

        public bool IsBuff => kind == RewardKind.MoveSpeedBuff || kind == RewardKind.SellPriceBuff;

        // "x3" o "x1,5" ("x1.5" en inglés): el multiplicador de un buff.
        public string DescribeMultiplier()
        {
            return "x" + magnitude.ToString("0.##", CultureInfo.InvariantCulture).Replace('.', Loc.DecimalSeparator);
        }
    }
}
