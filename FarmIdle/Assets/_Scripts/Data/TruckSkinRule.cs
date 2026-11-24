using System;
using UnityEngine;

namespace FarmIdle
{
    // Una mejora visual del camión de reparto: puro adorno, no da ninguna ventaja. Se compran en el orden de la lista del GameConfig (el
    // pad vende siempre la siguiente), así que la partida solo guarda CUÁNTAS tiene (TruckManager). El nombre sale de la tabla de
    // textos con la clave "truck.<id>". Números PROVISORIOS.
    [Serializable]
    public sealed class TruckSkinRule
    {
        [SerializeField, Tooltip("Identificador estable: minúsculas y _. Es parte de la clave de texto 'truck.<id>'.")]
        private string id = "";

        [SerializeField, Min(1), Tooltip("Monedas que cuesta en el pad del camión.")]
        private int cost = 1000;

        public TruckSkinRule(string id, int cost)
        {
            this.id = id;
            this.cost = cost;
        }

        public string Id => id;
        public int Cost => Mathf.Max(1, cost);

        // Clave de la tabla de textos con el nombre ("truck.<id>"); se arma una sola vez.
        [NonSerialized] private string _nameKey;
        public string NameKey => _nameKey ?? (_nameKey = "truck." + id);

        // Las tres mejoras del camión sobre el modelo de fábrica (nivel 0), de la más barata a la más cara.
        public static TruckSkinRule[] Defaults()
        {
            return new[]
            {
                new TruckSkinRule("truck_red", 4000),
                new TruckSkinRule("truck_green", 30000),
                new TruckSkinRule("truck_gold", 120000)
            };
        }
    }
}
