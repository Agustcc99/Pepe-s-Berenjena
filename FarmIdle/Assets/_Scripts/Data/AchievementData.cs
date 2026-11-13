using System.Globalization;
using UnityEngine;

namespace FarmIdle
{
    // Qué cuenta un logro. Son contadores de por vida: el prestigio no los reinicia (salvo Level, que es el nivel más alto alcanzado).
    public enum AchievementKind
    {
        Harvest,          // productos cosechados (entran a la mochila)
        Sell,             // unidades vendidas
        CoinsEarned,      // monedas ganadas vendiendo
        Level,            // nivel más alto alcanzado
        ZonesOpened,      // zonas abiertas
        UpgradesBought,   // niveles de mejora comprados
        FertilizerUsed,   // veces que se usó el fertilizante
        VideosWatched,    // videos de premio vistos hasta el final
        TurboUsed,        // veces que se activó el Turbo
        MissionsClaimed,  // premios de misiones diarias reclamados
        Seasons           // temporadas cerradas (prestigio)
    }

    // La plantilla de un logro (F-06): qué cuenta, cuánto hay que juntar y qué premio chico da al reclamarlo. Datos de solo lectura: lo
    // que el jugador lleva (los contadores, cuáles reclamó) vive en AchievementManager y se guarda por Id.
    // El texto sale de la tabla de idiomas con la clave "data.<id>", con la meta en {0} ("Cosechá {0} productos en total").
    // Premio: igual que las misiones: monedas = 'rewardMinutes' minutos de lo que producen hoy las parcelas (con la misma escala de
    // GameConfig.MissionRewardScale), o, si 'rewardFertilizer' es mayor que 0, esa cantidad de fertilizante; con el stock lleno cae a monedas.
    [CreateAssetMenu(fileName = "Achievement_Nuevo", menuName = "FarmIdle/Datos/Logro")]
    public sealed class AchievementData : GameDataAsset
    {
        [SerializeField, Tooltip("Texto por defecto (con {0} = la meta) si la tabla de idiomas no tiene la clave.")]
        private string displayName = "Logro: {0}";

        [SerializeField] private AchievementKind kind;

        [SerializeField, Min(1), Tooltip("Cuánto hay que juntar.")]
        private int target = 10;

        [SerializeField, Min(0f), Tooltip("Premio en monedas: minutos de producción actual de las parcelas (también es el premio de reemplazo si el stock de fertilizante está lleno).")]
        private float rewardMinutes = 5f;

        [SerializeField, Min(0), Tooltip("Premio en fertilizante (0 = el premio son monedas).")]
        private int rewardFertilizer;

        private string _key;

        public AchievementKind Kind => kind;
        public int Target => target;
        public float RewardMinutes => rewardMinutes;
        public int RewardFertilizer => rewardFertilizer;

        // "Cosechá 500 productos en total".
        public string Describe()
        {
            if (_key == null) _key = "data." + Id;
            return string.Format(CultureInfo.InvariantCulture, Loc.GetOr(_key, displayName), FormatNumber(target));
        }

        // 12.345 en español y 12,345 en inglés.
        public static string FormatNumber(long value)
        {
            string text = value.ToString("N0", CultureInfo.InvariantCulture);
            return Loc.DecimalSeparator == ',' ? text.Replace(',', '.') : text;
        }
    }
}
