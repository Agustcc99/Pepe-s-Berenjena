using System;
using UnityEngine;

namespace FarmIdle
{
    // Qué mejora permanente vende la tienda de prestigio (F-11).
    public enum PrestigeShopKind
    {
        StartCoins,     // monedas con las que arranca cada temporada (suma)
        GrowthSpeed,    // multiplicador de la velocidad de crecimiento (1,1 = 10 % más rápido)
        OfflineCap,     // segundos extra de tope offline
        SellPrice,      // multiplicador del precio de venta (se suma al del prestigio)
        StartFertilizer,// unidades de fertilizante que se regalan al empezar cada temporada
        XpGain          // multiplicador de la XP ganada
    }

    // Una mejora de la tienda: sus niveles tienen costo en semillas doradas y valor. Datos de solo lectura dentro del GameConfig.
    [Serializable]
    public sealed class PrestigeShopItem
    {
        [SerializeField] private string id = "";
        [SerializeField] private PrestigeShopKind kind;
        [SerializeField, Tooltip("Costo en semillas de cada nivel (el largo es el máximo de niveles).")] private int[] costs = { 1, 2, 4 };
        [SerializeField, Tooltip("Valor TOTAL con cada nivel comprado (mismo largo que los costos). Ver PrestigeShopKind para la unidad.")] private float[] values = { 1f, 1f, 1f };

        public PrestigeShopItem() { }

        public PrestigeShopItem(string id, PrestigeShopKind kind, int[] costs, float[] values)
        {
            this.id = id;
            this.kind = kind;
            this.costs = costs;
            this.values = values;
        }

        public string Id => id;
        public PrestigeShopKind Kind => kind;
        public int MaxLevel => costs != null ? costs.Length : 0;

        // Costo del paso de 'level' a 'level + 1' (0 a MaxLevel - 1).
        public int CostOf(int level)
        {
            return costs != null && level >= 0 && level < costs.Length ? costs[level] : 0;
        }

        // Valor total con 'level' niveles comprados; 0 niveles = neutro del tipo.
        public float ValueAt(int level)
        {
            if (level <= 0 || values == null || values.Length == 0) return Neutral(kind);
            return values[Mathf.Min(level, values.Length) - 1];
        }

        public static float Neutral(PrestigeShopKind kind)
        {
            return kind == PrestigeShopKind.GrowthSpeed || kind == PrestigeShopKind.SellPrice || kind == PrestigeShopKind.XpGain ? 1f : 0f;
        }

        // La tabla de fábrica (6 mejoras x 3 niveles). Los números se ajustan en el asset GameConfig.
        public static PrestigeShopItem[] Defaults()
        {
            return new[]
            {
                new PrestigeShopItem("shop_coins", PrestigeShopKind.StartCoins, new[] { 1, 1, 2 }, new[] { 300f, 1000f, 3000f }),
                new PrestigeShopItem("shop_growth", PrestigeShopKind.GrowthSpeed, new[] { 1, 2, 3 }, new[] { 1.1f, 1.2f, 1.35f }),
                new PrestigeShopItem("shop_offline", PrestigeShopKind.OfflineCap, new[] { 1, 1, 2 }, new[] { 3600f, 7200f, 14400f }),
                new PrestigeShopItem("shop_sell", PrestigeShopKind.SellPrice, new[] { 2, 3, 4 }, new[] { 1.1f, 1.2f, 1.3f }),
                new PrestigeShopItem("shop_fert", PrestigeShopKind.StartFertilizer, new[] { 1, 1, 2 }, new[] { 1f, 2f, 3f }),
                new PrestigeShopItem("shop_xp", PrestigeShopKind.XpGain, new[] { 1, 2, 2 }, new[] { 1.15f, 1.3f, 1.5f })
            };
        }
    }
}
