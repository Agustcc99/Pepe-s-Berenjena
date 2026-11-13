using System;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FarmIdle.Tests
{
    // El balance de la economía, vigilado por un bot perfecto (ver BalanceBot) que juega sobre los datos REALES del GameConfig.
    // Si alguien cambia un costo, un tiempo de cultivo o un efecto y la progresión se rompe, este test falla con la línea de
    // tiempo completa. Los tiempos se leen en minutos de JUGADOR REAL (los del bot por EconomyPlan.PlayerSlowdown).
    //   - Las 17 compras salen en el orden del EconomyPlan y cerca de su minuto objetivo (B-02).
    //   - Ningún hueco entre compras pasa de 25 min, y la primera llega en los primeros minutos.
    //   - El nivel 20 llega cerca de la hora 3.
    public class BalanceSimulationTests
    {
        private const float FullGameMaxSeconds = 8f * 3600f;
        private const float MinuteTolerance = 1.5f;       // margen en minutos de jugador...
        private const float RelativeTolerance = 0.15f;    // ...o este porcentaje del minuto objetivo, el que sea mayor

        private static GameConfig LoadConfig()
        {
            string[] guids = AssetDatabase.FindAssets("t:GameConfig");
            Assert.AreEqual(1, guids.Length, "Tiene que haber un único GameConfig.");
            return AssetDatabase.LoadAssetAtPath<GameConfig>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        private static string Timeline(BalanceBot bot)
        {
            var text = new StringBuilder(1024);
            text.AppendLine("Compras del bot (" + bot.Purchases.Count + " de " + bot.TotalPurchasesNeeded + "), en minutos de jugador real (x" + EconomyPlan.PlayerSlowdown + "):");
            foreach (BalanceBot.Purchase purchase in bot.Purchases)
            {
                text.AppendLine(string.Format("  min {0,6:0.0}  (+{1,5:0.0})  ${2,6}  {3}", purchase.AtSeconds * EconomyPlan.PlayerSlowdown / 60f,
                    purchase.SincePrevious * EconomyPlan.PlayerSlowdown / 60f, purchase.Cost, purchase.Name));
            }

            float last = Mathf.Max(1f, bot.Purchases.Count > 0 ? bot.Purchases[bot.Purchases.Count - 1].AtSeconds : 1f);
            text.AppendLine(string.Format("Fertilizantes usados {0} | monedas ganadas {1} | XP {2} | caminando {3:0}% | esperando {4:0.0}%",
                bot.FertilizersUsed, bot.CoinsEarned, bot.BaseXp, 100f * bot.WalkSeconds / last, 100f * bot.WaitSeconds / last));
            return text.ToString();
        }

        private static BalanceBot RunFullGame()
        {
            var bot = new BalanceBot(LoadConfig()) { MaxSeconds = FullGameMaxSeconds };
            bot.Run();
            return bot;
        }

        [Test]
        public void ThePurchases_FollowThePlayerSchedule_InOrder_WithNoLongWalls()
        {
            BalanceBot bot = RunFullGame();
            string timeline = Timeline(bot);
            Debug.Log("[FarmIdle] " + timeline);

            Assert.AreEqual(EconomyPlan.Entries.Length, bot.TotalPurchasesNeeded, "El plan tiene que tener todas las compras del juego.\n" + timeline);
            Assert.AreEqual(bot.TotalPurchasesNeeded, bot.Purchases.Count, "El bot no llegó a comprarlo todo en " + bot.MaxSeconds + " s.\n" + timeline);

            for (int i = 0; i < bot.Purchases.Count; i++)
            {
                BalanceBot.Purchase purchase = bot.Purchases[i];
                EconomyPlan.Entry planned = EconomyPlan.Entries[i];
                float minute = purchase.AtSeconds * EconomyPlan.PlayerSlowdown / 60f;
                float gap = purchase.SincePrevious * EconomyPlan.PlayerSlowdown / 60f;

                Assert.AreEqual(planned.Id, purchase.Id, "Compra #" + (i + 1) + " fuera de orden: " + purchase.Name + "\n" + timeline);
                Assert.AreEqual(planned.Minutes, minute, Math.Max(MinuteTolerance, planned.Minutes * RelativeTolerance), "Compra #" + (i + 1) + " lejos de su minuto: " + purchase.Name + "\n" + timeline);
                Assert.LessOrEqual(gap, EconomyPlan.MaxGapMinutes, "Muro de más de " + EconomyPlan.MaxGapMinutes + " min antes de: " + purchase.Name + "\n" + timeline);
            }

            Assert.LessOrEqual(bot.Purchases[0].AtSeconds * EconomyPlan.PlayerSlowdown / 60f, 2.5f, "La primera compra tiene que llegar en los primeros minutos.\n" + timeline);
        }

        // U-08: los primeros 3 minutos. La guía es sembrar -> cosechar -> vender -> comprar; entre un paso y el siguiente no puede haber
        // zonas muertas (esperas largas sin nada que hacer) y la primera compra tiene que caer dentro de los 3 minutos de jugador real.
        [Test]
        public void TheFirstMinutes_TutorialSteps_HaveNoDeadZones()
        {
            var bot = new BalanceBot(LoadConfig()) { MaxSeconds = 600f };
            bot.Run();

            float slow = EconomyPlan.PlayerSlowdown;
            float sow = bot.ThirdSowSeconds * slow, harvest = bot.ThirdHarvestSeconds * slow, sale = bot.FirstSaleSeconds * slow;
            float buy = bot.Purchases.Count > 0 ? bot.Purchases[0].AtSeconds * slow : -1f;
            string line = string.Format("sembrar x3 {0:0} s | 1.a cosecha {1:0} s | 3.a cosecha {2:0} s | 1.a venta {3:0} s ({4} monedas) | 1.a compra {5:0} s (costo {6}) [segundos de jugador real, x{7}]",
                sow, bot.FirstHarvestSeconds * slow, harvest, sale, bot.CoinsAfterFirstSale, buy, bot.Purchases.Count > 0 ? bot.Purchases[0].Cost : 0L, slow);
            Debug.Log("[FarmIdle] U-08 primeros minutos: " + line);

            Assert.Greater(sow, 0f, line);
            Assert.Greater(harvest, 0f, line);
            Assert.Greater(sale, 0f, line);
            Assert.Greater(buy, 0f, "No hubo primera compra. " + line);
            Assert.LessOrEqual(sow, 30f, "Sembrar las 3 parcelas tarda demasiado. " + line);
            Assert.LessOrEqual(harvest - sow, 60f, "Zona muerta entre sembrar y la 3.a cosecha. " + line);
            Assert.LessOrEqual(sale - harvest, 45f, "Zona muerta entre cosechar y vender. " + line);
            Assert.LessOrEqual(buy - sale, 60f, "Zona muerta entre la 1.a venta y la 1.a compra. " + line);
            Assert.LessOrEqual(buy, 180f, "La primera compra tiene que llegar dentro de los 3 minutos. " + line);
        }

        [Test]
        public void TheBot_ReachesLevelTwenty_NearHourThree()
        {
            BalanceBot bot = RunFullGame();
            bot.PlayUntil(5f * 3600f / EconomyPlan.PlayerSlowdown);   // lo que falte después de la última compra

            float minutes = bot.SecondsToLevel(20) * EconomyPlan.PlayerSlowdown / 60f;
            Debug.Log("[FarmIdle] El bot llega al nivel 20 en el minuto " + minutes.ToString("0") + " de jugador real (levelBaseXp " + LoadConfig().LevelBaseXp + ").");

            Assert.GreaterOrEqual(bot.SecondsToLevel(20), 0f, "El bot no llegó al nivel 20 en 5 h.");
            Assert.That(minutes, Is.InRange(EconomyPlan.LevelTwentyMinMinutes, EconomyPlan.LevelTwentyMaxMinutes), "El nivel 20 tiene que llegar cerca de la hora 3.");
        }

        [Test]
        public void TheFertilizer_SpeedsUpTheBot_ByAModestAmount()
        {
            GameConfig config = LoadConfig();

            var withFertilizer = new BalanceBot(config, BalanceBot.OriginalZoneIds) { MaxSeconds = FullGameMaxSeconds };
            withFertilizer.Run();

            var without = new BalanceBot(config, BalanceBot.OriginalZoneIds) { FertilizerVideoEverySeconds = 1e9f, MaxSeconds = FullGameMaxSeconds };
            without.Run();

            Assert.Greater(withFertilizer.FertilizersUsed, 0, "El bot nunca usó fertilizante.");
            float gain = 1f - withFertilizer.TotalPurchaseSeconds / without.TotalPurchaseSeconds;
            Debug.Log("[FarmIdle] Fertilizante: todo comprado en " + withFertilizer.TotalPurchaseSeconds.ToString("0") + " s con él y en "
                + without.TotalPurchaseSeconds.ToString("0") + " s sin él (ahorra " + (gain * 100f).ToString("0.0") + " %).");

            // Es una ayuda, no un atajo. Un bot perfecto camina casi todo el tiempo y apenas espera, así que el fertilizante (que está
            // lejos del campo, en la fila z = -16,9) le rinde ~0: ni lo atrasa de verdad ni lo acelera más de un cuarto del tiempo total.
            // Al jugador que espera a las plantas le sirve más (ese caso no lo cubre este bot).
            Assert.Greater(gain, -0.03f, "Gastar tiempo en el fertilizante atrasa demasiado al bot.");
            Assert.Less(gain, 0.25f, "El fertilizante acelera demasiado el juego.");
        }

        [Test]
        public void TheBot_IsDeterministic()
        {
            GameConfig config = LoadConfig();
            var a = new BalanceBot(config) { MaxSeconds = FullGameMaxSeconds };
            a.Run();
            var b = new BalanceBot(config) { MaxSeconds = FullGameMaxSeconds };
            b.Run();

            Assert.AreEqual(a.Purchases.Count, b.Purchases.Count);
            Assert.AreEqual(a.TotalPurchaseSeconds, b.TotalPurchaseSeconds, 1e-3f);
            Assert.AreEqual(a.CoinsEarned, b.CoinsEarned);
        }
    }
}
