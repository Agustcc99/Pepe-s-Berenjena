using System.Globalization;
using UnityEngine;

namespace FarmIdle
{
    // Qué cuenta una misión diaria.
    public enum MissionKind
    {
        Harvest,         // productos cosechados (entran a la mochila)
        Sell,            // unidades vendidas
        UseFertilizer,   // veces que se usa el fertilizante
        Buy              // zonas abiertas o niveles de mejora comprados
    }

    // La plantilla de una misión diaria (F-05): qué cuenta, cuánto hay que juntar, desde qué nivel puede salir y qué premio da. Datos de
    // solo lectura: lo que el jugador lleva (cuáles le tocaron hoy, su avance) vive en MissionManager y se guarda por Id.
    // El texto sale de la tabla de idiomas con la clave "data.<id>", con la meta en {0} ("Cosechá {0} productos").
    // Premio: monedas = 'rewardMinutes' minutos de lo que producen hoy las parcelas (vale igual a la hora 1 que a la hora 5), o, si
    // 'rewardFertilizer' es mayor que 0, esa cantidad de fertilizante; con el stock lleno cae a las monedas.
    [CreateAssetMenu(fileName = "Mission_Nueva", menuName = "FarmIdle/Datos/Mision")]
    public sealed class MissionData : GameDataAsset
    {
        [SerializeField, Tooltip("Texto por defecto (con {0} = la meta) si la tabla de idiomas no tiene la clave.")]
        private string displayName = "Misión: {0}";

        [SerializeField] private MissionKind kind;

        [SerializeField, Min(1), Tooltip("Cuántas unidades hay que juntar.")]
        private int target = 10;

        [SerializeField, Min(1), Tooltip("Nivel mínimo del jugador para que pueda tocarle esta misión.")]
        private int minLevel = 1;

        [SerializeField, Min(0f), Tooltip("Premio en monedas: minutos de producción actual de las parcelas (también es el premio de reemplazo si el stock de fertilizante está lleno).")]
        private float rewardMinutes = 5f;

        [SerializeField, Min(0), Tooltip("Premio en fertilizante (0 = el premio son monedas).")]
        private int rewardFertilizer;

        private string _key;

        public MissionKind Kind => kind;
        public int Target => target;
        public int MinLevel => minLevel;
        public float RewardMinutes => rewardMinutes;
        public int RewardFertilizer => rewardFertilizer;

        // "Cosechá 25 productos".
        public string Describe()
        {
            if (_key == null) _key = "data." + Id;
            return string.Format(CultureInfo.InvariantCulture, Loc.GetOr(_key, displayName), target);
        }
    }
}
