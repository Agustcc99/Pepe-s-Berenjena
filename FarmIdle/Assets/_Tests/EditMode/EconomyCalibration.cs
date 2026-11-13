using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FarmIdle.Tests
{
    // Herramienta, no prueba: calcula la tabla de costos que hace que el bot cumpla el EconomyPlan. Es explícita, así que no corre
    // con la suite; se corre a mano (filtro por nombre) cuando cambian los cultivos, las mejoras o el factor PlayerSlowdown, y
    // sus costos se pasan a las tablas de ProgressionData (y se aplican con Phase7SceneBuilder.ApplyBalance).
    public class EconomyCalibration
    {
        [Test, Explicit("Herramienta de calibración de B-02: imprime la tabla de costos y la XP que junta el bot.")]
        public void PrintTheCostTable()
        {
            string[] guids = AssetDatabase.FindAssets("t:GameConfig");
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>(AssetDatabase.GUIDToAssetPath(guids[0]));

            var steps = EconomyPlan.BotSteps();
            var bot = new BalanceBot(config);
            bot.Calibrate(steps);

            var text = new StringBuilder();
            text.AppendLine("CALIBRACION (costos para cumplir el plan)");
            foreach (BalanceBot.CalibrationStep step in steps)
            {
                text.AppendLine(string.Format("  {0,-18} minuto {1,6:0.0}  costo {2,7}  (ganado en total {3}, XP {4})", step.ZoneId ?? step.UpgradeId, step.AtSeconds * EconomyPlan.PlayerSlowdown / 60f, step.Cost, step.EarnedUntilHere, step.XpUntilHere));
            }

            // XP a las 3 h de jugador real: interpolada entre las dos compras que la rodean (el bot sigue jugando después de la última).
            float threeHours = 180f * 60f / EconomyPlan.PlayerSlowdown;
            long xpAt3h = bot.BaseXp;
            for (int i = 1; i < steps.Count; i++)
            {
                if (steps[i].AtSeconds < threeHours) continue;

                float k = (threeHours - steps[i - 1].AtSeconds) / (steps[i].AtSeconds - steps[i - 1].AtSeconds);
                xpAt3h = steps[i - 1].XpUntilHere + (long)(k * (steps[i].XpUntilHere - steps[i - 1].XpUntilHere));
                break;
            }

            text.AppendLine("XP del bot a las 3 h de jugador " + xpAt3h + " | levelBaseXp sugerido para el nivel 20 a las 3 h: " + xpAt3h / 361L);
            Debug.Log("[FarmIdle] " + text);
        }
    }
}
