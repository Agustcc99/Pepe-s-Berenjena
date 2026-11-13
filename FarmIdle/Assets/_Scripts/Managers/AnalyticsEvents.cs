namespace FarmIdle
{
    // Nombres de los eventos de analítica (ROADMAP sección 3, "Eventos mínimos"). Sin acentos y en minúsculas con guion bajo, como
    // piden los SDK (Firebase: hasta 40 caracteres). Los que dicen "sin emisor" esperan a que exista su sistema: el que lo cree
    // solo agrega un AnalyticsManager.Log con la constante.
    public static class AnalyticsEvents
    {
        public const string TutorialStep = "tutorial_paso";           // paso: Sow | Harvest | Sell | Buy | Done
        public const string SessionStart = "sesion_inicio";
        public const string SessionEnd = "sesion_fin";                // duracion: segundos de la sesión
        public const string LevelUp = "nivel_subido";                 // nivel: el nivel nuevo
        public const string PadBought = "pad_comprado";               // id: zona o mejora comprada
        public const string RewardedOffered = "rewarded_ofrecido";    // lugar: placement; sale cuando aparece el símbolo de oferta
        public const string RewardedAccepted = "rewarded_aceptado";   // lugar: el jugador pidió el video
        public const string RewardedCompleted = "rewarded_completado";// lugar: vio el video completo
        public const string OfflineClaimed = "offline_reclamado";     // duplicado: 0 | 1 (B-01)
        public const string FertilizerUsed = "fertilizante_usado";    // cantidad: parcelas que crecieron
        public const string TurboUsed = "turbo_usado";
        public const string Prestige = "prestigio";                   // temporada: el número de la que empieza (F-03)
        public const string MissionCompleted = "mision_completada";   // id: la plantilla de la misión (F-05)
        public const string AchievementUnlocked = "logro_desbloqueado";   // id: la plantilla del logro (F-06)
        public const string DailyReward = "premio_diario";            // dia: el día de la racha (1 a 7) que se reclamó (F-07)
        public const string Abandon = "abandono";                     // ultimo_pad: última compra antes de dejar el juego

        // N-06: los sistemas que llegaron después de N-01. Solo al jugar, nunca al restaurar una partida.
        public const string OrderCompleted = "pedido_completado";     // grande: 0 | 1 (F-18)
        public const string DecorationBought = "decoracion_comprada"; // id: la decoración (F-17)
        public const string HelperBought = "ayudante_comprado";       // nivel: el nivel de ayudante que quedó (F-17)
        public const string SkinBought = "skin_comprada";             // id: la skin (F-17)
        public const string SeedShopBought = "mejora_semillas";       // id: la mejora de la tienda de prestigio (F-11)
        public const string RareHarvest = "cosecha_rara";             // id: el cultivo (J-04)

        // Nombres de los parámetros.
        public const string StepKey = "paso";
        public const string DurationKey = "duracion";
        public const string LevelKey = "nivel";
        public const string IdKey = "id";
        public const string DayKey = "dia";
        public const string PlacementKey = "lugar";
        public const string DoubledKey = "duplicado";
        public const string AmountKey = "cantidad";
        public const string LastPadKey = "ultimo_pad";
        public const string SeasonKey = "temporada";
        public const string BigKey = "grande";
    }
}
