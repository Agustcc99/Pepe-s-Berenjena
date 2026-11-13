using System;

namespace FarmIdle
{
    // Reglas de una parcela: Empty -> Growing -> Ready (y de vuelta a Empty al cosechar).
    // Es C# puro, sin UnityEngine y sin conocer la vista (PlotView): quien la maneja le va pasando el tiempo con Tick.
    // Si ese tiempo es el del juego (Time.deltaTime), la parcela se congela sola en pausa y con un anuncio en pantalla.
    public sealed class PlotLogic
    {
        public PlotState State { get; private set; } = PlotState.Empty;

        // Duración del crecimiento en curso y tiempo ya transcurrido (0 y 0 con la parcela vacía).
        public float GrowSeconds { get; private set; }
        public float Elapsed { get; private set; }

        // 0 si está vacía, entre 0 y 1 mientras crece, 1 cuando está lista.
        public float Progress
        {
            get
            {
                switch (State)
                {
                    case PlotState.Growing: return Math.Min(1f, Elapsed / GrowSeconds);
                    case PlotState.Ready: return 1f;
                    default: return 0f;
                }
            }
        }

        // (estado anterior, estado nuevo). Se dispara después de cambiar el estado.
        public event Action<PlotState, PlotState> StateChanged;

        // Siembra: solo en una parcela vacía. Devuelve false (sin tocar nada) si ya tiene algo.
        public bool TryPlant(float growSeconds)
        {
            if (!(growSeconds > 0f) || float.IsInfinity(growSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(growSeconds), "El tiempo de crecimiento tiene que ser mayor que 0 y finito.");
            }
            if (State != PlotState.Empty) return false;

            GrowSeconds = growSeconds;
            Elapsed = 0f;
            SetState(PlotState.Growing);
            return true;
        }

        // Hace avanzar el crecimiento. Si el paso se pasa del final, la parcela queda lista justo en 'GrowSeconds'.
        // Los pasos de 0, negativos o NaN se ignoran.
        public void Tick(float deltaSeconds)
        {
            if (State != PlotState.Growing || !(deltaSeconds > 0f)) return;

            Elapsed += deltaSeconds;
            if (Elapsed >= GrowSeconds)
            {
                Elapsed = GrowSeconds;
                SetState(PlotState.Ready);
            }
        }

        // Termina el crecimiento de una vez (fertilizante, Fase 8): solo si está creciendo; queda lista justo en 'GrowSeconds'.
        public bool TryFinishGrowing()
        {
            if (State != PlotState.Growing) return false;

            Elapsed = GrowSeconds;
            SetState(PlotState.Ready);
            return true;
        }

        // Cosecha: solo si está lista; la parcela vuelve a quedar vacía. Quien entrega el producto es PlotView.
        public bool TryHarvest()
        {
            if (State != PlotState.Ready) return false;

            GrowSeconds = 0f;
            Elapsed = 0f;
            SetState(PlotState.Empty);
            return true;
        }

        private void SetState(PlotState next)
        {
            PlotState previous = State;
            State = next;
            StateChanged?.Invoke(previous, next);
        }
    }
}
