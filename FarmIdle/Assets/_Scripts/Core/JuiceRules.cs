namespace FarmIdle
{
    // Los umbrales del "jugo" del circuito cosechar-llenar-vender (J-08) que comparten el audio y lo que se ve. Son solo sensación:
    // ninguno da premio ni toca el balance.
    public static class JuiceRules
    {
        // Cosechas seguidas: cuánto puede pasar entre una y otra para que sigan siendo "seguidas".
        public const float HarvestStreakWindowSeconds = 0.9f;

        // Desde cuántas cosechas seguidas el "+1" sale grande.
        public const int HarvestStreakForBigPopup = 10;

        // Venta grande: unidades vendidas sin cortar (la mochila entera de una).
        public const int BigSaleUnits = 20;

        // Cuánto puede pasar entre dos ventas para que sigan siendo la misma tanda.
        public const float SaleRunWindowSeconds = 0.6f;
    }
}
