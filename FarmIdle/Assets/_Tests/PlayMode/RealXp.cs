namespace FarmIdle.Tests
{
    // La curva de nivel del GameConfig REAL (el de la escena), para los tests que juegan el MainLevel: la XP del nivel L es
    // base x (L-1)^2. El valor sale de la calibración de B-02 (Phase7SceneBuilder.LevelBaseXp, Docs/ECONOMIA_V1.md) y un test de
    // ProgressionIntegrationTests comprueba que sigue siendo el del juego. Los bancos de prueba con un GameConfig armado a mano
    // usan el valor por defecto del código (125) y no pasan por acá.
    internal static class RealXp
    {
        public const int BaseXp = 325;

        public static long ForLevel(int level)
        {
            return (long)BaseXp * (level - 1) * (level - 1);
        }

        // Cuántas unidades de ese precio base hay que vender para llegar justo al nivel.
        public static int UnitsToReachLevel(int level, int unitXp)
        {
            return (int)((ForLevel(level) + unitXp - 1) / unitXp);
        }
    }
}
