using System.Globalization;
using UnityEngine;

namespace FarmIdle
{
    // Qué da un perk cada vez que se elige.
    public enum PerkKind
    {
        BackpackCapacity,   // 'valuePerPick' lugares extra en la mochila por elección (Fuerza: +1)
        MoveSpeed,          // 'valuePerPick' se SUMA al multiplicador de velocidad por elección (Velocidad: +0,015 = +1,5 %)
        RareChance,         // F-24: 'valuePerPick' se SUMA a la probabilidad de cosecha dorada por elección (Suerte: +0,005 = +0,5 puntos)
        SellPrice,          // IN-04: 'valuePerPick' se SUMA al bono de precio de venta por elección (Regateo: +0,01 = +1 %); multiplicador aparte de las mejoras
        GrowthSpeed         // IN-04: 'valuePerPick' se SUMA al bono de velocidad de crecimiento por elección (Mano verde: +0,01 = +1 %)
    }

    // Un perk de nivel: lo que el jugador elige gastando un punto (se gana un punto por cada nivel que sube). Datos de
    // solo lectura: cuántas veces se eligió cada uno vive en el progreso (PlayerProgress), que guarda este asset por Id.
    // A diferencia de las mejoras de monedas, cada elección da el MISMO incremento (no hay tabla de niveles) y se agota
    // al llegar a 'maxPicks', así el efecto total de cada perk tiene un tope conocido (la capacidad máxima de la mochila
    // y la velocidad máxima salen de acá).
    [CreateAssetMenu(fileName = "Perk_Nuevo", menuName = "FarmIdle/Datos/Perk")]
    public sealed class PerkData : GameDataAsset
    {
        [SerializeField] private string displayName = "Perk";

        [SerializeField, Tooltip("Nombre corto para el panel de debug.")]
        private string shortLabel = "Perk";

        [SerializeField] private PerkKind kind;

        [SerializeField, Tooltip("Cuánto da cada elección: lugares de mochila (1), fracción de velocidad (0,015 = +1,5 %) o probabilidad de cosecha dorada (0,005 = +0,5 %).")]
        private float valuePerPick = 1f;

        [SerializeField, Min(1), Tooltip("Cuántas veces se puede elegir. Al llegar, el pad deja de gastar puntos.")]
        private int maxPicks = 10;

        public string DisplayName => LocalizedName(displayName);
        public string ShortLabel => shortLabel;
        public PerkKind Kind => kind;
        public float ValuePerPick => valuePerPick;
        public int MaxPicks => maxPicks;

        // El efecto total con 'picks' elecciones (lugares extra o fracción de velocidad). Fuera de 0..MaxPicks se acota.
        public float TotalAt(int picks)
        {
            return valuePerPick * Mathf.Clamp(picks, 0, maxPicks);
        }

        // Texto corto de lo que da UNA elección, para el cartel del pad: "+1 lugar", "+1,5 %".
        public string DescribePick()
        {
            if (kind == PerkKind.BackpackCapacity)
            {
                int slots = Mathf.RoundToInt(valuePerPick);
                return Loc.Format(slots == 1 ? "fmt.slot" : "fmt.slots", slots.ToString(CultureInfo.InvariantCulture));
            }

            float percent = valuePerPick * 100f;
            string text = Mathf.Approximately(percent, Mathf.Round(percent))
                ? Mathf.RoundToInt(percent).ToString(CultureInfo.InvariantCulture)
                : percent.ToString("0.#", CultureInfo.InvariantCulture).Replace('.', Loc.DecimalSeparator);   // coma o punto decimal, según el idioma
            return Loc.Format("fmt.percent", text);
        }
    }
}
