using System.Diagnostics;
using NUnit.Framework;
using UnityEditor;
using Debug = UnityEngine.Debug;

namespace FarmIdle.Tests
{
    // El final del juego, vigilado por el bot de prestigio (ver PrestigeBot): varias temporadas seguidas sobre los datos REALES.
    // La suite corre el caso voraz (el jugador que más rápido rompe el juego); el informe completo, con las cuatro combinaciones
    // de tienda y semillas de pedidos, es la prueba explícita PrintTheSeasonTable (filtro por nombre, como EconomyCalibration).
    public class PrestigeSimulationTests
    {
        private const int SeasonsPlayed = 12;

        private static GameConfig LoadConfig()
        {
            string[] guids = AssetDatabase.FindAssets("t:GameConfig");
            Assert.AreEqual(1, guids.Length, "Tiene que haber un único GameConfig.");
            return AssetDatabase.LoadAssetAtPath<GameConfig>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        private static PrestigeBot Play(PrestigeBot.ShopPolicy policy, int orderSeeds, int seasons = SeasonsPlayed)
        {
            var bot = new PrestigeBot(LoadConfig()) { Policy = policy, OrderSeedsPerSeason = orderSeeds };
            bot.Run(seasons);
            return bot;
        }

        [Test, Explicit("Herramienta de B-09: imprime la tabla de temporadas del bot de prestigio en las cuatro combinaciones.")]
        public void PrintTheSeasonTable()
        {
            GameConfig config = LoadConfig();
            foreach (PrestigeBot.ShopPolicy policy in new[] { PrestigeBot.ShopPolicy.IncomeFirst, PrestigeBot.ShopPolicy.CheapestFirst })
            {
                foreach (int orderSeeds in new[] { 0, config.OrderSeedsPerSeasonCap })
                {
                    var watch = Stopwatch.StartNew();
                    PrestigeBot bot = Play(policy, orderSeeds);
                    Debug.Log("[FarmIdle] " + bot.Report(EconomyPlan.PlayerSlowdown) + "(simulado en " + watch.ElapsedMilliseconds + " ms)");
                }
            }
        }
    }
}
