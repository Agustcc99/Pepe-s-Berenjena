using System.Collections.Generic;

namespace FarmIdle.Tests
{
    // El plan de ritmo de la economía v1.0 (B-02): en qué orden se compra cada cosa y en qué minuto de JUGADOR REAL. La tabla de
    // costos del juego sale de calibrar el bot contra este plan (EconomyCalibration) y BalanceSimulationTests verifica que el
    // bot, con los costos reales, lo cumple.
    //
    // El plan reparte las 25 compras que existen hoy (5 zonas y 20 niveles de mejora; los 2 últimos son el Almacén de B-01) en ~5 h 10 min: una compra cada 2 a 4 min al
    // principio (la primera en el primer par de minutos) y cada 15 a 25 min al final, sin muros de más de 25 min. Velocidad y
    // Mochila se quedan en 3 niveles (F-04): la velocidad permanente ya toca su tope x1,5 con los perks y la mochila ya llega a
    // los 30 lugares que aguanta la pila de la espalda. Lo que falta para llegar a 8 horas lo pone F-03 (prestigio).
    internal static class EconomyPlan
    {
        // El bot es perfecto: no se distrae ni se equivoca de parcela. Un jugador real tarda ~2,5 veces más: la economía original se
        // la compraba el bot en 13 min y en el Galaxy A70 se terminó en 30 a 41 min (Q-02 y ROADMAP). A VERIFICAR con las
        // mediciones de Q-01 y Q-03 y los datos del portal (N-04): si el factor cambia, se recalibra con EconomyCalibration.
        public const float PlayerSlowdown = 2.5f;

        public const float LevelTwentyMinMinutes = 150f;   // nivel 20 "cerca de la hora 3" (jugador real)
        public const float LevelTwentyMaxMinutes = 210f;
        public const float MaxGapMinutes = 25f;            // ningún muro más largo entre dos compras

        public sealed class Entry
        {
            public string ZoneId;       // la zona que se abre, o null
            public string UpgradeId;    // la mejora cuyo próximo nivel se compra, o null
            public float Minutes;       // minuto de jugador real

            public string Id { get { return ZoneId ?? UpgradeId; } }
        }

        public static readonly Entry[] Entries =
        {
            Up("upgrade_growth", 1.5f),
            Up("upgrade_price", 4f),
            Zone("zone_east", 7f),
            Up("upgrade_speed", 10f),
            Up("upgrade_growth", 13f),
            Up("upgrade_capacity", 16.5f),
            Up("upgrade_price", 20.5f),
            Up("upgrade_speed", 25f),
            Up("upgrade_capacity", 30f),
            Up("upgrade_growth", 36f),
            Zone("zone_west", 43f),
            Up("upgrade_speed", 52f),
            Up("upgrade_price", 62f),
            Up("upgrade_capacity", 74f),
            Zone("zone_north", 90f),
            Up("upgrade_growth", 106f),
            Up("upgrade_price", 124f),
            Zone("zone_northwest", 143f),
            Up("upgrade_growth", 166f),
            Up("upgrade_price", 190f),
            Zone("zone_northeast", 214f),
            Up("upgrade_growth", 238f),
            Up("upgrade_price", 262f),
            Up("upgrade_offline", 280f),   // B-01 (tanda 2): el Almacén es una compra de fin de juego (3 h y 4 h de tope offline)
            Up("upgrade_offline", 303f),
        };

        private static Entry Zone(string id, float minutes) { return new Entry { ZoneId = id, Minutes = minutes }; }
        private static Entry Up(string id, float minutes) { return new Entry { UpgradeId = id, Minutes = minutes }; }

        // Segundos de BOT en los que cada compra tendría que pasar (el bot es PlayerSlowdown veces más rápido).
        public static List<BalanceBot.CalibrationStep> BotSteps()
        {
            var steps = new List<BalanceBot.CalibrationStep>(Entries.Length);
            foreach (Entry entry in Entries)
            {
                steps.Add(new BalanceBot.CalibrationStep { ZoneId = entry.ZoneId, UpgradeId = entry.UpgradeId, AtSeconds = entry.Minutes * 60f / PlayerSlowdown });
            }

            return steps;
        }
    }
}
