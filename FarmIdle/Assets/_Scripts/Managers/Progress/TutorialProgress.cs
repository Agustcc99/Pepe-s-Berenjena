using System;

namespace FarmIdle
{
    // Dónde va el jugador en la guía de los primeros minutos: sembrar -> cosechar -> vender -> comprar la primera mejora. Es C# puro,
    // sin UnityEngine: se prueba sin escena. TutorialManager le cuenta lo que pasa en el juego y la guarda en la partida.
    //   - Los pasos solo avanzan: completar uno posterior completa también los anteriores (quien vende sin haber "terminado" de
    //     cosechar ya no necesita que se lo expliquen).
    //   - Sembrar: tres parcelas creciendo a la vez. Cosechar: tres cosechas (se cuentan acá).
    //   - Done es definitivo: no vuelve atrás ni siquiera al restaurar un paso menor (eso lo decide quien restaura).
    public sealed class TutorialProgress
    {
        public const int SowTarget = 3;
        public const int HarvestTarget = 3;

        public TutorialStep Step { get; private set; } = TutorialStep.Sow;
        public int Harvested { get; private set; }
        public bool IsDone => Step == TutorialStep.Done;

        // Se dispara con el paso NUEVO, cada vez que se cumple uno.
        public event Action<TutorialStep> StepChanged;

        // Cuántas parcelas están creciendo ahora mismo.
        public void ReportGrowing(int growing)
        {
            if (Step == TutorialStep.Sow && growing >= SowTarget) CompleteUpTo(TutorialStep.Sow);
        }

        // Una cosecha entró a la mochila.
        public void ReportHarvest()
        {
            if (IsDone) return;

            Harvested++;
            if (Harvested >= HarvestTarget) CompleteUpTo(TutorialStep.Harvest);
            else if (Step == TutorialStep.Sow) CompleteUpTo(TutorialStep.Sow);   // cosechar implica que ya sembró
        }

        public void ReportSold()
        {
            CompleteUpTo(TutorialStep.Sell);
        }

        public void ReportPurchased()
        {
            CompleteUpTo(TutorialStep.Buy);
        }

        // "Saltar" la guía entera (un jugador con partida anterior, o ajustes).
        public void Finish()
        {
            CompleteUpTo(TutorialStep.Buy);
        }

        // Pone el paso sin avisar (restaurar la partida guardada). Un valor inválido deja el paso inicial.
        public void Restore(int step)
        {
            Harvested = 0;
            Step = step >= 0 && step <= (int)TutorialStep.Done ? (TutorialStep)step : TutorialStep.Sow;
        }

        public void ResetProgress()
        {
            Restore((int)TutorialStep.Sow);
        }

        private void CompleteUpTo(TutorialStep step)
        {
            if (Step > step) return;   // ya estaba más adelante (o terminada)

            Step = step + 1;
            StepChanged?.Invoke(Step);
        }
    }
}
