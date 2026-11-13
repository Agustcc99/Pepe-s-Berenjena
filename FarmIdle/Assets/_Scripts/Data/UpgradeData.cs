using System;
using System.Globalization;
using UnityEngine;

namespace FarmIdle
{
    // Qué cambia una mejora cuando se compra. Cada tipo se aplica en la capa que corresponde (ProgressionManager reparte
    // el efecto): la mochila crece en BackpackManager, la velocidad la lee el jugador, el precio entra en PriceCalculator
    // y el crecimiento se lo pasa PlotField a las parcelas.
    public enum UpgradeKind
    {
        BackpackCapacity,   // 'value' de cada nivel: lugares EXTRA sobre la capacidad base del GameConfig (+4)
        MoveSpeed,          // multiplicador de la velocidad de movimiento (1,15 = 15 % más rápido)
        SellPrice,          // multiplicador del precio de venta (PriceCalculator.SetUpgradeMultiplier)
        GrowthSpeed,        // multiplicador de la velocidad de crecimiento de las plantas (1,25 = crecen 25 % más rápido)
        OfflineCap          // B-01: 'value' de cada nivel: SEGUNDOS EXTRA sobre el tope base del progreso offline (3600 = +1 h)
    }

    // Un nivel de una mejora: cuánto cuesta comprarlo y el efecto TOTAL que deja (no el incremento sobre el anterior).
    [Serializable]
    public struct UpgradeLevel
    {
        [SerializeField, Min(1), Tooltip("Monedas que cuesta pasar al nivel anterior a este.")]
        private long cost;

        [SerializeField, Tooltip("Efecto total con este nivel: lugares extra de mochila, o multiplicador (1,25 = +25 %).")]
        private float value;

        public UpgradeLevel(long cost, float value)
        {
            this.cost = cost;
            this.value = value;
        }

        public long Cost => cost;
        public float Value => value;
    }

    // Una mejora comprable: nombre, tipo y la tabla de niveles con su costo. Datos de solo lectura: el nivel que tiene el
    // jugador vive en el progreso (PlayerProgress), que guarda este asset por Id.
    // Nivel 0 = sin comprar (efecto neutro, ver Identity); el nivel 1 es el primero de la tabla.
    [CreateAssetMenu(fileName = "Upgrade_Nueva", menuName = "FarmIdle/Datos/Mejora")]
    public sealed class UpgradeData : GameDataAsset
    {
        [SerializeField] private string displayName = "Mejora";

        [SerializeField, Tooltip("Nombre corto para el panel de debug.")]
        private string shortLabel = "Mej";

        [SerializeField] private UpgradeKind kind;

        [SerializeField, Tooltip("Un elemento por nivel, del primero al último. El costo es el de comprar ESE nivel.")]
        private UpgradeLevel[] levels = new UpgradeLevel[0];

        public string DisplayName => LocalizedName(displayName);
        public string ShortLabel => shortLabel;
        public UpgradeKind Kind => kind;

        public int MaxLevel => levels != null ? levels.Length : 0;

        // El efecto del nivel 0: nada que sumar (0) o multiplicar por 1.
        public float Identity => kind == UpgradeKind.BackpackCapacity || kind == UpgradeKind.OfflineCap ? 0f : 1f;

        // Costo de comprar el nivel 'level' (1 = el primero). Fuera de la tabla devuelve 0.
        public long CostOfLevel(int level)
        {
            return level >= 1 && level <= MaxLevel ? levels[level - 1].Cost : 0L;
        }

        // Efecto total con 'level' comprado. El nivel 0 (o negativo) es el neutro y uno mayor al máximo vale como el máximo.
        public float ValueAtLevel(int level)
        {
            if (level <= 0 || MaxLevel == 0) return Identity;

            return levels[Math.Min(level, MaxLevel) - 1].Value;
        }

        // Texto corto del efecto de ese nivel, para el cartel del pad: "+4 lugares", "+15 %".
        public string Describe(int level)
        {
            float value = ValueAtLevel(level);

            if (kind == UpgradeKind.BackpackCapacity)
            {
                return Loc.Format("fmt.slots", Mathf.RoundToInt(value).ToString(CultureInfo.InvariantCulture));
            }

            if (kind == UpgradeKind.OfflineCap)
            {
                return Loc.Format("fmt.hours", (value / 3600f).ToString("0.##", CultureInfo.InvariantCulture).Replace('.', Loc.DecimalSeparator));
            }

            return Loc.Format("fmt.percent", Mathf.RoundToInt((value - 1f) * 100f).ToString(CultureInfo.InvariantCulture));
        }
    }
}
