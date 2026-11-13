using System;
using UnityEngine;

namespace FarmIdle
{
    // F-18: qué cultivos pueden salir en un pedido del pueblo, cuándo y con qué peso. Un cultivo sale recién cuando se abrió la zona
    // donde se planta (zoneId vacío = la granja inicial, siempre disponible). Datos de solo lectura dentro del GameConfig.
    [Serializable]
    public sealed class OrderCropRule
    {
        [SerializeField, Tooltip("Id del cultivo (CropData.Id).")]
        private string cropId = "";

        [SerializeField, Tooltip("Id de la zona que hay que tener abierta para que el cultivo pueda salir. Vacío = siempre.")]
        private string zoneId = "";

        [SerializeField, Min(1), Tooltip("Peso en el sorteo: los cultivos que más pagan salen más seguido.")]
        private int weight = 1;

        public OrderCropRule(string cropId, string zoneId, int weight)
        {
            this.cropId = cropId;
            this.zoneId = zoneId;
            this.weight = weight;
        }

        public string CropId => cropId;
        public string ZoneId => zoneId;
        public int Weight => Mathf.Max(1, weight);

        // Los ocho cultivos del juego en el orden de la granja; el peso crece con el precio.
        public static OrderCropRule[] Defaults()
        {
            return new[]
            {
                new OrderCropRule("wheat", "", 1),
                new OrderCropRule("carrot", "", 1),
                new OrderCropRule("tomato", "", 1),
                new OrderCropRule("pumpkin", "zone_west", 2),
                new OrderCropRule("corn", "zone_north", 2),
                new OrderCropRule("eggplant", "zone_north", 3),
                new OrderCropRule("watermelon", "zone_northwest", 3),
                new OrderCropRule("sunflower", "zone_northeast", 3)
            };
        }
    }
}
